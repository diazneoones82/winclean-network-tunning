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
- Close or minimize to keep the app running in the system tray.
- Tray menu includes Open, Run Full Cleanup, Run Network Cleanup, Run System Cleanup, Run Tunning, and Exit.
- Custom orange AMOLED cleanup/network icon is embedded in the app and used for the window, taskbar, and tray.
- Polished tray menu supports Windows light and dark app themes with rounded orange hover states.
- Tray restore and background runs use handle-safe UI updates to avoid WinForms handle timing errors.
- Modern borderless app chrome with rounded window corners, subtle orange borders, and polished custom title controls.
- Tray menu uses compact rounded command rows with a soft orange accent rail.
- Custom dark/orange segmented tabs, progress bar, and command list headers avoid stock WinForms white highlight blocks.
- Window and button painting explicitly clears AMOLED backgrounds to prevent white corner artifacts.

## Important

Some tuning actions make permanent Windows service, registry, power plan, pagefile, and TCP/IP changes. Run the Tunning section only when you want those settings applied.
