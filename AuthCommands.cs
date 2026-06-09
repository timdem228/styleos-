namespace StyleOS
{
    public static class AuthCommands
    {
        public static void Passwd()
        {
            Console.Write($"Changing password for {Program.CurrentUser.Username}.\nNew password: ");
            string p1 = AuthSystem.ReadPassword();
            Console.Write("\nRetype new password: ");
            string p2 = AuthSystem.ReadPassword();
            Console.WriteLine();

            if (p1 == p2)
            {
                AuthSystem.ChangePassword(Program.CurrentUser.Username, p1);
                Console.WriteLine("passwd: password updated successfully");
            }
            else Console.WriteLine("passwd: passwords do not match");
        }

        public static void UserAdd(List<string> args)
        {
            if (!Program.CurrentUser.IsRoot) { Console.WriteLine("useradd: Permission denied."); return; }
            if (args.Count == 0) { Console.WriteLine("Usage: useradd <username>"); return; }
            
            Console.Write("Set password: ");
            string pass = AuthSystem.ReadPassword();
            Console.WriteLine();
            try
            {
                AuthSystem.AddUser(args[0], pass);
                Console.WriteLine($"User '{args[0]}' created successfully.");
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        public static void UserDel(List<string> args)
        {
            if (!Program.CurrentUser.IsRoot) { Console.WriteLine("userdel: Permission denied."); return; }
            if (args.Count == 0) { Console.WriteLine("Usage: userdel <username>"); return; }
            try
            {
                AuthSystem.DeleteUser(args[0]);
                Console.WriteLine($"User '{args[0]}' deleted.");
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }
    }
}
