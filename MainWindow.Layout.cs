using System.Windows;

namespace Timer;

public partial class MainWindow
{
    private bool _widthLockedToContent;

    // SizeToContent="Width" measures the window against its current content once
    // it has laid out. We capture that width here and switch to Manual so later
    // content changes (e.g. the Twitch-connected badge appearing) can't resize
    // the window again, matching ResizeMode="NoResize" on the launch window.
    private void LockWindowWidthToContent()
    {
        if (_widthLockedToContent)
        {
            return;
        }

        _widthLockedToContent = true;
        Width = ActualWidth;
        SizeToContent = SizeToContent.Manual;
    }
}
