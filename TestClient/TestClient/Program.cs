using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 3 && args[0].ToLower().Equals("tcp"))
                {
                    IPEndPoint server = new IPEndPoint(IPAddress.Parse(args[1]), Int32.Parse(args[2]));
                    Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

                    socket.Connect(server);

                    if (socket.Connected)
                    {
                        Console.WriteLine("Connected with {0} at port {1}", server.Address, server.Port);
                        Console.WriteLine();

                        {
                            byte[] data = new byte[1024];
                            int bytes = socket.Receive(data, data.Length, SocketFlags.None);

                            Console.WriteLine(Encoding.ASCII.GetString(data, 0, bytes));
                            Console.WriteLine();
                        }

                        while (true)
                        {
                            string text = Console.ReadLine();

                            if (String.IsNullOrEmpty(text))
                            {
                                break;
                            }

                            byte[] data = Encoding.ASCII.GetBytes(text);

                            socket.Send(data, data.Length, SocketFlags.None);
                        }

                        socket.Close();

                        Console.WriteLine("Disconnected with {0}", server.Address);
                    }
                }
                else if (args.Length == 3 && args[0].ToLower().Equals("udp"))
                {
                    IPEndPoint server = new IPEndPoint(IPAddress.Parse(args[1]), Int32.Parse(args[2]));
                    Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, new IPv6MulticastOption(server.Address));

                    Console.WriteLine("Sending UDPv6 Messages to {0} at port {1}", server.Address, server.Port);
                    Console.WriteLine();

                    while (true)
                    {
                        string text = Console.ReadLine();
                        byte[] data = Encoding.ASCII.GetBytes(text);

                        socket.SendTo(data, server);

                        if (String.IsNullOrEmpty(text))
                        {
                            break;
                        }
                    }

                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.DropMembership, new IPv6MulticastOption(server.Address));
                    socket.Close();

                    Console.WriteLine();
                    Console.WriteLine("Stopped sending the UDPv6 Messages");
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
