using System.Configuration;
using System.Data;
using System.Windows;
using DrillIntel.Projects;
using DrillIntel.Services;

namespace DrillIntel;

public partial class App : Application
{
    public static IRecentProjectsService RecentProjectsService { get; } = new RecentProjectsService();
    public static ProjectSession Session { get; } = new ProjectSession();
    public static ProjectService ProjectService { get; } = new ProjectService(Session, RecentProjectsService);
}
