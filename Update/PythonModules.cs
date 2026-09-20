using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace StyleOS
{
    public class ModuleManifest
    {
        public string Name { get; set; }
        public string Version { get; set; } = "0.0.0";
        public string Description { get; set; } = "";
        public string Entry { get; set; } = "main.py";
        public List<string> Requires { get; set; } = new List<string>();
    }

    public class InstalledModule
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Entry { get; set; }
        public string InstalledPath { get; set; }
        public DateTime InstalledAt { get; set; }
    }

    /// <summary>
    /// A Python we can invoke: either a plain executable ("python3", "python3.12" ...) or
    /// the Windows py.exe launcher plus a version selector ("-3.12"). ExtraArg is empty for
    /// the plain case and gets prepended to every argument list for the launcher case.
    /// </summary>
    public class PythonInvocation
    {
        public string Exe;
        public string ExtraArg = "";

        public string Display => string.IsNullOrEmpty(ExtraArg) ? Exe : $"{Exe} {ExtraArg}";
    }

    /// <summary>
    /// Installs and runs small Python "modules" for StyleOS. Every module can
    /// `import styleos as s`, and every graphical toolkit that would pop up a real OS
    /// window is blocked - not by trusting the module to opt in, but because StyleOS sets
    /// PYTHONPATH only for the one process it launches, pointing at our own pylib folder
    /// (which carries a sitecustomize.py that imports styleos automatically). That keeps
    /// the block scoped to StyleOS module runs and never touches the user's own Python
    /// install or any of their other scripts.
    /// </summary>
    public static class PythonModules
    {
        public static readonly string PyLibDir = Path.Combine(Kernel.SysDir, "pylib");
        public static readonly string ModulesDir = Path.Combine(Kernel.SysDir, "modules");
        public static readonly string RegistryFile = Path.Combine(Kernel.SysDir, "modules_installed.json");
        private static readonly string PythonPathFile = Path.Combine(Kernel.SysDir, "python_path.txt");

        // ---- "modules install modules" - one-time bootstrap ----------------

        public static async Task Bootstrap(string version = null)
        {
            Console.WriteLine(version == null ? ":: Checking for Python..." : $":: Checking for Python {version}...");

            var python = await FindPython(version);
            if (python == null)
            {
                if (version != null)
                    Io.Error("modules", $"Python {version} was not found. On Windows, run 'py --list' to see installed versions.");
                else
                    Io.Error("modules", "Python was not found on PATH. Install Python 3 from python.org, then try again.");
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"   found: {python.Display}");
            Console.ResetColor();

            Console.WriteLine(":: Checking for pip...");
            if (!await RunOk(python, "-m pip --version"))
            {
                Io.Error("modules", "pip is not available for this Python install.");
                return;
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("   found: pip");
            Console.ResetColor();

            Directory.CreateDirectory(PyLibDir);
            Directory.CreateDirectory(ModulesDir);

            File.WriteAllText(Path.Combine(PyLibDir, "styleos.py"), StyleOsLibrarySource);
            File.WriteAllText(Path.Combine(PyLibDir, "sitecustomize.py"), SiteCustomizeSource);
            SavePython(python);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($":: The 'styleos' Python library is ready ({PyLibDir}), using {python.Display}.");
            Console.WriteLine("   Modules can now do: import styleos as s");
            Console.ResetColor();
            Console.WriteLine("   Next: put a setup.module file in your module's folder, then run");
            Console.WriteLine("         module install <path to that folder>");

            SystemLogger.Log("PYMOD", $"Bootstrap complete using {python.Display}");
        }

        public static bool IsBootstrapped() =>
            File.Exists(Path.Combine(PyLibDir, "styleos.py")) && File.Exists(PythonPathFile);

        private static void SavePython(PythonInvocation python) =>
            File.WriteAllText(PythonPathFile, python.Exe + "\n" + python.ExtraArg);

        private static PythonInvocation LoadKnownPython()
        {
            if (!File.Exists(PythonPathFile)) return null;
            var lines = File.ReadAllLines(PythonPathFile);
            return new PythonInvocation
            {
                Exe = lines.Length > 0 ? lines[0].Trim() : null,
                ExtraArg = lines.Length > 1 ? lines[1].Trim() : ""
            };
        }

        // ---- "module install <path>" ----------------------------------------

        public static async Task Install(string sourcePath)
        {
            if (!IsBootstrapped())
            {
                Io.Error("module", "run 'modules install modules' first to set up the Python bridge.");
                return;
            }

            string folder = PathUtil.Resolve(sourcePath);
            if (!Directory.Exists(folder)) { Io.Error("module", $"'{sourcePath}': no such directory"); return; }

            string manifestPath = Path.Combine(folder, "setup.module");
            if (!File.Exists(manifestPath))
            {
                Io.Error("module", $"'{sourcePath}' has no setup.module file - see 'man module' for the format.");
                return;
            }

            ModuleManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ModuleManifest>(File.ReadAllText(manifestPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex) { Io.Error("module", $"invalid setup.module: {ex.Message}"); return; }

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Name))
            {
                Io.Error("module", "setup.module must at least specify a \"name\"");
                return;
            }

            string entry = string.IsNullOrWhiteSpace(manifest.Entry) ? "main.py" : manifest.Entry;
            string entryPath = Path.Combine(folder, entry);
            if (!File.Exists(entryPath))
            {
                Io.Error("module", $"entry file '{entry}' not found in {sourcePath}");
                return;
            }

            Console.WriteLine($":: Installing module '{manifest.Name}' v{manifest.Version}...");

            var python = LoadKnownPython();
            foreach (var package in manifest.Requires ?? new List<string>())
            {
                Console.Write($"   dependency {package}: ");

                if (await RunOk(python, $"-m pip show {package}"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("already installed");
                    Console.ResetColor();
                    continue;
                }

                Console.WriteLine("not found, installing...");
                if (!await RunOk(python, $"-m pip install {package}"))
                {
                    Io.Error("module", $"failed to install dependency '{package}', aborting");
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"   dependency {package}: installed");
                Console.ResetColor();
            }

            string destination = Path.Combine(ModulesDir, manifest.Name);
            try
            {
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                CopyDirectory(folder, destination);
            }
            catch (Exception ex) { Io.Error("module", $"could not copy module files: {ex.Message}"); return; }

            var registry = LoadRegistry();
            registry.RemoveAll(m => m.Name == manifest.Name);
            registry.Add(new InstalledModule
            {
                Name = manifest.Name,
                Version = manifest.Version,
                Description = manifest.Description,
                Entry = entry,
                InstalledPath = destination,
                InstalledAt = DateTime.Now
            });
            SaveRegistry(registry);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($":: Module '{manifest.Name}' installed. Run it with: module run {manifest.Name}");
            Console.ResetColor();
            SystemLogger.Log("PYMOD", $"Installed module {manifest.Name} v{manifest.Version}");
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }

        // ---- "module list" / "module remove" / "module run" ------------------

        public static void List()
        {
            var registry = LoadRegistry();
            if (registry.Count == 0) { Console.WriteLine("No modules installed. See 'man module'."); return; }

            Console.WriteLine($"{"Name",-18}{"Version",-10}Description");
            foreach (var m in registry)
                Console.WriteLine($"{m.Name,-18}{m.Version,-10}{m.Description}");
        }

        public static void Remove(string name)
        {
            var registry = LoadRegistry();
            var found = registry.FirstOrDefault(m => m.Name == name);
            if (found == null) { Io.Error("module", $"'{name}' is not installed"); return; }

            try { if (Directory.Exists(found.InstalledPath)) Directory.Delete(found.InstalledPath, true); }
            catch (Exception ex) { Io.Error("module", ex.Message); return; }

            registry.Remove(found);
            SaveRegistry(registry);
            Console.WriteLine($":: Module '{name}' removed.");
        }

        public static async Task Run(string name, List<string> args)
        {
            if (!IsBootstrapped()) { Io.Error("module", "run 'modules install modules' first."); return; }

            var registry = LoadRegistry();
            var found = registry.FirstOrDefault(m => m.Name == name);
            if (found == null) { Io.Error("module", $"'{name}' is not installed. Try 'module list'."); return; }

            var python = LoadKnownPython();
            string entryPath = Path.Combine(found.InstalledPath, found.Entry);
            if (!File.Exists(entryPath)) { Io.Error("module", $"entry file missing: {found.Entry}"); return; }

            var psi = new ProcessStartInfo
            {
                FileName = python.Exe,
                WorkingDirectory = found.InstalledPath,
                UseShellExecute = false
            };
            if (!string.IsNullOrEmpty(python.ExtraArg)) psi.ArgumentList.Add(python.ExtraArg);
            psi.ArgumentList.Add(entryPath);
            foreach (var a in args) psi.ArgumentList.Add(a);

            // Scoped to this one process only - PYTHONPATH here never touches the user's
            // own Python setup or anything they run outside StyleOS.
            string existingPythonPath = Environment.GetEnvironmentVariable("PYTHONPATH");
            psi.EnvironmentVariables["PYTHONPATH"] = string.IsNullOrEmpty(existingPythonPath)
                ? PyLibDir
                : PyLibDir + Path.PathSeparator + existingPythonPath;

            psi.EnvironmentVariables["STYLEOS_USER"] = Kernel.CurrentUser?.Username ?? "";
            psi.EnvironmentVariables["STYLEOS_CWD"] = Kernel.CurrentDirectory;
            psi.EnvironmentVariables["STYLEOS_VERSION"] = Kernel.Version;

            try
            {
                using var process = Process.Start(psi);
                await process.WaitForExitAsync();
                ShellEnv.ExitCode = process.ExitCode;
            }
            catch (Exception ex) { Io.Error("module", $"failed to launch: {ex.Message}"); }
        }

        // ---- helpers -----------------------------------------------------------

        private static List<InstalledModule> LoadRegistry()
        {
            try
            {
                if (!File.Exists(RegistryFile)) return new List<InstalledModule>();
                return JsonSerializer.Deserialize<List<InstalledModule>>(File.ReadAllText(RegistryFile)) ?? new List<InstalledModule>();
            }
            catch { return new List<InstalledModule>(); }
        }

        private static void SaveRegistry(List<InstalledModule> registry)
        {
            try { File.WriteAllText(RegistryFile, JsonSerializer.Serialize(registry, new JsonSerializerOptions { WriteIndented = true })); }
            catch (Exception ex) { SystemLogger.Log("PYMOD", "Cannot save registry: " + ex.Message); }
        }

        /// <summary>
        /// No version given: tries the usual candidates in order and uses the first that runs.
        /// Version given (e.g. "3.12"): on Windows this asks the py.exe launcher for exactly
        /// that install ("py -3.12"), which is the standard way to pick among several Python
        /// versions on one machine; on Linux/macOS it looks for the versioned binary name
        /// ("python3.12"). An explicit version that isn't found is reported, not silently
        /// swapped for a different install.
        /// </summary>
        private static async Task<PythonInvocation> FindPython(string version)
        {
            if (!string.IsNullOrEmpty(version))
            {
                var candidate = Kernel.IsWindows
                    ? new PythonInvocation { Exe = "py", ExtraArg = $"-{version}" }
                    : new PythonInvocation { Exe = $"python{version}" };

                return await RunOk(candidate, "--version") ? candidate : null;
            }

            string[] names = Kernel.IsWindows ? new[] { "py", "python", "python3" } : new[] { "python3", "python" };

            foreach (var name in names)
            {
                var candidate = new PythonInvocation { Exe = name };
                if (await RunOk(candidate, "--version")) return candidate;
            }
            return null;
        }

        private static async Task<bool> RunOk(PythonInvocation python, string arguments)
        {
            string fullArgs = string.IsNullOrEmpty(python.ExtraArg) ? arguments : $"{python.ExtraArg} {arguments}";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = python.Exe,
                    Arguments = fullArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch { return false; }
        }

        // ---- embedded library source --------------------------------------------

        private const string StyleOsLibrarySource = @"'''StyleOS scripting bridge.

Import this from a StyleOS module:

    import styleos as s
    s.println('hello from a StyleOS module')

Any library that opens its own OS window (tkinter, PyQt, pygame, wx, kivy,
turtle, curses, and friends) is blocked the moment this file is imported -
StyleOS modules run inside the console only, they cannot pop up a window.
'''

import sys
import os
import builtins
import importlib.abc

__version__ = '1.0.0'

_BLOCKED = {
    'tkinter', 'Tkinter', '_tkinter',
    'PyQt5', 'PyQt6', 'PySide2', 'PySide6',
    'wx',
    'kivy',
    'pygame',
    'pyglet',
    'arcade',
    'PySimpleGUI',
    'turtle',
    'curses', '_curses',
    'pywebview', 'webview',
    'win32gui', 'win32ui',
}

_MESSAGE = (
    ""StyleOS: '{0}' opens its own window and cannot run inside StyleOS. ""
    'StyleOS modules are console-only.'
)


class _GuiBlocker(importlib.abc.MetaPathFinder):
    def find_spec(self, fullname, path, target=None):
        root = fullname.split('.')[0]
        if root in _BLOCKED:
            raise ImportError(_MESSAGE.format(root))
        return None


def _install_guard():
    if not any(isinstance(finder, _GuiBlocker) for finder in sys.meta_path):
        sys.meta_path.insert(0, _GuiBlocker())
    # matplotlib is not GUI-only, so instead of blocking it, force a headless backend.
    os.environ.setdefault('MPLBACKEND', 'Agg')


_install_guard()


def println(*values, sep=' '):
    '''Print a line, exactly like Python's own print().'''
    builtins.print(*values, sep=sep)


def user():
    '''The StyleOS username that launched this module.'''
    return os.environ.get('STYLEOS_USER', 'unknown')


def cwd():
    '''The StyleOS working directory this module was launched from.'''
    return os.environ.get('STYLEOS_CWD', os.getcwd())


def version():
    '''The StyleOS version this module is running under.'''
    return os.environ.get('STYLEOS_VERSION', 'unknown')


def read_file(path):
    with open(path, 'r', encoding='utf-8') as handle:
        return handle.read()


def write_file(path, content):
    with open(path, 'w', encoding='utf-8') as handle:
        handle.write(content)


def input_line(prompt=''):
    return builtins.input(prompt)
";

        private const string SiteCustomizeSource = @"'''Auto-loaded by Python whenever StyleOS launches a module (StyleOS puts this
folder on PYTHONPATH just for that one process). This is what makes the GUI
block apply even to a module that never imports styleos itself - do not
remove it from a module's own code.
'''
import styleos  # noqa: F401
";
    }
}
