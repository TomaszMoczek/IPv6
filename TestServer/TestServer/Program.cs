using System;
using System.Collections.Generic;
using System.IO;
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

        private class Client
        {
            private readonly IPEndPoint endPoint;
            private readonly byte[] iv;
            private readonly byte[] key;

            private string plaintext;

            public Client(IPAddress address, int port, byte[] iv, byte[] key)
            {
                this.endPoint = new IPEndPoint(address, port);
                this.iv = new byte[iv.Length];
                Buffer.BlockCopy(iv, 0, this.iv, 0, iv.Length);
                this.key = new byte[key.Length];
                Buffer.BlockCopy(key, 0, this.key, 0, key.Length);

                this.plaintext = String.Empty;
            }

            public IPEndPoint EndPoint
            {
                get { return endPoint; }
            }

            public byte[] IV
            {
                get { return iv; }
            }

            public byte[] Key
            {
                get { return key; }
            }

            public string Plaintext
            {
                get { return plaintext; }
                set { plaintext = value; }
            }
        }

        private readonly object obj;

        private readonly Dictionary<Guid, Client> clients;

        public Program(int port)
        {
            this.port = port;

            rsa = RSA.Create();

            rsa.KeySize = 1024;

            rsaParameters = rsa.ExportParameters(false);

            obj = new object();

            clients = new Dictionary<Guid, Client>();
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

                IPEndPoint socketEndPoint;

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

                    string plaintext = "Welcome to the IPv6 Server!";

                    byte[] ciphertext = Encrypt(plaintext, IV, Key);

                    if (client.Send(ciphertext, ciphertext.Length, SocketFlags.None) != ciphertext.Length)
                    {
                        throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                    }

                    int length = client.Receive(ciphertext);

                    if (length == 0)
                    {
                        throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                    }

                    plaintext = Decrypt(ciphertext, length, IV, Key);

                    socketEndPoint = new IPEndPoint(clientEndPoint.Address, int.Parse(plaintext));
                }

                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

                while (true)
                {
                    string plaintext;

                    {
                        byte[] ciphertext = new byte[1024];

                        int length = client.Receive(ciphertext);

                        if (length == 0)
                        {
                            throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                        }

                        plaintext = Decrypt(ciphertext, length, IV, Key);
                    }

                    {
                        byte[] ciphertext = Encrypt(plaintext, IV, Key);

                        if (socket.SendTo(ciphertext, ciphertext.Length, SocketFlags.None, socketEndPoint) != ciphertext.Length)
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

        private void OnConnected(Socket socket, Guid guid, IPAddress address, int port, byte[] iv, byte[] key)
        {
            Client client = new Client(address, port, iv, key);

            lock(obj)
            {
                clients.Add(guid, client);
            }
        }

        private void OnDataReceived(Socket socket, Guid guid, string plaintext)
        {
            lock(obj)
            {
                clients[guid].Plaintext = plaintext;
            }
        }

        private void OnDisconnected(Socket socket, Guid guid)
        {
            lock(obj)
            {
                clients.Remove(guid);
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
