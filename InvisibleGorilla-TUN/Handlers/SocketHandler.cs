using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace InvisibleGorillaTUN.Handlers
{
    using Foundation;
    using Values;

    public class SocketHandler : Handler
    {
        private IPEndPoint endPoint;
        private Socket listener;

        private Func<int> getPort;
        private Action<string, string, string, string, string> onStartTunneling;
        private Action onStopTunneling;

        public void Setup(
            Func<int> getPort, 
            Action<string, string, string, string, string> onStartTunneling, 
            Action onStopTunneling
        )
        {
            this.getPort = getPort;
            this.onStartTunneling = onStartTunneling;
            this.onStopTunneling = onStopTunneling;
        }

        public void Start()
        {
            try
            {
                int port = getPort.Invoke();
                DiagnosticLog.Write("SocketHandler", $"Start requested on 127.0.0.1:{port}");

                endPoint = new IPEndPoint(IPAddress.Loopback, port);
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                listener.Bind(endPoint);
                listener.Listen(1);
                DiagnosticLog.Write("SocketHandler", "Listener bound and listening");

                Console.WriteLine(Message.WAITING_FOR_CONNECTION);
                Socket clientSocket = listener.Accept();
                DiagnosticLog.Write("SocketHandler", $"Client connected from {clientSocket.RemoteEndPoint}");

                Console.WriteLine(Message.CLIENT_WAS_CONNECTED);
                Listen(clientSocket);
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteException("SocketHandler.Start", ex);
                Console.WriteLine(ex.Message);
            }
            finally
            {
                DiagnosticLog.Write("SocketHandler", "Invoking onStopTunneling from Start finally");
                onStopTunneling.Invoke();
            }

            void Listen(Socket socket)
            {
                try
                {
                    while (IsSocketConnected())
                    {
                        byte[] bytes = new byte[1024];
                        string command = "";

                        while (true)
                        {
                            int bytesCount = socket.Receive(bytes);
                            command += Encoding.ASCII.GetString(bytes, 0, bytesCount);

                            if (command.IndexOf(Command.EOF) > -1)
                                break;
                        }

                        DiagnosticLog.Write("SocketHandler", $"Raw command: {command}");
                        Console.WriteLine($"Receive command: '{command}'");
                        string latestCommand = FetchLatestCommand();
                        
                        DiagnosticLog.Write("SocketHandler", $"Latest command: {latestCommand}");
                        Console.WriteLine($"Execute command: '{latestCommand}'");
                        Execute(latestCommand);

                        string FetchLatestCommand()
                        {
                            return latestCommand = command.Split(Command.EOF).Last(
                                cmd => !string.IsNullOrEmpty(cmd)
                            );
                        }
                    }

                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
                catch(Exception ex)
                {
                    DiagnosticLog.WriteException("SocketHandler.Listen", ex);
                    Console.WriteLine(ex.Message);
                }

                bool IsSocketConnected()
                {
                    return socket != null && (
                        !(
                            socket.Poll(1000, SelectMode.SelectRead) && socket.Available == 0
                        ) || socket.Connected
                    );
                }
            }

            void Execute(string command)
            {
                string firstArgument = command.Split(" ").FirstOrDefault();
                DiagnosticLog.Write("SocketHandler", $"Execute firstArgument={firstArgument}");

                switch(firstArgument)
                {
                    case Command.ENABLE:
                        Enable();
                        break;
                    case Command.DISABLE:
                        Disable();
                        break;
                    default:
                        break;
                }

                void Enable()
                {
                    DiagnosticLog.Write("SocketHandler", "Enable command parsing started");
                    Parser parser = new Parser(new[] {
                        Global.COMMAND,
                        Global.DEVICE,
                        Global.PROXY,
                        Global.ADDRESS,
                        Global.SERVER,
                        Global.DNS
                    });

                    parser.Parse(command);
                    DiagnosticLog.Write(
                        "SocketHandler",
                        $"Enable parsed device={parser.GetFlag(Global.DEVICE)?.Value}, " +
                        $"proxy={parser.GetFlag(Global.PROXY)?.Value}, " +
                        $"address={parser.GetFlag(Global.ADDRESS)?.Value}, " +
                        $"server={parser.GetFlag(Global.SERVER)?.Value}, " +
                        $"dns={parser.GetFlag(Global.DNS)?.Value}"
                    );

                    onStartTunneling.Invoke(
                      parser.GetFlag(Global.DEVICE).Value,
                      parser.GetFlag(Global.PROXY).Value,
                      parser.GetFlag(Global.ADDRESS).Value,
                      parser.GetFlag(Global.SERVER).Value,
                      parser.GetFlag(Global.DNS).Value  
                    );
                    DiagnosticLog.Write("SocketHandler", "Enable command finished");
                }

                void Disable()
                {
                    DiagnosticLog.Write("SocketHandler", "Disable command received");
                    onStopTunneling.Invoke();
                }
            }
        }
    }
}