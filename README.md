# Timer (WPF)

Two-panel timer app (Countdown + Counter) built with .NET 10 WPF, plus a separate Twitch settings window.

## Features
- Editable countdown title (click to edit).
- Countdown timer with adjustable minutes step, reset, and pause.
- Custom beep selection and playback.
- Counter with adjustable step, reset, and hotkeys.
- Twitch settings live in a separate window (top-right "Twitch (設定)").
- Startup update check (opens GitHub release page if newer version is available).

## How to Run
```powershell
dotnet run --project Timer.csproj
```

## Publish (Framework-dependent, multi-file)
Smaller download size, but users must install the .NET Desktop Runtime.
```powershell
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=false
```

Output:
`bin/Release/net10.0-windows/win-x64/publish/`

Runtime download:
https://dotnet.microsoft.com/download/dotnet/10.0

## Controls
- Countdown: use "- mins" / "+ mins" to change the timer by the step value.
- Step minutes: enter a number in the allowed range (warning appears if invalid).
- Pause/Resume stops or continues the countdown.
- Beep: choose from the built-in list or Browse to select a file, then Play.
- Counter: change the step, use + / - / Reset buttons.
- Hotkeys: click a hotkey field, press your key combo to set.
  - Default: Increase = Num+, Decrease = Num-, Reset = 0
  - Hotkeys only work when no input or dropdown has focus.

## Twitch Integration (Device Code)
- Set `TwitchClientId` in `MainWindow.xaml.cs` before building.
- Click "Twitch (設定)" to open the Twitch settings window.
- Click "Connect Twitch" and complete verification in your browser.
- Click "Load rewards" to list channel point rewards and map them to minutes.
- Use "Start poll" to create an Agree/Disagree poll and apply the configured minutes.
- Pick whether Agree means add or subtract using the poll action dropdown.
- Tokens are stored in Windows Credential Manager under `JohnnyTimerEventSubWPF.TwitchToken`.

## Notes
- The default beep is embedded in the app so it works without external files.
- Browse uses external audio files and does not affect the embedded beep list.
- Update check uses GitHub releases; it opens the release page but does not auto-install.
