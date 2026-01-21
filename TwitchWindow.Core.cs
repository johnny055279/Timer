using System;
using System.Collections.ObjectModel;
using System.Windows;
using Timer.Application.Interfaces;
using Timer.Domain.Entities;
using Timer.Utilities;

namespace Timer;

public partial class TwitchWindow : Window
{
    private const int MinMinutes = 1;
    private const int MaxMinutes = 120;
    private const string TwitchTokenKey = "JohnnyTimerEventSubWPF.TwitchToken";

    private readonly ITwitchClient _twitchClient;
    private readonly IRewardMappingService _rewardMappingService;
    private readonly IPollDecisionService _pollDecisionService;
    private readonly ILogService _logService;
    private readonly ObservableCollection<TwitchReward> _twitchRewards = new();
    private readonly ObservableCollection<TwitchRewardMapping> _twitchRewardMappings = new();

    public TwitchWindow(
        ITwitchClient twitchClient,
        IRewardMappingService rewardMappingService,
        IPollDecisionService pollDecisionService,
        ILogService logService)
    {
        InitializeComponent();

        _twitchClient = twitchClient;
        _rewardMappingService = rewardMappingService;
        _pollDecisionService = pollDecisionService;
        _logService = logService;

        TwitchRewardsComboBox.ItemsSource = _twitchRewards;
        TwitchRewardsComboBox.DisplayMemberPath = nameof(TwitchReward.Display);
        TwitchRewardActionComboBox.ItemsSource = new[] { "Add (增加)", "Subtract (減少)" };
        TwitchRewardActionComboBox.SelectedIndex = 0;
        TwitchRewardMappingsListBox.ItemsSource = _twitchRewardMappings;
        TwitchRewardMappingsListBox.DisplayMemberPath = nameof(TwitchRewardMapping.Display);
        TwitchPollActionComboBox.ItemsSource = new[] { "Add (增加)", "Subtract (減少)" };
        TwitchPollActionComboBox.SelectedIndex = 1;

        RefreshRewardMappings();

        DataObject.AddPastingHandler(TwitchRewardMinutesTextBox, NumericInputHelper.HandlePasting);
        DataObject.AddPastingHandler(TwitchPollMinutesTextBox, NumericInputHelper.HandlePasting);

        _twitchClient.StatusChanged += OnStatusChanged;
        _twitchClient.DeviceCodeReceived += OnDeviceCodeReceived;
        Closed += OnWindowClosed;
    }
}
