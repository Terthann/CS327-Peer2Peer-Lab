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
        //private AutoResetEvent connectDone = new AutoResetEvent(false);
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
            Console.WriteLine(ip);
            
            Console.WriteLine("Client checking if P2P system...");
            client.Send(Encoding.ASCII.GetBytes("p2p system"));

            byte[] buffer = new byte[256];
            int size = client.Receive(buffer);
            byte[] message = new byte[size];

            for (int i = 0; i < message.Length; i++)
            {
                message[i] = buffer[i];
            }

            if (Encoding.ASCII.GetString(message) == "true")
            {
                Console.WriteLine("Connected with P2P system.");
                if (!files.isSyncing)
                {
                    Console.WriteLine("Client is not currently syncing.");
                    files.isSyncing = true;
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

                    foreach (string s in files.EnumerateFilesRecursively(files.syncPath))
                    {
                        client.Send(Encoding.ASCII.GetBytes(s.Replace(files.basePath,"")));

                        size = client.Receive(buffer);
                        message = new byte[size];
                        for (int i = 0; i < message.Length; i++)
                        {
                            message[i] = buffer[i];
                        }

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

                            if (Encoding.ASCII.GetString(message) == "true")
                            {
                                // files are the same, do nothing
                            }
                            else
                            {
                                // files are different, need to sync.
                                client.Send(Encoding.ASCII.GetBytes(files.GetTimeCreated(s).ToString()));
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
                            Console.WriteLine("Client made it here 1.");
                        }
                    }

                    // Finished Syncing
                    files.isSyncing = false;
                    syncDone.Set();
                }
                else
                {
                    // Waiting for another client to finish syncing.
                    syncDone.WaitOne();
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
    }
}