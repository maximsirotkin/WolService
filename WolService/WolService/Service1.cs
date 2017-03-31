using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Linq;
using System.Text.RegularExpressions;

namespace WolService
{
    public partial class Service1 : ServiceBase
    {
        private System.Timers.Timer timer;
        private double interval;
        private string[][] addrList;

        enum Params
        {
            None,
            Interval,
            Addr
        }

        public Service1()
        {
            InitializeComponent();
            timer = new System.Timers.Timer();
        }

        protected override void OnStart(string[] args)
        {
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\WolService", true);
            if (key == null)
            {
                key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey("SOFTWARE\\WolService");
            }

            if (args.Length > 0)
            {
                Params current = Params.None;
                List<string> addrs = null;

                foreach (string arg in args)
                {
                    if (arg == "-interval")
                    {
                        current = Params.Interval;
                    }
                    if (arg == "-addr")
                    {
                        current = Params.Addr;
                    }

                    switch (current)
                    {
                        case Params.Interval:
                            {
                                try
                                {
                                    interval = Convert.ToDouble(arg);
                                }
                                catch (FormatException ex)
                                {
                                    interval = 0;
                                }
                                break;
                            }
                        case Params.Addr:
                            {
                                string macPattern = @"[A-F0-9]{2}[-:]{1}[A-F0-9]{2}[-:]{1}[A-F0-9]{2}
                                                    [-:]{1}[A-F0-9]{2}[-:]{1}[A-F0-9]{2}[-:]{1}[A-F0-9]{2}";
                                try
                                {
                                    if (Regex.Match(arg, macPattern, RegexOptions.IgnoreCase).Success)
                                    {
                                        if (addrs == null)
                                        {
                                            addrs = new List<string>();
                                        }
                                        addrs.Add(arg);
                                    }
                                }
                                catch (ArgumentException ex) { }
                                break;
                            }
                        default:
                            break;
                    }
                }
                if (addrs != null)
                {
                    addrList = addrs.Select(s => s.Split(':', '-')).ToArray();
                }
                // write to registry
            }
            else
            {
                object intervalObj = key.GetValue("interval");
                object addrListObj = key.GetValue("addr");
                interval = intervalObj == null ? 0 : Convert.ToDouble(intervalObj);
                if (addrListObj != null)
                {
                    addrList = addrListObj.ToString().Split(';').Select(s => s.Split(':', '-')).ToArray();
                }
            }

            //DateTime localDate = DateTime.Now;
            //DateTime min = new DateTime(2016, 11, 25, 8, 45, 0);
            //DateTime max = new DateTime(2016, 11, 25, 12, 0, 0);

            //if (localDate.TimeOfDay > min.TimeOfDay)
            //{
            sendMagicPacket();
            //}

            timer.Interval = interval;
            timer.Elapsed += new System.Timers.ElapsedEventHandler(OnTimer);
            timer.Start();
        }

        protected override void OnStop()
        {
            // write params to registry
        }

        public void OnTimer(object sender, System.Timers.ElapsedEventArgs args)
        {
            sendMagicPacket();
            timer.Interval = interval;
            timer.Start();
        }

        private void sendMagicPacket()
        {
            List<List<byte>> addrs = new List<List<byte>>();
            foreach (string[] addr in addrList)
            {
                List<byte> arr = new List<byte>(102);

                for (int i = 0; i < 6; i++)
                    arr.Add(0xff);

                for (int j = 0; j < 16; j++)
                    for (int i = 0; i < 6; i++)
                        arr.Add(Convert.ToByte(addr[i], 16));
                addrs.Add(arr);
            }
            using (UdpClient udpClient = new UdpClient())
            {
                foreach (List<byte> arr in addrs)
                {
                    byte[] mac = arr.ToArray();
                    udpClient.Send(mac, mac.Length, new IPEndPoint(IPAddress.Broadcast, 9));
                }
            }
        }
    }
}
