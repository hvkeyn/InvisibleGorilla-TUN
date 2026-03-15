using System;
using System.Threading;

namespace InvisibleGorillaTUN.Managers
{
    using Core;
    using Foundation;
    using Handlers;
    using Values;

    public class ServiceManager
    {
        private InvisibleGorillaTunCore core;
        private HandlersManager handlersManager;
        private Func<int> getPort;

        private static Mutex mutex;
        private const string APP_GUID = "{8I7n9VIs-s9i2-84bl-A1em-12A5vN6eDH8M}";

        public ServiceManager(Func<int> getPort)
        {
            this.getPort = getPort;
        }

        public void Initialize()
        {
            DiagnosticLog.Write("ServiceManager", "Initialize start");
            AvoidRunningMultipleInstances();

            RegisterCore();
            RegisterHandlers();

            SetupHandlers();
            SetupCore();

            StartService();
            DiagnosticLog.Write("ServiceManager", "Initialize completed");
        }

        private void AvoidRunningMultipleInstances()
        {
            mutex = new Mutex(true, APP_GUID, out bool isCreatedNew);
            DiagnosticLog.Write("ServiceManager", $"Mutex createdNew={isCreatedNew}");
            if(!isCreatedNew)
            {
                DiagnosticLog.Write("ServiceManager", "Another service instance is already running");
                Console.WriteLine(Message.SERVICE_ALREADY_RUNNING);
                Environment.Exit(1);
            }
        }

        private void RegisterCore()
        {
            DiagnosticLog.Write("ServiceManager", "RegisterCore");
            core = new InvisibleGorillaTunCore();
        }

        private void RegisterHandlers()
        {
            DiagnosticLog.Write("ServiceManager", "RegisterHandlers");
            handlersManager = new HandlersManager();

            handlersManager.AddHandler(new SocketHandler());
            handlersManager.AddHandler(new TunnelHandler());
            handlersManager.AddHandler(new ProfileHandler());
        }

        private void SetupHandlers()
        {
            DiagnosticLog.Write("ServiceManager", "SetupHandlers");
            TunnelHandler tunnelHandler = handlersManager.GetHandler<TunnelHandler>();
            SocketHandler socketHandler = handlersManager.GetHandler<SocketHandler>();
            ProfileHandler profileHandler = handlersManager.GetHandler<ProfileHandler>();

            SetupSocketHandler();
            SetupTunnelHandler();

            void SetupSocketHandler()
            {
                DiagnosticLog.Write("ServiceManager", "Setup SocketHandler");
                socketHandler.Setup(
                    getPort: getPort,
                    onStartTunneling: tunnelHandler.Start,
                    onStopTunneling: tunnelHandler.Stop
                );
            }

            void SetupTunnelHandler()
            {
                DiagnosticLog.Write("ServiceManager", "Setup TunnelHandler");
                tunnelHandler.Setup(
                    onStopTunnel: core.StopTunnel,
                    onStartTunnel: core.StartTunnel,
                    onSetInterfaceAddress: core.SetInterfaceAddress,
                    onSetInterfaceDns: core.SetInterfaceDns,
                    onSetRoutes: core.SetRoutes,
                    isTunnelRunning: core.IsTunnelRunning,
                    getProfile: profileHandler.GetProfile
                );
            }
        }

        private void SetupCore()
        {
            DiagnosticLog.Write("ServiceManager", "SetupCore");
            SocketHandler socketHandler = handlersManager.GetHandler<SocketHandler>();

            core.Setup(
                onStartSocket: socketHandler.Start
            );
        }

        private void StartService()
        {
            DiagnosticLog.Write("ServiceManager", $"StartService port={getPort.Invoke()}");
            core.Start();
        }
    }
}