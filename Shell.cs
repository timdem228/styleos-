namespace StyleOS
{
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

        public static void DrawPrompt()
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
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentTop);
            
            DrawPrompt();
            Console.Write(input); 
            
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
}
