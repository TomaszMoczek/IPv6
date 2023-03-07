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
            private readonly object obj;
            private readonly Socket socket;
            private readonly IPEndPoint endPoint;
            private readonly byte[] iv;
            private readonly byte[] key;
            private string plaintext;

            public Client(IPAddress address, int port, byte[] iv, byte[] key)
            {
                this.obj = new object();
                this.socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                this.socket.DualMode = true;
                this.endPoint = new IPEndPoint(address, port);
                this.iv = new byte[iv.Length];
                Buffer.BlockCopy(iv, 0, this.iv, 0, iv.Length);
                this.key = new byte[key.Length];
                Buffer.BlockCopy(key, 0, this.key, 0, key.Length);
                this.plaintext = "CONNECTED";
            }

            public Socket Socket
            {
                get { return socket; }
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
                get { lock (obj) { return plaintext; } }
                set { lock(obj) { plaintext = value; } }
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
                IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.IPv6Any, port);

                server = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);

                server.DualMode = true;

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
                if (server != null)
                {
                    server.Close();
                }
            }
        }

        public void HandleClient(object obj)
        {
            Socket client = null;
            Guid guid = Guid.NewGuid();

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

                int remoteport;

                byte[] IV = new byte[aes.IV.Length];
                byte[] Key = new byte[aes.Key.Length];

                {
                    {
                        if (client.Send(rsaParameters.Exponent, rsaParameters.Exponent.Length, SocketFlags.None) != rsaParameters.Exponent.Length)
                        {
                            throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                        }

                        if (client.Send(rsaParameters.Modulus, rsaParameters.Modulus.Length, SocketFlags.None) != rsaParameters.Modulus.Length)
                        {
                            throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                        }
                    }

                    {
                        byte[] data = new byte[rsaParameters.Modulus.Length];

                        int received = 0;
                        while (received < data.Length)
                        {
                            int _received = client.Receive(data, received, data.Length - received, SocketFlags.None);
                            if (_received == 0)
                            {
                                throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                            }
                            received += _received;
                        }

                        byte[] iv = rsa.Decrypt(data, RSAEncryptionPadding.Pkcs1);

                        received = 0;
                        while (received < data.Length)
                        {
                            int _received = client.Receive(data, received, data.Length - received, SocketFlags.None);
                            if (_received == 0)
                            {
                                throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                            }
                            received += _received;
                        }

                        byte[] key = rsa.Decrypt(data, RSAEncryptionPadding.Pkcs1);

                        Buffer.BlockCopy(iv, iv.Length - IV.Length, IV, 0, IV.Length);
                        Buffer.BlockCopy(key, key.Length - Key.Length, Key, 0, Key.Length);
                    }

                    {
                        string plaintext = string.Format("{0} v{1}", "Welcome to the IPv6 Server", System.Reflection.Assembly.GetEntryAssembly().GetName().Version);

                        byte[] ciphertext = Encrypt(plaintext, IV, Key);

                        byte[] bytes = BitConverter.GetBytes(ciphertext.Length);

                        if (client.Send(bytes, bytes.Length, SocketFlags.None) != bytes.Length)
                        {
                            throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                        }

                        if (client.Send(ciphertext, ciphertext.Length, SocketFlags.None) != ciphertext.Length)
                        {
                            throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                        }
                    }

                    {
                        byte[] bytes = BitConverter.GetBytes(int.MaxValue);

                        int received = 0;
                        while (received < bytes.Length)
                        {
                            int _received = client.Receive(bytes, received, bytes.Length - received, SocketFlags.None);
                            if (_received == 0)
                            {
                                throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                            }
                            received += _received;
                        }

                        int length = BitConverter.ToInt32(bytes, 0);

                        byte[] ciphertext = new byte[length];

                        received = 0;
                        while (received < ciphertext.Length)
                        {
                            int _received = client.Receive(ciphertext, received, ciphertext.Length - received, SocketFlags.None);
                            if (_received == 0)
                            {
                                throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                            }
                            received += _received;
                        }

                        string plaintext = Decrypt(ciphertext, IV, Key);

                        remoteport = int.Parse(plaintext);
                    }
                }

                int count = OnConnected(guid, clientEndPoint.Address, remoteport, IV, Key);

                Console.WriteLine("Connection count: {0}", count);

                while (true)
                {
                    byte[] bytes = BitConverter.GetBytes(int.MaxValue);

                    int received = 0;
                    while (received < bytes.Length)
                    {
                        int _received = client.Receive(bytes, received, bytes.Length - received, SocketFlags.None);
                        if (_received == 0)
                        {
                            throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                        }
                        received += _received;
                    }

                    int length = BitConverter.ToInt32(bytes, 0);

                    byte[] ciphertext = new byte[length];

                    received = 0;
                    while (received < ciphertext.Length)
                    {
                        int _received = client.Receive(ciphertext, received, ciphertext.Length - received, SocketFlags.None);
                        if (_received == 0)
                        {
                            throw new Exception(string.Format("Connection closed: [{0}]:{1}", clientEndPoint.Address, clientEndPoint.Port));
                        }
                        received += _received;
                    }

                    string plaintext = Decrypt(ciphertext, IV, Key);

                    OnDataReceived(guid, plaintext);
                }
            }
            catch (SocketException exception)
            {
                Console.WriteLine("{0} [Error code: {1}]", exception.Message, exception.ErrorCode);

                int count = OnDisconnected(guid);

                if (count != -1)
                {
                    Console.WriteLine("Connection count: {0}", count);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);

                int count = OnDisconnected(guid);

                if (count != -1)
                {
                    Console.WriteLine("Connection count: {0}", count);
                }
            }
            finally
            {
                if (client != null)
                {
                    client.Close();
                }
            }
        }

        private int OnConnected(Guid guid, IPAddress address, int port, byte[] iv, byte[] key)
        {
            Guid[] guids;
            int count = 0;
            Client client = new Client(address, port, iv, key);

            lock (obj)
            {
                guids = new Guid[clients.Keys.Count];
                clients.Keys.CopyTo(guids, 0);
                clients.Add(guid, client);
                count = clients.Count;
            }

            foreach (Guid _guid in guids)
            {
                Client _client = null;

                lock (obj)
                {
                    clients.TryGetValue(_guid, out _client);
                }

                if (_client != null)
                {
                    {
                        string plaintext = _guid.ToString() + "|" + _client.Plaintext;
                        byte[] ciphertext = Encrypt(plaintext, client.IV, client.Key);

                        if (client.Socket.SendTo(ciphertext, ciphertext.Length, SocketFlags.None, client.EndPoint) != ciphertext.Length)
                        {
                            Console.WriteLine("Failed to send datagram: [{0}]:{1}", client.EndPoint.Address, client.EndPoint.Port);
                        }
                    }

                    {
                        string plaintext = guid.ToString() + "|" + client.Plaintext;
                        byte[] ciphertext = Encrypt(plaintext, _client.IV, _client.Key);

                        if (_client.Socket.SendTo(ciphertext, ciphertext.Length, SocketFlags.None, _client.EndPoint) != ciphertext.Length)
                        {
                            Console.WriteLine("Failed to send datagram: [{0}]:{1}", _client.EndPoint.Address, _client.EndPoint.Port);
                        }
                    }
                }
            }

            return count;
        }

        private void OnDataReceived(Guid guid, string plaintext)
        {
            Guid[] guids;

            lock (obj)
            {
                guids = new Guid[clients.Keys.Count];
                clients.Keys.CopyTo(guids, 0);
                clients[guid].Plaintext = plaintext;
            }

            foreach (Guid _guid in guids)
            {
                if (!_guid.Equals(guid))
                {
                    Client _client = null;

                    lock (obj)
                    {
                        clients.TryGetValue(_guid, out _client);
                    }

                    if (_client != null)
                    {
                        string _plaintext = guid.ToString() + "|" + plaintext;
                        byte[] ciphertext = Encrypt(_plaintext, _client.IV, _client.Key);

                        if (_client.Socket.SendTo(ciphertext, ciphertext.Length, SocketFlags.None, _client.EndPoint) != ciphertext.Length)
                        {
                            Console.WriteLine("Failed to send datagram: [{0}]:{1}", _client.EndPoint.Address, _client.EndPoint.Port);
                        }
                    }
                }
            }
        }

        private int OnDisconnected(Guid guid)
        {
            int count = -1;
            Guid[] guids = null;

            try
            {
                lock (obj)
                {
                    if (clients.Remove(guid))
                    {
                        guids = new Guid[clients.Keys.Count];
                        clients.Keys.CopyTo(guids, 0);
                        count = clients.Count;
                    }
                }

                if (guids != null)
                {
                    foreach (Guid _guid in guids)
                    {
                        Client _client = null;

                        lock (obj)
                        {
                            clients.TryGetValue(_guid, out _client);
                        }

                        if (_client != null)
                        {
                            string plaintext = guid.ToString() + "|DISCONNECTED";
                            byte[] ciphertext = Encrypt(plaintext, _client.IV, _client.Key);

                            if (_client.Socket.SendTo(ciphertext, ciphertext.Length, SocketFlags.None, _client.EndPoint) != ciphertext.Length)
                            {
                                Console.WriteLine("Failed to send datagram: [{0}]:{1}", _client.EndPoint.Address, _client.EndPoint.Port);
                            }
                        }
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

            return count;
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

        private string Decrypt(byte[] ciphertext, byte[] IV, byte[] Key)
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

                using (MemoryStream memoryStream = new MemoryStream(ciphertext))
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
                    throw new Exception("Usage: " + System.Reflection.Assembly.GetEntryAssembly().GetName().Name + " port");
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
            }
        }
    }
}
