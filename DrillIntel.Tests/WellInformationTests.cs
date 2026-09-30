using System;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class WellInformationTests
{
    [Fact]
    public void CancelCommand_InvokesRequestCloseWithFalse()
    {
        var vm = new WellInformationViewModel("Test Well", "Test Field");
        bool? requestCloseResult = null;
        vm.RequestClose += (result) => requestCloseResult = result;

        vm.CancelCommand.Execute(null);

        Assert.NotNull(requestCloseResult);
        Assert.False(requestCloseResult.Value);
    }

    [Fact]
    public void SaveCommand_WhenWellNameValid_InvokesRequestCloseWithTrue()
    {
        var vm = new WellInformationViewModel("Valid Well", "Valid Field");
        bool? requestCloseResult = null;
        vm.RequestClose += (result) => requestCloseResult = result;

        Assert.True(vm.CanSave);
        vm.SaveCommand.Execute(null);

        Assert.NotNull(requestCloseResult);
        Assert.True(requestCloseResult.Value);
        Assert.Empty(vm.ErrorMessage);
    }

    [Fact]
    public void SaveCommand_WhenWellNameEmpty_SetsErrorMessage_AndDoesNotInvokeRequestClose()
    {
        var vm = new WellInformationViewModel("", "Valid Field");
        bool? requestCloseResult = null;
        vm.RequestClose += (result) => requestCloseResult = result;

        Assert.False(vm.CanSave);
        vm.SaveCommand.Execute(null);

        Assert.Null(requestCloseResult);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
    }

    [Fact]
    public void LoadFromWell_And_ApplyToWell_MaintainsConsistency()
    {
        var well = new Well
        {
            ObjectID = "W123",
            name = "Discovery Well #1",
            nameLegal = "Discovery Legal Name",
            field = "North Sea",
            operatorName = "Operator A",
            operatorDiv = "Div B",
            statusWell = "Active",
            purposeWell = "Exploration",
            numAPI = "API-999",
            numLicense = "LIC-888",
            RigName = "Rig Trident",
            dTimSpud = "2026-01-01T00:00:00",
            country = "Norway",
            state = "Rogaland",
            county = "Stavanger",
            block = "Block 1",
            district = "District 2",
            region = "Region North",
            timeZone = "UTC+1",
            wellheadElevation = 45.5,
            groundElevation = 30.2,
            waterDepth = 120.0,
            Comments = "Test well remarks"
        };

        var vm = new WellInformationViewModel(well);
        Assert.Equal("Discovery Well #1", vm.WellName);
        Assert.Equal("North Sea", vm.FieldName);
        Assert.Equal("Operator A", vm.OperatorName);
        Assert.Equal(45.5, vm.WellheadElevation);

        // Modify and reapply
        vm.WellName = "Discovery Well #1 - Updated";
        vm.ApplyToWell(well);

        Assert.Equal("Discovery Well #1 - Updated", well.name);
        Assert.Equal("North Sea", well.field);
    }
}

