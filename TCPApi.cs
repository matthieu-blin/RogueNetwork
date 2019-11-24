using System;
using System.Collections.Generic;
using System.Linq;

using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Serialization.Formatters;
using System.IO;
using System.Security;

namespace Assets
{
    public class TCPApi
    {
        public delegate void LogDelegate (string txt);
        public delegate int MessageCallback(Message msg);

        public LogDelegate Log;
        public MessageCallback OnMessageReceived;

        public static EndPoint ComputeEndPoint(string host, int port)
        {
            IPAddress ipAddress;
            if (host.Length == 0)
            {
                IPHostEntry ipHost = Dns.GetHostEntry(Dns.GetHostName());
                ipAddress = ipHost.AddressList[0];
            }
            else
            {
                ipAddress = IPAddress.Parse(host);
            }
            return  new IPEndPoint(ipAddress , port);
        }

        public class Message
        {
            public uint m_playerID = 0;
            public byte[] m_message = null;
        }

        protected readonly Object lockMessage = new Object();
        protected List<Message> m_pendingMessages = new List<Message>();

        public void Process()
        {
            lock (lockMessage)
            {
                foreach(Message msg in m_pendingMessages)
                {
                    OnMessageReceived(msg);
                }
                m_pendingMessages.Clear();
            }
            lock(lockClients)
            {
                var removingClients = m_clients.FindAll(c => !c.m_Thread.IsAlive);
                foreach(var client in removingClients)
                {
                    EndPoint ep = client.m_ListenClient ? client.m_socket.LocalEndPoint : client.m_socket.RemoteEndPoint;
                    Log("client left " + ep.ToString());
                    m_clients.Remove(client);
                }
            }
        }

        public const uint BufferSize = 4096;
        class Client
        {
            public bool m_ListenClient = false;
            public Thread m_Thread = null;
            public Socket m_socket = null;
        }

        private readonly Object lockClients = new Object();
        List<Client> m_clients = new List<Client>();
        public TCPApi() { }

        private Client NewClient(Socket _socket = null)
        {
            Client client = new Client();
            if (_socket == null)
                client.m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            else
                client.m_socket = _socket;
            return client;
        }

        public EndPoint GetLocalEndPoint()
        {
            lock (lockClients)
            {
                return m_clients.ElementAt(0).m_socket.LocalEndPoint;
            }
        }
        public void FetchClients(List<EndPoint> _clients)
        {
            lock(lockClients)
            {
                foreach (var client in m_clients)
                {
                    if(client.m_ListenClient)
                        _clients.Add(client.m_socket.LocalEndPoint);
                    else
                        _clients.Add(client.m_socket.RemoteEndPoint);
                }
            }

        }

        private bool BindClient(ref Client _client, string localHost = "" , int localPort = 0)
        {
            //bind the socket 
            try
            {
                EndPoint localEndPoint = ComputeEndPoint(localHost, localPort);
                _client.m_socket.Bind(localEndPoint);
            }
            catch(ArgumentException )
            {
                Log("argument null");
                return false;
            }
            catch(SocketException ex )
            {
                Log("Socket error " + ex.ToString());
                return false;
            }
            catch(ObjectDisposedException )
            {
                Log("Socket closed ");
                return false;
            }
            catch(SecurityException )
            {
                Log("Permission not granted");
                return false;
            }
            return true;
        }

        private bool ConnectClient(ref Client _client, string localHost, int localPort)
        {
            EndPoint remoteEndPoint = ComputeEndPoint(localHost, localPort);
            try
            {
                _client.m_socket.Connect(remoteEndPoint);
            }
            catch (ArgumentNullException)
            {
                Log("Wrong argument");
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                Log("invalid Port");
                return false;
            }
            catch (SocketException ex)
            {
                Log("Socket error " + ex.ToString());
                return false;
            }
            catch (ObjectDisposedException)
            {
                Log("Socket closed");
                return false;
            }
            catch (NotSupportedException)
            {
                Log("Socket must be of IP or IPV6 type");
                return false;
            }
            catch (InvalidOperationException)
            {
                Log("Socket must be in Listen state");
                return false;
            }
            return true;
        }


        private Socket AcceptClient(ref Client client)
        {
            try
            {
                return client.m_socket.Accept();
            }
            catch (System.ObjectDisposedException ) // socket closed
            {
                return null;
            }
            catch (SocketException ex)
            {
                Log("Socket error " + ex.ToString());
                return null;
            }
        }

        //will start a Host client 
        //Host client will wait connection on given parameters and will assign ID to new Client
        public bool Listen(string localHost = "", int localPort = 0)
        {
            Log("Listening on" + localHost + ":" + localPort);
            Client client = NewClient();
            if (!BindClient(ref client, localHost, localPort))
            {
                Log("Cant start listening");
                return false;
            }
            client.m_ListenClient = true;
            //start listening for connection
            client.m_socket.Listen(200);
            //start accepting client thread
            client.m_Thread = new Thread(() =>
            {
               while (true)
               {
                    Socket newSocket = AcceptClient(ref client);
                    Log("client joined  " + newSocket.RemoteEndPoint.ToString());
                    Client newClient = NewClient(newSocket);
                    StartReceivingClient(newClient);
                    lock (lockClients)
                    {
                        m_clients.Add(newClient);
                    }
                }
            });
            client.m_Thread.Start();
            lock (lockClients)
            {
                m_clients.Add(client);
            }
            return true;

        }

        private void StartReceivingClient(Client client)
        {
            client.m_Thread = new Thread(() =>
            {
                //accept new client - blocking function
                byte[] bytes = new byte[BufferSize];
                while (true)
                {
                    //blocking function
                    int numByte = client.m_socket.Receive(bytes);
                    if (numByte == 4096)
                    {
                        Log("error : buffer size exceeded");
                        //should handle this
                        continue;
                    }
                    if (numByte == 0)
                    {
                        break;
                    }
                    Log("msg received with size " + numByte + " from " + client.m_socket.RemoteEndPoint.ToString());
                    Message msg = new Message();
                    msg.m_message = new byte[numByte];
                    Array.Copy(bytes, msg.m_message, numByte);
                    msg.m_playerID = 0;
                    lock (lockMessage)
                    {
                        m_pendingMessages.Add(msg);
                    }
                }
            });
            client.m_Thread.Start();
        }

        public bool Connect(string targetHost, int targetPort, string localHost = "", int localPort = 0 )
        {
            Client client = NewClient();
            if(localHost.Length > 0)
            {
                if (!BindClient(ref client, localHost, localPort))
                {
                    Log("Cant connect to " + targetHost + ":" + targetPort);
                    return false;
                }
            }
            Log("Trying to connect to " + targetHost + ":" + targetPort);
            if(!ConnectClient(ref client, targetHost, targetPort))
            {
                Log("Cant connect to " + targetHost + ":" + targetPort);
                return false;
            }
            StartReceivingClient(client);
            lock (lockClients)
            {
                m_clients.Add(client);
            }
            return true;
        }

        public int SendMessage(byte[] _msg)
        {
            if(_msg.Length > BufferSize)
            {
                Log("error : buffer size exceeded");
                return -1;
            }
            int numBytes = 0;
            lock (lockClients)
            {
                foreach (Client client in m_clients)
                {
                    if (!client.m_ListenClient)
                        numBytes += client.m_socket.Send(_msg);
                }
            }
            return numBytes;
        }

        public void End()
        {
            Log("Ending connection");
            lock (lockClients)
            {
                foreach (Client client in m_clients)
                {
                    try
                    {
                        client.m_socket.Shutdown(SocketShutdown.Both);
                    }
                    catch(Exception )
                    {

                    }
                    client.m_socket.Close();
                    client.m_Thread.Join();
                    client.m_socket = null;
                    client.m_Thread = null;
                }
                m_clients.Clear();
            }
            Log("Connection ended");
        }

    }
}
