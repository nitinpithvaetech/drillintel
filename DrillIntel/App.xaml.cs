using System;
using System.Windows;
using DrillIntel.Data;
using DrillIntel.Projects;
using DrillIntel.Services;

namespace DrillIntel;

public partial class App : Application
{
    public static IAppDatabaseService AppDatabaseService { get; } = new AppDatabaseService();
    public static IRecentProjectsService RecentProjectsService { get; } = new RecentProjectsService();
    public static ProjectSession Session { get; } = new ProjectSession();
    public static ProjectService ProjectService { get; } = new ProjectService(Session, RecentProjectsService, AppDatabaseService);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            AppDatabaseService.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize Application Database:\n\n{ex.Message}",
                "DrillIntel Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppDatabaseService.Dispose();
        Session.Dispose();
        base.OnExit(e);
    }
}

