using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TestServer
{
    class Program
    {
        private readonly RSA rsa;
        private RSAParameters rsaParameters;

        private readonly int port;

        public Program(int port)
        {
            this.port = port;

            rsa = RSA.Create();

            rsa.KeySize = 1024;

            rsaParameters = rsa.ExportParameters(false);

            Console.WriteLine("Exponent [{0}]: {1}", rsaParameters.Exponent.Length, Convert.ToBase64String(rsaParameters.Exponent));
            Console.WriteLine("Modulus [{0}]: {1}", rsaParameters.Modulus.Length, Convert.ToBase64String(rsaParameters.Modulus));
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
                    Thread thread = new Thread(new ParameterizedThreadStart(HandleClient));

                    thread.Start(client);
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

        public void HandleClient(object obj)
        {
            Socket client = null;
            Socket socket = null;

            try
            {
                Thread.CurrentThread.IsBackground = true;

                client = (Socket)obj;
                IPEndPoint clientEndPoint = (IPEndPoint)client.RemoteEndPoint;

                Console.WriteLine("Connection accepted: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port);

                Aes aes = Aes.Create();

                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                byte[] IV = new byte[aes.IV.Length];
                byte[] Key = new byte[aes.Key.Length];

                {
                    if (client.Send(rsaParameters.Exponent, rsaParameters.Exponent.Length, SocketFlags.None) != rsaParameters.Exponent.Length)
                    {
                        throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                    }

                    if (client.Send(rsaParameters.Modulus, rsaParameters.Modulus.Length, SocketFlags.None) != rsaParameters.Modulus.Length)
                    {
                        throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                    }

                    byte[] data = new byte[rsaParameters.Modulus.Length];

                    if (client.Receive(data) != data.Length)
                    {
                        throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                    }

                    byte[] iv = rsa.DecryptValue(data);

                    if (client.Receive(data) != data.Length)
                    {
                        throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                    }

                    byte[] key = rsa.DecryptValue(data);

                    Buffer.BlockCopy(iv, iv.Length - IV.Length, IV, 0, IV.Length);
                    Buffer.BlockCopy(key, key.Length - Key.Length, Key, 0, Key.Length);

                    Console.WriteLine("[{0}]:{1}: IV [{2}]: {3}", clientEndPoint.Address, clientEndPoint.Port, IV.Length, Convert.ToBase64String(IV));
                    Console.WriteLine("[{0}]:{1}: Key [{2}]: {3}", clientEndPoint.Address, clientEndPoint.Port, Key.Length, Convert.ToBase64String(Key));

                    data = Encoding.ASCII.GetBytes("Welcome to the IPv6 Server!");

                    if (client.Send(data, data.Length, SocketFlags.None) != data.Length)
                    {
                        throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                    }
                }

                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint socketEndPoint = new IPEndPoint(clientEndPoint.Address, port + 1);

                while (true)
                {
                    byte[] data = new byte[1024];

                    int length = client.Receive(data);

                    if (length == 0)
                    {
                        throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                    }

                    if (client.Send(data, length, SocketFlags.None) != length)
                    {
                        throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                    }

                    for (int i = 0; i < 2; ++i)
                    {
                        if (socket.SendTo(data, length, SocketFlags.None, socketEndPoint) != length)
                        {
                            Console.WriteLine("Failed to send datagram: [{0}]:{1}", socketEndPoint.Address, socketEndPoint.Port);
                        }
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
                if (client != null)
                {
                    client.Close();
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("{0} v{1}", System.Reflection.Assembly.GetEntryAssembly().GetName().Name
                    , System.Reflection.Assembly.GetEntryAssembly().GetName().Version);

                if (args.Length == 1)
                {
                    int port = int.Parse(args[0]);

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
