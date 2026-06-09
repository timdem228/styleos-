namespace StyleOS
{
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
                        
                        if (!string.IsNullOrEmpty(term))
                        {
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
                        
                        if (int.TryParse(lineStr, out int lNum))
                        {
                            lNum -= 1;
                            if (lNum >= 0 && lNum < lines.Count)
                            {
                                offset = lNum;
                                cy = 0;
                                cx = 0;
                            }
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
}
