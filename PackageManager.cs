namespace StyleOS
{
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

                Console.WriteLine($"Resolving {pkg} ... ");
                string repoUrl = $"http://api.timd.site/sos/r/{pkg}.txt";
                
                try
                {
                    using HttpClient client = NetworkUtils.GetBypassedHttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", "StyleOS-Client");
                    HttpResponseMessage metaResponse = client.GetAsync(repoUrl).GetAwaiter().GetResult();
                    
                    if (!metaResponse.IsSuccessStatusCode)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[PKG] Error: Package '{pkg}' not found.");
                        Console.ResetColor();
                        return;
                    }

                    string downloadUrl = metaResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult().Trim();
                    
                    if (string.IsNullOrEmpty(downloadUrl) || (!downloadUrl.StartsWith("http://") && !downloadUrl.StartsWith("https://")))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[PKG] Error: Invalid repository metadata.");
                        Console.ResetColor();
                        return;
                    }

                    Console.WriteLine($"Downloading {pkg} from repository...");
                    
                    byte[] fileBytes = client.GetByteArrayAsync(downloadUrl).GetAwaiter().GetResult();
                    string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                    
                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = $"{pkg}.bin";
                    }

                    string destPath = Path.Combine(Program.CurrentDirectory, fileName);
                    File.WriteAllBytes(destPath, fileBytes);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[PKG] Successfully installed {fileName}");
                    Console.ResetColor();

                    installed.Add(pkg);
                    File.WriteAllText(Program.PkgFile, JsonSerializer.Serialize(installed));
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[PKG] Error: {ex.Message}");
                    Console.ResetColor();
                }
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
}
