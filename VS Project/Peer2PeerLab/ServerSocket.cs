using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Peer2PeerLab
{
    // Manages server side of connections.
    class ServerSocket
    {
        // File Manager reference to access local files.
        private FileManager files;
        // The IP of the host.
        private IPAddress localIP;
        // Blocker to wait for one client to connect before accepting the next client.
        private AutoResetEvent connectDone = new AutoResetEvent(false);
        // Blocker to wait until the server is done syncing.
        private AutoResetEvent syncDone = new AutoResetEvent(false);
        // Mutex lock to protect shared variables.
        private static Mutex mut = new Mutex();

        // Constructor.
        public ServerSocket(FileManager f, List<string> ips)
        {
            // Initialize varibales.
            files = f;

            // Get the IP address of the server.
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    //Console.WriteLine("Local IP Address: " + ip.ToString());
                    localIP = ip;
                }
            }
            //Console.WriteLine(ips.Count);
            ips.Remove(localIP.ToString());
            //Console.WriteLine(ips.Count);

            Console.WriteLine("Server started.");
            // Start the listening server.
            Task server = new Task(Listen);
            server.Start();
        }

        // Listen for connection requests.
        void Listen()
        {
            Console.WriteLine("Server listening...");
            // Create socket to listen. Using port 33333 as unique entry point.
            IPEndPoint localEnd = new IPEndPoint(localIP, 33333);
            Socket listener = new Socket(localIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Start listening.
            listener.Bind(localEnd);
            listener.Listen(100);

            while (true)
            {
                // Get ready to accept the next connection attempt.
                listener.BeginAccept(new AsyncCallback(ServerAcceptCallback), listener);

                // Lock the blocker.
                connectDone.WaitOne();
            }
        }

        // Accept connection attempt.
        void ServerAcceptCallback(IAsyncResult result)
        {
            Console.WriteLine("Server accepted connection to client.");
            // Free the blocker.
            connectDone.Set();

            // Create new socket connected to client.
            Socket listener = (Socket)result.AsyncState;
            Socket server = listener.EndAccept(result);

            // Buffer for incoming messages.
            byte[] buffer = new byte[256];

            // Verify if client is part of P2P system.
            Console.WriteLine("Verifying client...");
            server.Receive(buffer);

            if (Encoding.ASCII.GetString(buffer).Contains("p2p system"))
            {
                Console.WriteLine("Connected with P2P system.\n");
                server.Send(Encoding.ASCII.GetBytes("true"));
            }
            else
            {
                Console.WriteLine("Not connected with P2P system.\n");
                server.Shutdown(SocketShutdown.Both);
                server.Close();
            }

            // Start syncing.
            while (SyncFiles(server, buffer)) { }
        }

        // Receive blocks of a file.
        public IEnumerable<byte[]> EnumerateFileBlocks(Socket server)
        {
            Console.WriteLine("Start file transfer.");
            byte[] buffer = new byte[1024];
            int size = server.Receive(buffer);
            byte[] message = new byte[size];

            for (int i = 0; i < message.Length; i++)
            {
                message[i] = buffer[i];
            }

            long fileSize = long.Parse(Encoding.ASCII.GetString(message));
            if (fileSize % 1024 == 0)
                fileSize = fileSize / 1024;
            else
                fileSize = (fileSize / 1024) + 1;

            server.Send(Encoding.ASCII.GetBytes("ready"));

            for (int j = 0; j < (int) fileSize; j++)
            {
                size = server.Receive(buffer);
                message = new byte[size];

                for (int i = 0; i < message.Length; i++)
                {
                    message[i] = buffer[i];
                }

                yield return message;
            }
            Console.WriteLine("File transfer complete.");
        }

        private bool SyncFiles(Socket server, byte[] buffer)
        {
            int size;
            byte[] message;
            string endCondition = "";
            while (endCondition != "end")
            {
                int temp = 0;
                try
                {
                    temp = server.Receive(buffer);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Client closed connection.");
                    return false;
                }
                size = temp;
                message = new byte[size];
                for (int i = 0; i < message.Length; i++)
                {
                    message[i] = buffer[i];
                }

                if (Encoding.ASCII.GetString(message) == "sync")
                {
                    mut.WaitOne();
                    Console.WriteLine("Server currently syncing: " + files.isSyncing);
                    if (files.isSyncing)
                    {

                    }
                    else
                    {
                        server.Send(Encoding.ASCII.GetBytes("server free"));
                        endCondition = "end";
                        files.isSyncing = true;
                    }
                    mut.ReleaseMutex();
                }
            }
            Console.WriteLine("Server ready to sync.\n");

            server.Receive(buffer);
            Console.WriteLine("Server receiving client files.");
            while (true)
            {
                server.Send(Encoding.ASCII.GetBytes("ready"));

                Console.WriteLine("\nServer is ready to recieve next file.");
                size = server.Receive(buffer);
                message = new byte[size];
                for (int i = 0; i < message.Length; i++)
                {
                    message[i] = buffer[i];
                }

                if (Encoding.ASCII.GetString(message) == "sync done")
                    break;

                //Console.WriteLine("Message Contains: " + Encoding.ASCII.GetString(message));
                string filePath = Encoding.ASCII.GetString(message);

                Console.WriteLine("Server has file: " + files.HasFile(filePath));
                if (files.HasFile(filePath))
                {
                    server.Send(Encoding.ASCII.GetBytes("true"));
                    // compare hashes
                    Console.WriteLine("Server is ready to compare hashes.");
                    size = server.Receive(buffer);
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
                        server.Send(Encoding.ASCII.GetBytes("true"));
                    }
                    else
                    {
                        // Files are different, need to sync.
                        server.Send(Encoding.ASCII.GetBytes("false"));

                        Console.WriteLine("Server is ready to compare dates.");
                        // Compare date times.
                        size = server.Receive(buffer);
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

                        Console.WriteLine("Client file is most recent: " + (date.CompareTo(date2) > 0));
                        if (date.CompareTo(date2) > 0)
                        {
                            // date later than date2
                            // keep date
                            Console.WriteLine("Client file is most recent.");

                            server.Send(Encoding.ASCII.GetBytes("true"));

                            // Wait to recieve file.
                            files.CreateFile(files.basePath + filePath, EnumerateFileBlocks(server));
                        }
                        else
                        {
                            // date earlier than date2 or the same
                            // keep date2
                            Console.WriteLine("Server file is most recent.");

                            server.Send(Encoding.ASCII.GetBytes("false"));
                        }
                    }
                }
                else
                {
                    server.Send(Encoding.ASCII.GetBytes("false"));

                    // Wait to recieve file.
                    files.CreateFile(files.basePath + filePath, EnumerateFileBlocks(server));
                }
            }

            Console.WriteLine("Server finished receiving.\n");

            server.Send(Encoding.ASCII.GetBytes("start sync"));

            Console.WriteLine("Server sending client files.");
            foreach (string s in files.EnumerateFilesRecursively(files.syncPath))
            {
                size = server.Receive(buffer);

                Console.WriteLine("\nServer checking if client has file...");
                server.Send(Encoding.ASCII.GetBytes(s.Replace(files.basePath, "")));

                size = server.Receive(buffer);
                message = new byte[size];
                for (int i = 0; i < message.Length; i++)
                {
                    message[i] = buffer[i];
                }

                Console.WriteLine("Client has file: " + Encoding.ASCII.GetString(message));
                if (Encoding.ASCII.GetString(message) == "true")
                {
                    // compare hashes
                    Console.WriteLine("Server compares hashes.");
                    server.Send(files.localFiles[s.Replace(files.basePath, "")]);

                    size = server.Receive(buffer);
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
                        server.Send(Encoding.ASCII.GetBytes(files.GetLastWrite(s).ToString()));

                        size = server.Receive(buffer);
                        message = new byte[size];
                        for (int i = 0; i < message.Length; i++)
                        {
                            message[i] = buffer[i];
                        }

                        // 
                        Console.WriteLine("Server file most recent: " + Encoding.ASCII.GetString(message));
                        if (Encoding.ASCII.GetString(message) == "true")
                        {
                            // send server file
                            Console.WriteLine("Server sends file.");

                            Console.WriteLine("File size: " + files.GetFileSize(s));
                            server.Send(Encoding.ASCII.GetBytes(files.GetFileSize(s).ToString()));

                            server.Receive(buffer);

                            server.SendFile(s);
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
                    Console.WriteLine("Server sends file.");

                    Console.WriteLine("File size: " + files.GetFileSize(s));
                    server.Send(Encoding.ASCII.GetBytes(files.GetFileSize(s).ToString()));

                    server.Receive(buffer);

                    server.SendFile(s);
                }
            }

            Console.WriteLine("\nServer finished sending.\n");
            Console.WriteLine("Server finished all syncing.");

            server.Receive(buffer);

            server.Send(Encoding.ASCII.GetBytes("sync done"));

            mut.WaitOne();
            files.isSyncing = false;
            mut.ReleaseMutex();
            return true;
        }
    }
}