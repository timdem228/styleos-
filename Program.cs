namespace StyleOS
{
    public class Program
    {
        public const string Version = "1.1.6";
        public static string CurrentDirectory { get; set; } = Directory.GetCurrentDirectory();
        public static User CurrentUser { get; set; } = null;
        public static bool IsRunning { get; set; } = true;
        public static bool RequestReboot { get; set; } = false;
        public static bool DebugMode { get; set; } = false;

        public static readonly string SysDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".styleos");
        public static readonly string ImageDir = Path.Combine(SysDir, "img");
        public static readonly string LogoFile = Path.Combine(ImageDir, "styleos.png");
        public static readonly string UsersFile = Path.Combine(SysDir, "users.json");
        public static readonly string ConfigFile = Path.Combine(SysDir, "config.json");
        public static readonly string HistoryFile = Path.Combine(SysDir, "history.log");
        public static readonly string PkgFile = Path.Combine(SysDir, "packages.json");
        public static readonly string LogFile = Path.Combine(SysDir, "system.log");
        public static readonly string KdeFlagFile = Path.Combine(SysDir, "kde.bin");

        public static SystemConfig Config { get; set; } = new SystemConfig();
        public static DateTime BootTime { get; set; }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int cmdShow);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        private const int SW_MAXIMIZE = 3;

        private static void EnableAnsiAndFullscreen()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var handle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (GetConsoleMode(handle, out uint mode))
                {
                    SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
                }
                IntPtr consoleWindow = GetConsoleWindow();
                if (consoleWindow != IntPtr.Zero)
                {
                    ShowWindow(consoleWindow, SW_MAXIMIZE);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;

                EnableAnsiAndFullscreen();
                EnsureSystemDirs();
                ConfigManager.LoadConfig();

                CheckDebugKey();

                while (IsRunning)
                {
                    RequestReboot = false;
                    BootTime = DateTime.Now;
                    
                    SystemLogger.Log("SYSTEM", $"Starting Boot Sequence v{Version}");
                    await BootSequence();

                    AuthSystem.Init();
                    while (CurrentUser == null && IsRunning && !RequestReboot)
                    {
                        AuthSystem.LoginPrompt();
                    }

                    if (CurrentUser != null)
                    {
                        SystemLogger.Log("AUTH", $"User '{CurrentUser.Username}' logged in.");
                        await Shell.StartSession();
                    }

                    if (RequestReboot)
                    {
                        CurrentUser = null;
                        Console.Clear();
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Restarting System...");
                        Thread.Sleep(1500);
                        Console.ResetColor();
                    }
                }

                Console.Clear();
            }
            catch (Exception ex)
            {
                CrashHandler.HandleCrash(ex);
            }
        }

        private static void CheckDebugKey()
        {
            Console.Clear();
            Console.WriteLine("Starting Style OS...");
            Console.WriteLine("Press F6 for Debug Mode Menu...");
            
            DateTime start = DateTime.Now;
            bool f6Pressed = false;
            
            while ((DateTime.Now - start).TotalSeconds < 1.5)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.F6)
                {
                    f6Pressed = true;
                    break;
                }
            }

            if (f6Pressed)
            {
                int selected = 0;
                string[] options = { "Boot Style OS", "Debug Mode: OFF" };

                while (true)
                {
                    Console.Clear();
                    Console.WriteLine("=== SYSTEM RECOVERY & DEBUG MENU ===\n");
                    for (int i = 0; i < options.Length; i++)
                    {
                        if (i == selected)
                        {
                            Console.BackgroundColor = ConsoleColor.White;
                            Console.ForegroundColor = ConsoleColor.Black;
                            Console.WriteLine($"> {options[i]}");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.WriteLine($"  {options[i]}");
                        }
                    }

                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.UpArrow) selected = Math.Max(0, selected - 1);
                    if (key == ConsoleKey.DownArrow) selected = Math.Min(options.Length - 1, selected + 1);
                    if (key == ConsoleKey.Enter)
                    {
                        if (selected == 0) break;
                        if (selected == 1)
                        {
                            AuthSystem.Init();
                            Console.Write("\nRoot Password required: ");
                            string pwd = AuthSystem.ReadPassword();
                            if (AuthSystem.VerifyRootForDebug(pwd))
                            {
                                DebugMode = !DebugMode;
                                options[1] = $"Debug Mode: {(DebugMode ? "ON" : "OFF")}";
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("\nAccess Denied.");
                                Console.ResetColor();
                                Thread.Sleep(1000);
                            }
                        }
                    }
                }
            }
        }

        private static void EnsureSystemDirs()
        {
            if (!Directory.Exists(SysDir)) Directory.CreateDirectory(SysDir);
            if (!Directory.Exists(ImageDir)) Directory.CreateDirectory(ImageDir);
            if (!File.Exists(LogFile)) File.Create(LogFile).Close();
            if (!File.Exists(UsersFile)) 
            {
                AuthSystem.Init();
            }
        }

        private static async Task BootSequence()
        {
            Console.Clear();
            Console.CursorVisible = false;

            if (DebugMode)
            {
                SystemLogger.Log("BOOT", "Debug mode initialized. Bypassing graphical splash.");
            }
            else
            {
                DrawCenteredLogo();
            }

            bool isBooting = true;
            Task spinner = Task.Run(() =>
            {
                char[] chars = { '/', '-', '\\', '|' };
                int i = 0;
                while (isBooting)
                {
                    if (!DebugMode)
                    {
                        Console.SetCursorPosition(0, Console.WindowHeight - 1);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"booting up {chars[i]}");
                        Console.ResetColor();
                    }
                    i = (i + 1) % chars.Length;
                    Thread.Sleep(100);
                }
            });

            SystemLogger.Log("BOOT", "Loading kernel modules...");
            await Task.Delay(DebugMode ? 500 : 100);
            SystemLogger.Log("BOOT", "Mounting virtual file systems...");
            await Task.Delay(DebugMode ? 500 : 100);
            SystemLogger.Log("BOOT", "Initializing network stack...");
            
            await UpdateSystem.CheckOnBootAsync();

            SystemLogger.Log("BOOT", "Checking for system updates... Done.");
            await Task.Delay(DebugMode ? 500 : 1500);
            
            isBooting = false;
            await spinner;

            Console.Clear();
            Console.CursorVisible = true;
        }

        private static void DrawCenteredLogo()
        {
            bool drawnImg = false;
            string targetFile = null;

            string[] possibleFiles = { 
                Path.Combine(ImageDir, "StyleOS.jpg"), 
                Path.Combine(ImageDir, "styleos.jpg"), 
                LogoFile 
            };

            foreach (var file in possibleFiles)
            {
                if (File.Exists(file))
                {
                    targetFile = file;
                    break;
                }
            }

            if (targetFile != null)
            {
                try
                {
#pragma warning disable CA1416
                    using (Bitmap bmp = new Bitmap(targetFile))
                    {
                        int consoleWidth = Math.Max(10, Console.WindowWidth - 4);
                        int consoleHeight = Math.Max(10, Console.WindowHeight - 6);
                        
                        int maxImgWidth = consoleWidth;
                        int maxImgHeight = consoleHeight * 2;

                        float ratioX = (float)maxImgWidth / bmp.Width;
                        float ratioY = (float)maxImgHeight / bmp.Height;
                        float ratio = Math.Min(ratioX, ratioY);
                        
                        int newWidth = Math.Max(1, (int)(bmp.Width * ratio));
                        int newHeight = Math.Max(1, (int)(bmp.Height * ratio));

                        using (Bitmap resized = new Bitmap(bmp, new Size(newWidth, newHeight)))
                        {
                            int startLeft = Math.Max(0, (Console.WindowWidth - newWidth) / 2);
                            int startTop = Math.Max(0, (Console.WindowHeight - (newHeight / 2)) / 2);

                            for (int y = 0; y < newHeight; y += 2)
                            {
                                Console.SetCursorPosition(startLeft, startTop + (y / 2));
                                for (int x = 0; x < newWidth; x++)
                                {
                                    Color top = resized.GetPixel(x, y);
                                    Color bottom = (y + 1 < newHeight) ? resized.GetPixel(x, y + 1) : Color.Black;
                                    
                                    bool tTrans = top.A < 128 || (top.R < 15 && top.G < 15 && top.B < 15);
                                    bool bTrans = bottom.A < 128 || (bottom.R < 15 && bottom.G < 15 && bottom.B < 15);

                                    if (tTrans && bTrans)
                                    {
                                        Console.Write("\x1b[0m ");
                                    }
                                    else if (tTrans && !bTrans)
                                    {
                                        Console.Write($"\x1b[38;2;{bottom.R};{bottom.G};{bottom.B}m▄\x1b[0m");
                                    }
                                    else if (!tTrans && bTrans)
                                    {
                                        Console.Write($"\x1b[38;2;{top.R};{top.G};{top.B}m▀\x1b[0m");
                                    }
                                    else
                                    {
                                        Console.Write($"\x1b[38;2;{top.R};{top.G};{top.B};48;2;{bottom.R};{bottom.G};{bottom.B}m▀\x1b[0m");
                                    }
                                }
                            }
                        }
                    }
#pragma warning restore CA1416
                    drawnImg = true;
                }
                catch 
                { 
                }
            }

            if (!drawnImg)
            {
                string[] asciiLogo = {
                    @"  ____  _         _        ____   _____ ",
                    @" / ___|| |_ _   _| | ___  / __ \ / ___| ",
                    @" \___ \| __| | | | |/ _ \| |  | |\___ \ ",
                    @"  ___) | |_| |_| | |  __/| |__| | ___) |",
                    @" |____/ \__|\__, |_|\___| \____/ |____/ ",
                    @"            |___/                       ",
                    $"",
                    $"v{Version}"
                };

                int top = Math.Max(0, (Console.WindowHeight - asciiLogo.Length) / 2);
                Console.ForegroundColor = ConsoleColor.Cyan;
                
                for (int i = 0; i < asciiLogo.Length; i++)
                {
                    int left = Math.Max(0, (Console.WindowWidth - asciiLogo[i].Length) / 2);
                    Console.SetCursorPosition(left, top + i);
                    Console.WriteLine(asciiLogo[i]);
                }
                Console.ResetColor();
            }
        }
    }
}
