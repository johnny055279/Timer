using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timer.Application.Interfaces;
using Timer.Application.Services;
using Timer.Infrastructure.Security;
using Timer.Infrastructure.Settings;
using Timer.Infrastructure.Twitch;
using Timer.Utilities;

namespace Timer;

public partial class MainWindow : Window
{
    private sealed record BeepOption(string DisplayName, Uri SourceUri);
    private sealed record Hotkey(Key Key, ModifierKeys Modifiers);

    private const string TwitchClientId = "n1smqlrxyxbkyuvys846qjrq8fgcyh";
    private const string DefaultEventSubWebSocketUrl = "wss://eventsub.wss.twitch.tv/ws";
    private const string DebugEventSubWebSocketUrl = "ws://127.0.0.1:8080/ws";
    private const int MinStepMinutes = 1;
    private const int MaxStepMinutes = 120;
    private const int MinCounterStep = 1;
    private const int MaxCounterStep = 999;

    private readonly DispatcherTimer _timer;
    private readonly MediaPlayer _player = new();
    private readonly string _beepsDirectory;
    private readonly ITimerService _timerService;
    private readonly ICounterService _counterService;
    private readonly IRewardMappingService _rewardMappingService;
    private readonly IBitsMappingService _bitsMappingService;
    private readonly IPollDecisionService _pollDecisionService;
    private readonly ITwitchClient _twitchClient;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILogService _logService;
    private DebugLogWindow? _debugWindow;
    private TwitchWindow? _twitchWindow;
    private bool _isLoadingTitle;
    private bool _isLoadingCounterTitle;
    private long _pendingMinutesDelta;
    private bool _updateChecked;
    private bool _twitchAutoConnectAttempted;
    private Hotkey _increaseHotkey = new(Key.Add, ModifierKeys.None);
    private Hotkey _decreaseHotkey = new(Key.Subtract, ModifierKeys.None);
    private Hotkey _resetHotkey = new(Key.D0, ModifierKeys.None);

    public MainWindow()
    {
        InitializeComponent();

        _beepsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "beeps");
        _timerService = new CountdownTimerService();
        _counterService = new CounterService();
        _rewardMappingService = new RewardMappingService();
        _bitsMappingService = new BitsMappingService();
        _pollDecisionService = new PollDecisionService();
        _settingsStore = new JsonAppSettingsStore();
        var settings = _settingsStore.Load();
        var eventSubUrl = string.IsNullOrWhiteSpace(settings.EventSubWebSocketUrl)
            ? DefaultEventSubWebSocketUrl
            : settings.EventSubWebSocketUrl;
#if DEBUG
        if (settings.UseDebugEventSub)
        {
            eventSubUrl = DebugEventSubWebSocketUrl;
        }
#endif
        _twitchClient = new TwitchClient(
            TwitchClientId,
            new System.Net.Http.HttpClient(),
            new WindowsCredentialStore(),
            eventSubUrl);
        _logService = new InMemoryLogService();

        _twitchClient.RewardRedeemed += (_, rewardId) => Dispatcher.Invoke(() => ApplyRewardAdjustment(rewardId));
        _twitchClient.BitsCheered += (_, bits) => Dispatcher.Invoke(() => ApplyBitsAdjustment(bits));
        _twitchClient.PollEnded += (_, winnerTitle) => Dispatcher.Invoke(() => ApplyPollAdjustment(winnerTitle));
        _twitchClient.StatusChanged += OnTwitchStatusChanged;
        _logService.LogAppended += OnLogAppended;

        LoadBeeps();
        UpdateTimeDisplay();
        UpdateCounterButtons();
        UpdateHotkeyTextBoxes();
        UpdateStepWarnings();
        LoadSettings();

        DataObject.AddPastingHandler(StepMinutesTextBox, NumericInputHelper.HandlePasting);
        DataObject.AddPastingHandler(CounterStepTextBox, NumericInputHelper.HandlePasting);

        InputManager.Current.PreProcessInput += OnPreProcessInput;
        Closed += OnWindowClosed;
        Loaded += OnWindowLoaded;
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Window_PreviewMouseDown), true);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

}
