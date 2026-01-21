using System.Windows;

namespace Timer;

public partial class DebugLogWindow : Window
{
    public DebugLogWindow()
    {
        InitializeComponent();
    }

    public void SetLog(string log)
    {
        LogTextBox.Text = log;
        LogTextBox.ScrollToEnd();
    }

    public void AppendLog(string log)
    {
        LogTextBox.AppendText(log);
        LogTextBox.ScrollToEnd();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(LogTextBox.Text);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
