using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Timer.Utilities;

namespace Timer;

public partial class MainWindow
{
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
        NumericInputHelper.HandlePreviewTextInput(sender, e);
    }

    private void NumericOnly_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        NumericInputHelper.HandlePreviewKeyDown(sender, e);
    }
}
