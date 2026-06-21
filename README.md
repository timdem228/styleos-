> [!TIP]
> hi guys, output 1.1.6 is being debugged for an indefinite period of time due to the fact that I have no ideas, throw some ideas into issues, and the code will also be placed in one file again  
> bye!


<p align="center">
  <img src="https://github.com/user-attachments/assets/cc340c4d-2057-41fe-b63c-220832767396" alt="StyleOS Logo" width="600">
</p>

---
> [!WARNING]
> This project is still in development, it is still an open beta, many features have not been added yet  
> It's also a console system, but I'll add the .iso soon.

---

Hey there! This is StyleOS, a project I've been working on for a while now. It's basically a custom shell environment for Windows made with C#. I wanted to make something that looks like those old-school terminals but actually has some modern stuff under the hood like image rendering and a package manager.

I'm still fixing stuff here and there, so if you find a bug, please let me know or use the new bugreport tool I just added.

## **Getting Started**

If you just downloaded this and want to get in, here is the default login. I haven't set up a complex setup wizard yet, so just use these:

*   **Login:** `root`
*   **Password:** (Just press **Enter**, there is no password by default)

> **Note:** You can change your password once your in by using the `passwd` command.

## **How to use it**

The system works mostly like a Linux terminal. If your lost, just type `help` to see what you can do.

### **Basic Commands**
*   `ls` or `dir` - see whats in the folder
*   `cd <folder>` - move around
*   `cat <file>` - read a text file
*   `nano <file>` - open the text editer
*   `neofetch` - show off your system specs
*   `clear` - if the screen gets to messy

### **System & Apps**
*   `pacman update` - checks for new versions on GitHub and updates everything
*   `bugreport` - if the system crashes or acts weird, use this to send me logs
*   `calc` - do some quick math
*   `theme` - change the colors if you don't like the green/blue look

### **The Debug Menu**
If you want to see what the system is doing while it boots up, mash the **F6** key right after you start the .exe. It will open a Debug Menu where you can enable "Verbose" mode (you'll need the root password for this though).

## **Installation**
Just grab the zip from the releases page, unpack it anywere, and run `StyleOS.exe`. It **DONT** needs .NET to run, but most Windows PCs have that anyway.

Hope you like it! If you have ideas for new features, text is to me! admin@timd.site

> [!TIP]
> [my web](https://timd.site)  
> [NOT NOT nikwonder](https://github.com/nikwonder)
