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
    class ServerSocket
    {
        private FileManager files;
        private IPAddress localIP;
        private AutoResetEvent connectDone = new AutoResetEvent(false);
        private AutoResetEvent syncDone = new AutoResetEvent(false);

        // Constructor.
        public ServerSocket(FileManager f, List<string> ips)
        {
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
            Console.WriteLine(ips.Count);
            ips.Remove(localIP.ToString());
            Console.WriteLine(ips.Count);

            // Start the listening server.
            Task server = new Task(Listen);
            server.Start();

            Console.WriteLine("Made it here 1.");
        }

        void Listen()
        {
            Console.WriteLine("Start Listen");
            IPEndPoint localEnd = new IPEndPoint(localIP, 33333);
            Socket listener = new Socket(localIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(localEnd);
            listener.Listen(100);

            while (true)
            {
                listener.BeginAccept(new AsyncCallback(ServerAcceptCallback), listener);

                // Lock the blocker.
                connectDone.WaitOne();
            }
        }

        void ServerAcceptCallback(IAsyncResult result)
        {
            Console.WriteLine("Server Connected.");
            // Free the blocker.
            connectDone.Set();

            Socket listener = (Socket)result.AsyncState;
            Socket server = listener.EndAccept(result);

            //server.BeginReceive();
            byte[] buffer = new byte[256];
            string endCondition = "";
            while (endCondition != "end")
            {
                server.Receive(buffer);
                Console.WriteLine("Buffer Contains: " + Encoding.ASCII.GetString(buffer));

                if (Encoding.ASCII.GetString(buffer).Contains("p2p system"))
                {
                    server.Send(Encoding.ASCII.GetBytes("true"));
                }

                if (Encoding.ASCII.GetString(buffer).Contains("sync"))
                {
                    Console.WriteLine("Server files syncing: " + files.isSyncing);
                    if (files.isSyncing)
                    {

                    }
                    else
                    {
                        server.Send(Encoding.ASCII.GetBytes("server free"));
                        endCondition = "end";
                    }
                }
            }
            Console.WriteLine("End recieved.");

            server.Receive(buffer);

            while (true)
            {
                server.Send(Encoding.ASCII.GetBytes("ready"));

                Console.WriteLine("Server is ready to recieve next file.");
                int size = server.Receive(buffer);
                byte[] message = new byte[size];
                for (int i = 0; i < message.Length; i++)
                {
                    message[i] = buffer[i];
                }

                if (Encoding.ASCII.GetString(message) == "sync done")
                    break;

                Console.WriteLine("Message Contains: " + Encoding.ASCII.GetString(message));
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
                    Console.WriteLine("Message Contains: " + message.Length);

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
                        Console.WriteLine("Message Contains: " + Encoding.ASCII.GetString(message));

                        DateTime date = DateTime.Parse(Encoding.ASCII.GetString(message));
                        DateTime date2 = DateTime.Parse(files.GetTimeCreated(files.basePath + filePath).ToString());
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

            Console.WriteLine("Server finished syncing.");
        }

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
        }
    }
}