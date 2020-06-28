using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace ScannerLib
{
    public class Host
    {
        public string HostName { get; private set; }

        public IPAddress IPAddress { get; private set; }

        public bool PingSuccess { get; private set; }

        public long PingTime { get; private set; }

        public int PingTimeOut { get; set; } = 500;

        public string Domain { get; private set; }

        public int NumberOfHops { get; private set; }

        public long LookupLatency { get; private set; }

        public bool ComputeHops { get; set; } = false;

        public Host(string hostname)
        {
            this.HostName = hostname;

            // set ping, success and rrt
            PingHost(hostname);

            // query dns latency
            QueryDNS(hostname);

            // optional long running task compute hops
            if (ComputeHops)
                GetHops(hostname);
        }

        public Host(string hostname, bool computeHops)
        {
            this.HostName = hostname;
            this.ComputeHops = computeHops;

            // set ping, success and rrt
            PingHost(hostname);

            // query dns latency
            QueryDNS(hostname);

            // optional long running task compute hops
            if (ComputeHops)
                GetHops(hostname);

            var dns = GetLocalDNSIP();

            CheckDNS(hostname, dns[0].ToString());

		}

		/// <summary>
		/// retrieve dns for local client
		/// </summary>
		/// <returns> list of IP addresses that can be looped through</returns>
        private List<IPAddress> GetLocalDNSIP()
        {
            var dnsList = new List<IPAddress>();

             NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            if (networkInterfaces.Length == 0)
                throw new ApplicationException("No active interfaces detected on localhost");

            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
                     IPAddressCollection dnsAddresses = ipProperties.DnsAddresses;

                    foreach (IPAddress dnsAdress in dnsAddresses)
                    {
                        // gets ipv4 addresses only
                        if (dnsAdress.AddressFamily == AddressFamily.InterNetwork)
                            dnsList.Add(dnsAdress);
                    }
                }
            }

            if (dnsList.Count == 0)
                throw new ApplicationException("unable to detect any active DNS");

            return dnsList;
        }



        /// <summary>
        /// retrieve ping information
        /// </summary>
        /// <param name="nameOrAddress">DNS registered hostname as a string representation</param>
        private void PingHost(string hostname)
        {
            using (var pinger = new Ping())
            {
                PingReply reply = pinger.Send(hostname, this.PingTimeOut);

                this.PingSuccess = reply.Status == IPStatus.Success ? true : false;
                this.PingTime = reply.RoundtripTime;
                this.IPAddress = reply.Address;
            }
        }

        /// <summary>
        /// Used to compute list of addresses and due to lazy execution it allows us to cancel part way
        /// Recommend running as an sync task as timeout is ~10 seconds 
        /// </summary>
        /// <param name="hostname">DNS registered hostname as a string representation</param>
        /// <returns></returns>
        private IEnumerable<IPAddress> GetTraceRoute(string hostname)
        {
            // following are similar to the defaults in the "traceroute" unix command.
            const int timeout = 10000;
            const int maxTTL = 30;
            const int bufferSize = 32;

            byte[] buffer = new byte[bufferSize];
            new Random().NextBytes(buffer);

            using (var pinger = new Ping())
            {
                for (int ttl = 1; ttl <= maxTTL; ttl++)
                {
                    PingOptions options = new PingOptions(ttl, true);
                    PingReply reply = pinger.Send(hostname, timeout, buffer, options);

                    // we've found a route at this ttl
                    if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                        yield return reply.Address;

                    // if we reach a status other than expired or timed out, we're done searching or there has been an error
                    if (reply.Status != IPStatus.TtlExpired && reply.Status != IPStatus.TimedOut)
                        break;
                }
            }
        }

        private void GetHops(string hostname)
        {
            this.NumberOfHops = GetTraceRoute(hostname).ToArray().Length;
        }

        private void QueryDNS(string hostname)
        {
            var watch = Stopwatch.StartNew();
            var response = Dns.GetHostEntry(hostname);
            watch.Stop();

            if (response != null)
            {
                this.LookupLatency = watch.ElapsedTicks;
            }
               
        }

		private void CheckDNS(string hostname, string dnsServer)
		{
			const int IPPort = 53;
			const string TransactionID1 = "Q1"; // Use transaction ID of Q1 and Q2 to identify our packet and DNS
			const string TypeString = "\u0001" + "\u0000" + "\u0000" + "\u0001" + "\u0000" + "\u0000" + "\u0000" + "\u0000" + "\u0000" + "\u0000";
			const string TrailerString = "\u0000" + "\u0000" + "\u0001" + "\u0000" + "\u0001";
			const int DNSReceiveTimeout = 5000;
			string URLNameStart, DomainName, QueryString, ReceiveString, IPResponse, sDeltaTime;
			int URLNameStartLength, DomainNameLength, index, TransactionDNS;
			long StartTime, StopTime;
			string DeltaTime;
			Socket DNSsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			DNSsocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, DNSReceiveTimeout);
			IPEndPoint dnsEP1 = new IPEndPoint(IPAddress.Parse(dnsServer), IPPort);

			// Start the clock
			StartTime = DateTime.Now.Ticks;

			// Domain name for testing
			DomainName = ".agilent.com";

			// build query and send to dns
			QueryString = TransactionID1 + TypeString + hostname + DomainName + TrailerString;
			byte[] Sendbytes = Encoding.ASCII.GetBytes(QueryString);
			DNSsocket.SendTo(Sendbytes, Sendbytes.Length, SocketFlags.None, dnsEP1);

			byte[] Receivebytes = new byte[512];

			try
			{
			// wait for a response up to timeout
			more: DNSsocket.Receive(Receivebytes);


				// make sure the message returned is ours
				if (Receivebytes[0] == Sendbytes[0] && (Receivebytes[1] == 0x31) || (Receivebytes[1] == 0x32))
				{
                    if (Receivebytes[2] == 0x81 && Receivebytes[3] == 0x80)
                    {
                        // Get the time now
                        StopTime = DateTime.Now.Ticks;
                        DeltaTime = Convert.ToString((double)(StopTime - StartTime) / 10000000);

                        // Decode the answers
                        // Find the URL that was returned
                        TransactionDNS = Receivebytes[1];
                        ReceiveString = Encoding.ASCII.GetString(Receivebytes);
                        index = 12;
                        URLNameStartLength = Receivebytes[index];
                        index++;
                        URLNameStart = ReceiveString.Substring(index, URLNameStartLength);
                        index = index + URLNameStartLength;
                        DomainNameLength = Receivebytes[index];
                        index++;
                        DomainName = ReceiveString.Substring(index, DomainNameLength);
                        index = index + DomainNameLength;
                        index = index + 8;

                        // Get the record type
                        int ResponseType = Receivebytes[index];
                        index = index + 9;

                        // Get the IP address if applicable
                        IPResponse = "";

                        switch (ResponseType)
                        {
                            case 1:
                                IPResponse = Convert.ToString(Receivebytes[index]) + "."
                                    + Convert.ToString(Receivebytes[index + 1]) + "."
                                    + Convert.ToString(Receivebytes[index + 2]) + "."
                                    + Convert.ToString(Receivebytes[index + 3]); break;
                            case 5: IPResponse = "CNAME"; break;
                            case 6: IPResponse = "SOA"; break;
                        }

                        switch (TransactionDNS)
                        {
                            case 0x31:
                                break;

                            case 0x32:
                                break;
                        }

                    }

					goto more;
				}
			}
			catch (SocketException e)
			{
                Console.WriteLine($"error: {e.Message}");
			}
			finally
			{
				// close the socket
				DNSsocket.Close();
			}

		}
	}
}
