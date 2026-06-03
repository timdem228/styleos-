<p align="center">
  <img src="https://github.com/user-attachments/assets/2a2a58ce-2334-40b7-9002-0375937b2b4b" alt="StyleOS Logo" width="600">
</p>

---
> [!WARNING]
> This project is still in development  
> YOU WANT TO HAVE PYTHON ON YOUR SYSTEM
---

A lightweight C# runner for Python scripts that enforces a `.style` file extension and a mandatory program header.

## How it works

The C# engine validates the file, checks for the required header on the first line, logs the execution status, and runs the rest of the code via the local Python interpreter. 

It fully supports UTF-8, so Cyrillic characters display correctly in the console.

### Code Example (`app.style`)

```python
program MyApp
import random

items = ["one", "two", "three"]
print(f"Selected: {random.choice(items)}")
```

> [!TIP]
> [my web](https://timd.site)  
> [bebebebebbeebbebebbebebe](https://github.com/nikwonder)
