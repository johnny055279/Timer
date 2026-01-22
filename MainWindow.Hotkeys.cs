using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Timer;

public partial class MainWindow
{
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
        var parts = new List<string>();
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
            _counterService.Increase();
            UpdateCounterDisplay();
            SaveSettings();
            e.Handled = true;
            return;
        }

        if (MatchesHotkey(_decreaseHotkey, key, modifiers))
        {
            _counterService.Decrease();
            UpdateCounterDisplay();
            SaveSettings();
            e.Handled = true;
            return;
        }

        if (MatchesHotkey(_resetHotkey, key, modifiers))
        {
            _counterService.Reset();
            UpdateCounterDisplay();
            SaveSettings();
            e.Handled = true;
        }
    }
}
