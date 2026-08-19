---
title: Frequently asked questions (FAQ)
description: Common Unity Assets Patcher questions and solutions.
sidebar:
  order: 2
---

## Why do I see an “old Windows console” warning?

The interactive UI needs the character rendering and layout support provided by a modern terminal. Borders, text, or layout may render incorrectly in the legacy Windows Console Host.

Install and use [Windows Terminal](https://aka.ms/terminal):

```powershell
winget install --id Microsoft.WindowsTerminal -e
```

In Windows Terminal, open **Settings → Startup**, set **Default terminal application** to **Windows Terminal**, and restart the application in a new terminal window. Existing console windows do not migrate after changing this setting.
