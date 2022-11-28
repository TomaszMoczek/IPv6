using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TestClient
{
    class Program
    {
        private readonly String host;
        private readonly int port;

        public Program(String host, int port)
        {
            this.host = host;
            this.port = port;
        }

        public void HandleTcpClient()
        {
            Socket socket = null;

            try
            {
                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint socketEndPoint = new IPEndPoint(IPAddress.Parse(host), port);

                socket.Connect(socketEndPoint);

                {
                    byte[] data = new byte[1024];
                    int length = socket.Receive(data);

                    Console.WriteLine("[{0}]:{1}: {2}", socketEndPoint.Address, socketEndPoint.Port
                        , Encoding.ASCII.GetString(data, 0, length));
                }

                while (true)
                {
                    string text = Console.ReadLine();

                    if (String.IsNullOrEmpty(text))
                    {
                        throw new Exception("Connection closed");
                    }

                    byte[] data = Encoding.ASCII.GetBytes(text);
                    if (socket.Send(data, data.Length, SocketFlags.None) == 0)
                    {
                        throw new Exception("Connection closed");
                    }

                    data = new byte[1024];
                    int length = socket.Receive(data);

                    if (length == 0)
                    {
                        throw new Exception("Connection closed");
                    }

                    Console.WriteLine("[{0}]:{1}: {2}", socketEndPoint.Address, socketEndPoint.Port
                        , Encoding.ASCII.GetString(data, 0, length));
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
            finally
            {
                if (socket != null)
                {
                    socket.Close();
                }
            }
        }

        public void HandleUdpClient()
        {
            Socket socket = null;

            try
            {
                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint socketEndPoint = new IPEndPoint(IPAddress.IPv6Any, port);

                socket.Bind(socketEndPoint);

                EndPoint remoteEndPoint = (EndPoint)new IPEndPoint(IPAddress.IPv6Any, 0);

                while (true)
                {
                    byte[] data = new byte[1024];
                    int length = socket.ReceiveFrom(data, ref remoteEndPoint);

                    Console.WriteLine("[{0}]:{1}: {2}", ((IPEndPoint)remoteEndPoint).Address, ((IPEndPoint)remoteEndPoint).Port
                        , Encoding.ASCII.GetString(data, 0, length));
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
            finally
            {
                if (socket != null)
                {
                    socket.Close();
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 3)
                {
                    String protocol = args[0];
                    String host = args[1];
                    int port = Int32.Parse(args[2]);
                    Program program = new Program(host, port);

                    if (protocol.ToLower().Equals("tcp"))
                    {
                        program.HandleTcpClient();
                    }
                    else if (protocol.ToLower().Equals("udp"))
                    {
                        program.HandleUdpClient();
                    }
                    else
                    {
                        throw new Exception("Usage: TestClient.exe protocol host port");
                    }
                }
                else
                {
                    throw new Exception("Usage: TestClient.exe protocol host port");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
        }
    }
}
