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
        private readonly string host;
        private readonly int port;

        public Program(string host, int port)
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
                    byte[] exponent = new byte[3];
                    int length = socket.Receive(exponent);

                    if (length != 3)
                    {
                        throw new Exception("Connection closed");
                    }

                    Console.WriteLine("Exponent [{0}]: {1}", exponent.Length, Convert.ToBase64String(exponent));

                    byte[] modulus = new byte[128];
                    length = socket.Receive(modulus);

                    if (length != 128)
                    {
                        throw new Exception("Connection closed");
                    }

                    Console.WriteLine("Modulus [{0}]: {1}", modulus.Length, Convert.ToBase64String(modulus));

                    RSAParameters rsaParameters = new RSAParameters();

                    rsaParameters.Exponent = exponent;
                    rsaParameters.Modulus = modulus;

                    RSA rsa = RSA.Create();

                    rsa.ImportParameters(rsaParameters);

                    byte[] data = new byte[1024];
                    length = socket.Receive(data);

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
