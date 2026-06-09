namespace StyleOS
{
    public static class ShitShellEnv
    {
        public static Dictionary<string, string> Variables = new Dictionary<string, string>();
        public static bool IsRunning = false;

        public static void Init()
        {
            if (Variables.Count == 0)
            {
                Variables["name"] = "ShitShell";
                Variables["version"] = "1.0";
                Variables["kernel"] = "base-1.0";
                Variables["cli_logo"] = 
@"+========================+
| ___ _  _ ___ _____     |
|/ __| || |_ _|_   _|    |
|\__ \ __ || |  | |      |
||___/_||_|___|_|_| _    |
|/ __| || | __| |  | |   |
|\__ \ __ | _|| |__| |__ |
||___/_||_|___|____|____||
+========================+";
            }
        }

        public static void Run()
        {
            Init();
            IsRunning = true;
            Console.WriteLine("Welcome to ShitShell!");

            while (IsRunning)
            {
                Console.Write("> ");
                string input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input)) continue;
                
                string output = ExecuteCommand(input);
                if (!string.IsNullOrEmpty(output))
                {
                    Console.WriteLine(output);
                }
            }
        }

        public static string ExecuteCommand(string input)
        {
            var parts = input.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";
            string cmd = parts[0].ToLower();
            string args = parts.Length > 1 ? parts[1] : "";

            switch (cmd)
            {
                case "help":
                    return @"        Built-in commands:  
    calc        Calculate an expression        |    help        Show this message
    echo        Echo a message                 |    read        Read a file
    exec        Execute a SS script            |    restart     Restart ShitShell
    exit        Exit ShitShell                 |    setvar      Set a variable
    getvar      Get a variable                 |    

        External programs:
    tedit: Text editor
    sysinfo: System information";
                case "exit":
                    IsRunning = false;
                    return "Exiting ShitShell...";
                case "restart":
                    return "Restarting ShitShell...\nWelcome to ShitShell!";
                case "echo":
                    return args;
                case "calc":
                    try { return new System.Data.DataTable().Compute(args, "").ToString(); } catch { return "Error calculating."; }
                case "getvar":
                    return Variables.ContainsKey(args) ? Variables[args] : $"Variable '{args}' not found.";
                case "setvar":
                    var vparts = args.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (vparts.Length == 2) { Variables[vparts[0]] = vparts[1]; return $"Set variable {vparts[0]} to {vparts[1]}"; }
                    return "Usage: setvar <name> <value>";
                case "read":
                    string path = CommandProcessor.ResolvePath(args);
                    if (File.Exists(path)) return File.ReadAllText(path);
                    return "File not found.";
                case "exec":
                    return ExecuteShish(args);
                case "sysinfo":
                    return $"ShitShell {Variables["version"]}\nKernel: {Variables["kernel"]}\n\n{Variables["cli_logo"]}";
                case "tedit":
                    TEdit(args);
                    return "";
                default:
                    string suggested = SpellCheck(cmd);
                    if (suggested != null)
                    {
                        return $"Unknown command: '{cmd}'. Did you mean '{suggested}'?";
                    }
                    return $"Unknown command: '{cmd}'";
            }
        }

        private static string ExecuteShish(string file)
        {
            string path = CommandProcessor.ResolvePath(file);
            if (!File.Exists(path)) return "Script not found.";
            StringBuilder sb = new StringBuilder();
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && parts[0] == "echo")
                {
                    sb.AppendLine(parts.Length > 1 ? parts[1] : "");
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static void TEdit(string title)
        {
            if (string.IsNullOrEmpty(title)) title = "New file";
            Console.Clear();
            Console.WriteLine($"TEdit | {title} | Use 't.!exit' to exit");
            List<string> lines = new List<string>();
            while (true)
            {
                string line = Console.ReadLine();
                if (line == "t.!exit") break;
                lines.Add(line);
            }
            Console.Write("\nSave file? (y/n) ");
            if (Console.ReadLine()?.ToLower() == "y")
            {
                string path = CommandProcessor.ResolvePath(title);
                File.WriteAllLines(path, lines);
            }
            Console.Clear();
        }

        private static string SpellCheck(string input)
        {
            string[] validCommands = { "calc", "echo", "exec", "exit", "getvar", "help", "read", "restart", "setvar", "sysinfo", "tedit" };
            foreach (var cmd in validCommands)
            {
                if (Math.Abs(cmd.Length - input.Length) <= 1)
                {
                    int diffs = 0;
                    int len = Math.Min(cmd.Length, input.Length);
                    for (int i = 0; i < len; i++) if (cmd[i] != input[i]) diffs++;
                    if (diffs <= 1) return cmd;
                }
            }
            return null;
        }
    }
}
