using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Peer2PeerLab
{
    // Manages client side of connections.
    class ClientSocket
    {
        // File Manager reference to access local files.
        private FileManager files;
        // Server refernece to communicate with the local server.
        private ServerSocket server;
        // Blocker for each connection to wait for successful connection.
        private Dictionary<string, AutoResetEvent> connects = new Dictionary<string, AutoResetEvent>();
        // Mutex lock to protect shared variables.
        private static Mutex mut = new Mutex();
        // Blocker to wait until a client is done syncing.
        private AutoResetEvent syncDone = new AutoResetEvent(false);

        // Constructor.
        public ClientSocket(FileManager f, ServerSocket s, List<string> ips)
        {
            // Initialize varibales.
            files = f;
            server = s;

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
            //byte[] message;
            //int size;

            if (CheckP2PSystem(client, buffer))
            {
                Console.WriteLine("Connected with P2P system.");

                // Initial Sync
                SyncAllFiles(client, buffer);

                // OLD: Checks for file changes every minute.
                // NEW: Sync every minute.
                while (true)
                {
                    Thread.Sleep(60000);

                    //if (files.filesUpdated)
                        SyncAllFiles(client, buffer);
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

        void SyncAllFiles(Socket client, byte[] buffer)
        {
            byte[] message;
            int size;

            mut.WaitOne();
            bool clientSyncing = files.isSyncing;

            if (clientSyncing)
            {
                // Waiting for another client to finish syncing.
                Console.WriteLine("Waiting on another client to finish.");
                mut.ReleaseMutex();
                syncDone.WaitOne();
            }

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
            client.Send(Encoding.ASCII.GetBytes("start sync"));

            Console.WriteLine("Client starts syncing.");
            foreach (string s in files.EnumerateFilesRecursively(files.syncPath))
            {
                size = client.Receive(buffer);

                Console.WriteLine("\nClient checking if server has file...");
                client.Send(Encoding.ASCII.GetBytes(s.Replace(files.basePath, "")));

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

                    Console.WriteLine("Files hashes are the same: " + Encoding.ASCII.GetString(message));
                    if (Encoding.ASCII.GetString(message) == "true")
                    {
                        // files are the same, do nothing
                    }
                    else
                    {
                        // files are different, need to sync.
                        // send time
                        client.Send(Encoding.ASCII.GetBytes(files.GetLastWrite(s).ToString()));

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

            client.Receive(buffer);

            while (true)
            {
                client.Send(Encoding.ASCII.GetBytes("ready"));

                Console.WriteLine("\nClient is ready to recieve next file.");
                size = client.Receive(buffer);
                message = new byte[size];
                for (int i = 0; i < message.Length; i++)
                {
                    message[i] = buffer[i];
                }

                if (Encoding.ASCII.GetString(message) == "sync done")
                    break;

                //Console.WriteLine("Message Contains: " + Encoding.ASCII.GetString(message));
                string filePath = Encoding.ASCII.GetString(message);

                Console.WriteLine("Client has file: " + files.HasFile(filePath));
                if (files.HasFile(filePath))
                {
                    client.Send(Encoding.ASCII.GetBytes("true"));
                    // compare hashes
                    Console.WriteLine("Client is ready to compare hashes.");
                    size = client.Receive(buffer);
                    message = new byte[size];
                    for (int i = 0; i < message.Length; i++)
                    {
                        message[i] = buffer[i];
                    }
                    //Console.WriteLine("Message Contains: " + message.Length);

                    Console.WriteLine("Files are the same: " + files.FileCompare(filePath, message));
                    if (files.FileCompare(filePath, message))
                    {
                        // Files are the same, don't need to do anything.
                        client.Send(Encoding.ASCII.GetBytes("true"));
                    }
                    else
                    {
                        // Files are different, need to sync.
                        client.Send(Encoding.ASCII.GetBytes("false"));

                        Console.WriteLine("Client is ready to compare dates.");
                        // Compare date times.
                        size = client.Receive(buffer);
                        message = new byte[size];
                        for (int i = 0; i < message.Length; i++)
                        {
                            message[i] = buffer[i];
                        }
                        //Console.WriteLine("Message Contains: " + Encoding.ASCII.GetString(message));

                        DateTime date = DateTime.Parse(Encoding.ASCII.GetString(message));
                        DateTime date2 = DateTime.Parse(files.GetLastWrite(files.basePath + filePath).ToString());
                        //Console.WriteLine(date.Ticks.ToString());
                        //Console.WriteLine(date2.Ticks.ToString());
                        //Console.WriteLine(date.CompareTo(date2));

                        Console.WriteLine("Server file is most recent: " + (date.CompareTo(date2) > 0));
                        if (date.CompareTo(date2) > 0)
                        {
                            // date later than date2
                            // keep date
                            Console.WriteLine("Server file is most recent.");

                            client.Send(Encoding.ASCII.GetBytes("true"));

                            // Wait to recieve file.
                            files.CreateFile(files.basePath + filePath, server.EnumerateFileBlocks(client));
                        }
                        else
                        {
                            // date earlier than date2 or the same
                            // keep date2
                            Console.WriteLine("Client file is most recent.");

                            client.Send(Encoding.ASCII.GetBytes("false"));
                        }
                    }
                }
                else
                {
                    client.Send(Encoding.ASCII.GetBytes("false"));

                    // Wait to recieve file.
                    files.CreateFile(files.basePath + filePath, server.EnumerateFileBlocks(client));
                }
            }

            Console.WriteLine("Client finished all syncing.");

            // Finished Syncing
            files.isSyncing = false;
            syncDone.Set();
        }
    }
}