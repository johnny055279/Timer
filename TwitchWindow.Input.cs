using System.Windows.Input;
using Timer.Utilities;

namespace Timer;

public partial class TwitchWindow
{
    private static bool TryGetMinutes(string? text, out int minutes)
    {
        if (!int.TryParse(text, out minutes))
        {
            return false;
        }

        if (minutes < MinMinutes)
        {
            minutes = MinMinutes;
            return false;
        }

        if (minutes > MaxMinutes)
        {
            minutes = MaxMinutes;
            return false;
        }

        return true;
    }

    private static bool TryGetCounterAmount(string? text, out int amount)
    {
        if (!int.TryParse(text, out amount))
        {
            return false;
        }

        if (amount < 1)
        {
            amount = 1;
            return false;
        }

        return true;
    }

    private static bool TryGetBitsAmount(string? text, out int amount)
    {
        if (!int.TryParse(text, out amount))
        {
            return false;
        }

        if (amount < 1)
        {
            amount = 1;
            return false;
        }

        return true;
    }

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        NumericInputHelper.HandlePreviewTextInput(sender, e);
    }

    private void NumericOnly_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        NumericInputHelper.HandlePreviewKeyDown(sender, e);
    }
}
