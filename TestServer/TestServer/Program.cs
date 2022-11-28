using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TestServer
{
    class Program
    {
        private readonly int port;

        public Program(int port)
        {
            this.port = port;
        }

        public void HandleServer()
        {
            Socket server = null;

            try
            {
                server = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.IPv6Any, port);

                server.Bind(serverEndPoint);
                server.Listen(int.MaxValue);

                Console.WriteLine("Waiting for the TCPv6 connections");

                while (true)
                {
                    Socket client = server.Accept();

                    HandleClient(client);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
            finally
            {
                if (server != null)
                {
                    server.Close();
                }
            }
        }

        public void HandleClient(Object obj)
        {
            Socket socket = null;
            Socket client = (Socket)obj;

            try
            {
                IPEndPoint clientEndPoint = (IPEndPoint)client.RemoteEndPoint;

                Console.WriteLine("Connection accepted: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port);

                {
                    byte[] data = Encoding.ASCII.GetBytes("Welcome to the IPv6 Server!");
                    client.Send(data, data.Length, SocketFlags.None);
                }

                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint socketEndPoint = new IPEndPoint(clientEndPoint.Address, port + 1);

                while (true)
                {
                    byte[] data = new byte[1024];
                    int length = client.Receive(data);

                    if (length == 0)
                    {
                        throw new Exception(String.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                    }

                    client.Send(data, length, SocketFlags.None);

                    for (int i = 0; i < 2; ++i)
                    {
                        socket.SendTo(data, length, SocketFlags.None, socketEndPoint);
                    }
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

                client.Close();
            }
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 1)
                {
                    int port = Int32.Parse(args[0]);
                    Program program = new Program(port);

                    program.HandleServer();
                }
                else
                {
                    throw new Exception("Usage: TestServer.exe port");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
        }
    }
}
