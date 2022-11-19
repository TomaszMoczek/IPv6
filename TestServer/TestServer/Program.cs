using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TestServer
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 2 && args[0].ToLower().Equals("tcp"))
                {
                    IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.IPv6Any, Int32.Parse(args[1]));
                    Socket server = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

                    server.Bind(serverEndPoint);
                    server.Listen(10);

                    Console.WriteLine("Waiting for the TCPv6 Client ...");
                    Console.WriteLine();

                    Socket client = server.Accept();
                    IPEndPoint clientEndPoint = (IPEndPoint)client.RemoteEndPoint;

                    Console.WriteLine("Connected with {0} at port {1}:", clientEndPoint.Address, clientEndPoint.Port);
                    Console.WriteLine();

                    {
                        byte[] data = Encoding.ASCII.GetBytes("Welcome to the TCPv6 Test Server !");
                        client.Send(data, data.Length, SocketFlags.None);
                    }

                    while (true)
                    {
                        byte[] data = new byte[1024];
                        int bytes = client.Receive(data);

                        if (bytes == 0)
                        {
                            break;
                        }

                        Console.WriteLine(Encoding.ASCII.GetString(data, 0, bytes));
                    }

                    client.Close();
                    server.Close();

                    Console.WriteLine();
                    Console.WriteLine("Disconnected with {0}", clientEndPoint.Address);
                }
                else if (args.Length == 3 && args[0].ToLower().Equals("udp"))
                {
                    UdpClient udpClient = new UdpClient(Int32.Parse(args[2]), AddressFamily.InterNetworkV6);
                    
                    udpClient.JoinMulticastGroup(IPAddress.Parse(args[1]));

                    Console.WriteLine("Waiting for the UDPv6 Messages ... ");
                    Console.WriteLine();

                    while (true)
                    {
                        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.IPv6Any, 0);
                        byte[] data = udpClient.Receive(ref remoteEndPoint);
                        string message = Encoding.ASCII.GetString(data);

                        if (String.IsNullOrEmpty(message))
                        {
                            break;
                        }

                        Console.WriteLine("Message from {0} at port {1}: {2}", remoteEndPoint.Address, remoteEndPoint.Port, message);
                    }

                    udpClient.Close();

                    Console.WriteLine();
                    Console.WriteLine("Stopped waiting for the UDPv6 Messages");
                }
                else
                {
                    throw new Exception("Input arguments are not recognizable !");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
            Console.ReadKey();
        }
    }
}
