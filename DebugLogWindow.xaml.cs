using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Timer.Application.Interfaces;

namespace Timer;

public partial class DebugLogWindow : Window
{
    private readonly ILogService _logService;
    private readonly ILogUploader _logUploader;

    public DebugLogWindow(ILogService logService, ILogUploader logUploader)
    {
        InitializeComponent();

        _logService = logService;
        _logUploader = logUploader;
        UploadButton.Visibility = logUploader.IsConfigured ? Visibility.Visible : Visibility.Collapsed;
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

    private async void Upload_Click(object sender, RoutedEventArgs e)
    {
        var consent = MessageBox.Show(
            this,
            "Send this session's log to the developer for troubleshooting? It includes your Twitch channel name, received events and mappings, but no passwords or tokens.\n\n" +
            "要把這次執行的記錄傳給開發者協助排查嗎？內容包含 Twitch 頻道名稱、收到的事件與對應設定，不含密碼或 token。",
            "Debug Log",
            MessageBoxButton.YesNo);
        if (consent != MessageBoxResult.Yes)
        {
            return;
        }

        UploadButton.IsEnabled = false;
        try
        {
            var reportId = await _logUploader.UploadAsync(_logService.GetEntries());
            _logService.LogInfo($"Log uploaded as report {reportId}.");
            TryCopyToClipboard(reportId);
            MessageBox.Show(
                this,
                $"Uploaded. Report ID: {reportId} (copied). Please send this ID to the developer.\n\n" +
                $"已上傳。回報代碼：{reportId}（已複製），請把代碼提供給開發者。",
                "Debug Log");
        }
        catch (Exception ex)
        {
            _logService.LogError("Log upload failed.", ex);
            MessageBox.Show(
                this,
                $"Upload failed: {ex.Message}\nUse \"Open folder\" and send the log file instead.\n\n" +
                "上傳失敗，請改用「開啟資料夾」把記錄檔傳給開發者。",
                "Debug Log");
        }
        finally
        {
            UploadButton.IsEnabled = true;
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_logService.LogDirectory);
        Process.Start(new ProcessStartInfo(_logService.LogDirectory) { UseShellExecute = true });
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(LogTextBox.Text);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void TryCopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard can be locked by another app; the ID is still shown in the dialog.
        }
    }
}
