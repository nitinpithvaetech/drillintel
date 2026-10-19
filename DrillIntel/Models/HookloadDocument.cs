using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using DrillIntel.Data;
using DrillIntel.Projects;
using DrillIntel.Services;

namespace DrillIntel.Models;

/// <summary>
/// Document representation for Hookload / Broomstick plots.
/// Exposes document state, calculation triggers, and decoupled dialog / plot refresh hooks.
/// </summary>
public partial class HookloadDocument : ObservableObject
{
    private readonly IBroomstickProcessor _broomstickProcessor;
    private readonly IBroomstickDataService _dataService;
    private readonly IAppLogger _logger;

    public const string ConfirmationMessage = "This action will re-calculate broomstick points from the beginning of the well. Do you want to continue?";
    public const string DialogTitle = "VuMax - Broomstick Calculation";

    [ObservableProperty]
    private string _wellID = string.Empty;

    [ObservableProperty]
    private string _wellboreID = string.Empty;

    [ObservableProperty]
    private string _logID = string.Empty;

    [ObservableProperty]
    private string _documentName = "Hookload / Broomstick Document";

    [ObservableProperty]
    private bool _isPlotRefreshing;

    [ObservableProperty]
    private bool _isCalculating;

    /// <summary>
    /// Hook for displaying confirmation prompts. Can be overridden in unit tests to avoid modal UI dialogs.
    /// Default implementation delegates to WPF MessageBox.Show.
    /// </summary>
    public Func<string, string, bool>? ConfirmationPromptHandler { get; set; }

    /// <summary>
    /// Hook for asynchronous plot refresh execution.
    /// </summary>
    public Func<CancellationToken, Task>? RefreshPlotHandlerAsync { get; set; }

    /// <summary>
    /// Synchronous plot refresh hook.
    /// </summary>
    public Action? RefreshPlotHandler { get; set; }

    /// <summary>
    /// Event fired after plot refresh completes.
    /// </summary>
    public event EventHandler? PlotRefreshed;

    public IBroomstickProcessor BroomstickProcessor => _broomstickProcessor;
    public IBroomstickDataService DataService => _dataService;

    public HookloadDocument(
        string wellId,
        IBroomstickProcessor broomstickProcessor,
        IBroomstickDataService dataService,
        IAppLogger? logger = null)
    {
        WellID = wellId ?? string.Empty;
        _broomstickProcessor = broomstickProcessor ?? throw new ArgumentNullException(nameof(broomstickProcessor));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _logger = logger ?? new AppLogger(nameof(HookloadDocument));
    }

    public HookloadDocument(
        ProjectSession session,
        string? wellId = null,
        IAppLogger? logger = null)
    {
        _logger = logger ?? new AppLogger(nameof(HookloadDocument));
        _dataService = new BroomstickDataService(session, _logger);
        _broomstickProcessor = new BroomstickProcessor(_dataService, session, _logger);
        WellID = wellId ?? string.Empty;
    }

    /// <summary>
    /// Hook for displaying error alerts. Can be overridden in unit tests to avoid modal UI dialogs.
    /// </summary>
    public Action<string, string>? ErrorAlertHandler { get; set; }

    /// <summary>
    /// Prompts the user with a confirmation dialog and, if confirmed, clears existing broomstick point
    /// entries from the database, recalculates broomstick points, and asynchronously refreshes the plot.
    /// </summary>
    public void CalculateBroomstickPoints()
    {
        _logger.LogInfo($"CalculateBroomstickPoints() invoked for WellID='{WellID}'.");

        try
        {
            // 1. Prompt user with confirmation dialog
            bool confirmed = PromptConfirmation(ConfirmationMessage, DialogTitle);
            if (!confirmed)
            {
                _logger.LogInfo("User opted NOT to recalculate broomstick points. Operation aborted.");
                return;
            }

            _logger.LogInfo("User confirmed broomstick points recalculation. Dispatching asynchronous calculation workflow...");

            // 2. Clear DB, recalculate points, and refresh plot asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteRecalculationWorkflowAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Background recalculation failed for WellID '{WellID}': {ex.Message}", ex);
                    ShowError($"An error occurred while calculating broomstick points:\n{ex.Message}", "Calculation Error");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception in CalculateBroomstickPoints: {ex.Message}", ex);
            ShowError($"Failed to initiate broomstick calculation:\n{ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Asynchronous version of CalculateBroomstickPoints allowing awaitable execution.
    /// </summary>
    public async Task<bool> CalculateBroomstickPointsAsync(CancellationToken ct = default)
    {
        _logger.LogInfo($"CalculateBroomstickPointsAsync() invoked for WellID='{WellID}'.");

        try
        {
            // 1. Confirmation dialog
            bool confirmed = PromptConfirmation(ConfirmationMessage, DialogTitle);
            if (!confirmed)
            {
                _logger.LogInfo("User opted NOT to recalculate broomstick points. Operation aborted.");
                return false;
            }

            // 2. Execution workflow
            await ExecuteRecalculationWorkflowAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in CalculateBroomstickPointsAsync for WellID='{WellID}': {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Executes the core sequence: clear DB entries -> recalculate points -> refresh plot asynchronously.
    /// </summary>
    private async Task ExecuteRecalculationWorkflowAsync(CancellationToken ct)
    {
        IsCalculating = true;
        try
        {
            // Step 1: Clear existing database entries using parameterized queries
            _logger.LogInfo($"Step 1/3: Clearing database entries in VMX_BROOMSTICK_POINTS & VMX_BROOMSTICK_PRC_INFO for WellID='{WellID}'...");
            await _broomstickProcessor.ClearAllDBEntriesAsync(WellID, ct);

            // Step 2: Call the Broomstick processor to recalculate points from the beginning of the well
            _logger.LogInfo($"Step 2/3: Calling Broomstick processor to recalculate points for WellID='{WellID}'...");
            var calcResult = await _broomstickProcessor.RecalculatePointsAsync(WellID, null, ct);

            if (!calcResult.Success && !string.IsNullOrWhiteSpace(calcResult.ErrorMessage))
            {
                _logger.LogWarning($"Broomstick processor reported non-fatal warning/error: {calcResult.ErrorMessage}");
            }

            // Step 3: Refresh the plot asynchronously to reflect updated broomstick points
            _logger.LogInfo($"Step 3/3: Refreshing plot asynchronously for WellID='{WellID}'...");
            await RefreshPlotAsync(ct);

            _logger.LogInfo($"Broomstick recalculation workflow completed successfully for WellID='{WellID}'.");
        }
        finally
        {
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Refreshes the plot asynchronously to reflect updated broomstick points.
    /// </summary>
    public async Task RefreshPlotAsync(CancellationToken ct = default)
    {
        try
        {
            IsPlotRefreshing = true;
            _logger.LogInfo($"Starting asynchronous plot refresh for WellID='{WellID}'...");

            if (RefreshPlotHandlerAsync != null)
            {
                await RefreshPlotHandlerAsync(ct);
            }
            else if (RefreshPlotHandler != null)
            {
                await Task.Run(RefreshPlotHandler, ct);
            }
            else
            {
                // Default dispatch / delay to simulate plot series repaint
                await Task.Delay(50, ct);
            }

            // Dispatch notification on UI thread if applicable
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => PlotRefreshed?.Invoke(this, EventArgs.Empty));
            }
            else
            {
                PlotRefreshed?.Invoke(this, EventArgs.Empty);
            }

            _logger.LogInfo($"Asynchronous plot refresh completed for WellID='{WellID}'.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during RefreshPlotAsync: {ex.Message}", ex);
            throw;
        }
        finally
        {
            IsPlotRefreshing = false;
        }
    }

    /// <summary>
    /// Synchronous plot refresh wrapper.
    /// </summary>
    public void RefreshPlot()
    {
        _ = RefreshPlotAsync(CancellationToken.None);
    }

    private bool PromptConfirmation(string message, string title)
    {
        if (ConfirmationPromptHandler != null)
        {
            return ConfirmationPromptHandler(message, title);
        }

        // Default UI Confirmation Prompt
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            return Application.Current.Dispatcher.Invoke(() =>
                MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        }

        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void ShowError(string message, string title)
    {
        if (ErrorAlertHandler != null)
        {
            ErrorAlertHandler(message, title);
            return;
        }

        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try { MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            });
            return;
        }

        try
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // Ignore in headless/test environments
        }
    }
}
