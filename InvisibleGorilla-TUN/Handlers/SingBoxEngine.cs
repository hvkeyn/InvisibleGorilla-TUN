using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace InvisibleGorillaTUN.Handlers
{
    using Foundation;

    /// <summary>
    /// Per-application split tunneling powered by a bundled sing-box engine.
    ///
    /// Unlike the default tun2socks path (which hijacks the whole default route),
    /// sing-box brings up its own TUN and routes traffic per owning process:
    ///   - BYPASS_SELECTED_APPS: everything goes through XRay (proxy), the selected
    ///     apps are sent out directly.
    ///   - ONLY_SELECTED_APPS:   only the selected apps go through XRay, the rest
    ///     is sent out directly.
    /// The real proxy egress is the local XRay SOCKS endpoint (the same -proxy the
    /// tun2socks path would use), so the existing connection chain is preserved.
    ///
    /// The engine is fail-safe: when sing-box.exe is missing or it fails to start,
    /// the caller falls back to the regular full-tunnel path, so connectivity is
    /// never worse than before.
    /// </summary>
    internal static class SingBoxEngine
    {
        private static readonly object SyncRoot = new();
        private static Process? process;

        private static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;
        private static string ExecutablePath => Path.Combine(BaseDirectory, "sing-box.exe");
        private static string ConfigPath => Path.Combine(BaseDirectory, "singbox-config.json");
        private static string LogPath => Path.Combine(BaseDirectory, "singbox.log");

        public static bool IsAvailable()
        {
            bool available = File.Exists(ExecutablePath);
            if (!available)
                DiagnosticLog.Write("SingBoxEngine", $"sing-box.exe not found at {ExecutablePath}; split tunnel unavailable.");
            return available;
        }

        public static bool IsRunning()
        {
            lock (SyncRoot)
            {
                try { return process is { HasExited: false }; }
                catch { return false; }
            }
        }

        /// <summary>
        /// Starts sing-box for the given split-tunnel parameters. Returns true when
        /// the process launched and stayed alive long enough to bring up the TUN.
        /// </summary>
        public static bool Start(
            string device,
            string address,
            string dns,
            string proxy,
            string serverIp,
            string mode,
            string[] apps)
        {
            lock (SyncRoot)
            {
                try
                {
                    Stop_NoLock();

                    string config = BuildConfig(device, address, dns, proxy, serverIp, mode, apps);
                    File.WriteAllText(ConfigPath, config, new UTF8Encoding(false));
                    DiagnosticLog.Write("SingBoxEngine", $"Wrote sing-box config to {ConfigPath} ({config.Length} bytes)");

                    ProcessStartInfo startInfo = new()
                    {
                        FileName = ExecutablePath,
                        Arguments = $"run -c \"{ConfigPath}\"",
                        WorkingDirectory = BaseDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    Process started = new() { StartInfo = startInfo, EnableRaisingEvents = true };
                    started.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) DiagnosticLog.Write("sing-box", e.Data); };
                    started.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) DiagnosticLog.Write("sing-box", e.Data); };

                    if (!started.Start())
                    {
                        DiagnosticLog.Write("SingBoxEngine", "Process.Start returned false.");
                        return false;
                    }

                    started.BeginOutputReadLine();
                    started.BeginErrorReadLine();
                    process = started;
                    DiagnosticLog.Write("SingBoxEngine", $"sing-box started (pid={started.Id}), mode={mode}, apps={apps.Length}");

                    // Give sing-box a moment to initialize. If it dies immediately the
                    // config is bad and we should fall back to the full tunnel.
                    Thread.Sleep(1200);
                    if (started.HasExited)
                    {
                        DiagnosticLog.Write("SingBoxEngine", $"sing-box exited early with code {started.ExitCode}. See {LogPath}.");
                        process = null;
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    DiagnosticLog.WriteException("SingBoxEngine.Start", ex);
                    Stop_NoLock();
                    return false;
                }
            }
        }

        public static void Stop()
        {
            lock (SyncRoot)
            {
                Stop_NoLock();
            }
        }

        /// <summary>
        /// Verifies that the local XRay SOCKS listener is reachable. We intentionally do
        /// not probe the internet through the TUN — that probe falsely failed on some
        /// setups and rolled back split-tunnel to full capture.
        /// </summary>
        public static bool VerifyLocalSocksProxy(string proxy)
        {
            ParseProxy(proxy, out string host, out int port, out _, out _);

            try
            {
                using TcpClient client = new();
                IAsyncResult asyncResult = client.BeginConnect(host, port, null, null);
                if (!asyncResult.AsyncWaitHandle.WaitOne(3000))
                {
                    try { client.Close(); } catch { }
                    DiagnosticLog.Write("SingBoxEngine", $"Local SOCKS probe to {host}:{port} timed out");
                    return false;
                }

                client.EndConnect(asyncResult);
                bool ok = client.Connected;
                DiagnosticLog.Write("SingBoxEngine", $"Local SOCKS probe to {host}:{port}: ok={ok}");
                return ok;
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write("SingBoxEngine", $"Local SOCKS probe failed: {ex.Message}");
                return false;
            }
        }

        private static void Stop_NoLock()
        {
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                {
                    DiagnosticLog.Write("SingBoxEngine", $"Stopping sing-box (pid={process.Id})");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(4000);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("SingBoxEngine.Stop", ex);
            }
            finally
            {
                try { process.Dispose(); } catch { }
                process = null;
            }
        }

        private static string BuildConfig(
            string device,
            string address,
            string dns,
            string proxy,
            string serverIp,
            string mode,
            string[] apps)
        {
            ParseProxy(proxy, out string proxyHost, out int proxyPort, out string? user, out string? pass);

            bool bypass = string.Equals(mode, "BYPASS_SELECTED_APPS", StringComparison.OrdinalIgnoreCase);
            // BYPASS: default goes through the proxy, selected apps go direct.
            // ONLY_SELECTED: default goes direct, selected apps go through the proxy.
            string finalOutbound = bypass ? "proxy" : "direct";
            string selectedOutbound = bypass ? "direct" : "proxy";

            string interfaceAddress = address.Contains('/') ? address : $"{address}/30";
            string safeDns = string.IsNullOrWhiteSpace(dns) ? "8.8.8.8" : dns.Trim();

            string processPaths = string.Join(",", apps.Select(JsonString));
            string processNames = string.Join(",", apps
                .Select(path => Path.GetFileName(path))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(JsonString));

            StringBuilder socksOutbound = new();
            socksOutbound.Append("{\"type\":\"socks\",\"tag\":\"proxy\",\"server\":")
                .Append(JsonString(proxyHost))
                .Append(",\"server_port\":").Append(proxyPort)
                .Append(",\"version\":\"5\",\"domain_strategy\":\"ipv4_only\",\"udp_fragment\":true");
            if (!string.IsNullOrEmpty(user))
            {
                socksOutbound.Append(",\"username\":").Append(JsonString(user))
                    .Append(",\"password\":").Append(JsonString(pass ?? string.Empty));
            }
            socksOutbound.Append('}');

            // sing-box 1.10 schema. Keep it minimal and robust; strict_route=false to
            // avoid breaking connectivity on unusual network setups.
            StringBuilder sb = new();
            sb.Append('{');

            sb.Append("\"log\":{\"level\":\"info\",\"timestamp\":true,\"output\":")
                .Append(JsonString(LogPath)).Append("},");

            // Resolve DNS directly on the host. Routing through the local SOCKS proxy for
            // DNS created a bootstrap loop (proxy needs DNS, DNS needs proxy).
            sb.Append("\"dns\":{\"servers\":[")
                .Append("{\"tag\":\"dns-direct\",\"address\":").Append(JsonString(safeDns)).Append(",\"detour\":\"direct\"}")
                .Append("],\"final\":\"dns-direct\"},");

            sb.Append("\"inbounds\":[{")
                .Append("\"type\":\"tun\",\"tag\":\"tun-in\",")
                .Append("\"interface_name\":").Append(JsonString(device)).Append(',')
                .Append("\"inet4_address\":").Append(JsonString(interfaceAddress)).Append(',')
                .Append("\"mtu\":1500,")
                // Only loopback must stay off the hijacked route table; LAN is handled
                // by the ip_is_private rule below.
                .Append("\"inet4_route_exclude_address\":[\"127.0.0.0/8\"],")
                .Append("\"endpoint_independent_nat\":true,")
                .Append("\"auto_route\":true,\"strict_route\":false,\"stack\":\"gvisor\",\"sniff\":true,\"sniff_override_destination\":false")
                .Append("}],");

            sb.Append("\"outbounds\":[")
                .Append(socksOutbound).Append(',')
                .Append("{\"type\":\"direct\",\"tag\":\"direct\",\"domain_strategy\":\"ipv4_only\"}")
                .Append("],");

            sb.Append("\"route\":{")
                .Append("\"auto_detect_interface\":true,\"find_process\":true,")
                .Append("\"final\":").Append(JsonString(finalOutbound)).Append(',')
                .Append("\"rules\":[")
                // Infrastructure bypass — must come before app rules.
                .Append("{\"ip_cidr\":[\"127.0.0.0/8\"],\"outbound\":\"direct\"},")
                .Append("{\"ip_is_private\":true,\"outbound\":\"direct\"},");

            if (!string.IsNullOrWhiteSpace(serverIp))
            {
                // Keep the XRay<->server connection out of the tunnel to avoid loops.
                sb.Append("{\"ip_cidr\":[").Append(JsonString($"{serverIp}/32")).Append("],\"outbound\":\"direct\"},");
            }

            if (apps.Length > 0)
            {
                string processPathRegex = string.Join(",", BuildProcessPathRegex(apps).Select(JsonString));

                // Match by full path, executable name, and folder regex (covers versioned
                // Yandex/Chrome installs and child processes that live in subfolders).
                sb.Append("{\"process_path\":[").Append(processPaths).Append("],\"outbound\":")
                    .Append(JsonString(selectedOutbound)).Append("},")
                    .Append("{\"process_name\":[").Append(processNames).Append("],\"outbound\":")
                    .Append(JsonString(selectedOutbound)).Append("},")
                    .Append("{\"process_path_regex\":[").Append(processPathRegex).Append("],\"outbound\":")
                    .Append(JsonString(selectedOutbound)).Append('}');
            }

            sb.Append("]}");
            sb.Append('}');

            return sb.ToString();
        }

        private static IEnumerable<string> BuildProcessPathRegex(string[] apps)
        {
            HashSet<string> patterns = new(StringComparer.OrdinalIgnoreCase);

            foreach (string app in apps)
            {
                if (string.IsNullOrWhiteSpace(app))
                    continue;

                string? directory = Path.GetDirectoryName(app);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    string escaped = Regex.Escape(directory).Replace("/", "\\\\");
                    patterns.Add($"(?i){escaped}\\\\.*\\.exe$");
                }

                string fileName = Path.GetFileName(app);
                if (!string.IsNullOrWhiteSpace(fileName))
                    patterns.Add($"(?i).*{Regex.Escape(fileName)}$");
            }

            // Broad Yandex coverage: versioned Application folders and AppData installs.
            patterns.Add("(?i)\\\\YandexBrowser\\\\.*\\.exe$");
            patterns.Add("(?i)\\\\Yandex\\\\[^\\\\]+\\.exe$");
            patterns.Add("(?i)\\\\YaPin\\\\.*\\.exe$");

            return patterns;
        }

        private static void ParseProxy(string proxy, out string host, out int port, out string? user, out string? pass)
        {
            host = "127.0.0.1";
            port = 10801;
            user = null;
            pass = null;

            if (string.IsNullOrWhiteSpace(proxy))
                return;

            try
            {
                if (proxy.Contains("://"))
                {
                    Uri uri = new(proxy);
                    if (!string.IsNullOrEmpty(uri.Host)) host = uri.Host;
                    if (uri.Port > 0) port = uri.Port;
                    if (!string.IsNullOrEmpty(uri.UserInfo))
                    {
                        string[] credentials = uri.UserInfo.Split(':', 2);
                        user = Uri.UnescapeDataString(credentials[0]);
                        if (credentials.Length > 1) pass = Uri.UnescapeDataString(credentials[1]);
                    }
                    return;
                }

                string hostPort = proxy;
                int at = hostPort.LastIndexOf('@');
                if (at >= 0)
                {
                    string credentials = hostPort.Substring(0, at);
                    hostPort = hostPort.Substring(at + 1);
                    string[] parts = credentials.Split(':', 2);
                    user = parts[0];
                    if (parts.Length > 1) pass = parts[1];
                }

                int colon = hostPort.LastIndexOf(':');
                if (colon > 0)
                {
                    host = hostPort.Substring(0, colon);
                    int.TryParse(hostPort.Substring(colon + 1), out port);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("SingBoxEngine.ParseProxy", ex);
            }
        }

        private static string JsonString(string value)
        {
            StringBuilder sb = new();
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
