using System;
using System.IO;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Projects;
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

    [Fact]
    public async Task MainViewModel_OpenWellEditorCommand_CanExecute_And_SavesWell()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"main_well_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);

        try
        {
            var session = new ProjectSession();
            var dummyProjectService = new DummyProjectService();
            var mainVm = new MainViewModel(session, dummyProjectService);

            // Initially no project open
            Assert.False(mainVm.OpenWellEditorCommand.CanExecute(null));
            Assert.False(mainVm.EditWellCommand.CanExecute(null));

            // Load project
            session.Load(tempDb);
            Assert.True(mainVm.OpenWellEditorCommand.CanExecute(null));
            Assert.True(mainVm.EditWellCommand.CanExecute(null));

            // Setup dialog handler
            mainVm.OpenEditWellDialogHandler = vm =>
            {
                vm.WellName = "Alpha Drill Site #1";
                vm.FieldName = "Deep Sea Field";
                return true;
            };

            await mainVm.OpenWellEditorCommand.ExecuteAsync(null);

            var repo = new WellDataRepository(session);
            var savedWell = await repo.GetProjectWellAsync();
            Assert.NotNull(savedWell);
            Assert.Equal("Alpha Drill Site #1", savedWell.name);
            Assert.Equal("Deep Sea Field", savedWell.field);
        }
        finally
        {
            try
            {
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
            catch { }
        }
    }

    [Fact]
    public void ResetCommand_RestoresLoadedWellValues()
    {
        var well = new Well
        {
            ObjectID = "W-RESTORE",
            name = "Baseline Well",
            field = "Baseline Field",
            RigName = "Rig Alpha",
            latitude = 25.1234,
            longitude = 55.4321,
            SEC = "12",
            TWP = "34",
            RGE = "56",
            Pump1Model = "Triplex-1600",
            Comments = "Initial notes"
        };

        var vm = new WellInformationViewModel(well);
        Assert.Equal("Baseline Well", vm.WellName);

        // Mutate fields
        vm.WellName = "Changed Name";
        vm.FieldName = "Changed Field";
        vm.RigName = "Rig Beta";
        vm.Latitude = 99.99;
        vm.Pump1Model = "MudPump-X";
        vm.Comments = "Modified notes";

        // Execute Reset
        vm.ResetCommand.Execute(null);

        Assert.Equal("Baseline Well", vm.WellName);
        Assert.Equal("Baseline Field", vm.FieldName);
        Assert.Equal("Rig Alpha", vm.RigName);
        Assert.Equal(25.1234, vm.Latitude);
        Assert.Equal("Triplex-1600", vm.Pump1Model);
        Assert.Equal("Initial notes", vm.Comments);
        Assert.True(vm.IsStatusVisible);
        Assert.False(vm.IsStatusError);
    }

    [Fact]
    public void SaveCommand_WhenInvalid_SetsStatusAlertState()
    {
        var vm = new WellInformationViewModel("", "Test Field");
        vm.SaveCommand.Execute(null);

        Assert.True(vm.IsStatusVisible);
        Assert.True(vm.IsStatusError);
        Assert.Contains("Well Name", vm.StatusMessage);

        // Typing a name clears error banner
        vm.WellName = "Valid Name Now";
        Assert.False(vm.IsStatusVisible);
    }

    [Fact]
    public void ExtendedParameters_LoadAndApply_MaintainsConsistency()
    {
        var well = new Well
        {
            Contractor = "Drilling Corp",
            pcInterest = "75%",
            DrillingSupr = "John Doe",
            DrillingEng = "Jane Smith",
            Rep = "Bob Lee",
            ToolPusher = "Jim Brown",
            DrlgEngDept = "Engineering",
            DrlgOpDept = "Operations",
            dTimeLicense = "15-Jan-2026",
            dTimPa = "30-Dec-2026",
            TightHoleNo = "TH-001",
            ReEntryNo = "RE-002",
            Objective = "Target Oil Zone",
            TDDate = "28-Feb-2026",
            TDFormation = "Sandstone B",
            PlannedDepth = 12500,
            PlannedDays = 45,
            RigType = "Jackup",
            ContType = "Daywork",
            EDRProvider = "Pason",
            DataSource = "WITSML",
            RigCost = 45000,
            Historical = true,
            PipeLength = 31.5,
            StandLength = 93.2,
            DrlgConnTime = 120,
            TripConnTime = 90,
            BTSTime = 15,
            STSTime = 18,
            STBTime = 12,
            TripInSpeed = 40,
            TripOutSpeed = 38,
            Pump1Model = "Model A",
            Pump1Stroke = "12",
            Pump1Liner = "6",
            Pump2Model = "Model B",
            Pump2Stroke = "10",
            Pump2Liner = "5.5",
            Pump3Model = "Model C",
            Pump3Stroke = "14",
            Pump3Liner = "7"
        };

        var vm = new WellInformationViewModel(well);
        Assert.Equal("Drilling Corp", vm.Contractor);
        Assert.Equal("75%", vm.PcInterest);
        Assert.Equal("John Doe", vm.DrillingSupr);
        Assert.Equal("Sandstone B", vm.TdFormation);
        Assert.Equal(12500, vm.PlannedDepth);
        Assert.Equal("Jackup", vm.RigType);
        Assert.True(vm.Historical);
        Assert.Equal("Model A", vm.Pump1Model);

        var targetWell = new Well();
        vm.ApplyToWell(targetWell);

        Assert.Equal("Drilling Corp", targetWell.Contractor);
        Assert.Equal("75%", targetWell.pcInterest);
        Assert.Equal("John Doe", targetWell.DrillingSupr);
        Assert.Equal("Sandstone B", targetWell.TDFormation);
        Assert.Equal(12500, targetWell.PlannedDepth);
        Assert.Equal("Jackup", targetWell.RigType);
        Assert.True(targetWell.Historical);
        Assert.Equal("Model A", targetWell.Pump1Model);
        Assert.Equal("12", targetWell.Pump1Stroke);
        Assert.Equal("6", targetWell.Pump1Liner);
    }

    private class DummyProjectService : IProjectService
    {
        public bool CreateNewProject() => true;
        public Task<bool> CreateNewProjectAsync() => Task.FromResult(true);
        public bool OpenProject() => true;
        public bool OpenProject(string filePath) => true;
    }
}

