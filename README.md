<p align="center">
  <img src="https://github.com/user-attachments/assets/cc340c4d-2057-41fe-b63c-220832767396" alt="StyleOS Logo" width="600">
</p>

---
> [!WARNING]
> This project is still in development, it is still an open beta, many features have not been added yet  
> It's also a console system, but I'll add the .iso soon.

---

Hey there! This is StyleOS, a project I've been working on for a while now. It's basically a custom shell environment for Windows made with C#. I wanted to make something that looks like those old-school terminals but actually has some modern stuff under the hood like image rendering, a package manager, and now a small Python scripting layer.

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
*   `pacman update` - checks for new versions on GitHub and updates everything
*   `pacman update beta` / `pacman update stable` - grab a build from a specific channel just this once
*   `pacman channel beta|stable` - remember a channel for good, so plain `pacman update` uses it from then on
*   `modules` / `lsmod` - see what's "loaded" under the hood (module name, size, what depends on it)
*   `bugreport` - if the system crashes or acts weird, use this to send me logs
*   `calc` - do some quick math
*   `theme` - change the colors if you don't like the green/blue look

### **Python Modules** (new in 1.1.6)
StyleOS can run small Python scripts as "modules." First time only, set up the bridge:

```
modules install modules
```

That checks your machine for Python and pip and installs the `styleos` library locally (nothing gets touched outside of StyleOS's own folder). After that, any folder with a `setup.module` manifest can be installed and run:

```
module install <path to the module's folder>
module run <name>
module list
module remove <name>
```

Inside a module you just do `import styleos as s` and you get a small API: `s.println()`, `s.user()`, `s.cwd()`, `s.version()`, `s.read_file()`, `s.write_file()`. Graphical libraries (tkinter, PyQt, pygame, and the rest) are blocked automatically when a module runs - StyleOS modules are console-only and can't pop open their own window.

### **The Debug Menu**
If you want to see what the system is doing while it boots up, mash the **F6** key right after you start the .exe. It will open a Debug Menu where you can enable "Verbose" mode (you'll need the root password for this though).

## **Installation**
Just grab the zip from the releases page, unpack it anywhere, and run `StyleOS.exe`. It doesn't need .NET installed separately to run, everything it needs is bundled in.

Hope you like it! If you have ideas for new features, text is to me! admin@timd.site

> [!TIP]
> [my web](https://timd.site)  
> [NOT NOT nikwonder](https://github.com/nikwonder)