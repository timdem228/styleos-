namespace StyleOS
{
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
}
