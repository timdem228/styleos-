namespace StyleOS
{
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
}
