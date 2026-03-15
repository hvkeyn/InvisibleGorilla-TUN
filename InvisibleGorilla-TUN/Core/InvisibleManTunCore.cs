using System;

namespace InvisibleGorillaTUN.Core
{
    using Foundation;

    public class InvisibleGorillaTunCore
    {
        private Action onStartSocket;

        public void Setup(Action onStartSocket)
        {
            this.onStartSocket = onStartSocket;
        }

        public void Start()
        {
            DiagnosticLog.Write("TunCore", "Start socket loop");
            onStartSocket.Invoke();
        }

        public void StartTunnel(string device, string proxy)
        {
            DiagnosticLog.Write("TunCore", $"StartTunnel device={device}, proxy={proxy}");
            TunWrapper.StartTunnel(device, proxy);
            DiagnosticLog.Write("TunCore", "StartTunnel returned");
        }

        public void StopTunnel()
        {
            DiagnosticLog.Write("TunCore", "StopTunnel");
            TunWrapper.StopTunnel();
            DiagnosticLog.Write("TunCore", "StopTunnel returned");
        }

        public bool IsTunnelRunning()
        {
            bool isRunning = TunWrapper.IsTunnelRunning();
            DiagnosticLog.Write("TunCore", $"IsTunnelRunning={isRunning}");
            return isRunning;
        }

        public void SetInterfaceAddress(string device, string address)
        {
            DiagnosticLog.Write("TunCore", $"SetInterfaceAddress device={device}, address={address}");
            TunWrapper.SetInterfaceAddress(device, address);
            DiagnosticLog.Write("TunCore", "SetInterfaceAddress returned");
        }

        public void SetInterfaceDns(string device, string dns)
        {
            DiagnosticLog.Write("TunCore", $"SetInterfaceDns device={device}, dns={dns}");
            TunWrapper.SetInterfaceDns(device, dns);
            DiagnosticLog.Write("TunCore", "SetInterfaceDns returned");
        }

        public void SetRoutes(string server, string address, string gateway, int index)
        {
            DiagnosticLog.Write(
                "TunCore",
                $"SetRoutes server={server}, address={address}, gateway={gateway}, index={index}");
            TunWrapper.SetRoutes(server, address, gateway, index);
            DiagnosticLog.Write("TunCore", "SetRoutes returned");
        }
    }
}