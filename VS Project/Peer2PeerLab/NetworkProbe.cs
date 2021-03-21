using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Peer2PeerLab
{
    class NetworkProbe
    {
        private static bool resolveNames = true;
        private static List<string> lanIPs = new List<string>();
        private static int waitingOn = 254;

        // Constructor.
        public NetworkProbe()
        {
            Task probe = new Task(StartProbes);
            probe.Start();
        }
        static void StartProbes()
        {
            Console.WriteLine("Network Probe starting...");
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            string localIP = "";
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine("Local IP Address: " + ip.ToString());
                    localIP = ip.ToString();
                }
            }
            Console.WriteLine("Pinging local network...");
            string[] ipParts = localIP.Split('.');
            string ipBase = ipParts[0] + "." + ipParts[1] + "." + ipParts[2] + ".";
            for (int i = 1; i < 255; i++)
            {
                string ip = ipBase + i.ToString();

                Ping p = new Ping();
                p.PingCompleted += new PingCompletedEventHandler(ProbeCompleted);
                p.SendAsync(ip, 100, ip);
            }
        }
        static void ProbeCompleted(object sender, PingCompletedEventArgs e)
        {
            string ip = (string)e.UserState;
            if (e.Reply != null && e.Reply.Status == IPStatus.Success)
            {
                if (resolveNames)
                {
                    string name;
                    try
                    {
                        IPHostEntry hostEntry = Dns.GetHostEntry(ip);
                        name = hostEntry.HostName;
                    }
                    catch (SocketException ex)
                    {
                        name = "?";
                    }
                    Console.WriteLine("{0} ({1}) is up: ({2} ms)", ip, name, e.Reply.RoundtripTime);
                }
                else
                {
                    Console.WriteLine("{0} is up: ({1} ms)", ip, e.Reply.RoundtripTime);
                }

                lanIPs.Add(ip);
            }
            else if (e.Reply != null)
            {
                Console.WriteLine("{0} is NOT up: ({1} ms)", ip, e.Reply.RoundtripTime);
            }
            else if (e.Reply == null)
            {
                Console.WriteLine("Pinging {0} failed. (Null Reply object?)", ip);
            }
            waitingOn--;
            Console.WriteLine(waitingOn);
        }

        public bool WaitingForPings()
        {
            if (waitingOn > 0)
                return true;
            else
                return false;
        }

        public List<string> GetLANIP()
        {
            return lanIPs;
        }
    }
}