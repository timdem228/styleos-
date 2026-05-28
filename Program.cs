using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StyleOS
{
    public class Program
    {
        public const string Version = "1.1.3";
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

    public static class CrashHandler
    {
        public static void HandleCrash(Exception ex)
        {
            Console.Clear();
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("".PadRight(Console.WindowWidth));
            Console.WriteLine(" FATAL SYSTEM ERROR ".PadRight(Console.WindowWidth));
            Console.WriteLine("".PadRight(Console.WindowWidth));
            Console.ResetColor();
            
            Console.WriteLine("\nПрограмма вылетела по неизвестной ошибке.");
            Console.WriteLine("Вы можете написать о баге разработчикам!");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"\nDetails: {ex.Message}");
            Console.ResetColor();

            Console.WriteLine("\n[ OK ] Закрыть ошибку (завершить работу)");
            Console.WriteLine("[ TEXT ] Написать разработчикам (откроется mail.google.com)");

            while (true)
            {
                Console.Write("\nSelect action (ok/text): ");
                string act = Console.ReadLine()?.ToLower().Trim();
                
                if (act == "ok")
                {
                    Environment.Exit(1);
                }
                else if (act == "text")
                {
                    try
                    {
                        string logs = "";
                        if (File.Exists(Program.LogFile))
                        {
                            var allLogs = File.ReadAllLines(Program.LogFile);
                            logs = string.Join("\n", allLogs.Skip(Math.Max(0, allLogs.Length - 30)));
                        }
                        
                        string subject = Uri.EscapeDataString("StyleOS Crash Report");
                        string body = Uri.EscapeDataString($"Система крашнулась!\n\nОшибка:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nПоследние логи:\n{logs}");
                        string url = $"https://mail.google.com/mail/?view=cm&fs=1&to=admin@timd.site&su={subject}&body={body}";
                        
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    }
                    catch { }
                    Environment.Exit(1);
                }
            }
        }
    }

    public static class AuthSystem
    {
        private static List<User> Users = new List<User>();

        public static void Init()
        {
            if (!File.Exists(Program.UsersFile))
            {
                Users.Add(new User { Username = "root", PasswordHash = HashPassword("root"), IsRoot = true });
                SaveUsers();
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(Program.UsersFile);
                    Users = JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
                }
                catch
                {
                    Users.Add(new User { Username = "root", PasswordHash = HashPassword("root"), IsRoot = true });
                }
            }
        }

        public static bool VerifyRootForDebug(string pwd)
        {
            var root = Users.FirstOrDefault(u => u.IsRoot);
            if (root != null && root.PasswordHash == HashPassword(pwd)) return true;
            return false;
        }

        public static void SaveUsers()
        {
            File.WriteAllText(Program.UsersFile, JsonSerializer.Serialize(Users, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static void LoginPrompt()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("Style OS login: ");
            string username = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(username)) return;

            Console.Write("Password: ");
            string password = ReadPassword();
            Console.WriteLine();

            var user = Users.FirstOrDefault(u => u.Username == username && u.PasswordHash == HashPassword(password));
            if (user != null)
            {
                Program.CurrentUser = user;
                Program.CurrentDirectory = user.IsRoot ? "C:\\" : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Login incorrect");
                SystemLogger.Log("AUTH", $"Failed login attempt for user '{username}'");
                Console.ResetColor();
            }
        }

        public static string ReadPassword()
        {
            string pass = "";
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    pass = pass.Substring(0, pass.Length - 1);
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    pass += key.KeyChar;
                }
            }
            return pass;
        }

        public static bool RequirePassword()
        {
            if (Program.CurrentUser == null) return false;
            Console.Write($"[sudo] password for {Program.CurrentUser.Username}: ");
            string input = ReadPassword();
            Console.WriteLine();
            
            if (HashPassword(input) == Program.CurrentUser.PasswordHash)
            {
                return true;
            }
            
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Sorry, try again.");
            Console.ResetColor();
            return false;
        }

        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        public static void AddUser(string username, string password)
        {
            if (Users.Any(u => u.Username == username)) throw new Exception("User already exists.");
            Users.Add(new User { Username = username, PasswordHash = HashPassword(password), IsRoot = false });
            SaveUsers();
        }

        public static void DeleteUser(string username)
        {
            var u = Users.FirstOrDefault(x => x.Username == username);
            if (u != null && u.Username != "root")
            {
                Users.Remove(u);
                SaveUsers();
            }
            else throw new Exception("Cannot delete root or non-existent user.");
        }

        public static void ChangePassword(string username, string newPassword)
        {
            var u = Users.FirstOrDefault(x => x.Username == username);
            if (u != null)
            {
                u.PasswordHash = HashPassword(newPassword);
                SaveUsers();
            }
        }
    }

    public class User
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public bool IsRoot { get; set; }
    }

    public class SystemConfig
    {
        public ConsoleColor PromptUserColor { get; set; } = ConsoleColor.Green;
        public ConsoleColor PromptDirColor { get; set; } = ConsoleColor.Blue;
        public ConsoleColor DefaultTextColor { get; set; } = ConsoleColor.White;
        public ConsoleColor DefaultBgColor { get; set; } = ConsoleColor.Black;
    }

    public static class ConfigManager
    {
        public static void LoadConfig()
        {
            if (File.Exists(Program.ConfigFile))
            {
                try
                {
                    Program.Config = JsonSerializer.Deserialize<SystemConfig>(File.ReadAllText(Program.ConfigFile));
                }
                catch { Program.Config = new SystemConfig(); }
            }
            ApplyTheme();
        }

        public static void SaveConfig()
        {
            File.WriteAllText(Program.ConfigFile, JsonSerializer.Serialize(Program.Config, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static void ApplyTheme()
        {
            Console.ForegroundColor = Program.Config.DefaultTextColor;
            Console.BackgroundColor = Program.Config.DefaultBgColor;
            Console.Clear();
        }
    }

    public static class SystemLogger
    {
        public static void Log(string module, string message)
        {
            try
            {
                string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{module}] {message}";
                File.AppendAllText(Program.LogFile, logLine + Environment.NewLine);
                
                if (Program.DebugMode)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(logLine);
                    Console.ResetColor();
                }
            }
            catch { }
        }
    }

    public static class UpdateSystem
    {
        public static string LatestVersionAvailable { get; private set; } = null;

        public static async Task CheckOnBootAsync()
        {
            try
            {
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "StyleOS-Client");
                string json = await client.GetStringAsync("https://api.github.com/repos/timdem228/styleos-/releases/latest");
                using JsonDocument doc = JsonDocument.Parse(json);
                string tag = doc.RootElement.GetProperty("tag_name").GetString();
                
                if (IsNewerVersion(Program.Version, tag))
                {
                    LatestVersionAvailable = tag;
                }
            }
            catch { }
        }

        public static void PrintBootNotification()
        {
            if (!string.IsNullOrEmpty(LatestVersionAvailable))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n[INFO] Available new release: Style OS {LatestVersionAvailable}!");
                Console.WriteLine($"       Use 'pacman update' to download and install.\n");
                Console.ResetColor();
            }
        }

        public static async Task RunUpdateProcess()
        {
            SystemLogger.Log("PACMAN", "Checking for system updates...");
            Console.WriteLine("Checking for updates...");
            try
            {
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "StyleOS-Client");
                string json = await client.GetStringAsync("https://api.github.com/repos/timdem228/styleos-/releases/latest");
                using JsonDocument doc = JsonDocument.Parse(json);
                string tag = doc.RootElement.GetProperty("tag_name").GetString();

                if (!IsNewerVersion(Program.Version, tag))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\nSystem is up to date! You are on the latest version (Style OS {Program.Version}).");
                    Console.ResetColor();
                    return;
                }

                Console.WriteLine($"New version {tag} found!");
                Console.Write("Do you want to download and install this update? [y/N]: ");
                string ans = Console.ReadLine()?.ToLower();
                if (ans != "y" && ans != "yes")
                {
                    Console.WriteLine("Update aborted.");
                    return;
                }

                if (!AuthSystem.RequirePassword())
                {
                    Console.WriteLine("Authentication failed. Update aborted.");
                    return;
                }

                var assets = doc.RootElement.GetProperty("assets");
                if (assets.GetArrayLength() == 0)
                {
                    Console.WriteLine("Error: No release artifacts found in the repository.");
                    return;
                }

                string downloadUrl = assets[0].GetProperty("browser_download_url").GetString();
                Console.WriteLine($"Downloading update archive...");

                string tempZipPath = Path.Combine(Path.GetTempPath(), "styleos_update.zip");
                string extractPath = Path.Combine(Path.GetTempPath(), "styleos_update_extracted");

                if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
                if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);

                byte[] zipData = await client.GetByteArrayAsync(downloadUrl);
                File.WriteAllBytes(tempZipPath, zipData);
                Console.WriteLine("Download complete. Extracting files...");

                ZipFile.ExtractToDirectory(tempZipPath, extractPath);
                
                Console.WriteLine("Preparing system for restart and replacement...");
                
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string currentDir = Path.GetDirectoryName(currentExe);
                string updaterBat = Path.Combine(Path.GetTempPath(), "styleos_updater.bat");

                string batCode = $@"@echo off
title Style OS Updater
echo Applying update, please wait...
ping 127.0.0.1 -n 3 > nul
echo Overwriting files...
xcopy /Y /E ""{extractPath}\*"" ""{currentDir}""
echo Cleaning up...
del ""{tempZipPath}""
rmdir /S /Q ""{extractPath}""
echo Restarting Style OS...
start """" ""{currentExe}""
del ""%~f0""
";
                File.WriteAllText(updaterBat, batCode);

                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterBat,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                Console.WriteLine("Update initialized. System will now shut down to apply changes...");
                Thread.Sleep(1000);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Update failed: {ex.Message}");
                Console.ResetColor();
            }
        }

        public static bool IsNewerVersion(string current, string latest)
        {
            try
            {
                string cleanLatest = latest.Trim('v', 'V', ' ');
                string cleanCurrent = current.Trim('v', 'V', ' ');

                string[] latestParts = cleanLatest.Split('-');
                string[] currentParts = cleanCurrent.Split('-');

                if (Version.TryParse(latestParts[0], out Version vLatest) && 
                    Version.TryParse(currentParts[0], out Version vCurrent))
                {
                    if (vLatest > vCurrent) return true;
                    if (vLatest < vCurrent) return false;

                    if (latestParts.Length == 1 && currentParts.Length > 1) return true;
                    if (latestParts.Length > 1 && currentParts.Length == 1) return false;

                    if (latestParts.Length > 1 && currentParts.Length > 1)
                    {
                        return string.Compare(cleanLatest, cleanCurrent, StringComparison.OrdinalIgnoreCase) > 0;
                    }
                }
            }
            catch { }
            
            return false;
        }
    }

    public static class Shell
    {
        private static List<string> History = new List<string>();

        public static async Task StartSession()
        {
            LoadHistory();
            Console.WriteLine($"Welcome to Style OS, {Program.CurrentUser.Username}. Type 'help' for available commands.");
            
            UpdateSystem.PrintBootNotification();

            while (Program.CurrentUser != null && Program.IsRunning && !Program.RequestReboot)
            {
                DrawPrompt();
                string input = CustomReadLine();
                
                if (string.IsNullOrWhiteSpace(input)) continue;

                AddToHistory(input);
                SystemLogger.Log("SHELL", $"User '{Program.CurrentUser.Username}' executed: {input}");

                try
                {
                    await CommandProcessor.Execute(input);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        private static void DrawPrompt()
        {
            string host = Environment.MachineName;
            string symbol = Program.CurrentUser.IsRoot ? "#" : "$";
            
            Console.ForegroundColor = Program.Config.PromptUserColor;
            Console.Write($"{Program.CurrentUser.Username}@{host}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(":");
            Console.ForegroundColor = Program.Config.PromptDirColor;
            
            string displayDir = Program.CurrentDirectory;
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (displayDir.StartsWith(userProfile))
                displayDir = "~" + displayDir.Substring(userProfile.Length).Replace("\\", "/");
            else
                displayDir = displayDir.Replace("\\", "/");

            Console.Write(displayDir);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{symbol} ");
            Console.ResetColor();
        }

        private static string CustomReadLine()
        {
            StringBuilder input = new StringBuilder();
            int historyIndex = History.Count;
            int cursorIndex = 0;

            while (true)
            {
                var keyInfo = Console.ReadKey(intercept: true);

                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return input.ToString();
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (cursorIndex > 0)
                    {
                        input.Remove(cursorIndex - 1, 1);
                        cursorIndex--;
                        RedrawInput(input.ToString(), cursorIndex);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.Delete)
                {
                    if (cursorIndex < input.Length)
                    {
                        input.Remove(cursorIndex, 1);
                        RedrawInput(input.ToString(), cursorIndex);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.LeftArrow)
                {
                    if (cursorIndex > 0)
                    {
                        cursorIndex--;
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.RightArrow)
                {
                    if (cursorIndex < input.Length)
                    {
                        cursorIndex++;
                        Console.SetCursorPosition(Console.CursorLeft + 1, Console.CursorTop);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.UpArrow)
                {
                    if (historyIndex > 0)
                    {
                        historyIndex--;
                        input.Clear();
                        input.Append(History[historyIndex]);
                        cursorIndex = input.Length;
                        RedrawInput(input.ToString(), cursorIndex);
                    }
                }
                else if (keyInfo.Key == ConsoleKey.DownArrow)
                {
                    if (historyIndex < History.Count - 1)
                    {
                        historyIndex++;
                        input.Clear();
                        input.Append(History[historyIndex]);
                        cursorIndex = input.Length;
                        RedrawInput(input.ToString(), cursorIndex);
                    }
                    else
                    {
                        historyIndex = History.Count;
                        input.Clear();
                        cursorIndex = 0;
                        RedrawInput(input.ToString(), cursorIndex);
                    }
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    input.Insert(cursorIndex, keyInfo.KeyChar);
                    cursorIndex++;
                    RedrawInput(input.ToString(), cursorIndex);
                }
            }
        }

        private static void RedrawInput(string input, int cursorIndex)
        {
            int currentLeft = Console.CursorLeft;
            int currentTop = Console.CursorTop;
            
            Console.SetCursorPosition(0, currentTop);
            DrawPrompt();
            Console.Write(input + " "); 
            
            int promptLength = $"{Program.CurrentUser.Username}@{Environment.MachineName}:".Length;
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string displayDir = Program.CurrentDirectory;
            if (displayDir.StartsWith(userProfile))
                displayDir = "~" + displayDir.Substring(userProfile.Length);
            promptLength += displayDir.Length + 3;

            Console.SetCursorPosition(promptLength + cursorIndex, currentTop);
        }

        private static void LoadHistory()
        {
            if (File.Exists(Program.HistoryFile))
            {
                History = File.ReadAllLines(Program.HistoryFile).ToList();
            }
        }

        private static void AddToHistory(string cmd)
        {
            History.Add(cmd);
            if (History.Count > 1000) History.RemoveAt(0);
            File.AppendAllText(Program.HistoryFile, cmd + Environment.NewLine);
        }
    }

    public static class CommandProcessor
    {
        public static async Task Execute(string input)
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

    public static class FileSystemCommands
    {
        public static void Ls(List<string> args)
        {
            string targetDir = args.Count > 0 ? CommandProcessor.ResolvePath(args[0]) : Program.CurrentDirectory;
            if (!Directory.Exists(targetDir))
            {
                Console.WriteLine($"ls: cannot access '{targetDir}': No such file or directory");
                return;
            }

            DirectoryInfo di = new DirectoryInfo(targetDir);
            
            Console.ForegroundColor = ConsoleColor.Blue;
            foreach (var d in di.GetDirectories())
            {
                Console.WriteLine($"drwxr-xr-x  {d.LastWriteTime:MMM dd HH:mm}  {d.Name}/");
            }
            
            Console.ForegroundColor = ConsoleColor.White;
            foreach (var f in di.GetFiles())
            {
                if (f.Extension is ".exe" or ".bat" or ".sh" or ".cmd")
                    Console.ForegroundColor = ConsoleColor.Green;
                else
                    Console.ForegroundColor = ConsoleColor.White;

                Console.WriteLine($"-rw-r--r--  {f.Length,10}  {f.LastWriteTime:MMM dd HH:mm}  {f.Name}");
            }
            Console.ResetColor();
        }

        public static void Cd(List<string> args)
        {
            if (args.Count == 0)
            {
                Program.CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return;
            }

            string target = CommandProcessor.ResolvePath(args[0]);
            if (Directory.Exists(target)) Program.CurrentDirectory = target;
            else Console.WriteLine($"cd: {args[0]}: No such file or directory");
        }

        public static void Mkdir(List<string> args)
        {
            if (args.Count == 0) { Console.WriteLine("mkdir: missing operand"); return; }
            string path = CommandProcessor.ResolvePath(args[0]);
            try { Directory.CreateDirectory(path); }
            catch (Exception ex) { Console.WriteLine($"mkdir: {ex.Message}"); }
        }

        public static void Rmdir(List<string> args)
        {
            if (args.Count == 0) { Console.WriteLine("rmdir: missing operand"); return; }
            string path = CommandProcessor.ResolvePath(args[0]);
            try { Directory.Delete(path); }
            catch (Exception ex) { Console.WriteLine($"rmdir: {ex.Message}"); }
        }

        public static void Touch(List<string> args)
        {
            if (args.Count == 0) { Console.WriteLine("touch: missing file operand"); return; }
            string path = CommandProcessor.ResolvePath(args[0]);
            try { File.Create(path).Close(); }
            catch (Exception ex) { Console.WriteLine($"touch: {ex.Message}"); }
        }

        public static void Rm(List<string> args)
        {
            if (args.Count == 0) { Console.WriteLine("rm: missing operand"); return; }
            string path = CommandProcessor.ResolvePath(args[0]);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path) && args.Contains("-r")) Directory.Delete(path, true);
                else Console.WriteLine($"rm: cannot remove '{args[0]}': No such file");
            }
            catch (Exception ex) { Console.WriteLine($"rm: {ex.Message}"); }
        }

        public static void Cp(List<string> args)
        {
            if (args.Count < 2) { Console.WriteLine("cp: missing file operand"); return; }
            string src = CommandProcessor.ResolvePath(args[0]);
            string dest = CommandProcessor.ResolvePath(args[1]);
            try { File.Copy(src, dest, true); }
            catch (Exception ex) { Console.WriteLine($"cp: {ex.Message}"); }
        }

        public static void Mv(List<string> args)
        {
            if (args.Count < 2) { Console.WriteLine("mv: missing file operand"); return; }
            string src = CommandProcessor.ResolvePath(args[0]);
            string dest = CommandProcessor.ResolvePath(args[1]);
            try { File.Move(src, dest); }
            catch (Exception ex) { Console.WriteLine($"mv: {ex.Message}"); }
        }

        public static void Cat(List<string> args)
        {
            if (args.Count == 0) return;
            string path = CommandProcessor.ResolvePath(args[0]);
            if (File.Exists(path))
            {
                try { Console.WriteLine(File.ReadAllText(path)); }
                catch (Exception ex) { Console.WriteLine($"cat: {ex.Message}"); }
            }
            else Console.WriteLine($"cat: {args[0]}: No such file or directory");
        }

        public static void Tree(List<string> args)
        {
            string root = args.Count > 0 ? CommandProcessor.ResolvePath(args[0]) : Program.CurrentDirectory;
            if (!Directory.Exists(root)) { Console.WriteLine($"tree: {root}: No such directory"); return; }
            PrintTree(new DirectoryInfo(root), "", true);
        }

        private static void PrintTree(DirectoryInfo dir, string indent, bool last)
        {
            Console.WriteLine(indent + (last ? "└── " : "├── ") + dir.Name);
            indent += last ? "    " : "│   ";
            try
            {
                var dirs = dir.GetDirectories();
                var files = dir.GetFiles();
                for (int i = 0; i < dirs.Length; i++) PrintTree(dirs[i], indent, i == dirs.Length - 1 && files.Length == 0);
                for (int i = 0; i < files.Length; i++)
                {
                    bool isLast = i == files.Length - 1;
                    Console.WriteLine(indent + (isLast ? "└── " : "├── ") + files[i].Name);
                }
            }
            catch (UnauthorizedAccessException) { Console.WriteLine(indent + "└── [Access Denied]"); }
        }

        public static void Find(List<string> args)
        {
            if (args.Count < 2) { Console.WriteLine("Usage: find <path> <pattern>"); return; }
            string path = CommandProcessor.ResolvePath(args[0]);
            string pattern = args[1];
            try
            {
                var files = Directory.GetFiles(path, pattern, SearchOption.AllDirectories);
                foreach (var f in files) Console.WriteLine(f);
            }
            catch (Exception ex) { Console.WriteLine($"find: {ex.Message}"); }
        }

        public static void Grep(List<string> args)
        {
            if (args.Count < 2) { Console.WriteLine("Usage: grep <pattern> <file>"); return; }
            string pattern = args[0];
            string path = CommandProcessor.ResolvePath(args[1]);
            
            if (!File.Exists(path)) { Console.WriteLine($"grep: {path}: No such file"); return; }
            
            try
            {
                string[] lines = File.ReadAllLines(path);
                for(int i=0; i<lines.Length; i++)
                {
                    if (lines[i].Contains(pattern))
                    {
                        Console.Write($"{path}:{i+1}: ");
                        var parts = lines[i].Split(new[] { pattern }, StringSplitOptions.None);
                        for(int p=0; p<parts.Length; p++)
                        {
                            Console.Write(parts[p]);
                            if (p < parts.Length - 1)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Write(pattern);
                                Console.ResetColor();
                            }
                        }
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"grep: {ex.Message}"); }
        }
    }

    public static class NanoEditor
    {
        private static string Clipboard = "";

        public static void Run(List<string> args)
        {
            if (args.Count == 0) { Console.WriteLine("nano: missing filename"); return; }
            string filePath = CommandProcessor.ResolvePath(args[0]);
            List<string> lines = new List<string>();

            if (File.Exists(filePath)) lines.AddRange(File.ReadAllLines(filePath));
            if (lines.Count == 0) lines.Add("");

            int cx = 0, cy = 0;
            int offset = 0, coffset = 0;
            string message = "";

            while (true)
            {
                Console.CursorVisible = false;
                Console.SetCursorPosition(0, 0);
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.ForegroundColor = ConsoleColor.Black;
                string header = $"  GNU nano 9.0".PadRight(Console.WindowWidth / 2) + $"File: {Path.GetFileName(filePath)}";
                Console.Write(header.PadRight(Console.WindowWidth));
                Console.ResetColor();

                int availableLines = Console.WindowHeight - 4;
                for (int i = 0; i < availableLines; i++)
                {
                    Console.SetCursorPosition(0, i + 1);
                    int lineIdx = offset + i;
                    if (lineIdx < lines.Count)
                    {
                        string l = lines[lineIdx];
                        if (coffset < l.Length)
                        {
                            string disp = l.Substring(coffset);
                            if (disp.Length > Console.WindowWidth) disp = disp.Substring(0, Console.WindowWidth);
                            Console.Write(disp.PadRight(Console.WindowWidth));
                        }
                        else
                        {
                            Console.Write("".PadRight(Console.WindowWidth));
                        }
                    }
                    else
                    {
                        Console.Write("".PadRight(Console.WindowWidth));
                    }
                }

                Console.SetCursorPosition(0, Console.WindowHeight - 3);
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.Write(message.PadRight(Console.WindowWidth));
                message = "";

                Console.SetCursorPosition(0, Console.WindowHeight - 2);
                string helpStr1 = "^S Save   ^X Exit   ^W Where Is   ^G Go To Line";
                Console.Write(helpStr1.PadRight(Console.WindowWidth));
                Console.SetCursorPosition(0, Console.WindowHeight - 1);
                string helpStr2 = "^K Cut    ^U Paste  Arrows Move";
                Console.Write(helpStr2.PadRight(Console.WindowWidth));
                Console.ResetColor();

                Console.SetCursorPosition(cx - coffset, cy + 1);
                Console.CursorVisible = true;

                var key = Console.ReadKey(true);
                
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    if (key.Key == ConsoleKey.X) break;
                    if (key.Key == ConsoleKey.S)
                    {
                        File.WriteAllLines(filePath, lines);
                        message = $"[ Wrote {lines.Count} lines ]";
                        continue;
                    }
                    if (key.Key == ConsoleKey.K)
                    {
                        Clipboard = lines[offset + cy];
                        lines.RemoveAt(offset + cy);
                        if (lines.Count == 0) lines.Add("");
                        if (cy >= lines.Count - offset) cy = Math.Max(0, lines.Count - offset - 1);
                        cx = 0;
                        coffset = 0;
                        continue;
                    }
                    if (key.Key == ConsoleKey.U)
                    {
                        lines.Insert(offset + cy, Clipboard);
                        cy++;
                        if (cy >= availableLines) { cy--; offset++; }
                        continue;
                    }
                    if (key.Key == ConsoleKey.W)
                    {
                        Console.SetCursorPosition(0, Console.WindowHeight - 3);
                        Console.BackgroundColor = ConsoleColor.Gray;
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.Write("Search: ".PadRight(Console.WindowWidth));
                        Console.SetCursorPosition(8, Console.WindowHeight - 3);
                        Console.CursorVisible = true;
                        string term = Console.ReadLine();
                        
                        char check = term[0]; 
                        
                        for (int i = 0; i < lines.Count; i++)
                        {
                            if (lines[i].Contains(term))
                            {
                                offset = i;
                                cy = 0;
                                cx = 0;
                                break;
                            }
                        }
                        continue;
                    }
                    if (key.Key == ConsoleKey.G)
                    {
                        Console.SetCursorPosition(0, Console.WindowHeight - 3);
                        Console.BackgroundColor = ConsoleColor.Gray;
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.Write("Go to line: ".PadRight(Console.WindowWidth));
                        Console.SetCursorPosition(12, Console.WindowHeight - 3);
                        Console.CursorVisible = true;
                        string lineStr = Console.ReadLine();
                        
                        int lNum = int.Parse(lineStr) - 1;
                        if (lNum >= 0 && lNum < lines.Count)
                        {
                            offset = lNum;
                            cy = 0;
                            cx = 0;
                        }
                        continue;
                    }
                }

                ProcessKey(key, lines, ref cx, ref cy, ref offset, ref coffset, availableLines);
            }
            Console.Clear();
        }

        public static void ProcessKey(ConsoleKeyInfo key, List<string> lines, ref int cx, ref int cy, ref int offset, ref int coffset, int availableLines)
        {
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (cy > 0) cy--;
                    else if (offset > 0) offset--;
                    cx = Math.Min(cx, lines[offset + cy].Length);
                    if (cx < coffset) coffset = cx;
                    break;
                case ConsoleKey.DownArrow:
                    if (offset + cy < lines.Count - 1)
                    {
                        if (cy < availableLines - 1) cy++;
                        else offset++;
                        cx = Math.Min(cx, lines[offset + cy].Length);
                        if (cx < coffset) coffset = cx;
                    }
                    break;
                case ConsoleKey.LeftArrow:
                    if (cx > 0)
                    {
                        cx--;
                        if (cx < coffset) coffset = cx;
                    }
                    else if (offset + cy > 0)
                    {
                        if (cy > 0) cy--; else offset--;
                        cx = lines[offset + cy].Length;
                        if (cx >= Console.WindowWidth) coffset = cx - Console.WindowWidth + 1;
                        else coffset = 0;
                    }
                    break;
                case ConsoleKey.RightArrow:
                    if (cx < lines[offset + cy].Length)
                    {
                        cx++;
                        if (cx >= coffset + Console.WindowWidth) coffset = cx - Console.WindowWidth + 1;
                    }
                    else if (offset + cy < lines.Count - 1)
                    {
                        if (cy < availableLines - 1) cy++; else offset++;
                        cx = 0;
                        coffset = 0;
                    }
                    break;
                case ConsoleKey.Enter:
                    string currentLine = lines[offset + cy];
                    string rem = currentLine.Substring(cx);
                    lines[offset + cy] = currentLine.Substring(0, cx);
                    lines.Insert(offset + cy + 1, rem);
                    cx = 0;
                    coffset = 0;
                    if (cy < availableLines - 1) cy++; else offset++;
                    break;
                case ConsoleKey.Backspace:
                    if (cx > 0)
                    {
                        lines[offset + cy] = lines[offset + cy].Remove(cx - 1, 1);
                        cx--;
                        if (cx < coffset) coffset = cx;
                    }
                    else if (offset + cy > 0)
                    {
                        int prevLen = lines[offset + cy - 1].Length;
                        lines[offset + cy - 1] += lines[offset + cy];
                        lines.RemoveAt(offset + cy);
                        if (cy > 0) cy--; else offset--;
                        cx = prevLen;
                        if (cx >= Console.WindowWidth) coffset = cx - Console.WindowWidth + 1;
                        else coffset = 0;
                    }
                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        lines[offset + cy] = lines[offset + cy].Insert(cx, key.KeyChar.ToString());
                        cx++;
                        if (cx >= coffset + Console.WindowWidth) coffset = cx - Console.WindowWidth + 1;
                    }
                    break;
            }
        }
    }

    public static class BugReportEditor
    {
        public static void Run()
        {
            List<string> lines = new List<string> { "" };
            int cx = 0, cy = 0;
            int offset = 0, coffset = 0;

            while (true)
            {
                Console.CursorVisible = false;
                Console.SetCursorPosition(0, 0);
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.ForegroundColor = ConsoleColor.White;
                string header = $"  StyleOS Bug Reporter".PadRight(Console.WindowWidth / 2) + $"Dest: admin@timd.site";
                Console.Write(header.PadRight(Console.WindowWidth));
                Console.ResetColor();

                int availableLines = Console.WindowHeight - 4;
                for (int i = 0; i < availableLines; i++)
                {
                    Console.SetCursorPosition(0, i + 1);
                    int lineIdx = offset + i;
                    if (lineIdx < lines.Count)
                    {
                        string l = lines[lineIdx];
                        if (coffset < l.Length)
                        {
                            string disp = l.Substring(coffset);
                            if (disp.Length > Console.WindowWidth) disp = disp.Substring(0, Console.WindowWidth);
                            Console.Write(disp.PadRight(Console.WindowWidth));
                        }
                        else
                        {
                            Console.Write("".PadRight(Console.WindowWidth));
                        }
                    }
                    else
                    {
                        Console.Write("".PadRight(Console.WindowWidth));
                    }
                }

                Console.SetCursorPosition(0, Console.WindowHeight - 2);
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.ForegroundColor = ConsoleColor.White;
                string helpStr = "^C Send    ^X Cancel    Arrows Move";
                Console.Write(helpStr.PadRight(Console.WindowWidth));
                Console.ResetColor();

                Console.SetCursorPosition(cx - coffset, cy + 1);
                Console.CursorVisible = true;

                var key = Console.ReadKey(true);
                
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.X) break;
                
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.C)
                {
                    Console.Clear();
                    Console.WriteLine("Формирование отчета об ошибке...");
                    
                    try
                    {
                        string reportText = string.Join("\n", lines);
                        string logs = "";
                        if (File.Exists(Program.LogFile))
                        {
                            var allLogs = File.ReadAllLines(Program.LogFile);
                            logs = string.Join("\n", allLogs.Skip(Math.Max(0, allLogs.Length - 40)));
                        }

                        string subject = Uri.EscapeDataString("StyleOS Bug Report");
                        string body = Uri.EscapeDataString($"User Description:\n{reportText}\n\nSystem Logs:\n{logs}");
                        string url = $"https://mail.google.com/mail/?view=cm&fs=1&to=admin@timd.site&su={subject}&body={body}";

                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Браузер с подготовленным письмом был открыт. Нажмите 'Отправить' в почте.");
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Ошибка при открытии браузера: {ex.Message}");
                    }
                    
                    Console.ResetColor();
                    Thread.Sleep(2000);
                    break;
                }

                NanoEditor.ProcessKey(key, lines, ref cx, ref cy, ref offset, ref coffset, availableLines);
            }
            Console.Clear();
        }
    }

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

            string[] ascii = {
                @"    _______    ",
                @"   /  ___  \   ",
                @"  |  (__ \_|   ",
                @"   \___  \     ",
                @"  |\___)  |    ",
                @"   \_____/     "
            };

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            
            int pCount = Math.Max(ascii.Length, 8);
            for (int i = 0; i < pCount; i++)
            {
                string left = i < ascii.Length ? ascii[i].PadRight(20) : new string(' ', 20);
                Console.Write(left);
                Console.ForegroundColor = ConsoleColor.White;

                switch (i)
                {
                    case 0: Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"{user}@{host}"); break;
                    case 1: Console.WriteLine(new string('-', $"{user}@{host}".Length)); break;
                    case 2: Console.WriteLine($"OS: Style OS {Program.Version} ({os})"); break;
                    case 3: Console.WriteLine($"Kernel: C# .NET 10 ({net})"); break;
                    case 4: Console.WriteLine($"Uptime: {uptime}"); break;
                    case 5: Console.WriteLine($"CPU: {hw.Cpu}"); break;
                    case 6: Console.WriteLine($"GPU: {hw.Gpu}"); break;
                    case 7: Console.WriteLine($"Memory: {hw.Memory}"); break;
                    case 8: Console.WriteLine($"Arch: {arch}"); break;
                }
                Console.ForegroundColor = ConsoleColor.Cyan;
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

    public static class DiskCommands
    {
        public static void Df()
        {
            Console.WriteLine($"{"Filesystem",-15} {"1K-blocks",-15} {"Used",-15} {"Available",-15} {"Use%",-5} {"Mounted on"}");
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    long total = drive.TotalSize / 1024;
                    long free = drive.TotalFreeSpace / 1024;
                    long used = total - free;
                    double percent = (double)used / total * 100;
                    
                    Console.WriteLine($"{drive.Name,-15} {total,-15} {used,-15} {free,-15} {Math.Round(percent)}%{"",-4} {drive.RootDirectory}");
                }
            }
        }

        public static void Drives() => Df();

        public static void Mount()
        {
            Console.WriteLine("sysfs on /sys type sysfs (rw,nosuid,nodev,noexec,relatime)");
            Console.WriteLine("proc on /proc type proc (rw,nosuid,nodev,noexec,relatime)");
            foreach(var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                Console.WriteLine($"{drive.RootDirectory} on {drive.Name} type {drive.DriveFormat} (rw,relatime)");
            }
        }
    }

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
            using HttpClient client = new HttpClient();
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
            using HttpClient client = new HttpClient();
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

    public static class AuthCommands
    {
        public static void Passwd()
        {
            Console.Write($"Changing password for {Program.CurrentUser.Username}.\nNew password: ");
            string p1 = AuthSystem.ReadPassword();
            Console.Write("\nRetype new password: ");
            string p2 = AuthSystem.ReadPassword();
            Console.WriteLine();

            if (p1 == p2)
            {
                AuthSystem.ChangePassword(Program.CurrentUser.Username, p1);
                Console.WriteLine("passwd: password updated successfully");
            }
            else Console.WriteLine("passwd: passwords do not match");
        }

        public static void UserAdd(List<string> args)
        {
            if (!Program.CurrentUser.IsRoot) { Console.WriteLine("useradd: Permission denied."); return; }
            if (args.Count == 0) { Console.WriteLine("Usage: useradd <username>"); return; }
            
            Console.Write("Set password: ");
            string pass = AuthSystem.ReadPassword();
            Console.WriteLine();
            try
            {
                AuthSystem.AddUser(args[0], pass);
                Console.WriteLine($"User '{args[0]}' created successfully.");
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        public static void UserDel(List<string> args)
        {
            if (!Program.CurrentUser.IsRoot) { Console.WriteLine("userdel: Permission denied."); return; }
            if (args.Count == 0) { Console.WriteLine("Usage: userdel <username>"); return; }
            try
            {
                AuthSystem.DeleteUser(args[0]);
                Console.WriteLine($"User '{args[0]}' deleted.");
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }
    }

    public static class PackageManager
    {
        public static void HandleCommand(List<string> args)
        {
            if (args.Count < 2) { Console.WriteLine("Usage: pkg <install|remove|search> <package>"); return; }
            string action = args[0].ToLower();
            string pkg = args[1].ToLower();

            List<string> installed = new List<string>();
            if (File.Exists(Program.PkgFile)) installed = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(Program.PkgFile));

            if (action == "install")
            {
                if (installed.Contains(pkg)) { Console.WriteLine($"{pkg} is already the newest version."); return; }
                
                Console.WriteLine($"Reading package lists... Done");
                Console.WriteLine($"Building dependency tree... Done");
                Console.WriteLine($"The following NEW packages will be installed:\n  {pkg}");
                Thread.Sleep(500);
                Console.WriteLine($"Get:1 http://repo.styleos.net/main {pkg} amd64 [1,204 kB]");
                Thread.Sleep(1000);
                Console.WriteLine($"Fetched 1,204 kB in 1s (1,204 kB/s)");
                Console.WriteLine($"Selecting previously unselected package {pkg}.");
                Console.WriteLine($"Preparing to unpack .../{pkg}.deb ...");
                Console.WriteLine($"Unpacking {pkg} ...");
                Console.WriteLine($"Setting up {pkg} ...");
                
                installed.Add(pkg);
                File.WriteAllText(Program.PkgFile, JsonSerializer.Serialize(installed));
            }
            else if (action == "remove")
            {
                if (!installed.Contains(pkg)) { Console.WriteLine($"Package '{pkg}' is not installed, so not removed"); return; }
                Console.WriteLine($"Removing {pkg} ...");
                installed.Remove(pkg);
                File.WriteAllText(Program.PkgFile, JsonSerializer.Serialize(installed));
                Console.WriteLine("Done.");
            }
            else if (action == "search")
            {
                Console.WriteLine($"Searching for {args[1]}...");
                string desc = args[2]; 
            }
        }

        public static async Task HandlePacman(List<string> args)
        {
            if (args.Count == 0) { Console.WriteLine("pacman: missing operation"); return; }
            string action = args[0].ToLower();

            if (action == "update")
            {
                await UpdateSystem.RunUpdateProcess();
            }
            else
            {
                Console.WriteLine($"pacman: invalid option '{action}'");
            }
        }
    }

    public static class Utils
    {
        public static void Authors()
        {
            Console.WriteLine("Tim");
        }

        public static void Help()
        {
            Console.WriteLine("Style OS - Available Commands:");
            Console.WriteLine("  Files:   ls, dir, cd, pwd, mkdir, rmdir, touch, rm, cp, mv, cat, nano, echo, clear, tree, find, grep");
            Console.WriteLine("  System:  neofetch, sysinfo, uname, hostname, time, date, reboot, shutdown, top, ps, authors, history");
            Console.WriteLine("  Network: ping, curl, wget, tc, ipconfig, netstat");
            Console.WriteLine("  Disks:   df, mount, drives");
            Console.WriteLine("  Auth:    passwd, useradd, userdel, whoami");
            Console.WriteLine("  Misc:    calc, theme, pkg/apt, pacman update, bugreport");
        }

        public static void Calc(List<string> args)
        {
            if (args.Count == 0) { Console.WriteLine("Usage: calc <expression>"); return; }
            string expr = string.Join("", args);
            try
            {
                var dt = new DataTable();
                var result = dt.Compute(expr, "");
                Console.WriteLine(result);
            }
            catch { Console.WriteLine("calc: invalid expression"); }
        }

        public static void Theme(List<string> args)
        {
            if (args.Count < 2)
            {
                Console.WriteLine("Usage: theme <prompt|bg|text> <color>");
                return;
            }
            if (Enum.TryParse(args[1], true, out ConsoleColor c))
            {
                switch (args[0].ToLower())
                {
                    case "prompt": Program.Config.PromptUserColor = c; break;
                    case "bg": Program.Config.DefaultBgColor = c; break;
                    case "text": Program.Config.DefaultTextColor = c; break;
                }
                ConfigManager.SaveConfig();
                ConfigManager.ApplyTheme();
                Console.WriteLine("Theme updated.");
            }
            else Console.WriteLine("Invalid color.");
        }

        public static void ShowHistory()
        {
            if (File.Exists(Program.HistoryFile))
            {
                var lines = File.ReadAllLines(Program.HistoryFile);
                for (int i = 0; i < lines.Length; i++)
                {
                    Console.WriteLine($"  {i + 1}  {lines[i]}");
                }
            }
        }
    }
}