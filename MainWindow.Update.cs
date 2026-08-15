using System;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace Timer;

public partial class MainWindow
{
    private const string UpdateRepoUrl = "https://github.com/johnny055279/Timer";

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
        var mgr = new UpdateManager(new GithubSource(UpdateRepoUrl, null, false));
        if (!mgr.IsInstalled)
        {
            return;
        }

        var checkTask = mgr.CheckForUpdatesAsync();
        var winner = await Task.WhenAny(checkTask, Task.Delay(TimeSpan.FromSeconds(5)));
        if (winner != checkTask)
        {
            return;
        }

        var updateInfo = await checkTask;
        if (updateInfo is null)
        {
            return;
        }

        var newVersion = updateInfo.TargetFullRelease.Version;
        var promptMessage = $"New version available ({newVersion}). Download and install now? (有新版本 {newVersion}，是否立即下載並安裝？)";
        if (MessageBox.Show(this, promptMessage, "Update", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
        {
            return;
        }

        await mgr.DownloadUpdatesAsync(updateInfo);

        const string restartMessage = "Update downloaded. The app will now restart to finish installing. (更新已下載，程式即將重新啟動以完成安裝。)";
        MessageBox.Show(this, restartMessage, "Update", MessageBoxButton.OK);

        mgr.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
    }
}
