using System;
using System.Linq;
using System.Reflection;
using System.Security.Principal;

namespace InvisibleGorillaTUN
{
    using Foundation;
    using Managers;
    using Values;

    public class Program
    {
        private static void Main(string[] args)
        {
            DiagnosticLog.Clear();
            DiagnosticLog.Write("Program", $"Service boot args: {string.Join(" ", args ?? Array.Empty<string>())}");
            DiagnosticLog.Write("Program", $"BaseDirectory={AppDomain.CurrentDomain.BaseDirectory}");
            DiagnosticLog.Write("Program", $"CurrentDirectory={System.IO.Directory.GetCurrentDirectory()}");
            DiagnosticLog.Write("Program", $"IsAdministrator={IsAdministrator()}");
            LogRuntimeFiles();

            PrintHeadLines();
            InitializeServiceManager();

            void PrintHeadLines()
            {
                Console.WriteLine("Invisible Gorilla TUN service");
                Console.WriteLine($"version {GetCurrentReleaseVersion()}\n");
                Console.WriteLine("usage: InvisibleGorilla-TUN -port={port}\n");
            }

            void InitializeServiceManager()
            {
                DiagnosticLog.Write("Program", "Creating ServiceManager");
                ServiceManager serviceManager = new ServiceManager(GetPort);
                serviceManager.Initialize();
            }

            string GetCurrentReleaseVersion() 
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }

            int GetPort()
            {
                Parser parser = new Parser(validFlags: new[] { Global.PORT });
                parser.Parse(args);

                if (!IsPortFlagExists())
                {
                    DiagnosticLog.Write("Program", "Port flag was not provided");
                    return -1;
                }

                try
                {
                    int port = Convert.ToInt32(parser.GetFlag(Global.PORT).Value);
                    DiagnosticLog.Write("Program", $"Parsed port={port}");
                    return port;
                }
                catch (Exception ex)
                {
                    DiagnosticLog.WriteException("Program.GetPort", ex);
                    return -1;
                }

                bool IsPortFlagExists() => parser.GetFlag(Global.PORT) != null;
            }

            void LogRuntimeFiles()
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] files = { "tun.dll", "tun2socks.exe", "wintun.dll" };

                foreach (string file in files)
                {
                    string fullPath = System.IO.Path.Combine(baseDir, file);
                    DiagnosticLog.Write("Program", $"{file} exists={System.IO.File.Exists(fullPath)} path={fullPath}");
                }
            }

            bool IsAdministrator()
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}