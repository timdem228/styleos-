<p align="center">
  <img src="https://github.com/user-attachments/assets/cc340c4d-2057-41fe-b63c-220832767396" alt="StyleOS Logo" width="600">
</p>

---
> [!WARNING]
> This project is still in development, it is still an open beta, many features have not been added yet  
> It's also a console system, but I'll add the .iso soon.

---

Hey there! This is StyleOS, a project I've been working on for a while now. It's basically a custom shell environment for Windows made with C#. I wanted to make something that looks like those old-school terminals but actually has some modern stuff under the hood like image rendering, a package manager, and a small Python scripting layer.

I'm still fixing stuff here and there, so if you find a bug, please let me know or use the bugreport tool.

## **Getting Started**

If you just downloaded this and want to get in, here is the default login. I haven't set up a complex setup wizard yet, so just use these:

*   **Login:** `root`
*   **Password:** (Just press **Enter**, there is no password by default)

> **Note:** You can change your password once you're in by using the `passwd` command.

## **How to use it**

The system works mostly like a Linux terminal. If you're lost, just type `help` to see what you can do, or `man <command>` for details on any one of them. Pipes, redirects (`|`, `>`, `>>`, `<`) and command chaining (`&&`, `||`, `;`) all work too.

### **Basic Commands**
*   `ls` or `dir` - see whats in the folder
*   `cd <folder>` - move around
*   `cat <file>` - read a text file
*   `nano <file>` - open the text editor
*   `fastfetch` / `neofetch` - show off your system specs
*   `clear` - if the screen gets too messy

### **System & Apps**
*   `pacman update` - checks for new versions on GitHub and updates everything (verifies the release signature first, if one was published)
*   `pacman update beta` / `pacman update stable` - grab a build from a specific channel just this once
*   `pacman channel beta|stable` - remember a channel for good, so plain `pacman update` uses it from then on
*   `whatsnew` / `whatsnew <version>` - show what changed in a version, straight from its GitHub release
*   `modules` / `lsmod` - see what's "loaded" under the hood (module name, size, what depends on it)
*   `bugreport` - if the system crashes or acts weird, use this to send me logs
*   `calc` - do some quick math
*   `theme` - change the colors if you don't like the green/blue look

### **Python Modules**
StyleOS can run small Python scripts as "modules." First time only, set up the bridge:

```
modules install modules
```

That checks your machine for Python and pip and installs the `styleos` library locally (nothing gets touched outside of StyleOS's own folder). If you've got more than one Python version on your machine, you can pin one:

```
modules install modules 3.12
```

After that, any folder with a `setup.module` manifest can be installed and run:

```
module install <path to the module's folder>
module run <name>
module list
module remove <name>
```

Inside a module you just do `import styleos as s` and you get a small API: `s.println()`, `s.user()`, `s.cwd()`, `s.version()`, `s.data_dir()`, `s.read_file()`, `s.write_file()`. Graphical libraries (tkinter, PyQt, pygame, and the rest) are always blocked - StyleOS modules are console-only and can't pop open their own window.

**New in 1.1.7:** a module's `setup.module` can also ask for permissions it needs beyond that - `network` for sockets/http, `process` for spawning other programs, `filesystem` for reaching outside its own data folder. `module install` shows you exactly what's being asked for before installing anything. Every module also gets killed if it runs longer than 60 seconds (or whatever `timeout_seconds` says), so a hung script can't hang the shell. None of this is a full sandbox - it's there to catch accidents and casual misuse, not a deliberately malicious script that's trying to get around it.

### **Signed updates**
If a release has a published signature, `pacman update` checks it before installing anything - a release that doesn't match its signature gets refused outright. Releases without a signature (anything published before this feature) just get a plain warning and install like before, so this doesn't break on older releases.

### **The Debug Menu**
If you want to see what the system is doing while it boots up, mash the **F6** key right after you start the .exe. It will open a Debug Menu where you can enable "Verbose" mode (you'll need the root password for this though).

## **Installation**
Just grab the zip from the releases page, unpack it anywhere, and run `StyleOS.exe`. It doesn't need .NET installed separately to run, everything it needs is bundled in.

Hope you like it! If you have ideas for new features, text is to me! admin@timd.site

> [!TIP]
> [my web](https://timd.site)  
> [NOT NOT nikwonder](https://github.com/nikwonder)
