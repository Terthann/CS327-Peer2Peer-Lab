using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Peer2PeerLab
{
    class Program
    {
        static void Main(string[] args)
        {
            NetworkProbe probe = new NetworkProbe();

            FileManager files = new FileManager();

            ServerSocket server = new ServerSocket(files, probe.GetLANIP());

            ClientSocket client = new ClientSocket(files, server, probe.GetLANIP());
            Console.ReadLine();

            client.stopClient = true;
            Console.ReadLine();
        }
    }
}