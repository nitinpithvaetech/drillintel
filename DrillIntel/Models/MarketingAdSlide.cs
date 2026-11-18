using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DrillIntel.Models;

/// <summary>
/// Model representing a single marketing/advertising slide in the Dashboard showcase.
/// </summary>
public partial class MarketingAdSlide : ObservableObject
{
    public int Index { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Badge { get; set; } = "Featured";

    public string Description { get; set; } = string.Empty;

    public string ImageSource { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = "https://etechinter.com/products/vumaxpro-real-time-drilling-intelligence/";

    public string CategoryColor { get; set; } = "#4F46E5";

    public string AccentIcon { get; set; } = "Sparkles";

    [ObservableProperty]
    private bool _isActive;
}

