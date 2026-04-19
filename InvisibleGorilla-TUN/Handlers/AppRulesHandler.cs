using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InvisibleGorillaTUN.Handlers
{
    using Foundation;

    internal static class AppRulesHandler
    {
        private const string DefaultMode = "ALL_APPS";
        private static readonly object SyncRoot = new();
        private static readonly string ActiveRulesPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "active-app-rules.txt");

        private sealed record DecodedAppRulesPayload(string Mode, string[] RequestedApps);

        public static void ApplyEncodedPayload(string? encodedPayload)
        {
            lock (SyncRoot)
            {
                DecodedAppRulesPayload payload = DecodePayload(encodedPayload);
                string[] acceptedApps = payload.RequestedApps
                    .Select(NormalizePath)
                    .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                string[] rejectedApps = payload.RequestedApps
                    .Select(NormalizePath)
                    .Where(path => string.IsNullOrWhiteSpace(path) || !acceptedApps.Contains(path, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                DiagnosticLog.Write(
                    "AppRulesHandler",
                    $"Applying app rules: mode={payload.Mode}, requested={payload.RequestedApps.Length}, accepted={acceptedApps.Length}, rejected={rejectedApps.Length}");

                foreach (string acceptedApp in acceptedApps)
                    DiagnosticLog.Write("AppRulesHandler", $"Accepted app rule path: {acceptedApp}");

                foreach (string rejectedApp in rejectedApps)
                    DiagnosticLog.Write("AppRulesHandler", $"Rejected app rule path: {rejectedApp}");

                string[] stagedLines = new[] { $"mode={payload.Mode}" }
                    .Concat(acceptedApps)
                    .ToArray();
                File.WriteAllLines(ActiveRulesPath, stagedLines, Encoding.UTF8);
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                if (File.Exists(ActiveRulesPath))
                {
                    File.Delete(ActiveRulesPath);
                    DiagnosticLog.Write("AppRulesHandler", "Cleared active app rules file");
                }
            }
        }

        private static DecodedAppRulesPayload DecodePayload(string? encodedPayload)
        {
            if (string.IsNullOrWhiteSpace(encodedPayload))
                return new DecodedAppRulesPayload(DefaultMode, Array.Empty<string>());

            try
            {
                byte[] payloadBytes = Convert.FromBase64String(encodedPayload);
                string payloadText = Encoding.UTF8.GetString(payloadBytes);
                string[] lines = payloadText
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Trim())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .ToArray();

                if (lines.Length == 0)
                    return new DecodedAppRulesPayload(DefaultMode, Array.Empty<string>());

                // Backward compatibility for the old bypass-only payload.
                if (!lines[0].StartsWith("MODE=", StringComparison.OrdinalIgnoreCase))
                    return new DecodedAppRulesPayload("BYPASS_SELECTED_APPS", lines);

                string mode = NormalizeMode(lines[0][5..]);
                string[] apps = lines.Skip(1).ToArray();
                return new DecodedAppRulesPayload(mode, apps);
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("AppRulesHandler.DecodePayload", ex);
                return new DecodedAppRulesPayload(DefaultMode, Array.Empty<string>());
            }
        }

        private static string NormalizePath(string? appPath)
        {
            if (string.IsNullOrWhiteSpace(appPath))
                return string.Empty;

            try
            {
                return Path.GetFullPath(appPath.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeMode(string? mode)
        {
            return mode?.Trim().ToUpperInvariant() switch
            {
                "BYPASS_SELECTED_APPS" => "BYPASS_SELECTED_APPS",
                "ONLY_SELECTED_APPS" => "ONLY_SELECTED_APPS",
                _ => DefaultMode
            };
        }
    }
}
