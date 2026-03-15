using System.Runtime.InteropServices;

namespace InvisibleGorillaTUN.Core
{
    using System;
    using Foundation;
    using Values;

    internal class TunWrapper
    {
        public static void StartTunnel(string device, string proxy)
        {
            try
            {
                DiagnosticLog.Write("TunWrapper", $"Native StartTunnel call: device={device}, proxy={proxy}");
                StartTunnelNative(device, proxy);
                DiagnosticLog.Write("TunWrapper", "Native StartTunnel returned");
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("TunWrapper.StartTunnel", ex);
                throw;
            }

            [DllImport(Path.TUN_DLL, EntryPoint = "StartTunnel")]
            static extern void StartTunnelNative(string device, string proxy);
        }

        public static void StopTunnel()
        {
            try
            {
                DiagnosticLog.Write("TunWrapper", "Native StopTunnel call");
                StopTunnelNative();
                DiagnosticLog.Write("TunWrapper", "Native StopTunnel returned");
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("TunWrapper.StopTunnel", ex);
                throw;
            }

            [DllImport(Path.TUN_DLL, EntryPoint = "StopTunnel")]
            static extern void StopTunnelNative();
        }

        public static bool IsTunnelRunning()
        {
            try
            {
                bool isRunning = IsTunnelRunningNative();
                DiagnosticLog.Write("TunWrapper", $"Native IsTunnelRunning={isRunning}");
                return isRunning;
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("TunWrapper.IsTunnelRunning", ex);
                throw;
            }

            [DllImport(Path.TUN_DLL, EntryPoint = "IsTunnelRunning")]
            static extern bool IsTunnelRunningNative();
        }

        public static void SetInterfaceAddress(string device, string address)
        {
            try
            {
                DiagnosticLog.Write("TunWrapper", $"Native SetInterfaceAddress: device={device}, address={address}");
                SetInterfaceAddressNative(device, address);
                DiagnosticLog.Write("TunWrapper", "Native SetInterfaceAddress returned");
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("TunWrapper.SetInterfaceAddress", ex);
                throw;
            }

            [DllImport(Path.TUN_DLL, EntryPoint = "SetInterfaceAddress")]
            static extern void SetInterfaceAddressNative(string device, string address);
        }

        public static void SetInterfaceDns(string device, string dns)
        {
            try
            {
                DiagnosticLog.Write("TunWrapper", $"Native SetInterfaceDns: device={device}, dns={dns}");
                SetInterfaceDnsNative(device, dns);
                DiagnosticLog.Write("TunWrapper", "Native SetInterfaceDns returned");
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("TunWrapper.SetInterfaceDns", ex);
                throw;
            }

            [DllImport(Path.TUN_DLL, EntryPoint = "SetInterfaceDns")]
            static extern void SetInterfaceDnsNative(string device, string dns);
        }

        public static void SetRoutes(string server, string address, string gateway, int index)
        {
            try
            {
                DiagnosticLog.Write(
                    "TunWrapper",
                    $"Native SetRoutes: server={server}, address={address}, gateway={gateway}, index={index}");
                SetRoutesNative(server, address, gateway, index);
                DiagnosticLog.Write("TunWrapper", "Native SetRoutes returned");
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("TunWrapper.SetRoutes", ex);
                throw;
            }

            [DllImport(Path.TUN_DLL, EntryPoint = "SetRoutes")]
            static extern void SetRoutesNative(string server, string address, string gateway, int index);
        }
    }
}