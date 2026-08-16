using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Threading.Tasks;
using Timer.Application.Interfaces;
using Timer.Application.Models;
using Timer.Domain.Entities;
using Timer.Utilities;

namespace Timer;

public partial class TwitchWindow : Window
{
    private const int MinMinutes = 1;
    private const int MaxMinutes = 120;
    private const string DefaultEventSubWebSocketUrl = "wss://eventsub.wss.twitch.tv/ws";
    private const string DebugEventSubWebSocketUrl = "ws://127.0.0.1:8080/ws";
    private static readonly Brush DefaultBorderBrush = new SolidColorBrush(Color.FromRgb(233, 236, 239));
    private static readonly Brush DebugBorderBrush = new SolidColorBrush(Color.FromRgb(180, 35, 24));
    private static readonly string[] RewardTargetOptions =
    [
        "Countdown (倒數)",
        "Counter (計數器)"
    ];
    private static readonly string[] RewardCountdownActions =
    [
        "Add (增加)",
        "Subtract (減少)"
    ];
    private static readonly string[] RewardCounterActions =
    [
        "Increase (增加)",
        "Decrease (減少)",
        "Reset (歸零)"
    ];

    private readonly ITwitchClient _twitchClient;
    private readonly IRewardMappingService _rewardMappingService;
    private readonly IBitsMappingService _bitsMappingService;
    private readonly IPollDecisionService _pollDecisionService;
    private readonly ILogService _logService;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ObservableCollection<TwitchReward> _twitchRewards = new();
    private readonly ObservableCollection<TwitchRewardMapping> _twitchRewardMappings = new();
    private readonly ObservableCollection<TwitchBitsMapping> _twitchBitsMappings = new();
    private bool _debugNoticeShown;
    private bool _isDebugMode;

    public TwitchWindow(
        ITwitchClient twitchClient,
        IRewardMappingService rewardMappingService,
        IBitsMappingService bitsMappingService,
        IPollDecisionService pollDecisionService,
        ILogService logService,
        IAppSettingsStore settingsStore)
    {
        InitializeComponent();

        _twitchClient = twitchClient;
        _rewardMappingService = rewardMappingService;
        _bitsMappingService = bitsMappingService;
        _pollDecisionService = pollDecisionService;
        _logService = logService;
        _settingsStore = settingsStore;

        TwitchRewardsComboBox.ItemsSource = _twitchRewards;
        TwitchRewardsComboBox.DisplayMemberPath = nameof(TwitchReward.Display);
        TwitchRewardTargetComboBox.ItemsSource = RewardTargetOptions;
        TwitchRewardTargetComboBox.SelectedIndex = 0;
        TwitchRewardActionComboBox.ItemsSource = RewardCountdownActions;
        TwitchRewardActionComboBox.SelectedIndex = 0;
        TwitchRewardMappingsListBox.ItemsSource = _twitchRewardMappings;
        TwitchRewardMappingsListBox.DisplayMemberPath = nameof(TwitchRewardMapping.Display);
        TwitchBitsTargetComboBox.ItemsSource = RewardTargetOptions;
        TwitchBitsTargetComboBox.SelectedIndex = 0;
        TwitchBitsActionComboBox.ItemsSource = RewardCountdownActions;
        TwitchBitsActionComboBox.SelectedIndex = 0;
        TwitchBitsMappingsListBox.ItemsSource = _twitchBitsMappings;
        TwitchBitsMappingsListBox.DisplayMemberPath = nameof(TwitchBitsMapping.Display);
        TwitchPollActionComboBox.ItemsSource = new[] { "Add (增加)", "Subtract (減少)" };
        TwitchPollActionComboBox.SelectedIndex = 1;

        RefreshRewardMappings();
        RefreshBitsMappings();

        DataObject.AddPastingHandler(TwitchRewardMinutesTextBox, NumericInputHelper.HandlePasting);
        DataObject.AddPastingHandler(TwitchBitsAmountTextBox, NumericInputHelper.HandlePasting);
        DataObject.AddPastingHandler(TwitchBitsValueTextBox, NumericInputHelper.HandlePasting);
        DataObject.AddPastingHandler(TwitchPollMinutesTextBox, NumericInputHelper.HandlePasting);

        _twitchClient.StatusChanged += OnStatusChanged;
        _twitchClient.DeviceCodeReceived += OnDeviceCodeReceived;
        Closed += OnWindowClosed;

#if DEBUG
        DebugModeCheckBox.Visibility = Visibility.Visible;
#endif

        LoadDebugSettings();
    }

    private void LoadDebugSettings()
    {
        if (DebugModeCheckBox is null)
        {
            return;
        }

        var settings = _settingsStore.Load();
#if DEBUG
        DebugModeCheckBox.IsChecked = false;
        if (settings.UseDebugEventSub)
        {
            settings.UseDebugEventSub = false;
            _settingsStore.Save(settings);
        }

        ApplyDebugMode(false, settings);
        LoadRewardMappingsFromSettings(false);
        LoadBitsMappingsFromSettings(false);
        _ = UpdateTokenStatusAsync();
#else
        ApplyDebugMode(false, settings);
#endif
    }

    private void DebugModeCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        UpdateDebugMode(true);
    }

    private void DebugModeCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        UpdateDebugMode(false);
    }

    private async void UpdateDebugMode(bool isEnabled)
    {
        var settings = _settingsStore.Load();
        settings.UseDebugEventSub = isEnabled;
        settings.EventSubWebSocketUrl = DefaultEventSubWebSocketUrl;
        _settingsStore.Save(settings);
        ApplyDebugMode(isEnabled, settings);

        if (isEnabled)
        {
            if (!_debugNoticeShown)
            {
                _debugNoticeShown = true;
                MessageBox.Show(this, "Debug mode enabled. (已開啟)", "Twitch");
            }

            LoadRewardMappingsFromSettings(true);
            LoadBitsMappingsFromSettings(true);
            _ = UpdateTokenStatusAsync();
            return;
        }

        LoadRewardMappingsFromSettings(false);
        LoadBitsMappingsFromSettings(false);
        var reconnected = await _twitchClient.TryReconnectAsync();
        if (!reconnected)
        {
            MessageBox.Show(this, "Reconnect required. (請重新連線)", "Twitch");
        }

        _ = UpdateTokenStatusAsync();
    }

    private void ApplyDebugMode(bool isEnabled, AppSettings settings)
    {
        _twitchClient.EventSubWebSocketUrl = isEnabled
            ? DebugEventSubWebSocketUrl
            : DefaultEventSubWebSocketUrl;

        if (RootBorder is null
            || DebugModeBanner is null
            || DebugModeHintTextBlock is null
            || DebugTokenStatusTextBlock is null
            || DebugSimulateRewardButton is null
            || DebugSimulateBitsButton is null
            || DebugTriggerRewardButton is null
            || DebugTriggerBitsButton is null
            || DebugSelectedRewardTextBlock is null
            || DebugSelectedBitsTextBlock is null
            || TwitchConnectPanel is null
            || TwitchStatusTextBlock is null)
        {
            return;
        }

        RootBorder.BorderBrush = isEnabled ? DebugBorderBrush : DefaultBorderBrush;
        RootBorder.BorderThickness = isEnabled ? new Thickness(2) : new Thickness(1);
        DebugModeBanner.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        DebugModeHintTextBlock.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        DebugTokenStatusTextBlock.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        DebugSimulateRewardButton.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        DebugSimulateBitsButton.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        DebugTriggerRewardButton.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        DebugTriggerBitsButton.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        TwitchConnectPanel.Visibility = isEnabled ? Visibility.Collapsed : Visibility.Visible;
        TwitchStatusTextBlock.Visibility = isEnabled ? Visibility.Collapsed : Visibility.Visible;

        if (_isDebugMode && !isEnabled)
        {
            ClearDebugRewards();
            ClearDebugMappings();
            ClearDebugBitsMappings();
            var reset = _settingsStore.Load();
            reset.DebugRewardMappings = new System.Collections.Generic.List<TwitchRewardMappingSetting>();
            reset.DebugBitsMappings = new System.Collections.Generic.List<TwitchBitsMappingSetting>();
            _settingsStore.Save(reset);
            DebugSelectedRewardTextBlock.Text = "None";
            DebugSelectedBitsTextBlock.Text = "None";
        }

        _isDebugMode = isEnabled;
    }

    private async Task UpdateTokenStatusAsync()
    {
        if (DebugTokenStatusTextBlock is null || DebugModeCheckBox is null)
        {
            return;
        }

        if (DebugModeCheckBox.IsChecked != true)
        {
            return;
        }

        var status = await _twitchClient.GetTokenStatusAsync();
        if (!status.HasToken)
        {
            DebugTokenStatusTextBlock.Text = "Token status: missing";
            return;
        }

        var scopeText = status.HasRequiredScopes ? "scopes ok" : "scopes missing";
        var expiresAt = status.ExpiresAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "unknown";
        DebugTokenStatusTextBlock.Text = $"Token status: {scopeText}, expires {expiresAt}";
    }
}
