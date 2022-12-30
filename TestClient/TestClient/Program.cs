using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TestClient
{
    class Program
    {
        private readonly Aes aes;

        private readonly string host;
        private readonly int port;

        public Program(string host, int port)
        {
            this.host = host;
            this.port = port;

            aes = Aes.Create();

            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            Console.WriteLine("IV [{0}]: {1}", aes.IV.Length, Convert.ToBase64String(aes.IV));
            Console.WriteLine("Key [{0}]: {1}", aes.Key.Length, Convert.ToBase64String(aes.Key));
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
                    RSA rsa = RSA.Create();

                    rsa.KeySize = 1024;

                    RSAParameters rsaParameters = rsa.ExportParameters(false);

                    if (socket.Receive(rsaParameters.Exponent) != rsaParameters.Exponent.Length)
                    {
                        throw new Exception("Connection closed");
                    }

                    if (socket.Receive(rsaParameters.Modulus) != rsaParameters.Modulus.Length)
                    {
                        throw new Exception("Connection closed");
                    }

                    rsa.ImportParameters(rsaParameters);

                    byte[] data = rsa.EncryptValue(aes.IV);

                    if (socket.Send(data, data.Length, SocketFlags.None) != data.Length)
                    {
                        throw new Exception("Connection closed");
                    }

                    data = rsa.EncryptValue(aes.Key);

                    if (socket.Send(data, data.Length, SocketFlags.None) != data.Length)
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

                while (true)
                {
                    string text = Console.ReadLine();

                    if (string.IsNullOrEmpty(text))
                    {
                        throw new Exception("Connection closed");
                    }

                    byte[] data = Encoding.ASCII.GetBytes(text);

                    if (data.Length > 1024)
                    {
                        throw new Exception("Connection closed");
                    }

                    if (socket.Send(data, data.Length, SocketFlags.None) != data.Length)
                    {
                        throw new Exception("Connection closed");
                    }

                    if (socket.Receive(data) != data.Length)
                    {
                        throw new Exception("Connection closed");
                    }

                    Console.WriteLine("[{0}]:{1}: {2}", socketEndPoint.Address, socketEndPoint.Port
                        , Encoding.ASCII.GetString(data, 0, data.Length));
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
                Thread.CurrentThread.IsBackground = true;

                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint socketEndPoint = new IPEndPoint(IPAddress.IPv6Any, port + 1);

                socket.Bind(socketEndPoint);

                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.IPv6Any, 0);

                while (true)
                {
                    byte[] data = new byte[1024];

                    int length = socket.ReceiveFrom(data, ref remoteEndPoint);

                    Console.WriteLine("[{0}]:{1}: {2}", ((IPEndPoint)remoteEndPoint).Address, ((IPEndPoint)remoteEndPoint).Port
                        , length == 0 ? "Failed to receive datagram" : Encoding.ASCII.GetString(data, 0, length));
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
                Console.WriteLine("{0} v{1}", System.Reflection.Assembly.GetEntryAssembly().GetName().Name
                    , System.Reflection.Assembly.GetEntryAssembly().GetName().Version);

                if (args.Length == 2)
                {
                    string host = args[0];
                    int port = int.Parse(args[1]);

                    Program program = new Program(host, port);
                    Thread thread = new Thread(program.HandleUdpClient);

                    thread.Start();
                    program.HandleTcpClient();
                }
                else
                {
                    throw new Exception("Usage: TestClient.exe host port");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
        }
    }
}
