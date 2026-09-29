using System.Windows;
using DrillIntel.ViewModels;
using Fluent;

namespace DrillIntel;

public partial class MainWindow : RibbonWindow
{
    public MainWindow()
    {
        InitializeComponent();
        WindowState = WindowState.Maximized;
        DataContext = new MainViewModel(App.Session, App.ProjectService, App.RecentProjectsService);
    }
}