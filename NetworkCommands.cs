namespace StyleOS
{
    public static class NetworkCommands
    {
        public static async Task Ping(List<string> args)
        {
            if (args.Count == 0) { Console.WriteLine("ping: missing host"); return; }
            string host = args[0];
            using Ping p = new Ping();
            try
            {
                Console.WriteLine($"PING {host} ({host}) 56(84) bytes of data.");
                for (int i = 0; i < 4; i++)
                {
                    var reply = await p.SendPingAsync(host, 2000);
                    if (reply.Status == IPStatus.Success)
                    {
                        Console.WriteLine($"64 bytes from {reply.Address}: icmp_seq={i + 1} ttl={reply.Options?.Ttl ?? 64} time={reply.RoundtripTime} ms");
                    }
                    else Console.WriteLine($"Destination Host Unreachable.");
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex) { Console.WriteLine($"ping: {ex.Message}"); }
        }

        public static async Task Curl(List<string> args)
        {
            if (args.Count == 0) { Console.WriteLine("curl: try 'curl --help'"); return; }
            using HttpClient client = NetworkUtils.GetBypassedHttpClient();
            try
            {
                string content = await client.GetStringAsync(args[0]);
                Console.WriteLine(content);
            }
            catch (Exception ex) { Console.WriteLine($"curl: {ex.Message}"); }
        }

        public static async Task Download(List<string> args)
        {
            if (args.Count == 0) { Console.WriteLine("wget: missing URL"); return; }
            string url = args[0];
            string filename = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(filename)) filename = "index.html";
            
            string dest = Path.Combine(Program.CurrentDirectory, filename);
            using HttpClient client = NetworkUtils.GetBypassedHttpClient();
            try
            {
                Console.WriteLine($"--{DateTime.Now:yyyy-MM-dd HH:mm:ss}--  {url}");
                Console.WriteLine($"Resolving... connected.");
                Console.WriteLine($"HTTP request sent, awaiting response... 200 OK");
                
                byte[] data = await client.GetByteArrayAsync(url);
                File.WriteAllBytes(dest, data);
                
                Console.WriteLine($"Length: {data.Length} [{data.Length}]");
                Console.WriteLine($"Saving to: '{filename}'\n\n100%[======================================>] {data.Length,-10}  --.-KB/s    in 0s");
            }
            catch (Exception ex) { Console.WriteLine($"wget: {ex.Message}"); }
        }

        public static void IpConfig()
        {
            foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                Console.WriteLine($"{netInterface.Name}: flags=4163<UP,BROADCAST,RUNNING,MULTICAST>  mtu 1500");
                var props = netInterface.GetIPProperties();
                foreach (var ip in props.UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        Console.WriteLine($"        inet {ip.Address}  netmask {ip.IPv4Mask}");
                    }
                }
                string mac = string.Join(":", netInterface.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
                if (!string.IsNullOrEmpty(mac))
                    Console.WriteLine($"        ether {mac}  txqueuelen 1000  (Ethernet)");
                Console.WriteLine();
            }
        }

        public static void Netstat()
        {
            Console.WriteLine("Active Internet connections (w/o servers)");
            Console.WriteLine("Proto Recv-Q Send-Q Local Address           Foreign Address         State");
            var props = IPGlobalProperties.GetIPGlobalProperties();
            foreach (var conn in props.GetActiveTcpConnections())
            {
                Console.WriteLine($"tcp        0      0 {conn.LocalEndPoint,-21} {conn.RemoteEndPoint,-21} {conn.State}");
            }
        }
    }
}
