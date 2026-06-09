namespace StyleOS
{
    public static class BugReportEditor
    {
        public static void Run()
        {
            List<string> lines = new List<string> { "" };
            int cx = 0, cy = 0;
            int offset = 0, coffset = 0;
            string statusMsg = "";

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

                Console.SetCursorPosition(0, Console.WindowHeight - 3);
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.Write(statusMsg.PadRight(Console.WindowWidth));
                statusMsg = "";

                Console.SetCursorPosition(0, Console.WindowHeight - 2);
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.ForegroundColor = ConsoleColor.White;
                string helpStr = "^S Send    ^X Cancel    Arrows Move";
                Console.Write(helpStr.PadRight(Console.WindowWidth));
                Console.ResetColor();

                Console.SetCursorPosition(cx - coffset, cy + 1);
                Console.CursorVisible = true;

                var key = Console.ReadKey(true);
                
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.X) break;
                
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.S)
                {
                    statusMsg = "Preparing and sending bug report...";
                    Console.SetCursorPosition(0, Console.WindowHeight - 3);
                    Console.BackgroundColor = ConsoleColor.Gray;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write(statusMsg.PadRight(Console.WindowWidth));
                    Thread.Sleep(1000);

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
}
