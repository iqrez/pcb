# ReziRemapLite

ReziRemapLite is a clean-room Windows utility that maps mouse and keyboard input to an emulated Xbox 360 controller through the [ViGEm Bus Driver](https://vigem.org/). The tool runs from the system tray and is designed for shooters that expect controller input while still using mouse and keyboard precision.

## Requirements

- Windows 10 or later
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/)
- [ViGEm Bus Driver](https://vigem.org/) installed system-wide

## Features

- Middle Mouse Button (MMB) release toggles between Controller mode and Mouse+Keyboard passthrough.
- Controller mode routes mouse/keyboard to the virtual Xbox 360 controller and suppresses legacy mouse window messages so games stay in controller UI.
- Mouse movement drives the right stick via carefully tuned curves with deadzone, anti-deadzone, optional smoothing, and sensitivity controls.
- Keyboard `WASD` drives the left stick with normalized diagonals.
- Dedicated bindings for common shooter actions including triggers, bumpers, face buttons, D-Pad, thumb clicks, and Start/Back.
- Scroll wheel pulses the `Y` button for 50 ms to replicate weapon swaps.
- Safety quit chord: hold `1` + `0` to exit immediately.

## Default bindings

| Input | Xbox 360 Mapping |
|-------|------------------|
| Mouse Move | Right Stick |
| Left Mouse Button | Right Trigger |
| Right Mouse Button | Left Trigger |
| Middle Mouse Button (release) | Toggle Controller/Mouse mode |
| Mouse Button 4 | LB |
| Mouse Button 5 | RB |
| Mouse Wheel (any direction) | Tap Y for 50 ms |
| Esc | Start |
| Tab | Back |
| Space | A |
| L or Shift (either) | B |
| F | X |
| R | Y (held) |
| 1 | D-Pad Down |
| 2 | D-Pad Left |
| 3 | D-Pad Right |
| Q | Left Thumb (L3) |
| E | Right Thumb (R3) |
| WASD | Left Stick |
| 1 + 0 (held) | Exit app |

## Build, run, and publish

```powershell
# Restore, build, and run
scripts/run.ps1

# Publish a self-contained single file (win-x64)
dotnet publish ./src/ReziRemapLite/ReziRemapLite.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

The published executable is created under `src/ReziRemapLite/bin/Release/net8.0-windows10.0.17763.0/win-x64/publish/`.

## Tray controls

- **Controller Mode: On/Off** shows and toggles the current routing state.
- **Exit** terminates the application immediately.

## Notes

- The app shows a balloon notification once ViGEm successfully connects.
- When Controller mode is off, all inputs pass through normally and the virtual controller returns to a neutral state.
- No legacy assets or logging files are created; the project is a single WinForms message loop with a tray icon only.
