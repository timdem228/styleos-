namespace StyleOS
{
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
}
