using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models.Common;
using Microsoft.Win32;

namespace DrillIntel.ViewModels;

/// <summary>
/// ViewModel managing global application system settings stored in DrillIntelApp.sqlite (VMX_SETTINGS).
/// </summary>
public partial class EditSystemSettingsViewModel : ObservableObject
{
    private readonly IDataServiceDIntel? _dataService;
    private readonly SystemSettings _systemSettings;

    public event Action<bool?>? RequestClose;

    #region Tab 1: General & Precision Properties
    [ObservableProperty]
    private int _digitSignificance = 2;

    [ObservableProperty]
    private double _depthOffset = 0.0;

    [ObservableProperty]
    private bool _ignoreNegative = true;

    [ObservableProperty]
    private bool _doNotPerformSlideCleanup = false;

    [ObservableProperty]
    private bool _optimizeDisplay = true;

    [ObservableProperty]
    private bool _useDataPaging = true;

    [ObservableProperty]
    private int _dataPageSize = 5000;

    [ObservableProperty]
    private int _overlapSize = 100;

    [ObservableProperty]
    private int _waitTime = 30;
    #endregion

    #region Tab 2: Log Rates & Alarms Properties
    [ObservableProperty]
    private double _timeLogFrequency = 5.0;

    [ObservableProperty]
    private double _depthLogFrequency = 0.5;

    [ObservableProperty]
    private double _trajectoryFrequency = 30.0;

    [ObservableProperty]
    private double _mudLogFrequency = 10.0;

    [ObservableProperty]
    private double _timeDataAlarmTime = 15.0;

    [ObservableProperty]
    private double _depthDataAlarmTime = 30.0;

    [ObservableProperty]
    private bool _wellCheckEnabled = true;

    [ObservableProperty]
    private bool _alarmAcknowledgement = true;
    #endregion

    #region Tab 3: Directories & Timezone Properties
    [ObservableProperty]
    private string _logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DrillIntel", "Logs");

    [ObservableProperty]
    private bool _logWITSMLTransactions = true;

    [ObservableProperty]
    private string _timeZone = "UTC";

    public List<string> AvailableTimeZones { get; } = new()
    {
        "UTC",
        "Local",
        "CST (UTC-06:00)",
        "EST (UTC-05:00)",
        "MST (UTC-07:00)",
        "PST (UTC-08:00)",
        "GMT (UTC+00:00)"
    };

    [ObservableProperty]
    private string _dayTimeFrom = "06:00:00";

    [ObservableProperty]
    private string _dayTimeTo = "18:00:00";
    #endregion

    #region Tab 4: D-Server & Network Properties
    [ObservableProperty]
    private string _dServerIP = "127.0.0.1";

    [ObservableProperty]
    private int _downloadTimeOut = 30;

    [ObservableProperty]
    private int _downloadChunkSize = 1024;

    [ObservableProperty]
    private string _supportEmail = "support@drillintel.com";

    [ObservableProperty]
    private string _chatServerDomain = "chat.drillintel.net";

    [ObservableProperty]
    private string _chatHostName = "xmpp.drillintel.net";

    [ObservableProperty]
    private int _chatPort = 5222;

    [ObservableProperty]
    private bool _chatUseSSL = true;

    [ObservableProperty]
    private bool _chatResolveSrvRecords = true;
    #endregion

    #region Tab 5: Audio & Notifications Properties
    [ObservableProperty]
    private string _soundFileRed = string.Empty;

    [ObservableProperty]
    private string _soundFileYellow = string.Empty;

    [ObservableProperty]
    private string _soundFileRemarks = string.Empty;
    #endregion

    #region Tab 6: Branding & Reporting Properties
    [ObservableProperty]
    private string _companyLogoPath = string.Empty;

    [ObservableProperty]
    private string _companyLogo2Path = string.Empty;

    [ObservableProperty]
    private string _pageSize = "A4";

    public List<string> AvailablePageSizes { get; } = new() { "A4", "Letter", "Legal" };

    [ObservableProperty]
    private string _orientation = "Landscape";

    public List<string> AvailableOrientations { get; } = new() { "Landscape", "Portrait" };

    [ObservableProperty]
    private double _leftMargin = 15.0;

    [ObservableProperty]
    private double _topMargin = 15.0;

    [ObservableProperty]
    private double _rightMargin = 15.0;

    [ObservableProperty]
    private double _bottomMargin = 15.0;

    [ObservableProperty]
    private bool _shareBroomstickDocuments = true;
    #endregion

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Parameterless constructor for designer / preview support.
    /// </summary>
    public EditSystemSettingsViewModel()
    {
        _systemSettings = new SystemSettings();
    }

    /// <summary>
    /// Runtime constructor initializing settings from the active application database service.
    /// </summary>
    /// <param name="dataService">The database service pointing to DrillIntelApp.sqlite.</param>
    public EditSystemSettingsViewModel(IDataServiceDIntel dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _systemSettings = new SystemSettings();
        LoadSettings();
    }

    /// <summary>
    /// Loads all settings from the database and maps them to observable properties.
    /// </summary>
    public void LoadSettings()
    {
        if (_dataService == null) return;

        try
        {
            _systemSettings.LoadSettings(_dataService);

            // 1. General & Precision
            string digitSignificanceStr = _systemSettings.getSetting(SystemSettings.DigitSignificance);
            if (int.TryParse(digitSignificanceStr, out int sig)) DigitSignificance = sig;

            DepthOffset = _systemSettings.getNumericSetting(SystemSettings.DepthOffset);
            IgnoreNegative = _systemSettings.getBooleanSetting(SystemSettings.IgnoreNegative);
            DoNotPerformSlideCleanup = _systemSettings.getBooleanSetting(SystemSettings.DoNotPerformSlideCleanup);

            OptimizeDisplay = _systemSettings.getBooleanSetting(SystemSettings.OptimizeDisplay);
            UseDataPaging = _systemSettings.getBooleanSetting(SystemSettings.UseDataPaging);

            double pageSizeVal = _systemSettings.getNumericSetting(SystemSettings.DataPageSize);
            if (pageSizeVal > 0) DataPageSize = (int)pageSizeVal;

            double overlapVal = _systemSettings.getNumericSetting(SystemSettings.OverlapSize);
            if (overlapVal > 0) OverlapSize = (int)overlapVal;

            double waitVal = _systemSettings.getNumericSetting(SystemSettings.WaitTime);
            if (waitVal > 0) WaitTime = (int)waitVal;

            // 2. Log Rates & Alarms
            double timeLogFreq = _systemSettings.getNumericSetting(SystemSettings.TimeLogFrequency);
            if (timeLogFreq > 0) TimeLogFrequency = timeLogFreq;

            double depthLogFreq = _systemSettings.getNumericSetting(SystemSettings.DepthLogFrequency);
            if (depthLogFreq > 0) DepthLogFrequency = depthLogFreq;

            double trajFreq = _systemSettings.getNumericSetting(SystemSettings.TrajectoryFrequency);
            if (trajFreq > 0) TrajectoryFrequency = trajFreq;

            double mudFreq = _systemSettings.getNumericSetting(SystemSettings.MudLogFrequency);
            if (mudFreq > 0) MudLogFrequency = mudFreq;

            double timeAlarm = _systemSettings.getNumericSetting(SystemSettings.TimeDataAlarmTime);
            if (timeAlarm > 0) TimeDataAlarmTime = timeAlarm;

            double depthAlarm = _systemSettings.getNumericSetting(SystemSettings.DepthDataAlarmTime);
            if (depthAlarm > 0) DepthDataAlarmTime = depthAlarm;

            WellCheckEnabled = _systemSettings.getBooleanSetting(SystemSettings.WellCheckEnabled);
            AlarmAcknowledgement = _systemSettings.getBooleanSetting(SystemSettings.AlarmAcknowledgement);

            // 3. Folders & Timezone
            string logFld = _systemSettings.getSetting(SystemSettings.LogFolder);
            if (!string.IsNullOrWhiteSpace(logFld)) LogFolder = logFld;

            LogWITSMLTransactions = _systemSettings.getBooleanSetting(SystemSettings.LogWITSMLTransactions);

            string tz = _systemSettings.getSetting(SystemSettings.TimeZone);
            if (!string.IsNullOrWhiteSpace(tz)) TimeZone = tz;

            string dayFrom = _systemSettings.getSetting(SystemSettings.DayTimeFrom);
            if (!string.IsNullOrWhiteSpace(dayFrom)) DayTimeFrom = dayFrom;

            string dayTo = _systemSettings.getSetting(SystemSettings.DayTimeTo);
            if (!string.IsNullOrWhiteSpace(dayTo)) DayTimeTo = dayTo;

            // 4. D-Server & Network
            string dServer = _systemSettings.getSetting(SystemSettings.DServerIP);
            if (!string.IsNullOrWhiteSpace(dServer)) DServerIP = dServer;

            double timeoutVal = _systemSettings.getNumericSetting(SystemSettings.DownloadTimeOut);
            if (timeoutVal > 0) DownloadTimeOut = (int)timeoutVal;

            double chunkVal = _systemSettings.getNumericSetting(SystemSettings.DownloadChunkSize);
            if (chunkVal > 0) DownloadChunkSize = (int)chunkVal;

            string email = _systemSettings.getSetting(SystemSettings.SupportEmail);
            if (!string.IsNullOrWhiteSpace(email)) SupportEmail = email;

            string chatDomain = _systemSettings.getSetting(SystemSettings.ChatServerDomain);
            if (!string.IsNullOrWhiteSpace(chatDomain)) ChatServerDomain = chatDomain;

            string chatHost = _systemSettings.getSetting(SystemSettings.chatHostName);
            if (!string.IsNullOrWhiteSpace(chatHost)) ChatHostName = chatHost;

            double chatPortVal = _systemSettings.getNumericSetting(SystemSettings.chatPort);
            if (chatPortVal > 0) ChatPort = (int)chatPortVal;

            ChatUseSSL = _systemSettings.getBooleanSetting(SystemSettings.chatUseSSL);
            ChatResolveSrvRecords = _systemSettings.getBooleanSetting(SystemSettings.chatResolveSrvRecords);

            // 5. Audio
            SoundFileRed = _systemSettings.getSetting(SystemSettings.SoundFileRed);
            SoundFileYellow = _systemSettings.getSetting(SystemSettings.SoundFileYellow);
            SoundFileRemarks = _systemSettings.getSetting(SystemSettings.SoundFileRemarks);

            // 6. Branding & Reports
            string logo1 = _systemSettings.loadBinaryValue(_dataService, SystemSettings.CompanyLogo);
            if (!string.IsNullOrWhiteSpace(logo1)) CompanyLogoPath = logo1;

            string logo2 = _systemSettings.loadBinaryValue(_dataService, SystemSettings.CompanyLogo2);
            if (!string.IsNullOrWhiteSpace(logo2)) CompanyLogo2Path = logo2;

            string ps = _systemSettings.getSetting(SystemSettings.PageSize);
            if (!string.IsNullOrWhiteSpace(ps)) PageSize = ps;

            string orient = _systemSettings.getSetting(SystemSettings.Orientation);
            if (!string.IsNullOrWhiteSpace(orient)) Orientation = orient;

            double lm = _systemSettings.getNumericSetting(SystemSettings.LeftMargin);
            if (lm > 0) LeftMargin = lm;

            double tm = _systemSettings.getNumericSetting(SystemSettings.TopMargin);
            if (tm > 0) TopMargin = tm;

            double rm = _systemSettings.getNumericSetting(SystemSettings.RightMargin);
            if (rm > 0) RightMargin = rm;

            double bm = _systemSettings.getNumericSetting(SystemSettings.BottomMargin);
            if (bm > 0) BottomMargin = bm;

            ShareBroomstickDocuments = _systemSettings.getBooleanSetting(SystemSettings.ShareBroomstickDouments);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
    }

    #region Save, Cancel & Restore Defaults Commands
    [RelayCommand]
    private void Save()
    {
        if (_dataService == null)
        {
            RequestClose?.Invoke(false);
            return;
        }

        try
        {
            // 1. General & Precision
            _systemSettings.setSetting(SystemSettings.DigitSignificance, DigitSignificance.ToString());
            _systemSettings.setSetting(SystemSettings.DepthOffset, DepthOffset.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.IgnoreNegative, IgnoreNegative.ToString());
            _systemSettings.setSetting(SystemSettings.DoNotPerformSlideCleanup, DoNotPerformSlideCleanup.ToString());
            _systemSettings.setSetting(SystemSettings.OptimizeDisplay, OptimizeDisplay.ToString());
            _systemSettings.setSetting(SystemSettings.UseDataPaging, UseDataPaging.ToString());
            _systemSettings.setSetting(SystemSettings.DataPageSize, DataPageSize.ToString());
            _systemSettings.setSetting(SystemSettings.OverlapSize, OverlapSize.ToString());
            _systemSettings.setSetting(SystemSettings.WaitTime, WaitTime.ToString());

            // 2. Log Rates & Alarms
            _systemSettings.setSetting(SystemSettings.TimeLogFrequency, TimeLogFrequency.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.DepthLogFrequency, DepthLogFrequency.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.TrajectoryFrequency, TrajectoryFrequency.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.MudLogFrequency, MudLogFrequency.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.TimeDataAlarmTime, TimeDataAlarmTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.DepthDataAlarmTime, DepthDataAlarmTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.WellCheckEnabled, WellCheckEnabled.ToString());
            _systemSettings.setSetting(SystemSettings.AlarmAcknowledgement, AlarmAcknowledgement.ToString());

            // 3. Folders & Timezone
            _systemSettings.setSetting(SystemSettings.LogFolder, LogFolder);
            _systemSettings.setSetting(SystemSettings.LogWITSMLTransactions, LogWITSMLTransactions.ToString());
            _systemSettings.setSetting(SystemSettings.TimeZone, TimeZone);
            _systemSettings.setSetting(SystemSettings.DayTimeFrom, DayTimeFrom);
            _systemSettings.setSetting(SystemSettings.DayTimeTo, DayTimeTo);

            // 4. D-Server & Network
            _systemSettings.setSetting(SystemSettings.DServerIP, DServerIP);
            _systemSettings.setSetting(SystemSettings.DownloadTimeOut, DownloadTimeOut.ToString());
            _systemSettings.setSetting(SystemSettings.DownloadChunkSize, DownloadChunkSize.ToString());
            _systemSettings.setSetting(SystemSettings.SupportEmail, SupportEmail);
            _systemSettings.setSetting(SystemSettings.ChatServerDomain, ChatServerDomain);
            _systemSettings.setSetting(SystemSettings.chatHostName, ChatHostName);
            _systemSettings.setSetting(SystemSettings.chatPort, ChatPort.ToString());
            _systemSettings.setSetting(SystemSettings.chatUseSSL, ChatUseSSL.ToString());
            _systemSettings.setSetting(SystemSettings.chatResolveSrvRecords, ChatResolveSrvRecords.ToString());

            // 5. Audio
            _systemSettings.setSetting(SystemSettings.SoundFileRed, SoundFileRed);
            _systemSettings.setSetting(SystemSettings.SoundFileYellow, SoundFileYellow);
            _systemSettings.setSetting(SystemSettings.SoundFileRemarks, SoundFileRemarks);

            // 6. Branding & Binary Logos
            if (!string.IsNullOrWhiteSpace(CompanyLogoPath) && File.Exists(CompanyLogoPath))
            {
                _systemSettings.setBinarySetting(SystemSettings.CompanyLogo, CompanyLogoPath);
            }
            if (!string.IsNullOrWhiteSpace(CompanyLogo2Path) && File.Exists(CompanyLogo2Path))
            {
                _systemSettings.setBinarySetting(SystemSettings.CompanyLogo2, CompanyLogo2Path);
            }

            _systemSettings.setSetting(SystemSettings.PageSize, PageSize);
            _systemSettings.setSetting(SystemSettings.Orientation, Orientation);
            _systemSettings.setSetting(SystemSettings.LeftMargin, LeftMargin.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.TopMargin, TopMargin.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.RightMargin, RightMargin.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.BottomMargin, BottomMargin.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _systemSettings.setSetting(SystemSettings.ShareBroomstickDouments, ShareBroomstickDocuments.ToString());

            bool success = _systemSettings.SaveSettings(_dataService);
            if (!success)
            {
                MessageBox.Show($"Failed to save system settings: {_systemSettings.LastError}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error while saving settings: {ex.Message}", "Save Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    [RelayCommand]
    private void RestoreDefaults()
    {
        var result = MessageBox.Show(
            "Are you sure you want to restore system default settings?",
            "Confirm Defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        DigitSignificance = 2;
        DepthOffset = 0.0;
        IgnoreNegative = true;
        DoNotPerformSlideCleanup = false;
        OptimizeDisplay = true;
        UseDataPaging = true;
        DataPageSize = 5000;
        OverlapSize = 100;
        WaitTime = 30;

        TimeLogFrequency = 5.0;
        DepthLogFrequency = 0.5;
        TrajectoryFrequency = 30.0;
        MudLogFrequency = 10.0;
        TimeDataAlarmTime = 15.0;
        DepthDataAlarmTime = 30.0;
        WellCheckEnabled = true;
        AlarmAcknowledgement = true;

        LogFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DrillIntel", "Logs");
        LogWITSMLTransactions = true;
        TimeZone = "UTC";
        DayTimeFrom = "06:00:00";
        DayTimeTo = "18:00:00";

        DServerIP = "127.0.0.1";
        DownloadTimeOut = 30;
        DownloadChunkSize = 1024;
        SupportEmail = "support@drillintel.com";
        ChatServerDomain = "chat.drillintel.net";
        ChatHostName = "xmpp.drillintel.net";
        ChatPort = 5222;
        ChatUseSSL = true;
        ChatResolveSrvRecords = true;

        SoundFileRed = string.Empty;
        SoundFileYellow = string.Empty;
        SoundFileRemarks = string.Empty;

        PageSize = "A4";
        Orientation = "Landscape";
        LeftMargin = 15.0;
        TopMargin = 15.0;
        RightMargin = 15.0;
        BottomMargin = 15.0;
        ShareBroomstickDocuments = true;
    }
    #endregion

    #region Dialog Browse & Sound Preview Commands
    [RelayCommand]
    private void BrowseLogFolder()
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select System Logs Directory",
                InitialDirectory = Directory.Exists(LogFolder) ? LogFolder : Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                LogFolder = dialog.FolderName;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open folder dialog: {ex.Message}", "Folder Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void BrowseSoundRed()
    {
        string? selected = OpenAudioFileDialog("Select Critical Alarm Audio File (Red)");
        if (!string.IsNullOrEmpty(selected)) SoundFileRed = selected;
    }

    [RelayCommand]
    private void BrowseSoundYellow()
    {
        string? selected = OpenAudioFileDialog("Select Warning Alarm Audio File (Yellow)");
        if (!string.IsNullOrEmpty(selected)) SoundFileYellow = selected;
    }

    [RelayCommand]
    private void BrowseSoundRemarks()
    {
        string? selected = OpenAudioFileDialog("Select Remarks / Event Audio File");
        if (!string.IsNullOrEmpty(selected)) SoundFileRemarks = selected;
    }

    private static string? OpenAudioFileDialog(string title)
    {
        var dlg = new OpenFileDialog
        {
            Title = title,
            Filter = "Audio files (*.wav)|*.wav|All files (*.*)|*.*"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    [RelayCommand]
    private void PlaySoundRed() => PlayAudioFile(SoundFileRed);

    [RelayCommand]
    private void PlaySoundYellow() => PlayAudioFile(SoundFileYellow);

    [RelayCommand]
    private void PlaySoundRemarks() => PlayAudioFile(SoundFileRemarks);

    private static void PlayAudioFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            SystemSounds.Beep.Play();
            return;
        }

        try
        {
            using var player = new SoundPlayer(filePath);
            player.Play();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to play audio: {ex.Message}", "Audio Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void BrowseCompanyLogo()
    {
        string? selected = OpenImageFileDialog("Select Primary Company Logo");
        if (!string.IsNullOrEmpty(selected)) CompanyLogoPath = selected;
    }

    [RelayCommand]
    private void BrowseCompanyLogo2()
    {
        string? selected = OpenImageFileDialog("Select Operator / Client Logo");
        if (!string.IsNullOrEmpty(selected)) CompanyLogo2Path = selected;
    }

    private static string? OpenImageFileDialog(string title)
    {
        var dlg = new OpenFileDialog
        {
            Title = title,
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*"
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
    #endregion
}

