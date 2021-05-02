using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Peer2PeerLab
{
    // Creates a list of LAN IP addresses by pinging the LAN for active addresses.
    class NetworkProbe
    {
        // Mutex lock for working with threads.
        private static Mutex mut = new Mutex();
        // Blocker to block until the probe is finished.
        private static AutoResetEvent probeDone = new AutoResetEvent(false);
        // List of local active IP addresses.
        private static List<string> lanIPs = new List<string>();
        // Counter of active probes.
        private static int waitingOn = 256;

        // Constructor.
        public NetworkProbe()
        {
            // Start the probe in a new thread.
            Task probe = new Task(StartProbe);
            probe.Start();
            // Block until probe finished.
            probeDone.WaitOne();
            Console.WriteLine("Network probe finished.\n");
        }

        // Generate a ping to each potential local IP address.
        static void StartProbe()
        {
            Console.WriteLine("Network probe started.");

            // Get the host IP address.
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            string localIP = "";
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    //Console.WriteLine("Local IP Address: " + ip.ToString());
                    localIP = ip.ToString();
                }
            }

            // Split up the host IP to get the first three parts of the LAN IP, then generate all potential fourth parts. (0-255)
            Console.WriteLine("Pinging local network...");
            string[] ipParts = localIP.Split('.');
            string ipBase = ipParts[0] + "." + ipParts[1] + "." + ipParts[2] + ".";
            for (int i = 0; i < 256; i++)
            {
                string ip = ipBase + i.ToString();
                //Console.WriteLine("Pinging: " + ip);
                Ping p = new Ping();
                // When a ping is finished call ProbeCompleted().
                p.PingCompleted += new PingCompletedEventHandler(ProbeCompleted);
                // Send each ping asyncronously with a timeout of 100ms.
                p.SendAsync(ip, 100, ip);
            }
        }

        // When a ping is completed reduce the waiting counter, and if a reply was recieved add the ip to the list.
        static void ProbeCompleted(object sender, PingCompletedEventArgs e)
        {
            string ip = (string)e.UserState;
            if (e.Reply != null && e.Reply.Status == IPStatus.Success)
            {
                string name;
                try
                {
                    IPHostEntry hostEntry = Dns.GetHostEntry(ip);
                    name = hostEntry.HostName;

                    mut.WaitOne();
                    //Console.WriteLine("Adding ip: " + ip);
                    lanIPs.Add(ip);
                    mut.ReleaseMutex();
                }
                catch (SocketException ex)
                {
                    //Console.WriteLine(ip);
                    name = "?";
                }
                //Console.WriteLine("{0} ({1}) is up: ({2} ms)", ip, name, e.Reply.RoundtripTime);
            }
            else if (e.Reply != null)
            {
                //Console.WriteLine("{0} is NOT up: ({1} ms)", ip, e.Reply.RoundtripTime);
            }
            else if (e.Reply == null)
            {
                //Console.WriteLine("Pinging {0} failed. (Null Reply object?)", ip);
            }

            // Use the mutex lock to guarentee accurate reading and updating of the waiting counter.
            mut.WaitOne();
            waitingOn--;
            //Console.WriteLine(waitingOn);

            // Check if waiting for more pings to finish.
            if (!WaitingForPings())
            {
                Console.WriteLine("Pinging finished.");
                // Release the block.
                probeDone.Set();
            }
            mut.ReleaseMutex();
        }

        // Checks if any pings have not completed yet.
        public static bool WaitingForPings()
        {
            if (waitingOn > 0)
                return true;
            else
                return false;
        }

        // Get the list of ips.
        public List<string> GetLANIP()
        {
            return lanIPs;
        }
    }
}