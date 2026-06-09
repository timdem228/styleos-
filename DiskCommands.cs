namespace StyleOS
{
    public static class DiskCommands
    {
        public static void Df()
        {
            Console.WriteLine($"{"Filesystem",-15} {"1K-blocks",-15} {"Used",-15} {"Available",-15} {"Use%",-5} {"Mounted on"}");
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    long total = drive.TotalSize / 1024;
                    long free = drive.TotalFreeSpace / 1024;
                    long used = total - free;
                    double percent = (double)used / total * 100;
                    
                    Console.WriteLine($"{drive.Name,-15} {total,-15} {used,-15} {free,-15} {Math.Round(percent)}%{"",-4} {drive.RootDirectory}");
                }
            }
        }

        public static void Drives() => Df();

        public static void Mount()
        {
            Console.WriteLine("sysfs on /sys type sysfs (rw,nosuid,nodev,noexec,relatime)");
            Console.WriteLine("proc on /proc type proc (rw,nosuid,nodev,noexec,relatime)");
            foreach(var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                Console.WriteLine($"{drive.RootDirectory} on {drive.Name} type {drive.DriveFormat} (rw,relatime)");
            }
        }
    }
}
