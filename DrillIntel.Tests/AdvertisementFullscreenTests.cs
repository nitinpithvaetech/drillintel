using System;
using System.Collections.Generic;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class AdvertisementFullscreenTests
{
    private List<MarketingAdSlide> CreateSampleSlides()
    {
        return new List<MarketingAdSlide>
        {
            new MarketingAdSlide { Index = 0, Title = "Slide 1", Badge = "KPI", ImageSource = "slide1.png" },
            new MarketingAdSlide { Index = 1, Title = "Slide 2", Badge = "Analytics", ImageSource = "slide2.png" },
            new MarketingAdSlide { Index = 2, Title = "Slide 3", Badge = "Rig State", ImageSource = "slide3.png" },
            new MarketingAdSlide { Index = 3, Title = "Slide 4", Badge = "AI Alerts", ImageSource = "slide4.png" },
            new MarketingAdSlide { Index = 4, Title = "Slide 5", Badge = "Mobile", ImageSource = "slide5.png" }
        };
    }

    [Fact]
    public void AdvertisementFullscreenViewModel_InitializesWithSlides_AndActiveIndex()
    {
        var slides = CreateSampleSlides();
        var vm = new AdvertisementFullscreenViewModel(slides, initialIndex: 2);

        try
        {
            Assert.Equal(5, vm.AdSlides.Count);
            Assert.Equal(2, vm.CurrentSlideIndex);
            Assert.NotNull(vm.CurrentAdSlide);
            Assert.Equal("Slide 3", vm.CurrentAdSlide.Title);
            Assert.Equal("3 / 5", vm.SlideCountDisplay);
            Assert.True(vm.AdSlides[2].IsActive);
            Assert.False(vm.AdSlides[0].IsActive);
        }
        finally
        {
            vm.StopTimer();
        }
    }

    [Fact]
    public void AdvertisementFullscreenViewModel_NavigationCommands_NextPreviousAndSelect()
    {
        var slides = CreateSampleSlides();
        var vm = new AdvertisementFullscreenViewModel(slides, initialIndex: 1);

        try
        {
            // Next
            vm.NextAdSlideCommand.Execute(null);
            Assert.Equal(2, vm.CurrentSlideIndex);
            Assert.Equal("Slide 3", vm.CurrentAdSlide?.Title);
            Assert.True(vm.AdSlides[2].IsActive);

            // Previous
            vm.PreviousAdSlideCommand.Execute(null);
            Assert.Equal(1, vm.CurrentSlideIndex);
            Assert.Equal("Slide 2", vm.CurrentAdSlide?.Title);

            // Select index directly
            vm.SelectAdSlide(4);
            Assert.Equal(4, vm.CurrentSlideIndex);
            Assert.Equal("Slide 5", vm.CurrentAdSlide?.Title);

            // Wrap around on Next
            vm.NextAdSlideCommand.Execute(null);
            Assert.Equal(0, vm.CurrentSlideIndex);

            // Wrap around on Previous
            vm.PreviousAdSlideCommand.Execute(null);
            Assert.Equal(4, vm.CurrentSlideIndex);

            // Toggle pause
            Assert.False(vm.IsAutoScrollPaused);
            vm.ToggleAutoScrollCommand.Execute(null);
            Assert.True(vm.IsAutoScrollPaused);
        }
        finally
        {
            vm.StopTimer();
        }
    }

    [Fact]
    public void AdvertisementFullscreenViewModel_CloseCommand_StopsTimerAndInvokesRequestClose()
    {
        var slides = CreateSampleSlides();
        var vm = new AdvertisementFullscreenViewModel(slides, initialIndex: 0);

        bool closeInvoked = false;
        vm.RequestClose += () => closeInvoked = true;

        vm.CloseCommand.Execute(null);

        Assert.True(closeInvoked);
    }

    [Fact]
    public void DashboardViewModel_OpenFullscreenAdDialogCommand_OpensDialogAndSyncsIndex()
    {
        var session = new ProjectSession();
        var dashboardVm = new DashboardViewModel(session);

        Assert.True(dashboardVm.AdSlides.Count > 0);
        Assert.Equal(0, dashboardVm.CurrentSlideIndex);

        bool dialogOpened = false;
        dashboardVm.OpenFullscreenAdDialogHandler = vm =>
        {
            dialogOpened = true;
            Assert.Equal(0, vm.CurrentSlideIndex);
            // Simulate user navigating to slide 4 in the fullscreen dialog
            vm.SelectAdSlide(4);
            return true;
        };

        dashboardVm.OpenFullscreenAdDialogCommand.Execute(null);

        Assert.True(dialogOpened);
        // Verify dashboard synced to slide 4
        Assert.Equal(4, dashboardVm.CurrentSlideIndex);
        Assert.Equal("Mobile Operations Hub", dashboardVm.CurrentAdSlide?.Title);
    }
}
