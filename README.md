# Timer (WPF)

Two-panel timer app built with .NET 10 WPF.

## Features
- Editable countdown title (click to edit).
- Countdown timer with adjustable minutes step, reset, and pause.
- Custom beep selection and playback.
- Death counter with adjustable step, reset, and hotkeys.

## How to Run
```powershell
dotnet run --project Timer.csproj
```

## Controls
- Countdown: use "- mins" / "+ mins" to change the timer by the step value.
- Step minutes: enter a positive number; a warning appears if empty or invalid.
- Pause/Resume stops or continues the countdown.
- Beep: choose from dropdown or Browse to select a file, then Play.
- Death counter: change the step, use + / - / Reset buttons.
- Hotkeys: click a hotkey field, press your key combo to set.
  - Default: Increase = Num+, Decrease = Num-, Reset = 0
  - Hotkeys only work when no input or dropdown has focus.

## Notes
- Beep audio files live in `beeps/` and are copied to the output on build.
