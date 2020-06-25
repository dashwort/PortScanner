using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace ScannerLib
{
    public class Host
    {
        public string HostName { get; private set; }

        public IPAddress IPAddress { get; private set; }

        public bool PingSuccess { get; private set; }

        public long PingTime { get; private set; }

        public int PingTimeOut { get; set; } = 200;

        public string Domain { get; private set; }

        public int NumberOfHops { get; private set; }

        public int LookupLatency { get; private set; }

        public bool ComputeHops { get; set; } = false;

        public Host(string hostname)
        {
            this.HostName = hostname;

            // set ping, success and rrt
            PingHost(hostname);


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

        private int GetHops(string hostname)
        {
            return GetTraceRoute(hostname).ToArray().Length;
        }

        private void QueryDNS(string hostname)
        {
            var response = Dns.GetHostEntry(hostname);
        }
    }
}
