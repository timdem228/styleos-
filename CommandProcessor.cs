namespace StyleOS
{
    public static class CommandProcessor
    {
        public static async Task Execute(string input, bool localOnly = false)
        {
            var parts = ParseCommand(input);
            if (parts.Count == 0) return;

            string cmd = parts[0].ToLower();
            List<string> args = parts.Skip(1).ToList();

            switch (cmd)
            {
                case "ls": case "dir": FileSystemCommands.Ls(args); break;
                case "cd": FileSystemCommands.Cd(args); break;
                case "pwd": Console.WriteLine(Program.CurrentDirectory); break;
                case "mkdir": FileSystemCommands.Mkdir(args); break;
                case "rmdir": FileSystemCommands.Rmdir(args); break;
                case "touch": FileSystemCommands.Touch(args); break;
                case "rm": FileSystemCommands.Rm(args); break;
                case "cp": FileSystemCommands.Cp(args); break;
                case "mv": FileSystemCommands.Mv(args); break;
                case "cat": FileSystemCommands.Cat(args); break;
                case "nano": NanoEditor.Run(args); break;
                case "echo": Console.WriteLine(string.Join(" ", args)); break;
                case "clear": case "cls": Console.Clear(); break;

                case "neofetch": case "sysinfo": SysInfoCommands.Neofetch(); break;
                case "uname": Console.WriteLine($"StyleOS Kernel {Program.Version} {RuntimeInformation.OSArchitecture}"); break;
                case "hostname": Console.WriteLine(Environment.MachineName); break;
                case "time": Console.WriteLine(DateTime.Now.ToString("HH:mm:ss")); break;
                case "date": Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd")); break;
                case "reboot": 
                    if (AuthSystem.RequirePassword()) {
                        Console.WriteLine("Powering off..."); 
                        Program.RequestReboot = true; 
                    }
                    break;
                case "shutdown": 
                    if (AuthSystem.RequirePassword()) {
                        Console.WriteLine("Powering off..."); 
                        Program.IsRunning = false; 
                    }
                    break;
                case "exit": Program.CurrentUser = null; break;
                case "authors": Utils.Authors(); break;
                case "shitshell":
                    if (args.Count > 0 && args[0].ToLower() == "start")
                    {
                        ShitShellEnv.Run();
                    }
                    else
                    {
                        Utils.Shitshell();
                    }
                    break;
                case "top": SysInfoCommands.Top(); break;
                case "ps": SysInfoCommands.Ps(); break;
                case "help": Utils.Help(); break;

                case "ping": await NetworkCommands.Ping(args); break;
                case "curl": await NetworkCommands.Curl(args); break;
                case "wget": case "tc": await NetworkCommands.Download(args); break;
                case "ipconfig": NetworkCommands.IpConfig(); break;
                case "netstat": NetworkCommands.Netstat(); break;

                case "df": DiskCommands.Df(); break;
                case "mount": DiskCommands.Mount(); break;
                case "drives": DiskCommands.Drives(); break;

                case "passwd": AuthCommands.Passwd(); break;
                case "useradd": AuthCommands.UserAdd(args); break;
                case "userdel": AuthCommands.UserDel(args); break;
                case "whoami": Console.WriteLine(Program.CurrentUser.Username); break;

                case "tree": FileSystemCommands.Tree(args); break;
                case "find": FileSystemCommands.Find(args); break;
                case "grep": FileSystemCommands.Grep(args); break;
                case "calc": Utils.Calc(args); break;
                case "color": case "theme": Utils.Theme(args); break;
                case "history": Utils.ShowHistory(); break;

                case "apt": case "pkg": PackageManager.HandleCommand(args); break;
                case "pacman": await PackageManager.HandlePacman(args); break;
                case "bugreport": BugReportEditor.Run(); break;

                case "tai": await TaiEngine.HandleCommand(args); break;

                case "kde":
                    if (args.Count == 0)
                    {
                        Console.WriteLine("Usage: kde <install|connect|stop>");
                    }
                    else if (args[0].ToLower() == "install")
                    {
                        Console.WriteLine("Installing KDE Plasma Desktop Environment...");
                        Thread.Sleep(1500);
                        KdeEnvironment.IsInstalled = true;
                        Console.WriteLine("KDE installed successfully. Type 'kde connect' to launch.");
                    }
                    else if (args[0].ToLower() == "connect")
                    {
                        KdeEnvironment.Run();
                    }
                    else if (args[0].ToLower() == "stop")
                    {
                        Console.WriteLine("KDE is not currently running.");
                    }
                    else
                    {
                        Console.WriteLine($"kde: invalid option '{args[0]}'");
                    }
                    break;

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"bash: {cmd}: command not found");
                    Console.ResetColor();
                    break;
            }
        }

        private static List<string> ParseCommand(string input)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '"') inQuotes = !inQuotes;
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else current.Append(c);
            }
            if (current.Length > 0) result.Add(current.ToString());
            return result;
        }

        public static string ResolvePath(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return Program.CurrentDirectory;
            if (target == "~") return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (target.StartsWith("~/") || target.StartsWith("~\\"))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), target.Substring(2));

            string fullPath = Path.GetFullPath(Path.Combine(Program.CurrentDirectory, target));
            return fullPath;
        }
    }
}
