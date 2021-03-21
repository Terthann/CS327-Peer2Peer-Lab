using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Peer2PeerLab
{
    public partial class Form1 : Form
    {
        ServerSocket server;

        public Form1()
        {
            InitializeComponent();

            //SocketController testing = new SocketController(label1);
            NetworkProbe probe = new NetworkProbe();
            WaitForProbe(probe);
        }

        async void WaitForProbe(NetworkProbe probe)
        {
            while (probe.WaitingForPings())
            {
                await Task.Delay(100);
            }
            Console.WriteLine("Done waiting.");

            StartServer(probe);
        }

        void StartServer(NetworkProbe probe)
        {
            server = new ServerSocket(probe.GetLANIP());
            
        }
    }
}
