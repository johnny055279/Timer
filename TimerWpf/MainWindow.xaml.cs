using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace TimerWpf;

public partial class MainWindow : Window
{
    private sealed record BeepOption(string DisplayName, string FullPath);
    private sealed record Hotkey(Key Key, ModifierKeys Modifiers);

    private readonly DispatcherTimer _timer;
    private readonly MediaPlayer _player = new();
    private readonly string _beepsDirectory;
    private TimeSpan _remaining = TimeSpan.Zero;
    private bool _isPaused;
    private bool _canPlay;
    private int _deathCount;
    private Hotkey _increaseHotkey = new(Key.Add, ModifierKeys.None);
    private Hotkey _decreaseHotkey = new(Key.Subtract, ModifierKeys.None);
    private Hotkey _resetHotkey = new(Key.D0, ModifierKeys.None);

    public MainWindow()
    {
        InitializeComponent();
        _beepsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "beeps");
        LoadBeeps();
        UpdateTimeDisplay();
        UpdateDeathButtons();
        UpdateHotkeyTextBoxes();
        UpdateStepWarnings();

        DataObject.AddPastingHandler(StepMinutesTextBox, NumericOnly_Pasting);
        DataObject.AddPastingHandler(DeathStepTextBox, NumericOnly_Pasting);

        InputManager.Current.PreProcessInput += OnPreProcessInput;
        Closed += OnWindowClosed;
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Window_PreviewMouseDown), true);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    private void LoadBeeps()
    {
        BeepComboBox.DisplayMemberPath = nameof(BeepOption.DisplayName);
        if (!Directory.Exists(_beepsDirectory))
        {
            BeepComboBox.ItemsSource = Array.Empty<BeepOption>();
            return;
        }

        var files = Directory.EnumerateFiles(_beepsDirectory)
            .Where(file => file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                           || file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            .Select(path => new BeepOption(Path.GetFileName(path) ?? path, path))
            .ToList();

        BeepComboBox.ItemsSource = files;
        if (files.Count > 0)
        {
            BeepComboBox.SelectedIndex = 0;
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isPaused || _remaining <= TimeSpan.Zero)
        {
            return;
        }

        _remaining = _remaining.Subtract(TimeSpan.FromSeconds(1));
        if (_remaining <= TimeSpan.Zero)
        {
            _remaining = TimeSpan.Zero;
            UpdateTimeDisplay();
            if (_canPlay)
            {
                PlaySelectedBeep();
            }
            _canPlay = false;
            return;
        }

        UpdateTimeDisplay();
    }

    private void UpdateTimeDisplay()
    {
        TimeDisplayTextBlock.Text = $"{(int)_remaining.TotalHours:00}:{_remaining.Minutes:00}:{_remaining.Seconds:00}";
    }

    private int GetStepMinutes()
    {
        if (int.TryParse(StepMinutesTextBox.Text, out var minutes) && minutes > 0)
        {
            return minutes;
        }

        return 5;
    }

    private int GetDeathStep()
    {
        if (int.TryParse(DeathStepTextBox.Text, out var step) && step > 0)
        {
            return step;
        }

        return 1;
    }

    private void AdjustTime(int minutes)
    {
        _remaining = _remaining.Add(TimeSpan.FromMinutes(minutes));
        if (_remaining < TimeSpan.Zero)
        {
            _remaining = TimeSpan.Zero;
        }

        _canPlay = _remaining > TimeSpan.Zero;
        UpdateTimeDisplay();
    }

    private void IncreaseTime_Click(object sender, RoutedEventArgs e)
    {
        AdjustTime(GetStepMinutes());
    }

    private void DecreaseTime_Click(object sender, RoutedEventArgs e)
    {
        AdjustTime(-GetStepMinutes());
    }

    private void ResetCountdown_Click(object sender, RoutedEventArgs e)
    {
        _remaining = TimeSpan.Zero;
        _canPlay = false;
        UpdateTimeDisplay();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        PauseButton.Content = _isPaused ? "Resume" : "Pause";
    }

    private void IncreaseDeath_Click(object sender, RoutedEventArgs e)
    {
        _deathCount += GetDeathStep();
        UpdateDeathCountDisplay();
    }

    private void DecreaseDeath_Click(object sender, RoutedEventArgs e)
    {
        _deathCount = Math.Max(0, _deathCount - GetDeathStep());
        UpdateDeathCountDisplay();
    }

    private void ResetDeath_Click(object sender, RoutedEventArgs e)
    {
        _deathCount = 0;
        UpdateDeathCountDisplay();
    }

    private void UpdateDeathCountDisplay()
    {
        DeathCountTextBlock.Text = _deathCount.ToString();
    }

    private void DeathStepTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateDeathButtons();
        UpdateStepWarnings();
    }

    private void StepMinutesTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateStepWarnings();
    }

    private void UpdateDeathButtons()
    {
        var step = GetDeathStep();
        IncreaseDeathButton.Content = $"+{step}";
        DecreaseDeathButton.Content = $"-{step}";
    }

    private void UpdateStepWarnings()
    {
        if (StepMinutesWarningTextBlock is null || DeathStepWarningTextBlock is null)
        {
            return;
        }

        StepMinutesWarningTextBlock.Visibility = IsPositiveInt(StepMinutesTextBox.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
        DeathStepWarningTextBlock.Visibility = IsPositiveInt(DeathStepTextBox.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static bool IsPositiveInt(string text)
    {
        return int.TryParse(text, out var value) && value > 0;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        TryHandleHotkey(e);
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var key = NormalizeKey(e);
        if (IsModifierKey(key))
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var hotkey = new Hotkey(key, modifiers);
        switch (textBox.Tag as string)
        {
            case "Increase":
                _increaseHotkey = hotkey;
                break;
            case "Decrease":
                _decreaseHotkey = hotkey;
                break;
            case "Reset":
                _resetHotkey = hotkey;
                break;
        }

        UpdateHotkeyTextBoxes();
        e.Handled = true;
    }

    private void UpdateHotkeyTextBoxes()
    {
        IncreaseHotkeyTextBox.Text = FormatHotkey(_increaseHotkey);
        DecreaseHotkeyTextBox.Text = FormatHotkey(_decreaseHotkey);
        ResetHotkeyTextBox.Text = FormatHotkey(_resetHotkey);
    }

    private static string FormatHotkey(Hotkey hotkey)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (hotkey.Modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (hotkey.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (hotkey.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (hotkey.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(FormatKey(hotkey.Key, hotkey.Modifiers));
        return string.Join("+", parts);
    }

    private static string FormatKey(Key key, ModifierKeys modifiers)
    {
        return key switch
        {
            Key.OemPlus => modifiers.HasFlag(ModifierKeys.Shift) ? "+" : "=",
            Key.OemMinus => "-",
            Key.Add => "Num+",
            Key.Subtract => "Num-",
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            _ => key.ToString()
        };
    }

    private static bool MatchesHotkey(Hotkey hotkey, Key key, ModifierKeys modifiers)
    {
        return hotkey.Key == key && hotkey.Modifiers == modifiers;
    }

    private static Key NormalizeKey(KeyEventArgs e)
    {
        return e.Key == Key.System ? e.SystemKey : e.Key;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftShift or Key.RightShift
            or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin;
    }

    private static bool IsEditableFocused()
    {
        return Keyboard.FocusedElement is TextBoxBase or ComboBox;
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInsideEditable(source))
        {
            return;
        }

        if (BeepComboBox.IsDropDownOpen)
        {
            BeepComboBox.IsDropDownOpen = false;
        }

        Keyboard.ClearFocus();
        Keyboard.Focus(FocusSink);
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (!IsActive)
        {
            return;
        }

        if (e.StagingItem.Input is not KeyEventArgs keyEventArgs)
        {
            return;
        }

        if (keyEventArgs.RoutedEvent != Keyboard.KeyDownEvent
            && keyEventArgs.RoutedEvent != Keyboard.PreviewKeyDownEvent)
        {
            return;
        }

        if (keyEventArgs.Handled)
        {
            return;
        }

        TryHandleHotkey(keyEventArgs);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        InputManager.Current.PreProcessInput -= OnPreProcessInput;
        Closed -= OnWindowClosed;
    }

    private void TryHandleHotkey(KeyEventArgs e)
    {
        if (IsEditableFocused())
        {
            return;
        }

        var key = NormalizeKey(e);
        var modifiers = Keyboard.Modifiers;
        if (MatchesHotkey(_increaseHotkey, key, modifiers))
        {
            _deathCount += GetDeathStep();
            UpdateDeathCountDisplay();
            e.Handled = true;
            return;
        }

        if (MatchesHotkey(_decreaseHotkey, key, modifiers))
        {
            _deathCount = Math.Max(0, _deathCount - GetDeathStep());
            UpdateDeathCountDisplay();
            e.Handled = true;
            return;
        }

        if (MatchesHotkey(_resetHotkey, key, modifiers))
        {
            _deathCount = 0;
            UpdateDeathCountDisplay();
            e.Handled = true;
        }
    }

    private static bool IsInsideEditable(DependencyObject source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is TextBoxBase or ComboBox)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsDigitsOnly(e.Text);
    }

    private void NumericOnly_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private void NumericOnly_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var text = (string?)e.DataObject.GetData(typeof(string));
        if (string.IsNullOrWhiteSpace(text) || !IsDigitsOnly(text))
        {
            e.CancelCommand();
        }
    }

    private static bool IsDigitsOnly(string text)
    {
        return text.All(char.IsDigit);
    }

    private void BeepComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        StopPlayback();
    }

    private void BeepComboBox_DropDownClosed(object sender, EventArgs e)
    {
        Keyboard.ClearFocus();
    }

    private void PlayBeep_Click(object sender, RoutedEventArgs e)
    {
        PlaySelectedBeep();
    }

    private void BrowseBeep_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Audio Files (*.mp3;*.wav)|*.mp3;*.wav|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var selection = new BeepOption(Path.GetFileName(dialog.FileName), dialog.FileName);
        var items = BeepComboBox.ItemsSource as System.Collections.Generic.IEnumerable<BeepOption>;
        var list = items?.ToList() ?? new System.Collections.Generic.List<BeepOption>();
        if (list.All(item => !string.Equals(item.FullPath, selection.FullPath, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(selection);
        }

        BeepComboBox.ItemsSource = list;
        BeepComboBox.SelectedItem = list.FirstOrDefault(item =>
            string.Equals(item.FullPath, selection.FullPath, StringComparison.OrdinalIgnoreCase));
        PlaySelectedBeep();
    }

    private void PlaySelectedBeep()
    {
        var selectedPath = GetSelectedBeepPath();
        if (string.IsNullOrWhiteSpace(selectedPath) || !File.Exists(selectedPath))
        {
            return;
        }

        _player.Stop();
        _player.Open(new Uri(selectedPath, UriKind.Absolute));
        _player.Play();
    }

    private void StopPlayback()
    {
        _player.Stop();
    }

    private string? GetSelectedBeepPath()
    {
        return BeepComboBox.SelectedItem is BeepOption option ? option.FullPath : null;
    }
}
