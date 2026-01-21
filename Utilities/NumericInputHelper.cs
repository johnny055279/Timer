using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Timer.Utilities;

public static class NumericInputHelper
{
    public static void HandlePreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsDigitsOnly(e.Text);
    }

    public static void HandlePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    public static void HandlePasting(object sender, DataObjectPastingEventArgs e)
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

    public static bool IsDigitsOnly(string text)
    {
        return text.All(char.IsDigit);
    }
}
