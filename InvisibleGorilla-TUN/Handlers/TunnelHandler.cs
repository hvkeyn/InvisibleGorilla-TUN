using System;
using System.Threading.Tasks;

namespace InvisibleGorillaTUN.Handlers
{
    using Foundation;
    using Handlers.Profiles;
    using Utilities;

    public class TunnelHandler : Handler
    {
        private Scheduler scheduler;

        private Action onStopTunnel;
        private Action<string, string> onStartTunnel;
        private Action<string, string> onSetInterfaceAddress;
        private Action<string, string> onSetInterfaceDns;
        private Action<string, string, string, int> onSetRoutes;
        private Func<bool> isTunnelRunning;
        private Func<IProfile> getProfile;

        public TunnelHandler()
        {
            this.scheduler = new Scheduler();
        }

        public void Setup(
            Action onStopTunnel,
            Action<string, string> onStartTunnel,
            Action<string, string> onSetInterfaceAddress,
            Action<string, string> onSetInterfaceDns,
            Action<string, string, string, int> onSetRoutes,
            Func<bool> isTunnelRunning,
            Func<IProfile> getProfile
        )
        {
            this.onStopTunnel = onStopTunnel;
            this.onStartTunnel = onStartTunnel;
            this.onSetInterfaceAddress = onSetInterfaceAddress;
            this.onSetInterfaceDns = onSetInterfaceDns;
            this.onSetRoutes = onSetRoutes;
            this.isTunnelRunning = isTunnelRunning;
            this.getProfile = getProfile;
        }

        public void Start(string device, string proxy, string address, string server, string dns, string appRulesPayload)
        {
            DiagnosticLog.Write(
                "TunnelHandler",
                $"Start requested: device={device}, proxy={proxy}, address={address}, server={server}, dns={dns}, hasAppRules={(!string.IsNullOrWhiteSpace(appRulesPayload)).ToString()}");

            try
            {
                ActiveAppRules activeRules = AppRulesHandler.ApplyEncodedPayload(appRulesPayload);

                bool wantsSplitTunnel =
                    !string.Equals(activeRules.Mode, "ALL_APPS", StringComparison.OrdinalIgnoreCase)
                    && activeRules.Apps.Length > 0;

                if (wantsSplitTunnel && SingBoxEngine.IsAvailable())
                {
                    DiagnosticLog.Write(
                        "TunnelHandler",
                        $"Per-app split tunnel requested: mode={activeRules.Mode}, apps={activeRules.Apps.Length}. Starting sing-box engine.");

                    bool splitStarted = SingBoxEngine.Start(
                        device: device,
                        address: address,
                        dns: dns,
                        proxy: proxy,
                        serverIp: server,
                        mode: activeRules.Mode,
                        apps: activeRules.Apps);

                    if (splitStarted)
                    {
                        WaitUntilInterfaceCreated();

                        if (SingBoxEngine.VerifyLocalSocksProxy(proxy))
                        {
                            DiagnosticLog.Write("TunnelHandler", "Split tunnel (sing-box) start completed successfully");
                            return;
                        }

                        DiagnosticLog.Write(
                            "TunnelHandler",
                            "sing-box started but local xray SOCKS is unreachable; falling back to full tunnel.");
                        SingBoxEngine.Stop();
                    }
                    else
                    {
                        DiagnosticLog.Write(
                            "TunnelHandler",
                            "sing-box engine failed to start; falling back to the regular full tunnel.");
                    }
                }

                bool isRunning = IsTunnelRunning();
                DiagnosticLog.Write("TunnelHandler", $"IsTunnelRunning before start={isRunning}");

                if (!isRunning)
                {
                    CleanupProfile();
                    StartTunnel();
                }

                WaitUntilInterfaceCreated();
                SetInterfaceAddress();
                WaitUntilInterfaceAddressSet();
                SetInterfaceDns();
                SetRoutes();
                DiagnosticLog.Write("TunnelHandler", "Start completed successfully");
            }
            catch(Exception ex)
            {
                DiagnosticLog.WriteException("TunnelHandler.Start", ex);
                Console.WriteLine(ex.Message);
                return;
            }

            bool IsTunnelRunning()
            {
                return isTunnelRunning.Invoke();
            }

            void CleanupProfile()
            {
                DiagnosticLog.Write("TunnelHandler", $"CleanupProfile for {device}");
                getProfile.Invoke().CleanupProfiles(device);
            }

            void StartTunnel()
            {
                DiagnosticLog.Write("TunnelHandler", "Starting tunnel task");
                new Task(() => {
                    try
                    {
                        DiagnosticLog.Write("TunnelHandler", $"onStartTunnel invoke: device={device}, proxy={proxy}");
                        onStartTunnel.Invoke(device, proxy);
                        DiagnosticLog.Write("TunnelHandler", "onStartTunnel returned");
                    }
                    catch (Exception ex)
                    {
                        DiagnosticLog.WriteException("TunnelHandler.StartTunnelTask", ex);
                    }
                }).Start();
            }

            void WaitUntilInterfaceCreated()
            {
                DiagnosticLog.Write("TunnelHandler", $"Waiting until interface exists: {device}");
                scheduler.WaitUntil(
                    condition: IsInterfaceExists,
                    millisecondsTimeout: 6000,
                    $"Device with the name '{device}' was not found."
                );
                DiagnosticLog.Write("TunnelHandler", $"Interface exists: {device}");
            }

            void SetInterfaceAddress()
            {
                DiagnosticLog.Write("TunnelHandler", $"Setting interface address: device={device}, address={address}");
                onSetInterfaceAddress.Invoke(device, address);
                DiagnosticLog.Write("TunnelHandler", "SetInterfaceAddress returned");
            }

            void WaitUntilInterfaceAddressSet()
            {
                DiagnosticLog.Write("TunnelHandler", $"Waiting until interface address is set: {address}");
                scheduler.WaitUntil(
                    condition: IsInterfaceAddressWasSet,
                    millisecondsTimeout: 6000,
                    $"'{address}' was not set to '{device}' device."
                );
                DiagnosticLog.Write("TunnelHandler", $"Interface address confirmed: {address}");
            }

            void SetInterfaceDns()
            {
                DiagnosticLog.Write("TunnelHandler", $"Setting interface DNS: device={device}, dns={dns}");
                onSetInterfaceDns.Invoke(device, dns);
                DiagnosticLog.Write("TunnelHandler", "SetInterfaceDns returned");
            }

            void SetRoutes()
            {
                string gateway = NetworkUtility.GetDefaultGateway(address);
                int index = NetworkUtility.GetNetworkInterfaceIndex(device);
                DiagnosticLog.Write(
                    "TunnelHandler",
                    $"Setting routes: server={server}, address={address}, gateway={gateway}, index={index}");

                onSetRoutes.Invoke(
                    server, 
                    address, 
                    gateway, 
                    index
                );
                DiagnosticLog.Write("TunnelHandler", "SetRoutes returned");
            }

            bool IsInterfaceExists() => NetworkUtility.IsInterfaceExists(device);

            bool IsInterfaceAddressWasSet() => NetworkUtility.IsInterfaceAddressWasSet(device, address);
        }

        public void Stop()
        {
            DiagnosticLog.Write("TunnelHandler", "Stop requested");
            AppRulesHandler.Clear();

            // Stop the split-tunnel engine first (no-op when it was never started).
            SingBoxEngine.Stop();

            // Only tear down tun2socks when it was actually running; calling the
            // native stop with no process would fault.
            if (isTunnelRunning.Invoke())
                onStopTunnel.Invoke();

            DiagnosticLog.Write("TunnelHandler", "Stop returned");
        }
    }
}