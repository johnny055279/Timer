using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace Timer;

public partial class MainWindow
{
    private void LoadBeeps()
    {
        BeepComboBox.DisplayMemberPath = nameof(BeepOption.DisplayName);
        var files = LoadEmbeddedBeeps()
            .Concat(LoadExternalBeeps())
            .GroupBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        BeepComboBox.ItemsSource = files;
        if (files.Count > 0)
        {
            BeepComboBox.SelectedIndex = 0;
        }
    }

    private static System.Collections.Generic.IEnumerable<BeepOption> LoadEmbeddedBeeps()
    {
        var assembly = typeof(MainWindow).Assembly;
        var resourceManager = new ResourceManager("Timer.g", assembly);
        var resourceSet = resourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
        if (resourceSet is null)
        {
            yield break;
        }

        foreach (System.Collections.DictionaryEntry entry in resourceSet)
        {
            if (entry.Key is not string key)
            {
                continue;
            }

            if (!key.StartsWith("beeps/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!key.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                && !key.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = Path.GetFileName(key);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var packUri = new Uri($"pack://application:,,,/{key}", UriKind.Absolute);
            yield return new BeepOption(fileName, packUri);
        }
    }

    private System.Collections.Generic.IEnumerable<BeepOption> LoadExternalBeeps()
    {
        if (!Directory.Exists(_beepsDirectory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(_beepsDirectory))
        {
            if (!path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            yield return new BeepOption(fileName, new Uri(path, UriKind.Absolute));
        }
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

        var selection = new BeepOption(Path.GetFileName(dialog.FileName), new Uri(dialog.FileName, UriKind.Absolute));
        var items = BeepComboBox.ItemsSource as System.Collections.Generic.IEnumerable<BeepOption>;
        var list = items?.ToList() ?? new System.Collections.Generic.List<BeepOption>();
        if (list.All(item => !Uri.Equals(item.SourceUri, selection.SourceUri)))
        {
            list.Add(selection);
        }

        BeepComboBox.ItemsSource = list;
        BeepComboBox.SelectedItem = list.FirstOrDefault(item =>
            Uri.Equals(item.SourceUri, selection.SourceUri));
        PlaySelectedBeep();
    }

    private void PlaySelectedBeep()
    {
        var selectedUri = GetSelectedBeepUri();
        if (selectedUri is null)
        {
            return;
        }

        if (selectedUri.IsFile && !File.Exists(selectedUri.LocalPath))
        {
            return;
        }

        _player.Stop();
        _player.Open(selectedUri);
        _player.Play();
    }

    private void StopPlayback()
    {
        _player.Stop();
    }

    private Uri? GetSelectedBeepUri()
    {
        return BeepComboBox.SelectedItem is BeepOption option ? option.SourceUri : null;
    }
}
