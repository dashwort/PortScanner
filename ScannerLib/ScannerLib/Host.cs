using DnsClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
            // QueryDNS(hostname);

            // optional long running task compute hops
            if (ComputeHops)
                GetHops(hostname);

            var dns = GetLocalDNSIP();
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

        private async Task QueryDNS(string hostname, IPAddress dnsAddress)
        {
            long startTime = DateTime.Now.Ticks;

            var lookup = new LookupClient(dnsAddress, 53);

            var result = await lookup.QueryAsync("google.com", QueryType.A);

            long stopTime = DateTime.Now.Ticks;

            if (lookup != null)
            {
                this.LookupLatency = (stopTime - startTime) / 10000000;
            }
               
        }

		

		}
	}

    public class LookupResult
{

}
}
