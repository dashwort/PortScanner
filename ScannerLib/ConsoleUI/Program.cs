using ScannerLib;
using System;

namespace ConsoleUI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"Enter the host you would like to test");
            var hostName = "cheadle-ol-wks1"; //Console.ReadLine();

            // no checking as only used for demo
            Console.WriteLine($"Testing {hostName}, please wait");
            var host = new Host(hostName, true);

            // print properties to screen
            Console.WriteLine($"hostname: {host.HostName}");
            Console.WriteLine($"IPAddress: {host.IPAddress}");
            Console.WriteLine($"Lookup latency: {host.LookupLatency}");
            Console.WriteLine($"Ping success: {host.PingSuccess}");
            Console.WriteLine($"Ping time: {host.PingTime}");
            Console.WriteLine($"Ping timeout: {host.PingTimeOut}");
            Console.WriteLine($"Number of hops: {host.NumberOfHops}");
        }
    }
}
