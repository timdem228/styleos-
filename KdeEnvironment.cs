namespace StyleOS
{
    public class KdeWindow
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Title { get; set; }
        public string Type { get; set; } // "term", "calc", "dolphin", "kwrite", "sysguard", "shitshell"
        public List<string> Buffer { get; set; } = new List<string>();
        
        public int DolphinSelectedIndex { get; set; } = 0;
        public string DolphinPath { get; set; } = Program.CurrentDirectory;

        public string KWriteFilePath { get; set; } = "";
        public List<string> KWriteLines { get; set; } = new List<string> { "" };
        public int KWriteCursorX { get; set; } = 0;
        public int KWriteCursorY { get; set; } = 0;
    }

    public static class KdeEnvironment
    {
        public static bool IsInstalled
        {
            get { return File.Exists(Program.KdeFlagFile); }
            set { if (value) File.WriteAllText(Program.KdeFlagFile, "1"); else if (File.Exists(Program.KdeFlagFile)) File.Delete(Program.KdeFlagFile); }
        }

        private static int bufWidth;
        private static int bufHeight;
        private static char[,] backChar;
        private static ConsoleColor[,] backFg;
        private static ConsoleColor[,] backBg;

        private static char[,] frontChar;
        private static ConsoleColor[,] frontFg;
        private static ConsoleColor[,] frontBg;

        private static void InitDoubleBuffer()
        {
            bufWidth = Console.WindowWidth;
            bufHeight = Console.WindowHeight;

            backChar = new char[bufWidth, bufHeight];
            backFg = new ConsoleColor[bufWidth, bufHeight];
            backBg = new ConsoleColor[bufWidth, bufHeight];

            frontChar = new char[bufWidth, bufHeight];
            frontFg = new ConsoleColor[bufWidth, bufHeight];
            frontBg = new ConsoleColor[bufWidth, bufHeight];

            for (int y = 0; y < bufHeight; y++)
            {
                for (int x = 0; x < bufWidth; x++)
                {
                    frontChar[x, y] = ' ';
                    frontFg[x, y] = ConsoleColor.White;
                    frontBg[x, y] = ConsoleColor.Black;
                }
            }
        }

        private static void DrawWallpaper(int frame)
        {
            Random rnd = new Random(1337);
            for (int y = 0; y < bufHeight - 1; y++)
            {
                double aurora = Math.Sin((double)y / 5.0 + (double)frame / 10.0) * 12.0;
                
                for (int x = 0; x < bufWidth; x++)
                {
                    backChar[x, y] = ' ';
                    double distance = Math.Abs(x - (bufWidth / 2.0 + aurora));
                    
                    ConsoleColor bg = ConsoleColor.Black;
                    if (distance < 12) bg = ConsoleColor.DarkBlue;
                    else if (distance < 28) bg = ConsoleColor.DarkMagenta;
                    
                    backBg[x, y] = bg;
                    backFg[x, y] = ConsoleColor.DarkGray;

                    if (rnd.Next(200) < 2)
                    {
                        int twinkle = (frame + rnd.Next(15)) % 6;
                        if (twinkle == 0)
                        {
                            backChar[x, y] = '✦';
                            backFg[x, y] = ConsoleColor.White;
                        }
                        else if (twinkle == 1 || twinkle == 2)
                        {
                            backChar[x, y] = '*';
                            backFg[x, y] = ConsoleColor.Gray;
                        }
                        else
                        {
                            backChar[x, y] = '•';
                            backFg[x, y] = ConsoleColor.DarkGray;
                        }
                    }
                }
            }
        }

        private static void WriteToBuffer(int x, int y, string text, ConsoleColor fg, ConsoleColor bg)
        {
            if (y < 0 || y >= bufHeight) return;
            for (int i = 0; i < text.Length; i++)
            {
                int targetX = x + i;
                if (targetX >= 0 && targetX < bufWidth)
                {
                    backChar[targetX, y] = text[i];
                    backFg[targetX, y] = fg;
                    backBg[targetX, y] = bg;
                }
            }
        }

        private static void RenderFrame()
        {
            for (int y = 0; y < bufHeight; y++)
            {
                for (int x = 0; x < bufWidth; x++)
                {
                    if (backChar[x, y] != frontChar[x, y] || 
                        backFg[x, y] != frontFg[x, y] || 
                        backBg[x, y] != frontBg[x, y])
                    {
                        Console.SetCursorPosition(x, y);
                        Console.ForegroundColor = backFg[x, y];
                        Console.BackgroundColor = backBg[x, y];
                        Console.Write(backChar[x, y]);

                        frontChar[x, y] = backChar[x, y];
                        frontFg[x, y] = backFg[x, y];
                        frontBg[x, y] = backBg[x, y];
                    }
                }
            }
        }

        public static void Run()
        {
            if (!IsInstalled)
            {
                Console.WriteLine("KDE is not installed. Type 'kde install' first.");
                return;
            }

            InitDoubleBuffer();

            bool running = true;
            List<KdeWindow> windows = new List<KdeWindow>();
            
            windows.Add(new KdeWindow { Id = 0, X = 5, Y = 2, Width = 55, Height = 14, Title = "Terminal", Type = "term", Buffer = new List<string> { "root@kde:~# " } });
            windows.Add(new KdeWindow { Id = 1, X = 63, Y = 3, Width = 30, Height = 9, Title = "Calculator", Type = "calc", Buffer = new List<string> { "0" } });
            
            int activeWin = 0;
            bool startOpen = false;
            int frame = 0;

            Console.CursorVisible = false;

            while (running)
            {
                Draw(windows, activeWin, startOpen, frame);
                RenderFrame();
                frame++;

                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(true);

                    if (keyInfo.Key == ConsoleKey.F1)
                    {
                        startOpen = !startOpen;
                    }
                    else if (keyInfo.Key == ConsoleKey.Tab)
                    {
                        if (windows.Count > 0)
                        {
                            activeWin = (activeWin + 1) % windows.Count;
                        }
                    }
                    else if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        if (keyInfo.Key == ConsoleKey.UpArrow && windows.Count > 0) windows[activeWin].Y = Math.Max(0, windows[activeWin].Y - 1);
                        if (keyInfo.Key == ConsoleKey.DownArrow && windows.Count > 0) windows[activeWin].Y = Math.Min(Console.WindowHeight - windows[activeWin].Height - 3, windows[activeWin].Y + 1);
                        if (keyInfo.Key == ConsoleKey.LeftArrow && windows.Count > 0) windows[activeWin].X = Math.Max(0, windows[activeWin].X - 1);
                        if (keyInfo.Key == ConsoleKey.RightArrow && windows.Count > 0) windows[activeWin].X = Math.Min(Console.WindowWidth - windows[activeWin].Width, windows[activeWin].X + 1);
                        
                        if (keyInfo.Key == ConsoleKey.W && windows.Count > 0)
                        {
                            windows.RemoveAt(activeWin);
                            if (windows.Count > 0) activeWin = 0;
                        }
                        else if (keyInfo.Key == ConsoleKey.S && windows.Count > 0 && windows[activeWin].Type == "kwrite")
                        {
                            var kw = windows[activeWin];
                            try
                            {
                                File.WriteAllLines(kw.KWriteFilePath, kw.KWriteLines);
                                kw.Title = $"KWrite - {Path.GetFileName(kw.KWriteFilePath)} [Saved]";
                            }
                            catch (Exception ex)
                            {
                                kw.Title = $"KWrite [Error saving: {ex.Message}]";
                            }
                        }
                    }
                    else if (startOpen && !keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                    {
                        if (keyInfo.KeyChar == '1')
                        {
                            windows.Add(new KdeWindow { Id = windows.Count, X = 8, Y = 6, Width = 55, Height = 14, Title = "Terminal", Type = "term", Buffer = new List<string> { "root@kde:~# " } });
                            startOpen = false;
                            activeWin = windows.Count - 1;
                        }
                        else if (keyInfo.KeyChar == '2')
                        {
                            windows.Add(new KdeWindow { Id = windows.Count, X = 12, Y = 5, Width = 30, Height = 9, Title = "Calculator", Type = "calc", Buffer = new List<string> { "0" } });
                            startOpen = false;
                            activeWin = windows.Count - 1;
                        }
                        else if (keyInfo.KeyChar == '3')
                        {
                            windows.Add(new KdeWindow { Id = windows.Count, X = 16, Y = 4, Width = 45, Height = 12, Title = "Dolphin-lite", Type = "dolphin", DolphinPath = Program.CurrentDirectory });
                            startOpen = false;
                            activeWin = windows.Count - 1;
                        }
                        else if (keyInfo.KeyChar == '4')
                        {
                            windows.Add(new KdeWindow { Id = windows.Count, X = 20, Y = 3, Width = 40, Height = 10, Title = "KSysGuard", Type = "sysguard" });
                            startOpen = false;
                            activeWin = windows.Count - 1;
                        }
                        else if (keyInfo.KeyChar == '5')
                        {
                            windows.Add(new KdeWindow { Id = windows.Count, X = 15, Y = 6, Width = 50, Height = 16, Title = "ShitShell Browser", Type = "shitshell", Buffer = new List<string> { "shitshell - nikwonder's console system, go to him and help him shove a huge dildo up his ass ╰(*°▽°*)╯", "shit> " } });
                            startOpen = false;
                            activeWin = windows.Count - 1;
                        }
                        else if (keyInfo.KeyChar == '6')
                        {
                            running = false;
                        }
                    }
                    else if (windows.Count > 0)
                    {
                        var w = windows[activeWin];

                        if (w.Type == "term" || w.Type == "shitshell")
                        {
                            if (keyInfo.Key == ConsoleKey.Backspace)
                            {
                                string promptStr = w.Type == "term" ? "root@kde:~# " : "shit> ";
                                if (w.Buffer[w.Buffer.Count - 1].Length > promptStr.Length)
                                    w.Buffer[w.Buffer.Count - 1] = w.Buffer[w.Buffer.Count - 1].Substring(0, w.Buffer[w.Buffer.Count - 1].Length - 1);
                            }
                            else if (keyInfo.Key == ConsoleKey.Enter)
                            {
                                if (w.Type == "term")
                                {
                                    string cmd = w.Buffer[w.Buffer.Count - 1].Replace("root@kde:~# ", "").Trim();
                                    if (cmd.ToLower() == "kde stop" || cmd.ToLower() == "exit")
                                    {
                                        running = false;
                                        break;
                                    }
                                    
                                    if (cmd.ToLower() == "shitshell start")
                                    {
                                        windows.Add(new KdeWindow { Id = windows.Count, X = 15, Y = 6, Width = 50, Height = 16, Title = "ShitShell Browser", Type = "shitshell", Buffer = new List<string> { "shitshell - nikwonder's console system, go to him and help him shove a huge dildo up his ass ╰(*°▽°*)╯", "shit> " } });
                                        activeWin = windows.Count - 1;
                                        w.Buffer.Add("root@kde:~# ");
                                        continue;
                                    }

                                    w.Buffer.Add("Executing: " + cmd);

                                    var sw = new StringWriter();
                                    var originalOut = Console.Out;
                                    Console.SetOut(sw);
                                    try
                                    {
                                        CommandProcessor.Execute(cmd, true).GetAwaiter().GetResult();
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error: {ex.Message}");
                                    }
                                    Console.SetOut(originalOut);

                                    var lines = sw.ToString().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                                    foreach(var line in lines)
                                    {
                                        if (!string.IsNullOrEmpty(line))
                                            w.Buffer.Add(line);
                                    }

                                    w.Buffer.Add("root@kde:~# ");
                                }
                                else if (w.Type == "shitshell")
                                {
                                    string cmd = w.Buffer[w.Buffer.Count - 1].Replace("shit> ", "").Trim();
                                    if (cmd.ToLower() == "exit")
                                    {
                                        windows.RemoveAt(activeWin);
                                        if (windows.Count > 0) activeWin = 0;
                                    }
                                    else
                                    {
                                        ShitShellEnv.Init();
                                        string output = ShitShellEnv.ExecuteCommand(cmd);
                                        if (!string.IsNullOrEmpty(output))
                                        {
                                            foreach(var line in output.Split('\n')) w.Buffer.Add(line.Replace("\r", ""));
                                        }
                                        w.Buffer.Add("shit> ");
                                    }
                                }
                            }
                            else if (!char.IsControl(keyInfo.KeyChar))
                            {
                                w.Buffer[w.Buffer.Count - 1] += keyInfo.KeyChar;
                            }
                        }
                        else if (w.Type == "calc")
                        {
                            if (keyInfo.Key == ConsoleKey.Backspace)
                            {
                                if (w.Buffer[0].Length > 0)
                                {
                                    w.Buffer[0] = w.Buffer[0].Substring(0, w.Buffer[0].Length - 1);
                                    if (w.Buffer[0] == "") w.Buffer[0] = "0";
                                }
                            }
                            else if (keyInfo.Key == ConsoleKey.Enter)
                            {
                                try
                                {
                                    var dt = new DataTable();
                                    w.Buffer[0] = dt.Compute(w.Buffer[0], "").ToString();
                                }
                                catch { w.Buffer[0] = "Error"; }
                            }
                            else if (!char.IsControl(keyInfo.KeyChar))
                            {
                                if (w.Buffer[0] == "0" || w.Buffer[0] == "Error") w.Buffer[0] = "";
                                w.Buffer[0] += keyInfo.KeyChar;
                            }
                        }
                        else if (w.Type == "dolphin")
                        {
                            var dirs = Directory.GetDirectories(w.DolphinPath).ToList();
                            var files = Directory.GetFiles(w.DolphinPath).ToList();
                            int totalItems = dirs.Count + files.Count;

                            if (keyInfo.Key == ConsoleKey.UpArrow)
                            {
                                w.DolphinSelectedIndex = Math.Max(0, w.DolphinSelectedIndex - 1);
                            }
                            else if (keyInfo.Key == ConsoleKey.DownArrow)
                            {
                                w.DolphinSelectedIndex = Math.Min(totalItems - 1, w.DolphinSelectedIndex + 1);
                            }
                            else if (keyInfo.Key == ConsoleKey.Enter && totalItems > 0)
                            {
                                if (w.DolphinSelectedIndex < dirs.Count)
                                {
                                    w.DolphinPath = dirs[w.DolphinSelectedIndex];
                                    w.DolphinSelectedIndex = 0;
                                }
                                else
                                {
                                    string selFile = files[w.DolphinSelectedIndex - dirs.Count];
                                    if (selFile.EndsWith(".txt") || selFile.EndsWith(".json") || selFile.EndsWith(".log"))
                                    {
                                        var kw = new KdeWindow
                                        {
                                            Id = windows.Count,
                                            X = w.X + 4,
                                            Y = w.Y + 4,
                                            Width = 45,
                                            Height = 12,
                                            Title = $"KWrite - {Path.GetFileName(selFile)}",
                                            Type = "kwrite",
                                            KWriteFilePath = selFile,
                                            KWriteLines = File.Exists(selFile) ? File.ReadAllLines(selFile).ToList() : new List<string> { "" }
                                        };
                                        if (kw.KWriteLines.Count == 0) kw.KWriteLines.Add("");
                                        windows.Add(kw);
                                        activeWin = windows.Count - 1;
                                    }
                                }
                            }
                        }
                        else if (w.Type == "kwrite")
                        {
                            if (keyInfo.Key == ConsoleKey.Backspace)
                            {
                                if (w.KWriteCursorX > 0)
                                {
                                    w.KWriteLines[w.KWriteCursorY] = w.KWriteLines[w.KWriteCursorY].Remove(w.KWriteCursorX - 1, 1);
                                    w.KWriteCursorX--;
                                }
                                else if (w.KWriteCursorY > 0)
                                {
                                    int prevLen = w.KWriteLines[w.KWriteCursorY - 1].Length;
                                    w.KWriteLines[w.KWriteCursorY - 1] += w.KWriteLines[w.KWriteCursorY];
                                    w.KWriteLines.RemoveAt(w.KWriteCursorY);
                                    w.KWriteCursorY--;
                                    w.KWriteCursorX = prevLen;
                                }
                            }
                            else if (keyInfo.Key == ConsoleKey.Enter)
                            {
                                string curr = w.KWriteLines[w.KWriteCursorY];
                                string rem = curr.Substring(w.KWriteCursorX);
                                w.KWriteLines[w.KWriteCursorY] = curr.Substring(0, w.KWriteCursorX);
                                w.KWriteLines.Insert(w.KWriteCursorY + 1, rem);
                                w.KWriteCursorY++;
                                w.KWriteCursorX = 0;
                            }
                            else if (keyInfo.Key == ConsoleKey.UpArrow)
                            {
                                if (w.KWriteCursorY > 0)
                                {
                                    w.KWriteCursorY--;
                                    w.KWriteCursorX = Math.Min(w.KWriteCursorX, w.KWriteLines[w.KWriteCursorY].Length);
                                }
                            }
                            else if (keyInfo.Key == ConsoleKey.DownArrow)
                            {
                                if (w.KWriteCursorY < w.KWriteLines.Count - 1)
                                {
                                    w.KWriteCursorY++;
                                    w.KWriteCursorX = Math.Min(w.KWriteCursorX, w.KWriteLines[w.KWriteCursorY].Length);
                                }
                            }
                            else if (keyInfo.Key == ConsoleKey.LeftArrow)
                            {
                                w.KWriteCursorX = Math.Max(0, w.KWriteCursorX - 1);
                            }
                            else if (keyInfo.Key == ConsoleKey.RightArrow)
                            {
                                w.KWriteCursorX = Math.Min(w.KWriteLines[w.KWriteCursorY].Length, w.KWriteCursorX + 1);
                            }
                            else if (!char.IsControl(keyInfo.KeyChar))
                            {
                                w.KWriteLines[w.KWriteCursorY] = w.KWriteLines[w.KWriteCursorY].Insert(w.KWriteCursorX, keyInfo.KeyChar.ToString());
                                w.KWriteCursorX++;
                            }
                        }
                    }
                }
                else
                {
                    Thread.Sleep(40);
                }
            }

            Console.Clear();
            Console.CursorVisible = true;
            ConfigManager.ApplyTheme();
        }

        private static void Draw(List<KdeWindow> windows, int activeWin, bool startOpen, int frame)
        {
            DrawWallpaper(frame);

            WriteToBuffer(2, 2, "   [1] Terminal (Launcher)", ConsoleColor.White, ConsoleColor.Black);
            WriteToBuffer(2, 3, "   [2] Calculator (Launcher)", ConsoleColor.White, ConsoleColor.Black);
            WriteToBuffer(2, 4, "   [3] Dolphin File Manager", ConsoleColor.White, ConsoleColor.Black);
            WriteToBuffer(2, 5, "   [4] KSysGuard Monitor", ConsoleColor.White, ConsoleColor.Black);
            WriteToBuffer(2, 6, "   [5] ShitShell Browser", ConsoleColor.White, ConsoleColor.Black);

            WriteToBuffer(bufWidth - 30, 2, $"StyleOS KDE Plasma v{Program.Version}", ConsoleColor.Yellow, ConsoleColor.Black);

            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                bool isActive = i == activeWin;
                ConsoleColor titleBg = isActive ? ConsoleColor.Blue : ConsoleColor.DarkGray;
                
                ConsoleColor borderCol = isActive ? 
                    (new[] { ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.White })[(frame / 4) % 3] 
                    : ConsoleColor.DarkGray;

                char tl = isActive ? '╔' : '┌';
                char tr = isActive ? '╗' : '┐';
                char bl = isActive ? '╚' : '└';
                char br = isActive ? '╝' : '┘';
                char h = isActive ? '═' : '─';
                char v = isActive ? '║' : '│';

                string titleString = $"[ {w.Title} ]";
                int sidesLength = (w.Width - 2 - titleString.Length) / 2;
                if (sidesLength < 0) sidesLength = 0;
                string topBorder = tl + new string(h, sidesLength) + titleString + new string(h, Math.Max(0, w.Width - 2 - sidesLength - titleString.Length)) + tr;
                WriteToBuffer(w.X, w.Y, topBorder, borderCol, ConsoleColor.Black);

                for (int r = 0; r < w.Height; r++)
                {
                    WriteToBuffer(w.X, w.Y + 1 + r, v.ToString(), borderCol, ConsoleColor.Black);
                    WriteToBuffer(w.X + 1, w.Y + 1 + r, new string(' ', w.Width - 2), ConsoleColor.White, ConsoleColor.Black);
                    WriteToBuffer(w.X + w.Width - 1, w.Y + 1 + r, v.ToString(), borderCol, ConsoleColor.Black);
                }

                string bottomBorder = bl + new string(h, w.Width - 2) + br;
                WriteToBuffer(w.X, w.Y + 1 + w.Height, bottomBorder, borderCol, ConsoleColor.Black);

                if (w.Type == "term" || w.Type == "shitshell")
                {
                    int startLine = Math.Max(0, w.Buffer.Count - w.Height);
                    for (int r = 0; r < Math.Min(w.Height, w.Buffer.Count); r++)
                    {
                        string line = w.Buffer[startLine + r];
                        if (line.Length > w.Width - 4) line = line.Substring(0, w.Width - 4);
                        WriteToBuffer(w.X + 2, w.Y + 1 + r, line, ConsoleColor.White, ConsoleColor.Black);
                    }
                }
                else if (w.Type == "calc")
                {
                    string disp = w.Buffer[0];
                    if (disp.Length > w.Width - 6) disp = disp.Substring(0, w.Width - 6);
                    WriteToBuffer(w.X + 2, w.Y + 3, "Expr: " + disp, ConsoleColor.Green, ConsoleColor.Black);
                }
                else if (w.Type == "dolphin")
                {
                    var dirs = Directory.GetDirectories(w.DolphinPath).ToList();
                    var files = Directory.GetFiles(w.DolphinPath).ToList();
                    var allItems = new List<string>();
                    foreach (var d in dirs) allItems.Add($"[Dir]  {Path.GetFileName(d)}/");
                    foreach (var f in files) allItems.Add($"[File] {Path.GetFileName(f)}");

                    string shortPath = w.DolphinPath;
                    if (shortPath.Length > w.Width - 6) shortPath = "..." + shortPath.Substring(shortPath.Length - (w.Width - 10));
                    WriteToBuffer(w.X + 2, w.Y + 1, " Path: " + shortPath, ConsoleColor.Yellow, ConsoleColor.Black);

                    for (int r = 0; r < w.Height - 2; r++)
                    {
                        int itemIdx = r;
                        if (itemIdx < allItems.Count)
                        {
                            bool isSelected = itemIdx == w.DolphinSelectedIndex;
                            ConsoleColor fg = isSelected ? ConsoleColor.Black : ConsoleColor.White;
                            ConsoleColor bg = isSelected ? ConsoleColor.White : ConsoleColor.Black;
                            string line = (isSelected ? "> " : "  ") + allItems[itemIdx];
                            if (line.Length > w.Width - 4) line = line.Substring(0, w.Width - 4);
                            WriteToBuffer(w.X + 2, w.Y + 2 + r, line.PadRight(w.Width - 4), fg, bg);
                        }
                    }
                }
                else if (w.Type == "kwrite")
                {
                    for (int r = 0; r < w.Height; r++)
                    {
                        int lineIdx = r;
                        if (lineIdx < w.KWriteLines.Count)
                        {
                            string line = w.KWriteLines[lineIdx];
                            if (line.Length > w.Width - 4) line = line.Substring(0, w.Width - 4);
                            WriteToBuffer(w.X + 2, w.Y + 1 + r, line, ConsoleColor.White, ConsoleColor.Black);
                        }
                    }
                    if (isActive && w.KWriteCursorY < w.Height)
                    {
                        int cPosX = w.X + 2 + w.KWriteCursorX;
                        int cPosY = w.Y + 1 + w.KWriteCursorY;
                        if (cPosX < w.X + w.Width - 2)
                        {
                            backBg[cPosX, cPosY] = ConsoleColor.White;
                            backFg[cPosX, cPosY] = ConsoleColor.Black;
                        }
                    }
                }
                else if (w.Type == "sysguard")
                {
                    Random rnd = new Random();
                    int cpuVal = rnd.Next(15, 45);
                    int ramVal = (int)((Process.GetCurrentProcess().WorkingSet64 * 100) / (8L * 1024 * 1024 * 1024));
                    ramVal = Math.Clamp(ramVal, 5, 95);

                    string cpuBar = GetProgressBar(cpuVal);
                    string ramBar = GetProgressBar(ramVal);

                    WriteToBuffer(w.X + 2, w.Y + 2, $"CPU usage: {cpuBar} {cpuVal}%", ConsoleColor.Green, ConsoleColor.Black);
                    WriteToBuffer(w.X + 2, w.Y + 4, $"RAM usage: {ramBar} {ramVal}%", ConsoleColor.Cyan, ConsoleColor.Black);
                    WriteToBuffer(w.X + 2, w.Y + 6, $"Active Process PID: {Process.GetCurrentProcess().Id}", ConsoleColor.DarkGray, ConsoleColor.Black);
                }
            }

            string taskbar = $"  [F1] Start   [Tab] Switch   [Ctrl+Arrows] Move   [Ctrl+W] Close ".PadRight(Math.Max(0, bufWidth - 10)) + DateTime.Now.ToString("HH:mm") + "  ";
            if (taskbar.Length > bufWidth) taskbar = taskbar.Substring(0, bufWidth);
            WriteToBuffer(0, bufHeight - 1, taskbar, ConsoleColor.Black, ConsoleColor.Gray);

            if (startOpen)
            {
                WriteToBuffer(0, bufHeight - 8, " Start Menu ".PadRight(22), ConsoleColor.Black, ConsoleColor.Gray);
                WriteToBuffer(0, bufHeight - 7, " 1. Terminal".PadRight(22), ConsoleColor.Black, ConsoleColor.Gray);
                WriteToBuffer(0, bufHeight - 6, " 2. Calculator".PadRight(22), ConsoleColor.Black, ConsoleColor.Gray);
                WriteToBuffer(0, bufHeight - 5, " 3. Dolphin FM".PadRight(22), ConsoleColor.Black, ConsoleColor.Gray);
                WriteToBuffer(0, bufHeight - 4, " 4. KSysGuard".PadRight(22), ConsoleColor.Black, ConsoleColor.Gray);
                WriteToBuffer(0, bufHeight - 3, " 5. ShitShell".PadRight(22), ConsoleColor.Black, ConsoleColor.Gray);
                WriteToBuffer(0, bufHeight - 2, " 6. Exit KDE".PadRight(22), ConsoleColor.Black, ConsoleColor.Gray);
            }
        }

        private static string GetProgressBar(int val)
        {
            int blockCount = val / 10;
            string filled = new string('█', blockCount);
            string empty = new string('░', 10 - blockCount);
            return $"[{filled}{empty}]";
        }
    }
}
