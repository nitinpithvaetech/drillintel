using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Models;

namespace DrillIntel.ViewModels;

/// <summary>
/// ViewModel driving the full-screen Advertisement Dialog popup.
/// </summary>
public partial class AdvertisementFullscreenViewModel : ObservableObject
{
    private DispatcherTimer? _autoScrollTimer;

    public event Action? RequestClose;

    public ObservableCollection<MarketingAdSlide> AdSlides { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SlideCountDisplay))]
    private int _currentSlideIndex = 0;

    [ObservableProperty]
    private MarketingAdSlide? _currentAdSlide;

    [ObservableProperty]
    private bool _isAutoScrollPaused = false;

    public string SlideCountDisplay => AdSlides.Count > 0 ? $"{CurrentSlideIndex + 1} / {AdSlides.Count}" : "1 / 1";

    public AdvertisementFullscreenViewModel(IEnumerable<MarketingAdSlide> slides, int initialIndex = 0)
    {
        foreach (var slide in slides)
        {
            // Clone or reference slides
            AdSlides.Add(new MarketingAdSlide
            {
                Index = slide.Index,
                Title = slide.Title,
                Badge = slide.Badge,
                Description = slide.Description,
                ImageSource = slide.ImageSource,
                TargetUrl = slide.TargetUrl,
                CategoryColor = slide.CategoryColor,
                AccentIcon = slide.AccentIcon,
                IsActive = false
            });
        }

        if (initialIndex >= 0 && initialIndex < AdSlides.Count)
        {
            CurrentSlideIndex = initialIndex;
        }
        else
        {
            CurrentSlideIndex = 0;
        }

        UpdateActiveSlide();
        StartAutoScrollTimer();
    }

    private void StartAutoScrollTimer()
    {
        _autoScrollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoScrollTimer.Tick += (s, e) =>
        {
            if (!IsAutoScrollPaused)
            {
                NextAdSlide();
            }
        };
        _autoScrollTimer.Start();
    }

    public void StopTimer()
    {
        if (_autoScrollTimer != null)
        {
            _autoScrollTimer.Stop();
            _autoScrollTimer = null;
        }
    }

    [RelayCommand]
    public void NextAdSlide()
    {
        if (AdSlides.Count == 0) return;
        CurrentSlideIndex = (CurrentSlideIndex + 1) % AdSlides.Count;
        UpdateActiveSlide();
    }

    [RelayCommand]
    public void PreviousAdSlide()
    {
        if (AdSlides.Count == 0) return;
        CurrentSlideIndex = (CurrentSlideIndex - 1 + AdSlides.Count) % AdSlides.Count;
        UpdateActiveSlide();
    }

    [RelayCommand]
    public void SelectAdSlide(int index)
    {
        if (index >= 0 && index < AdSlides.Count)
        {
            CurrentSlideIndex = index;
            UpdateActiveSlide();
        }
    }

    [RelayCommand]
    public void ToggleAutoScroll()
    {
        IsAutoScrollPaused = !IsAutoScrollPaused;
    }

    [RelayCommand]
    public void PauseAdScroll()
    {
        IsAutoScrollPaused = true;
    }

    [RelayCommand]
    public void ResumeAdScroll()
    {
        IsAutoScrollPaused = false;
    }

    private void UpdateActiveSlide()
    {
        for (int i = 0; i < AdSlides.Count; i++)
        {
            AdSlides[i].IsActive = (i == CurrentSlideIndex);
        }

        CurrentAdSlide = (CurrentSlideIndex >= 0 && CurrentSlideIndex < AdSlides.Count)
            ? AdSlides[CurrentSlideIndex]
            : null;
    }

    [RelayCommand]
    public void OpenEtechWebsite(string? url = null)
    {
        try
        {
            var targetUrl = !string.IsNullOrWhiteSpace(url)
                ? url
                : (CurrentAdSlide?.TargetUrl ?? "https://etechinter.com/products/vumaxpro-real-time-drilling-intelligence/");

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = targetUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdvertisementFullscreenViewModel] Error opening website: {ex}");
        }
    }

    [RelayCommand]
    public void Close()
    {
        StopTimer();
        RequestClose?.Invoke();
    }
}

