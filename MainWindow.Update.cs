using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Timer;

public partial class MainWindow
{
    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (_updateChecked)
        {
            return;
        }

        _updateChecked = true;
        try
        {
            await CheckForUpdatesAsync();
        }
        catch
        {
            // Ignore update failures to avoid blocking startup.
        }

        _ = TryAutoConnectTwitchAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var latest = await _updateCheckUseCase.ExecuteAsync(_currentVersion, cts.Token);
        if (latest is null)
        {
            return;
        }

        var message = $"New version available ({latest.Version}). Open download page? (有新版本 {latest.Version}，是否前往下載頁？)";
        var result = MessageBox.Show(this, message, "Update", MessageBoxButton.YesNo);
        if (result == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo(latest.Url) { UseShellExecute = true });
        }
    }
}
