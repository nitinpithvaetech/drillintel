using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace DrillIntel.Models;

public partial class ChannelMapping : ObservableObject
{
    [ObservableProperty]
    private string _csvColumnHeader = string.Empty;

    [ObservableProperty]
    private string _mappedVumaxChannel = string.Empty;

    // Available standard channels to map to
    public ObservableCollection<string> AvailableChannels { get; } = new()
    {
        "Dynamic (New Column)",
        "Depth",
        "Hookload",
        "RPM",
        "Pump Pressure",
        "Torque"
    };
}
