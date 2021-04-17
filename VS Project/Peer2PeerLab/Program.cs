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

            //Console.ReadLine();

            FileManager files = new FileManager();

            ServerSocket server = new ServerSocket(files, probe.GetLANIP());
            
            Console.WriteLine("Made it here 2.");
            //Console.ReadLine();

            ClientSocket client = new ClientSocket(files, probe.GetLANIP());
            Console.ReadLine();

            //ClientSocket client1 = new ClientSocket(files);
            //Console.ReadLine();

            //ClientSocket client2 = new ClientSocket(files);
            //Console.ReadLine();
        }

        // Lookup (key-id)
        // {
        //      succ <- my successor
        //      if (my-id < succ < key-id) // next hop
        //          call Lookup(key-id) on succ
        //      else // done
        //          return succ
        // }


        // File Info needed to guarntee unique file.
        // File Name, File Data, Directory path file is in from root folder
    }
}