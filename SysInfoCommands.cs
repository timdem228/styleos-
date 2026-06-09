namespace StyleOS
{
    public static class SysInfoCommands
    {
        public static void Neofetch()
        {
            string os = Environment.OSVersion.ToString();
            string user = Program.CurrentUser.Username;
            string host = Environment.MachineName;
            string net = RuntimeInformation.FrameworkDescription;
            string arch = RuntimeInformation.ProcessArchitecture.ToString();
            string uptime = (DateTime.Now - Program.BootTime).ToString(@"hh\:mm\:ss");
            
            var hw = GetHardwareInfo();

            string[] rawLogo = {
                "              ################              ",
                "        ############################        ",
                "     ##################################     ",
                "    ####################################    ",
                "   #######     #########     #########      ",
                "   ######       #######       #######       ",
                "   #####        ######         #####        ",
                "   ######      #######                      ",
                "    #################                       ",
                "     #####################                  ",
                "        #####################               ",
                "            ####################            ",
                "                #####  #######              ",
                "   #           #####    #######             ",
                "  ####        #####      #######            ",
                " ##########  #####        #######           ",
                " ##############           #######           ",
                "  ############             #####            ",
                "    ########                                "
            };

            Console.WriteLine();
            
            int pCount = Math.Max(rawLogo.Length, 10);
            for (int i = 0; i < pCount; i++)
            {
                if (i < rawLogo.Length)
                {
                    string line = rawLogo[i].PadRight(46);
                    StringBuilder coloredLine = new StringBuilder();
                    for (int j = 0; j < line.Length; j++)
                    {
                        double t = (double)j / line.Length;
                        int r = 255;
                        int g, b;
                        if (t < 0.5)
                        {
                            double localT = t * 2.0;
                            g = (int)(240 - (240 - 150) * localT);
                            b = (int)(229 - (229 - 80) * localT);
                        }
                        else
                        {
                            double localT = (t - 0.5) * 2.0;
                            g = (int)(150 - (150 - 102) * localT);
                            b = (int)(80 - 80 * localT);
                        }
                        coloredLine.Append($"\x1b[38;2;{r};{g};{b}m{line[j]}");
                    }
                    coloredLine.Append("\x1b[0m");
                    Console.Write(coloredLine.ToString() + "   ");
                }
                else
                {
                    Console.Write(new string(' ', 49));
                }

                Console.ForegroundColor = ConsoleColor.White;
                switch (i)
                {
                    case 0: Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"{user}@{host}"); break;
                    case 1: Console.WriteLine(new string('-', $"{user}@{host}".Length)); break;
                    case 2: Console.WriteLine($"OS: Style OS {Program.Version} ({os})"); break;
                    case 3: Console.WriteLine($"Kernel: C# .NET ({net})"); break;
                    case 4: Console.WriteLine($"Uptime: {uptime}"); break;
                    case 5: Console.WriteLine($"CPU: {hw.Cpu}"); break;
                    case 6: Console.WriteLine($"GPU: {hw.Gpu}"); break;
                    case 7: Console.WriteLine($"Memory: {hw.Memory}"); break;
                    case 8: Console.WriteLine($"Arch: {arch}"); break;
                    default: Console.WriteLine(); break;
                }
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        private static (string Cpu, string Gpu, string Memory) GetHardwareInfo()
        {
            string cpu = $"{Environment.ProcessorCount} Cores";
            string gpu = "Standard Graphics";
            string mem = "Unknown";

            try
            {
                var gcMem = GC.GetGCMemoryInfo();
                mem = $"{gcMem.TotalAvailableMemoryBytes / (1024 * 1024 * 1024)} GB RAM";
            }
            catch { }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
#pragma warning disable CA1416
                    using (var searcher = new ManagementObjectSearcher("select Name from Win32_Processor"))
                    {
                        foreach (var item in searcher.Get()) cpu = item["Name"].ToString();
                    }
                    using (var searcher = new ManagementObjectSearcher("select Name from Win32_VideoController"))
                    {
                        foreach (var item in searcher.Get()) gpu = item["Name"].ToString();
                    }
                    using (var searcher = new ManagementObjectSearcher("select TotalVisibleMemorySize from Win32_OperatingSystem"))
                    {
                        foreach (var item in searcher.Get())
                        {
                            long kb = Convert.ToInt64(item["TotalVisibleMemorySize"]);
                            mem = $"{(kb / 1024 / 1024)} GB RAM";
                        }
                    }
#pragma warning restore CA1416
                }
                catch
                {
                    try
                    {
                        cpu = RunCmd("wmic cpu get name").Split('\n')[1].Trim();
                        gpu = RunCmd("wmic path win32_VideoController get name").Split('\n')[1].Trim();
                    }
                    catch { }
                }
            }

            return (cpu, gpu, mem);
        }

        private static string RunCmd(string cmd)
        {
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {cmd}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }

        public static void Top()
        {
            Console.Clear();
            Console.WriteLine($"top - {DateTime.Now:HH:mm:ss} up {(DateTime.Now - Program.BootTime):hh\\:mm\\:ss}");
            Console.WriteLine("PID\tUSER\tPR\tNI\tVIRT\tRES\tSHR\tS\t%CPU\t%MEM\tTIME+\tCOMMAND");
            
            var procs = Process.GetProcesses().OrderByDescending(p => p.WorkingSet64).Take(20).ToList();
            foreach(var p in procs)
            {
                try
                {
                    Console.WriteLine($"{p.Id}\troot\t20\t0\t{(p.VirtualMemorySize64/1024/1024)}M\t{(p.WorkingSet64/1024/1024)}M\t0\tS\t0.0\t0.0\t0:00.00\t{p.ProcessName}");
                }
                catch { }
            }
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey(true);
        }

        public static void Ps()
        {
            Console.WriteLine("  PID TTY          TIME CMD");
            var current = Process.GetCurrentProcess();
            Console.WriteLine($"{current.Id,5} pts/0    00:00:00 {current.ProcessName}");
            Console.WriteLine($"{Process.GetCurrentProcess().Id + 1,5} pts/0    00:00:00 bash");
        }
    }
}
