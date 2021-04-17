using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Peer2PeerLab
{
    class ClientSocket
    {
        private FileManager files;
        private Dictionary<string, AutoResetEvent> connects = new Dictionary<string, AutoResetEvent>();
        private static Mutex mut = new Mutex();
        private AutoResetEvent syncDone = new AutoResetEvent(false);

        // Constructor.
        public ClientSocket(FileManager f, List<string> ips)
        {
            files = f;

            // Start the clients.
            foreach (string i in ips)
            {
                //Console.WriteLine("Client " + i + " Started.");
                connects.Add(i, new AutoResetEvent(false));
                Task client = new Task(() => Connect(i));
                client.Start();
            }
        }

        void Connect(string ip)
        {
            //Console.WriteLine("Start Connect");
            IPAddress ipAddress = IPAddress.Parse(ip);
            IPEndPoint localEnd = new IPEndPoint(ipAddress, 33333);
            Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                client.BeginConnect(localEnd, new AsyncCallback(ClientConnectCallback), client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            // Lock the blocker.
            connects[ip].WaitOne();

            byte[] buffer = new byte[256];
            byte[] message;
            int size;

            if (CheckP2PSystem(client, buffer))
            {
                Console.WriteLine("Connected with P2P system.");

                mut.WaitOne();
                bool clientSyncing = files.isSyncing;

                if (!clientSyncing)
                {
                    Console.WriteLine("Client is not currently syncing. Preparing to sync.");
                    files.isSyncing = true;
                    mut.ReleaseMutex();
                    Console.WriteLine("Client checking if server is currently syncing...");
                    client.Send(Encoding.ASCII.GetBytes("sync"));

                    do
                    {
                        size = client.Receive(buffer);
                        message = new byte[size];
                        for (int i = 0; i < message.Length; i++)
                        {
                            message[i] = buffer[i];
                        }
                    } while (Encoding.ASCII.GetString(message) == "server busy");

                    Console.WriteLine("Server is free.");

                    Console.WriteLine("Client starts syncing.");
                    foreach (string s in files.EnumerateFilesRecursively(files.syncPath))
                    {
                        Console.WriteLine("Client checking if server has file...");
                        client.Send(Encoding.ASCII.GetBytes(s.Replace(files.basePath,"")));

                        size = client.Receive(buffer);
                        message = new byte[size];
                        for (int i = 0; i < message.Length; i++)
                        {
                            message[i] = buffer[i];
                        }

                        Console.WriteLine("Server has file: " + Encoding.ASCII.GetString(message));
                        if (Encoding.ASCII.GetString(message) == "true")
                        {
                            // compare hashes
                            Console.WriteLine("Client compares hashes.");
                            client.Send(files.localFiles[s.Replace(files.basePath, "")]);

                            size = client.Receive(buffer);
                            message = new byte[size];
                            for (int i = 0; i < message.Length; i++)
                            {
                                message[i] = buffer[i];
                            }

                            Console.WriteLine("Files hashes are the sameL " + Encoding.ASCII.GetString(message));
                            if (Encoding.ASCII.GetString(message) == "true")
                            {
                                // files are the same, do nothing
                            }
                            else
                            {
                                // files are different, need to sync.
                                // send time
                                client.Send(Encoding.ASCII.GetBytes(files.GetTimeCreated(s).ToString()));

                                size = client.Receive(buffer);
                                message = new byte[size];
                                for (int i = 0; i < message.Length; i++)
                                {
                                    message[i] = buffer[i];
                                }

                                // 
                                Console.WriteLine("Client file most recent: " + Encoding.ASCII.GetString(message));
                                if (Encoding.ASCII.GetString(message) == "true")
                                {
                                    // send client file
                                    Console.WriteLine("Client sends file.");

                                    Console.WriteLine("File size: " + files.GetFileSize(s));
                                    client.Send(Encoding.ASCII.GetBytes(files.GetFileSize(s).ToString()));

                                    client.Receive(buffer);

                                    client.SendFile(s);
                                }
                                else
                                {
                                    // do nothing
                                }
                            }
                        }
                        else
                        {
                            // send file
                            Console.WriteLine("Client sends file.");

                            Console.WriteLine("File size: " + files.GetFileSize(s));
                            client.Send(Encoding.ASCII.GetBytes(files.GetFileSize(s).ToString()));

                            client.Receive(buffer);

                            client.SendFile(s);
                        }
                    }

                    Console.WriteLine("Client finished syncing files.");

                    client.Receive(buffer);

                    client.Send(Encoding.ASCII.GetBytes("sync done"));

                    // Finished Syncing
                    files.isSyncing = false;
                    syncDone.Set();
                }
                else
                {
                    // Waiting for another client to finish syncing.
                    mut.ReleaseMutex();
                }
            }
            else
            {
                Console.WriteLine("Not connected with P2P system.");
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
        }

        void ClientConnectCallback(IAsyncResult result)
        {
            try
            {
                Socket client = (Socket)result.AsyncState;
                client.EndConnect(result);

                Console.WriteLine("Client connect successful.");

                // Free the blocker.
                connects[client.RemoteEndPoint.ToString().Replace(":33333","")].Set();
            }
            catch (Exception e)
            {
                //Console.WriteLine(e.ToString());
            }
        }

        bool CheckP2PSystem(Socket client, byte[] buffer)
        {
            Console.WriteLine("Client checking if P2P system...");
            client.Send(Encoding.ASCII.GetBytes("p2p system"));

            int size = client.Receive(buffer);
            byte[] message = new byte[size];

            for (int i = 0; i < message.Length; i++)
                message[i] = buffer[i];

            if (Encoding.ASCII.GetString(message) == "true")
                return true;
            else
                return false;
        }
    }
}