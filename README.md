# WinClean & Network Tunning

WinClean & Network Tunning is a standalone Windows utility for running common cleanup, network reset, and system tuning tasks from a simple AMOLED-style desktop UI.

## Standalone EXE

Use `WinCleanNetworkTunning.exe`.

The app is a single Windows executable built with .NET Framework WinForms. It does not need the source code, batch file, or any extra app files beside it. On modern Windows, .NET Framework 4.x is normally already installed. If .NET Framework is old or missing, the app includes a `Get .NET` button that opens Microsoft's official .NET Framework 4.8 download page.

## Features

- Full Cleanup, Network Cleanup, and System Cleanup buttons.
- Separate Tunning tab for permanent performance, telemetry, pagefile, power, and TCP/IP tuning changes.
- Auto-elevates with UAC when administrator access is needed.
- Creates a fresh timestamped log file for every run.
- Shows per-command progress and stores command result details for completed or failed steps.
- Optional startup checkbox to auto-run System Cleanup at user sign-in through an elevated Windows Scheduled Task, avoiding repeated UAC prompts after the one-time setup.

## Important

Some tuning actions make permanent Windows service, registry, power plan, pagefile, and TCP/IP changes. Run the Tunning section only when you want those settings applied.
