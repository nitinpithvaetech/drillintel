using System;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DrillIntel.Models.TagVisualizer;

/// <summary>
/// Observable item representing an interactive phase tag on the Depth vs DateTime graph.
/// Supports interactive resizing (left/right drag handles), nudge adjustments, and database persistence.
/// </summary>
public partial class TagVisualizerItem : ObservableObject
{
    [ObservableProperty]
    private long _phaseIndex;

    [ObservableProperty]
    private string _wellId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Duration))]
    [NotifyPropertyChangedFor(nameof(DurationFormatted))]
    private DateTime _startDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Duration))]
    [NotifyPropertyChangedFor(nameof(DurationFormatted))]
    private DateTime _endDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagDisplayName))]
    [NotifyPropertyChangedFor(nameof(PhaseColor))]
    [NotifyPropertyChangedFor(nameof(FillBrush))]
    [NotifyPropertyChangedFor(nameof(BorderBrush))]
    [NotifyPropertyChangedFor(nameof(HeaderBrush))]
    private string _phaseId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagDisplayName))]
    private string _stepId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagDisplayName))]
    private string _emphId = string.Empty;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private string? _createdBy;

    [ObservableProperty]
    private DateTime? _createdDate;

    [ObservableProperty]
    private string? _modifiedBy;

    [ObservableProperty]
    private DateTime? _modifiedDate;

    [ObservableProperty]
    private int _isOpen;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private double _startDepth;

    [ObservableProperty]
    private double _endDepth;

    public TimeSpan Duration => EndDate > StartDate ? EndDate - StartDate : TimeSpan.Zero;

    public string DurationFormatted
    {
        get
        {
            var d = Duration;
            if (d.TotalDays >= 1)
                return $"{(int)d.TotalDays}d {d.Hours}h {d.Minutes}m";
            if (d.TotalHours >= 1)
                return $"{(int)d.TotalHours}h {d.Minutes}m";
            return $"{d.Minutes}m {d.Seconds}s";
        }
    }

    /// <summary>
    /// Combined name of Phase + Step + Emph matching requirement.
    /// </summary>
    public string TagDisplayName
    {
        get
        {
            var p = !string.IsNullOrWhiteSpace(PhaseId) ? PhaseId.Trim() : "PHASE";
            var s = !string.IsNullOrWhiteSpace(StepId) ? StepId.Trim() : "";
            var e = !string.IsNullOrWhiteSpace(EmphId) ? EmphId.Trim() : "";

            if (!string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(e))
                return $"{p} + {s} + {e}";
            if (!string.IsNullOrEmpty(s))
                return $"{p} + {s}";
            if (!string.IsNullOrEmpty(e))
                return $"{p} + {e}";
            return p;
        }
    }

    public Color PhaseColor => GetPhaseColor(PhaseId);

    public Brush FillBrush
    {
        get
        {
            var c = PhaseColor;
            // Translucent fill so Depth curve is visible beneath
            return new SolidColorBrush(Color.FromArgb(0x4D, c.R, c.G, c.B)); // ~30% opacity
        }
    }

    public Brush BorderBrush => new SolidColorBrush(PhaseColor);

    public Brush HeaderBrush
    {
        get
        {
            var c = PhaseColor;
            return new SolidColorBrush(Color.FromArgb(0xDD, c.R, c.G, c.B)); // ~87% opacity for header
        }
    }

    public static Color GetPhaseColor(string? phase)
    {
        if (string.IsNullOrWhiteSpace(phase)) return Color.FromRgb(0x54, 0x6E, 0x7A); // Slate

        string p = phase.ToUpperInvariant();

        if (p.Contains("DRIL"))
            return Color.FromRgb(0x2E, 0x7D, 0x32); // Forest Green

        if (p.Contains("RIG") || p.Contains("RIGUP") || p.Contains("RIG_UP"))
            return Color.FromRgb(0xEF, 0x6C, 0x00); // Amber / Orange

        if (p.Contains("CUC") || p.Contains("CLEAN") || p.Contains("CIRC"))
            return Color.FromRgb(0x7B, 0x1F, 0xA2); // Deep Purple

        if (p.Contains("OUT") || p.Contains("POOH"))
            return Color.FromRgb(0xC6, 0x28, 0x28); // Crimson Red

        if (p.Contains("IN") || p.Contains("RIH"))
            return Color.FromRgb(0x15, 0x65, 0xC0); // Deep Blue

        if (p.Contains("CASE") || p.Contains("CASING"))
            return Color.FromRgb(0x00, 0x89, 0x7B); // Teal

        if (p.Contains("LOG") || p.Contains("WIRE"))
            return Color.FromRgb(0x00, 0x97, 0xA7); // Cyan

        if (p.Contains("REAM") || p.Contains("WIPER"))
            return Color.FromRgb(0xAD, 0x14, 0x57); // Pink/Rose

        return Color.FromRgb(0x45, 0x5A, 0x64); // Dark Blue Grey default
    }

    public TagVisualizerItem Clone()
    {
        return new TagVisualizerItem
        {
            PhaseIndex = this.PhaseIndex,
            WellId = this.WellId,
            StartDate = this.StartDate,
            EndDate = this.EndDate,
            PhaseId = this.PhaseId,
            StepId = this.StepId,
            EmphId = this.EmphId,
            Notes = this.Notes,
            CreatedBy = this.CreatedBy,
            CreatedDate = this.CreatedDate,
            ModifiedBy = this.ModifiedBy,
            ModifiedDate = this.ModifiedDate,
            IsOpen = this.IsOpen,
            StartDepth = this.StartDepth,
            EndDepth = this.EndDepth,
            IsSelected = this.IsSelected,
            IsModified = this.IsModified
        };
    }
}

