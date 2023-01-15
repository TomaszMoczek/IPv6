using System;
using System.IO;
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
        private readonly int localport;

        public Program(string host, int port)
        {
            this.host = host;
            this.port = port;
            this.localport = new Random().Next(49152, 65535);

            aes = Aes.Create();

            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
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

                    int received = 0;
                    while (received < rsaParameters.Exponent.Length)
                    {
                        int _received = socket.Receive(rsaParameters.Exponent, received, rsaParameters.Exponent.Length - received, SocketFlags.None);
                        if (_received == 0)
                        {
                            throw new Exception("Connection closed");
                        }
                        received += _received;
                    }

                    received = 0;
                    while (received < rsaParameters.Modulus.Length)
                    {
                        int _received = socket.Receive(rsaParameters.Modulus, received, rsaParameters.Modulus.Length - received, SocketFlags.None);
                        if (_received == 0)
                        {
                            throw new Exception("Connection closed");
                        }
                        received += _received;
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

                    byte[] bytes = BitConverter.GetBytes(int.MaxValue);

                    received = 0;
                    while (received < bytes.Length)
                    {
                        int _received = socket.Receive(bytes, received, bytes.Length - received, SocketFlags.None);
                        if (_received == 0)
                        {
                            throw new Exception("Connection closed");
                        }
                        received += _received;
                    }

                    int length = BitConverter.ToInt32(bytes, 0);

                    byte[] ciphertext = new byte[length];

                    received = 0;
                    while (received < ciphertext.Length)
                    {
                        int _received = socket.Receive(ciphertext, received, ciphertext.Length - received, SocketFlags.None);
                        if (_received == 0)
                        {
                            throw new Exception("Connection closed");
                        }
                        received += _received;
                    }

                    string plaintext = Decrypt(ciphertext, ciphertext.Length, aes.IV, aes.Key);

                    Console.WriteLine("[{0}]:{1}: {2}", socketEndPoint.Address, socketEndPoint.Port, plaintext);

                    plaintext = localport.ToString();

                    ciphertext = Encrypt(plaintext, aes.IV, aes.Key);

                    bytes = BitConverter.GetBytes(ciphertext.Length);

                    if (socket.Send(bytes, bytes.Length, SocketFlags.None) != bytes.Length)
                    {
                        throw new Exception("Connection closed");
                    }

                    if (socket.Send(ciphertext, ciphertext.Length, SocketFlags.None) != ciphertext.Length)
                    {
                        throw new Exception("Connection closed");
                    }
                }

                while (true)
                {
                    string plaintext = Console.ReadLine();

                    if (string.IsNullOrEmpty(plaintext))
                    {
                        throw new Exception("Connection closed");
                    }

                    byte[] ciphertext = Encrypt(plaintext, aes.IV, aes.Key);

                    if (ciphertext.Length > 1024)
                    {
                        throw new Exception("Connection closed");
                    }

                    byte[] bytes = BitConverter.GetBytes(ciphertext.Length);

                    if (socket.Send(bytes, bytes.Length, SocketFlags.None) != bytes.Length)
                    {
                        throw new Exception("Connection closed");
                    }

                    if (socket.Send(ciphertext, ciphertext.Length, SocketFlags.None) != ciphertext.Length)
                    {
                        throw new Exception("Connection closed");
                    }
                }
            }
            catch (SocketException exception)
            {
                Console.WriteLine("{0} [Error code: {1}]", exception.Message, exception.ErrorCode);
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
                IPEndPoint socketEndPoint = new IPEndPoint(IPAddress.IPv6Any, localport);

                socket.Bind(socketEndPoint);

                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.IPv6Any, 0);

                while (true)
                {
                    byte[] ciphertext = new byte[1024];

                    int length = socket.ReceiveFrom(ciphertext, 0, ciphertext.Length, SocketFlags.None, ref remoteEndPoint);

                    if (length == 0)
                    {
                        Console.WriteLine("[{0}]:{1}: {2}", ((IPEndPoint)remoteEndPoint).Address, ((IPEndPoint)remoteEndPoint).Port
                            , "Failed to receive datagram");
                    }
                    else
                    {
                        string plaintext = Decrypt(ciphertext, length, aes.IV, aes.Key);

                        Console.WriteLine("[{0}]:{1}: {2}", ((IPEndPoint)remoteEndPoint).Address, ((IPEndPoint)remoteEndPoint).Port
                            , plaintext);
                    }
                }
            }
            catch (SocketException exception)
            {
                Console.WriteLine("{0} [Error code: {1}]", exception.Message, exception.ErrorCode);
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

        private byte[] Encrypt(string plaintext, byte[] IV, byte[] Key)
        {
            byte[] ciphertext;

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                aes.IV = IV;
                aes.Key = Key;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter(cryptoStream))
                        {
                            streamWriter.Write(plaintext);
                        }
                    }
                    ciphertext = memoryStream.ToArray();
                }
            }

            return ciphertext;
        }

        private string Decrypt(byte[] ciphertext, int length, byte[] IV, byte[] Key)
        {
            string plaintext;

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                aes.IV = IV;
                aes.Key = Key;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream(ciphertext, 0, length))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader(cryptoStream))
                        {
                            plaintext = streamReader.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
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
