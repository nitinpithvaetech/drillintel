using System;
using System.IO;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using DrillIntel.ViewModels;
using Dapper;
using Xunit;

namespace DrillIntel.Tests;

public class SyncDataWithParentTimelogTests
{
    private class DummyProjectService : IProjectService
    {
        public bool CreateNewProject() => true;
        public Task<bool> CreateNewProjectAsync() => Task.FromResult(true);
        public bool OpenProject() => true;
        public bool OpenProject(string filePath) => true;
    }

    [Fact]
    public void RibbonCommand_IsEnabled_OnlyWhenTimelogSelectedFromWellTree()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_ribbon_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            var dummyProjectService = new DummyProjectService();
            var mainVm = new MainViewModel(session, dummyProjectService);

            // 1. When no project is open -> Command cannot execute
            Assert.False(mainVm.CanSyncDataToParentTimelog);
            Assert.False(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));

            // 2. Load project -> DashboardViewModel is active, but no tree node selected yet
            session.Load(tempDb);
            Assert.True(session.IsProjectOpen);
            Assert.IsType<DashboardViewModel>(mainVm.CurrentViewModel);

            var dashboardVm = (DashboardViewModel)mainVm.CurrentViewModel;
            Assert.Null(dashboardVm.SelectedNode);
            Assert.False(mainVm.CanSyncDataToParentTimelog);
            Assert.False(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));

            // 3. User selects a Well node in the tree -> Command should STILL be disabled
            var wellNode = new WellTreeNode
            {
                Name = "Camel1_2016",
                Type = WellTreeNodeType.Well
            };
            dashboardVm.OnTreeNodeSelected(wellNode);
            Assert.False(mainVm.CanSyncDataToParentTimelog);
            Assert.False(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));

            // 4. User selects a DepthLog node in the tree -> Command should STILL be disabled
            var depthLogNode = new WellTreeNode
            {
                Name = "DepthLog 1",
                Type = WellTreeNodeType.DepthLog
            };
            dashboardVm.OnTreeNodeSelected(depthLogNode);
            Assert.False(mainVm.CanSyncDataToParentTimelog);
            Assert.False(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));

            // 5. User selects a TimeLog node in the tree -> Command MUST BE ENABLED!
            var timeLog = new TimeLog
            {
                ObjectID = "TL_001",
                nameLog = "Source Time log",
                nameWell = "Camel1_2016",
                nameWellbore = "Wellbore1"
            };
            var timeLogNode = new WellTreeNode
            {
                Name = timeLog.nameLog,
                Type = WellTreeNodeType.TimeLog,
                Tag = timeLog
            };
            dashboardVm.OnTreeNodeSelected(timeLogNode);
            Assert.True(mainVm.CanSyncDataToParentTimelog);
            Assert.True(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));

            // 6. User deselects or selects folder -> Command disables again
            var folderNode = new WellTreeNode
            {
                Name = "Timelogs",
                Type = WellTreeNodeType.Folder
            };
            dashboardVm.OnTreeNodeSelected(folderNode);
            Assert.False(mainVm.CanSyncDataToParentTimelog);
            Assert.False(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));
        }
        finally
        {
            try
            {
                session.Close();
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
            catch { }
        }
    }

    [Fact]
    public async Task RibbonCommand_Execution_OpensDialogWithTargetInformation()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_dialog_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var dummyProjectService = new DummyProjectService();
            var mainVm = new MainViewModel(session, dummyProjectService);

            var dashboardVm = (DashboardViewModel)mainVm.CurrentViewModel;
            var timeLog = new TimeLog
            {
                ObjectID = "TL_SRC",
                nameLog = "Child Timelog",
                nameWell = "Camel1_2016",
                nameWellbore = "Wellbore1",
                LinkToParent = true,
                LinkWellID = "Camel1_2016",
                LinkWellboreID = "Wellbore1",
                LinkLogID = "RS Time log",
                startIndex = "2026-06-01 00:00:00",
                endIndex = "2026-06-05 12:00:00"
            };
            var node = new WellTreeNode
            {
                Name = timeLog.nameLog,
                Type = WellTreeNodeType.TimeLog,
                Tag = timeLog
            };
            dashboardVm.OnTreeNodeSelected(node);

            SyncDataWithParentTimelogViewModel? dialogVm = null;
            mainVm.OpenSyncDataDialogHandler = vm =>
            {
                dialogVm = vm;
                return true;
            };

            await mainVm.SyncDataToParentTimelogCommand.ExecuteAsync(null);

            Assert.NotNull(dialogVm);
            Assert.Equal("Camel1_2016", dialogVm.TargetWellName);
            Assert.Equal("Wellbore1", dialogVm.TargetWellboreName);
            Assert.Equal("RS Time log", dialogVm.TargetTimeLogName);
        }
        finally
        {
            try
            {
                session.Close();
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
            catch { }
        }
    }

    [Fact]
    public async Task ViewModel_StartSync_Succeeds_AndDisplaysSuccessMessage()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_vm_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var timeLog = new TimeLog
            {
                ObjectID = "TL_TEST",
                nameLog = "Source Log",
                nameWell = "Camel1_2016",
                nameWellbore = "Wellbore1",
                startIndex = "2026-05-01 08:00:00",
                endIndex = "2026-05-03 18:00:00"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, timeLog);
            await vm.InitializeAsync();

            Assert.Equal("Camel1_2016", vm.TargetWellName);
            Assert.Equal("Wellbore1", vm.TargetWellboreName);
            Assert.False(string.IsNullOrWhiteSpace(vm.TargetTimeLogName));

            // Initial state
            Assert.True(vm.CanStart);
            Assert.False(vm.IsRunning);
            Assert.False(vm.IsCompleted);
            Assert.False(vm.HasSuccess);

            // Execute sync
            await vm.StartSyncCommand.ExecuteAsync(null);

            // Verify success state & message
            Assert.True(vm.IsCompleted);
            Assert.True(vm.HasSuccess);
            Assert.False(vm.HasError);
            Assert.Equal(100, vm.ProgressPercent);
            Assert.Equal("Data synced successfully to Parent Timelog.", vm.SuccessMessage);

            // Verify close closes dialog
            bool closeCalled = false;
            vm.RequestClose += success =>
            {
                closeCalled = true;
                Assert.True(success);
            };
            vm.CloseCommand.Execute(null);
            Assert.True(closeCalled);
        }
        finally
        {
            try
            {
                session.Close();
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
            catch { }
        }
    }

    [Fact]
    public async Task ViewModel_InvalidDateRange_ShowsError_AndAllowsRetry()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_err_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var timeLog = new TimeLog
            {
                ObjectID = "TL_TEST2",
                nameLog = "Source Log 2",
                nameWell = "Camel1_2016",
                nameWellbore = "Wellbore1"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, timeLog);
            await vm.InitializeAsync();

            // Set invalid range: From Date > To Date
            vm.FromDate = DateTime.Today.AddDays(5);
            vm.ToDate = DateTime.Today.AddDays(1);

            await vm.StartSyncCommand.ExecuteAsync(null);

            Assert.True(vm.HasError);
            Assert.False(vm.HasSuccess);
            Assert.Contains("Invalid Date Range", vm.ErrorMessage);
            Assert.True(vm.CanStart); // Ready for retry

            // Fix the range and retry
            vm.FromDate = DateTime.Today.AddDays(1);
            vm.ToDate = DateTime.Today.AddDays(5);

            await vm.StartSyncCommand.ExecuteAsync(null);

            Assert.False(vm.HasError);
            Assert.True(vm.HasSuccess);
            Assert.Equal("Data synced successfully to Parent Timelog.", vm.SuccessMessage);
        }
        finally
        {
            try
            {
                session.Close();
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
            catch { }
        }
    }

    [Fact]
    public async Task ViewModel_ResolvesWellNameAndWellboreName_InsteadOfIDs()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_names_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            // Insert into VMX_WELL and VMX_WELLBORE with IDs and friendly names
            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W-999', 'Camel1_2016');");
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB-888', 'W-999', 'Wellbore1');");
            conn.Execute("INSERT INTO VMX_TIME_LOG (LOG_ID, WELL_ID, WELLBORE_ID, LOG_NAME) VALUES ('TL-777', 'W-999', 'WB-888', 'RS Time log');");

            var repo = new WellDataRepository(session);

            // Child timelog has raw IDs stored in LinkWellID and LinkWellboreID
            var childTimeLog = new TimeLog
            {
                ObjectID = "TL-CHILD",
                nameLog = "Child Log",
                WellID = "W-999",
                WellboreID = "WB-888",
                nameWell = "",     // Empty, only WellID is set
                nameWellbore = "", // Empty, only WellboreID is set
                LinkToParent = true,
                LinkWellID = "W-999",
                LinkWellboreID = "WB-888",
                LinkLogID = "TL-777"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, childTimeLog, repo);
            await vm.InitializeAsync();

            // Verify human-readable names are displayed instead of IDs!
            Assert.Equal("Camel1_2016", vm.TargetWellName);
            Assert.NotEqual("W-999", vm.TargetWellName);

            Assert.Equal("Wellbore1", vm.TargetWellboreName);
            Assert.NotEqual("WB-888", vm.TargetWellboreName);

            Assert.Equal("RS Time log", vm.TargetTimeLogName);
            Assert.NotEqual("TL-777", vm.TargetTimeLogName);

            // Verify source well and wellbore also resolved to names instead of IDs
            Assert.Equal("Camel1_2016", vm.SourceWellName);
            Assert.Equal("Wellbore1", vm.SourceWellboreName);
        }
        finally
        {
            try
            {
                session.Close();
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
            catch { }
        }
    }

    [Fact]
    public async Task ViewModel_WhenTimelogNotLinkedOrEmptyLinkLogId_DisplaysSelectedTimelogName()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_timelog_name_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            string wellId = Guid.NewGuid().ToString();
            string wellboreId = Guid.NewGuid().ToString();
            string logId = Guid.NewGuid().ToString();

            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES (@wId, 'Prath3');", new { wId = wellId });
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES (@wbId, @wId, 'Prath3');", new { wbId = wellboreId, wId = wellId });
            conn.Execute("INSERT INTO VMX_TIME_LOG (LOG_ID, WELL_ID, WELLBORE_ID, LOG_NAME, LINK_TO_PARENT, LINK_LOG_ID) VALUES (@lId, @wId, @wbId, 'time1', 1, '');", new { lId = logId, wId = wellId, wbId = wellboreId });

            var repo = new WellDataRepository(session);

            // Case A: LinkToParent is true, but LinkLogID is empty (like in user's Prath3 project)
            var logWithEmptyParent = new TimeLog
            {
                ObjectID = logId,
                nameLog = "time1",
                WellID = wellId,
                WellboreID = wellboreId,
                LinkToParent = true,
                LinkLogID = ""
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, logWithEmptyParent, repo);
            await vm.InitializeAsync();

            Assert.Equal("Prath3", vm.TargetWellName);
            Assert.Equal("Prath3", vm.TargetWellboreName);
            Assert.Equal("time1", vm.TargetTimeLogName);
            Assert.NotEqual("RS Time log", vm.TargetTimeLogName);

            // Case B: Unlinked timelog (LinkToParent is false, like in user's Prath2 project)
            var unlinkedLog = new TimeLog
            {
                ObjectID = Guid.NewGuid().ToString(),
                nameLog = "Time",
                WellID = wellId,
                WellboreID = wellboreId,
                LinkToParent = false,
                LinkLogID = ""
            };

            var vm2 = new SyncDataWithParentTimelogViewModel(session, unlinkedLog, repo);
            await vm2.InitializeAsync();

            Assert.Equal("Prath3", vm2.TargetWellName);
            Assert.Equal("Prath3", vm2.TargetWellboreName);
            Assert.Equal("Time", vm2.TargetTimeLogName);
            Assert.NotEqual("RS Time log", vm2.TargetTimeLogName);
        }
        finally
        {
            try
            {
                session.Close();
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
            catch { }
        }
    }
}

