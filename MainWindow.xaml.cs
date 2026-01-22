using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timer.Application.Interfaces;
using Timer.Application.Services;
using Timer.Application.UseCases;
using Timer.Infrastructure.Security;
using Timer.Infrastructure.Settings;
using Timer.Infrastructure.Twitch;
using Timer.Infrastructure.Updates;
using Timer.Utilities;

namespace Timer;

public partial class MainWindow : Window
{
    private sealed record BeepOption(string DisplayName, Uri SourceUri);
    private sealed record Hotkey(Key Key, ModifierKeys Modifiers);

    private const string TwitchClientId = "n1smqlrxyxbkyuvys846qjrq8fgcyh";
    private const string UpdateRepo = "johnny055279/Timer";
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
    private readonly IPollDecisionService _pollDecisionService;
    private readonly ITwitchClient _twitchClient;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILogService _logService;
    private readonly CheckForUpdatesUseCase _updateCheckUseCase;
    private readonly Version _currentVersion;
    private DebugLogWindow? _debugWindow;
    private TwitchWindow? _twitchWindow;
    private bool _isLoadingSettings;
    private bool _isLoadingTitle;
    private long _pendingMinutesDelta;
    private bool _updateChecked;
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
        _pollDecisionService = new PollDecisionService();
        _twitchClient = new TwitchClient(TwitchClientId, new System.Net.Http.HttpClient(), new WindowsCredentialStore());
        _settingsStore = new JsonAppSettingsStore();
        _logService = new InMemoryLogService();

        _currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(2, 1, 0, 0);
        _updateCheckUseCase = new CheckForUpdatesUseCase(
            new GitHubUpdateService(UpdateRepo, $"Timer/{_currentVersion}"));

        _twitchClient.RewardRedeemed += (_, rewardId) => Dispatcher.Invoke(() => ApplyRewardAdjustment(rewardId));
        _twitchClient.PollEnded += (_, winnerTitle) => Dispatcher.Invoke(() => ApplyPollAdjustment(winnerTitle));
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
