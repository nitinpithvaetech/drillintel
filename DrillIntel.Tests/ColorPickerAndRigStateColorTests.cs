using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.ViewModels;
using DrillIntel.Views;
using System.Threading;
using System.Windows.Media;
using Xunit;

namespace DrillIntel.Tests;

public class ColorPickerAndRigStateColorTests
{
    [Fact]
    public void RigStateItemModel_OnColorHexChanged_UpdatesIntegerColor()
    {
        var item = new RigStateItemModel
        {
            Number = 0,
            Name = "Rotary Drill",
            ColorHex = "#00FF00"
        };

        // Green: R=0, G=255, B=0 -> ARGB: 0xFF00FF00 = -16711936
        Assert.Equal(-16711936, item.Color);

        // Update hex to Red: #FF0000 -> 0xFFFF0000 = -65536
        item.ColorHex = "#FF0000";
        Assert.Equal(-65536, item.Color);
    }

    [Fact]
    public void RigStateItemModel_SetColor_UpdatesColorHex()
    {
        var item = new RigStateItemModel();
        item.SetColor(-65536); // Red

        Assert.Equal("#FF0000", item.ColorHex);
        Assert.Equal(-65536, item.Color);
    }

    [Fact]
    public void RigStateViewModel_OnUnknownColorHexChanged_UpdatesUnknownColor()
    {
        var vm = new RigStateViewModel
        {
            UnknownColorHex = "#0000FF" // Blue
        };

        // Blue: 0xFF0000FF = -16776961
        Assert.Equal(-16776961, vm.UnknownColor);
        Assert.Equal("#0000FF", vm.UnknownColorHex);
    }

    [Fact]
    public void RigStateService_HexConversions_AreBidirectionalAndConsistent()
    {
        string[] testHexes = { "#00FF00", "#FF0000", "#0000FF", "#FFFF00", "#FFFFFF", "#000000", "#FF6D00" };

        foreach (var hex in testHexes)
        {
            int colorInt = RigStateService.ConvertHexToColor(hex);
            string roundTripHex = RigStateService.ConvertColorToHex(colorInt);

            Assert.Equal(hex, roundTripHex);
        }
    }

    [Fact]
    public void ColorPickerDialog_Initialization_NormalizesHexAndPopulatesFields()
    {
        // STA thread required for WPF controls
        var t = new Thread(() =>
        {
            var dlg = new ColorPickerDialog("#00c853");
            Assert.Equal("#00C853", dlg.OriginalHex);
            Assert.Equal("#00C853", dlg.SelectedHex);

            var dlgNoHash = new ColorPickerDialog("FF6D00");
            Assert.Equal("#FF6D00", dlgNoHash.OriginalHex);
            Assert.Equal("#FF6D00", dlgNoHash.SelectedHex);

            var dlgNull = new ColorPickerDialog(null);
            Assert.Equal("#000000", dlgNull.OriginalHex);
            Assert.Equal("#000000", dlgNull.SelectedHex);
        });

        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
    }
}

