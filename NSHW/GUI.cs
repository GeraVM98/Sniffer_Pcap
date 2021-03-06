﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using PcapDotNet.Analysis;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Base;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using PcapDotNet.Packets.Http;
using PcapDotNet.Packets.Ethernet;

namespace NSHW
{
    public partial class GUI : Form
    {

        private  IList<LivePacketDevice> AdaptersList;
        private PacketDevice selectedAdapter;
        private bool first_time = true;
        public static byte[] payload;
        public static Dictionary<int, Packet1> packets = new Dictionary<int, Packet1>();
        public List<Packet> paquetes = new List<Packet>();
        public Packet paqueter;
        string fullpath = Path.Combine(Application.StartupPath, @"captured\");


        public GUI()
        {
            InitializeComponent();

            try
            {
                AdaptersList = LivePacketDevice.AllLocalMachine;
            }
            catch(Exception e)
            {
                MessageBox.Show("Ejecutar como administrador e instalar winpcap");
            }

            PcapDotNetAnalysis.OptIn = true;

            if (AdaptersList.Count == 0)
            {

                MessageBox.Show("No se encontraron adaptadores");

                return;

            }

            for (int i = 0; i != AdaptersList.Count; ++i)
            {
                LivePacketDevice Adapter = AdaptersList[i];

                if (Adapter.Description != null)

                    adapters_list.Items.Add(Adapter.Description);
                else
                    adapters_list.Items.Add("Desconocido");
            }

        }
       
  
        private void start_button_Click(object sender, EventArgs e)
        {


            if (!first_time)
            {
                Application.Restart();
            }

            else if (adapters_list.SelectedIndex >= 0)
            {
                timer1.Enabled = true;
                selectedAdapter = AdaptersList[adapters_list.SelectedIndex];
                backgroundWorker1.RunWorkerAsync();
                backgroundWorker2.RunWorkerAsync();
                start_button.Enabled = false;
                stop_button.Enabled = true;
                adapters_list.Enabled = false;
                first_time = false;
                save.Enabled = false;
            }
            else
            {
                MessageBox.Show("Seleccionar un adaptador","Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }


        private void stop_button_Click(object sender, EventArgs e)
        {
            start_button.Enabled = true;
            stop_button.Enabled = false;
            adapters_list.Enabled = true;
            timer1.Enabled = false;
            start_button.Text = "Recapturar";
            save.Enabled = true;

        }


        string count = "";
        string time = "";
        string source = "";
        string destination = "";
        string protocol = "";
        string length = "";


        private void PacketHandler(Packet packet)
        {
            this.count = ""; this.time = ""; this.source = ""; this.destination = ""; this.protocol = ""; this.length = "";


            paqueter = packet;

            EthernetDatagram eth=packet.Ethernet;
            IpV4Datagram ip = packet.Ethernet.IpV4;
            TcpDatagram tcp = ip.Tcp;
            UdpDatagram udp = ip.Udp;


            HttpDatagram httpPacket=null;
            


            if (ip.Protocol.ToString().Equals("Tcp"))
            {
                count = packet.Count.ToString();
                time = packet.Timestamp.ToString();
                this.source = ip.Source.ToString();
                this.destination = ip.Destination.ToString();
                length = eth.Length.ToString();
                protocol = ip.Protocol.ToString();
            }
            else
            {
                if ((ip.Protocol.ToString().Equals("Udp")))
                {
                    count = packet.Count.ToString();
                    time = packet.Timestamp.ToString();
                    this.source = ip.Source.ToString();
                    this.destination = ip.Destination.ToString();
                    length = eth.Length.ToString();
                    protocol = ip.Protocol.ToString();
                }
            }

            if (ip.Protocol.ToString().Equals("Tcp")&&(save.Checked))
            {
                int _source = tcp.SourcePort;
                int _destination = tcp.DestinationPort;

                if (tcp.PayloadLength != 0) 
                {
                    payload = new byte[tcp.PayloadLength];
                    tcp.Payload.ToMemoryStream().Read(payload, 0, tcp.PayloadLength);
                    if (_destination == 80)
                    {
                        Packet1 packet1 = new Packet1();
                        int i = Array.IndexOf(payload, (byte)32, 6);
                        byte[] t = new byte[i - 5];
                        Array.Copy(payload, 5, t, 0, i - 5);
                        packet1.Name = System.Text.ASCIIEncoding.ASCII.GetString(t);

                        if (!packets.ContainsKey(_source))
                            packets.Add(_source, packet1);
                    }
                    else
                        if (_source == 80)
                            if (packets.ContainsKey(_destination))
                            {
                                Packet1 packet1 = packets[_destination];
                                if (packet1.Data == null)
                                {
                                    if ((httpPacket.Header != null) && (httpPacket.Header.ContentLength != null))
                                    {
                                        packet1.Data = new byte[(uint)httpPacket.Header.ContentLength.ContentLength];
                                        Array.Copy(httpPacket.Body.ToMemoryStream().ToArray(), packet1.Data, httpPacket.Body.Length);
                                        packet1.Order = (uint)(tcp.SequenceNumber + payload.Length - httpPacket.Body.Length);
                                        packet1.Data_Length = httpPacket.Body.Length;
                                        for (int i = 0; i < packet1.TempPackets.Count; i++)
                                        {
                                            Temp tempPacket = packet1.TempPackets[i];
                                            Array.Copy(tempPacket.data, 0, packet1.Data, tempPacket.tempSeqNo - packet1.Order, tempPacket.data.Length);
                                            packet1.Data_Length += tempPacket.data.Length;
                                        }
                                    }
                                    else
                                    {
                                        Temp tempPacket = new Temp();
                                        tempPacket.tempSeqNo = (uint)tcp.SequenceNumber;
                                        tempPacket.data = new byte[payload.Length];
                                        Array.Copy(payload, tempPacket.data, payload.Length);
                                        packet1.TempPackets.Add(tempPacket);
                                    }
                                }
                                else if (packet1.Data_Length != packet1.Data.Length)
                                {
                                    Array.Copy(payload, 0, packet1.Data, tcp.SequenceNumber - packet1.Order, payload.Length);

                                    packet1.Data_Length += payload.Length;
                                }

                                if (packet1.Data != null)
                                    if (packet1.Data_Length == packet1.Data.Length)
                                    {

                                        using (BinaryWriter writer = new BinaryWriter(File.Open(fullpath + Directory.CreateDirectory(Path.GetFileName(packet1.Name)), FileMode.Create)))
                                        {
                                            writer.Write(packet1.Data);

                                        }

                                        packets.Remove(_destination);

                                    }
                            }
                }
            }


        }

        private void timer1_Tick(object sender, EventArgs e)
        {

            if (!count.Equals(""))
            {
                ListViewItem item = new ListViewItem(time);
                item.SubItems.Add(source);
                item.SubItems.Add(destination);
                item.SubItems.Add(protocol);
                paquetes.Insert(0,paqueter);
                item.SubItems.Add(length);
                listView1.Items.Insert(0, item);
            }
        }


        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {

            using (PacketCommunicator communicator = selectedAdapter.Open(65536, PacketDeviceOpenAttributes.Promiscuous, 1000))
            {
                if (communicator.DataLink.Kind != DataLinkKind.Ethernet)
                {
                    MessageBox.Show("Este programa solo funciona con redes Ethernet");

                    return;
                }

                using (BerkeleyPacketFilter filter = communicator.CreateFilter("tcp or udp"))
                {
                    communicator.SetFilter(filter);
                }

                communicator.ReceivePackets(0, PacketHandler);
                

            }

        }

        private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
        {
            if (save.Checked)
            {
                using (PacketCommunicator communicator = selectedAdapter.Open(65536, PacketDeviceOpenAttributes.Promiscuous, 1000))
                {
                    using (PacketDumpFile dumpFile = communicator.OpenDump(fullpath + "Paquetes.pcap"))
                    {
                        communicator.ReceivePackets(0, dumpFile.Dump);
                    }
                }
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                int indice = listView1.Items.IndexOf(listView1.SelectedItems[0]);
                Packet paquete = paquetes[indice];

                EthernetDatagram eth = paquete.Ethernet;
                IpV4Datagram ip = paquete.Ethernet.IpV4;
                TcpDatagram tcp = ip.Tcp;
                UdpDatagram udp = ip.Udp;

                long checksum = Convert.ToInt64(ip.HeaderChecksum);

                string df="", mf="";
                IpV4Fragmentation flags = ip.Fragmentation;
                if (flags.Options.ToString().Equals("MoreFragments"))
                {
                    df = "True";
                    mf = "True";
                }
                if (flags.Options.ToString().Equals("None"))
                {
                    df = "True";
                    mf = "False";
                }
                if (flags.Options.ToString().Equals("DoNotFragment"))
                {
                    df = "False";
                    mf = "False";
                }

                treeView1.Nodes.Clear();

                TreeNode node1 = new TreeNode("ETHERNET");
                node1.ForeColor = Color.Blue;
                node1.Nodes.Add("MAC de Origen: " + eth.Source.ToString().ToUpper());
                node1.Nodes.Add("MAC de Destino: " + eth.Destination.ToString().ToUpper());
                node1.Nodes.Add("Tipo de servicio: " + eth.EtherType.ToString().ToUpper());

                TreeNode node2 = new TreeNode("IP");
                node2.ForeColor = Color.Green;
                node2.Nodes.Add("Version: " + ip.Version.ToString());
                node2.Nodes.Add("Longitud de la cabecera: " + ip.HeaderLength.ToString() + " Bytes");
                node2.Nodes.Add("Tipo de servicio: " + ip.TypeOfService.ToString());
                node2.Nodes.Add("Longitud del paquete: " + ip.Length.ToString() + " Bytes");
                node2.Nodes.Add("Identificacion: " + Convert.ToString(Convert.ToInt64(ip.Identification), 16));
                node2.Nodes.Add("Fragmentado: " + df);
                node2.Nodes.Add("Mas fragmentos: " + mf);
                node2.Nodes.Add("Desplazamiento del fragmento: " + flags.Offset.ToString());
                node2.Nodes.Add("Tiempo de vida: " + ip.Ttl.ToString() + " Segundos");
                node2.Nodes.Add("Protocolo: " + ip.Protocol.ToString());
                node2.Nodes.Add("Checksum 0x" + Convert.ToString(Convert.ToInt64(checksum), 16)+ "  [ " + ip.IsHeaderChecksumCorrect.ToString() + " ] ");
                node2.Nodes.Add("Ip origen: " + ip.Source.ToString());
                node2.Nodes.Add("Ip destino: " + ip.Destination.ToString());

                node1.Nodes.Add(node2);

                if (ip.Protocol.ToString().Equals("Tcp"))
                {
                    long checksumtcp = Convert.ToInt64(tcp.Checksum);
                    int flag;

                    TreeNode node3 = new TreeNode("TCP");
                    node3.ForeColor = Color.Red;
                    node3.Nodes.Add("Puerto de Origen: " + tcp.SourcePort.ToString());
                    node3.Nodes.Add("Puerto de Destino: " + tcp.DestinationPort.ToString());
                    node3.Nodes.Add("Numero de secuencia: " + tcp.SequenceNumber.ToString());
                    node3.Nodes.Add("Siguiente numero de secuencia: " + tcp.NextSequenceNumber.ToString());
                    node3.Nodes.Add("Numero de confirmacion: " + tcp.AcknowledgmentNumber.ToString());
                    node3.Nodes.Add("Longitud de la cabecera: " + tcp.HeaderLength.ToString() + " Bytes");
                    node3.Nodes.Add("0000 00.. ....  : Reservado");
                    flag = tcp.IsUrgent ? 1 : 0;
                    node3.Nodes.Add(".... .." + flag.ToString() + ". ....  : URG =" + tcp.IsUrgent.ToString());
                    flag = tcp.IsAcknowledgment ? 1 : 0;
                    node3.Nodes.Add(".... ..." + flag.ToString() + " ....  : ACK =" + tcp.IsAcknowledgment.ToString());
                    flag = tcp.IsPush ? 1 : 0;
                    node3.Nodes.Add(".... .... " + flag.ToString() + "...  : PSH =" + tcp.IsPush.ToString());
                    flag = tcp.IsReset ? 1 : 0;
                    node3.Nodes.Add(".... .... ." + flag.ToString() + "..  : RST =" + tcp.IsReset.ToString());
                    flag = tcp.IsSynchronize ? 1 : 0;
                    node3.Nodes.Add(".... .... .." + flag.ToString() + ".  : SYN =" + tcp.IsSynchronize.ToString());
                    flag = tcp.IsFin ? 1 : 0;
                    node3.Nodes.Add(".... .... ..." + flag.ToString() + "  : FIN =" + tcp.IsFin.ToString());
                    node3.Nodes.Add("Tamaño de ventana: " + tcp.Window.ToString() + " Bytes");
                    node3.Nodes.Add("Checksum 0x" + Convert.ToString(Convert.ToInt64(checksumtcp), 16));
                    node3.Nodes.Add("Puntero urgente: " + tcp.UrgentPointer.ToString());

                    node2.Nodes.Add(node3);
                }
                
                if ((ip.Protocol.ToString().Equals("Udp")))
                {
                    long checksumudp = Convert.ToInt64(udp.Checksum);

                    TreeNode node3 = new TreeNode("UDP");
                    node3.ForeColor = Color.Red;
                    node3.Nodes.Add("Puerto de Origen: " + udp.SourcePort.ToString());
                    node3.Nodes.Add("Puerto de Destino: " + udp.DestinationPort.ToString());
                    node3.Nodes.Add("Longitud de la cabecera: " + udp.TotalLength.ToString() + " Bytes");
                    node3.Nodes.Add("Checksum 0x" + Convert.ToString(Convert.ToInt64(checksumudp), 16));

                    node2.Nodes.Add(node3);
                }

                treeView1.Nodes.Add(node1);
                treeView1.ExpandAll();

            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void button1_Click(object sender, EventArgs e)
        {

            string inputpath = Path.Combine(Application.StartupPath, @"input\");
            //MessageBox.Show(inputpath);

            if (!first_time){
                Application.Restart();
            }

            else{
                string filename = "";

                OpenFileDialog SerialFileDialog = new OpenFileDialog();
                SerialFileDialog.Title = "Select Serial traffic file";
                SerialFileDialog.Filter = "TXT files|*.txt";
                SerialFileDialog.RestoreDirectory = false;

                if (SerialFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filename = SerialFileDialog.FileName;

                }

                //call wireshark text2pcap to convert the txt file
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.FileName = "C:\\Program Files\\Wireshark\\text2pcap.exe";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                //add " around new file name, in case file name has space and will cause text2pcap execute wrong
                startInfo.Arguments = " \"" + filename + "\"" + " " + "\"" + inputpath + "input.pcap" + "\"";

                try
                {
                    // Start the process with the info we specified.
                    // Call WaitForExit and then the using statement will close.
                    using (Process exeProcess = Process.Start(startInfo))
                    {
                        exeProcess.WaitForExit();
                    }
                }
                catch
                {
                    // Log error.
                }

                // Create the offline device
                OfflinePacketDevice selectedDevice = new OfflinePacketDevice(inputpath + "input.pcap");

                // Open the capture file
                using (PacketCommunicator communicator =
                    selectedDevice.Open(65536,                                  // portion of the packet to capture
                                                                                // 65536 guarantees that the whole packet will be captured on all the link layers
                                        PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                        1000))                                  // read timeout
                {
                    // Read and dispatch packets until EOF is reached
                    communicator.ReceivePackets(0, PacketHandler);

                    ListViewItem item = new ListViewItem(time);
                    item.SubItems.Add(source);
                    item.SubItems.Add(destination);
                    item.SubItems.Add(protocol);
                    paquetes.Insert(0, paqueter);

                    item.SubItems.Add(length);
                    listView1.Items.Insert(0, item);
                }
            }

        }
    }
}
