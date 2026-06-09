namespace StyleOS
{
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
}
