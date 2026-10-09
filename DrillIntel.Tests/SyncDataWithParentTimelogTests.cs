using System;
using System.IO;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
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

            // 5. User selects an unlinked TimeLog node in the tree -> Command must be DISABLED!
            var timeLog = new TimeLog
            {
                ObjectID = "TL_001",
                nameLog = "Source Time log",
                nameWell = "Camel1_2016",
                nameWellbore = "Wellbore1",
                LinkToParent = false
            };
            var timeLogNode = new WellTreeNode
            {
                Name = timeLog.nameLog,
                Type = WellTreeNodeType.TimeLog,
                Tag = timeLog
            };
            dashboardVm.OnTreeNodeSelected(timeLogNode);
            Assert.False(timeLogNode.IsLinkedTimeLog);
            Assert.False(mainVm.CanSyncDataToParentTimelog);
            Assert.False(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));

            // 5b. When timelog is properly linked with another timelog in Tab-3 -> Command MUST BE ENABLED!
            timeLog.LinkToParent = true;
            timeLog.LinkWellID = "Camel1_2016";
            timeLog.LinkWellboreID = "Wellbore1";
            timeLog.LinkLogID = "TL_PARENT";
            timeLogNode.NotifyLinkedStatusChanged();
            Assert.True(timeLogNode.IsLinkedTimeLog);
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

    [Fact]
    public void CombinedDateTimeField_ProvidesFormattedText_AndSupportsTwoWayBinding()
    {
        var session = new ProjectSession();
        var timeLog = new TimeLog
        {
            ObjectID = "TL_COMBINED",
            nameLog = "Combined Log",
            nameWell = "WellA",
            nameWellbore = "WB1"
        };

        var vm = new SyncDataWithParentTimelogViewModel(session, timeLog);

        // Set combined From and To dates directly
        var testFrom = new DateTime(2026, 6, 15, 8, 30, 0);
        var testTo = new DateTime(2026, 6, 20, 17, 45, 0);

        vm.FromDateTime = testFrom;
        vm.ToDateTime = testTo;

        // Verify underlying Date and Time properties updated
        Assert.Equal(new DateTime(2026, 6, 15), vm.FromDate);
        Assert.Equal(testFrom, vm.FromDateTime);
        Assert.Equal(new DateTime(2026, 6, 20), vm.ToDate);
        Assert.Equal(testTo, vm.ToDateTime);

        // Verify formatting adheres to DD/MM/YYYY HH:MM
        Assert.Equal("15/06/2026 08:30", vm.FromDateTimeText);
        Assert.Equal("20/06/2026 17:45", vm.ToDateTimeText);
    }

    [Fact]
    public void CombinedDateTimeField_DirectTextEditing_ParsesMultipleDateFormats()
    {
        var session = new ProjectSession();
        var timeLog = new TimeLog
        {
            ObjectID = "TL_PARSE",
            nameLog = "Parse Log",
            nameWell = "WellA",
            nameWellbore = "WB1"
        };

        var vm = new SyncDataWithParentTimelogViewModel(session, timeLog);

        // 1. Slash format (DD/MM/YYYY HH:MM)
        vm.FromDateTimeText = "05/11/2026 14:15";
        Assert.Equal(new DateTime(2026, 11, 5, 14, 15, 0), vm.FromDateTime);
        Assert.Equal("05/11/2026 14:15", vm.FromDateTimeText);

        // 2. Hyphen format (DD-MM-YYYY HH:MM)
        vm.ToDateTimeText = "10-12-2026 23:59";
        Assert.Equal(new DateTime(2026, 12, 10, 23, 59, 0), vm.ToDateTime);
        Assert.Equal("10/12/2026 23:59", vm.ToDateTimeText);

        // 3. ISO format (YYYY-MM-DD HH:MM)
        vm.FromDateTimeText = "2026-07-04 09:00";
        Assert.Equal(new DateTime(2026, 7, 4, 9, 0, 0), vm.FromDateTime);
        Assert.Equal("04/07/2026 09:00", vm.FromDateTimeText);
    }

    [Fact]
    public void CombinedDateTimeField_PresetChanges_UpdatesBothCombinedFieldsInstantly()
    {
        var session = new ProjectSession();
        var timeLog = new TimeLog
        {
            ObjectID = "TL_PRESET",
            nameLog = "Preset Log",
            nameWell = "WellA",
            nameWellbore = "WB1",
            startIndex = "2026-06-01 00:00:00",
            endIndex = "2026-06-10 18:00:00"
        };

        var vm = new SyncDataWithParentTimelogViewModel(session, timeLog);

        // Test All Available Data preset
        vm.SelectedDateRangePreset = "All Available Data";
        Assert.Equal("01/06/2026 00:00", vm.FromDateTimeText);
        Assert.Equal("10/06/2026 18:00", vm.ToDateTimeText);

        // Test Last 24 Hours preset
        vm.SelectedDateRangePreset = "Last 24 Hours";
        Assert.Equal(vm.ToDateTime.AddHours(-24), vm.FromDateTime);

        // Test Last 7 Days preset
        vm.SelectedDateRangePreset = "Last 7 Days";
        Assert.Equal(vm.ToDateTime.AddDays(-7), vm.FromDateTime);
    }

    [Theory]
    [InlineData("25/12/2026 10:30", true, 2026, 12, 25, 10, 30)]
    [InlineData("25-12-2026 10:30", true, 2026, 12, 25, 10, 30)]
    [InlineData("2026-12-25 10:30", true, 2026, 12, 25, 10, 30)]
    [InlineData("25/12/2026 10:30:45", true, 2026, 12, 25, 10, 30)]
    [InlineData("25/12/2026", true, 2026, 12, 25, 0, 0)]
    [InlineData("invalid-date-string", false, 0, 0, 0, 0, 0)]
    [InlineData("", false, 0, 0, 0, 0, 0)]
    public void DateTimePicker_TryParseDateTime_HandlesAllStandardFormatsCorrectly(
        string input, bool expectedSuccess, int year, int month, int day, int hour, int minute)
    {
        bool success = DrillIntel.Controls.DateTimePicker.TryParseDateTime(input, out var parsed);
        Assert.Equal(expectedSuccess, success);

        if (expectedSuccess)
        {
            Assert.Equal(year, parsed.Year);
            Assert.Equal(month, parsed.Month);
            Assert.Equal(day, parsed.Day);
            Assert.Equal(hour, parsed.Hour);
            Assert.Equal(minute, parsed.Minute);
        }
    }

    [Fact]
    public void WellTreeNode_IsLinkedTimeLog_EnforcesAllTab3LinkageCriteria()
    {
        // 1. Non-TimeLog nodes are never considered linked
        var wellNode = new WellTreeNode { Type = WellTreeNodeType.Well, Name = "Well1" };
        Assert.False(wellNode.IsLinkedTimeLog);

        var depthNode = new WellTreeNode { Type = WellTreeNodeType.DepthLog, Name = "DepthLog1" };
        Assert.False(depthNode.IsLinkedTimeLog);

        var folderNode = new WellTreeNode { Type = WellTreeNodeType.Folder, Name = "Timelogs" };
        Assert.False(folderNode.IsLinkedTimeLog);

        // 2. TimeLog node with no tag
        var nodeNoTag = new WellTreeNode { Type = WellTreeNodeType.TimeLog, Name = "Log1" };
        Assert.False(nodeNoTag.IsLinkedTimeLog);

        // 3. TimeLog node with LinkToParent = false
        var tlUnchecked = new TimeLog
        {
            ObjectID = "TL_1",
            nameLog = "Log 1",
            LinkToParent = false,
            LinkWellID = "W1",
            LinkWellboreID = "WB1",
            LinkLogID = "TL_PARENT"
        };
        var nodeUnchecked = new WellTreeNode { Type = WellTreeNodeType.TimeLog, Tag = tlUnchecked };
        Assert.False(nodeUnchecked.IsLinkedTimeLog);

        // 4. TimeLog node with LinkToParent = true, but missing WellName/WellID
        var tlMissingWell = new TimeLog
        {
            ObjectID = "TL_1",
            LinkToParent = true,
            LinkWellID = "   ",
            LinkWellboreID = "WB1",
            LinkLogID = "TL_PARENT"
        };
        var nodeMissingWell = new WellTreeNode { Type = WellTreeNodeType.TimeLog, Tag = tlMissingWell };
        Assert.False(nodeMissingWell.IsLinkedTimeLog);

        // 5. TimeLog node with LinkToParent = true, but missing WellboreName/WellboreID
        var tlMissingWb = new TimeLog
        {
            ObjectID = "TL_1",
            LinkToParent = true,
            LinkWellID = "W1",
            LinkWellboreID = "",
            LinkLogID = "TL_PARENT"
        };
        var nodeMissingWb = new WellTreeNode { Type = WellTreeNodeType.TimeLog, Tag = tlMissingWb };
        Assert.False(nodeMissingWb.IsLinkedTimeLog);

        // 6. TimeLog node with LinkToParent = true, but missing Parent Timelog
        var tlMissingLog = new TimeLog
        {
            ObjectID = "TL_1",
            LinkToParent = true,
            LinkWellID = "W1",
            LinkWellboreID = "WB1",
            LinkLogID = ""
        };
        var nodeMissingLog = new WellTreeNode { Type = WellTreeNodeType.TimeLog, Tag = tlMissingLog };
        Assert.False(nodeMissingLog.IsLinkedTimeLog);

        // 7. TimeLog node linking to itself
        var tlSelfLink = new TimeLog
        {
            ObjectID = "TL_1",
            LinkToParent = true,
            LinkWellID = "W1",
            LinkWellboreID = "WB1",
            LinkLogID = "TL_1"
        };
        var nodeSelfLink = new WellTreeNode { Type = WellTreeNodeType.TimeLog, Tag = tlSelfLink };
        Assert.False(nodeSelfLink.IsLinkedTimeLog);

        // 8. Properly linked TimeLog node meeting all Tab-3 criteria
        var tlValid = new TimeLog
        {
            ObjectID = "TL_CHILD",
            nameLog = "Child Log",
            LinkToParent = true,
            LinkWellID = "Well_A",
            LinkWellboreID = "WB_A",
            LinkLogID = "TL_PARENT"
        };
        var nodeValid = new WellTreeNode { Type = WellTreeNodeType.TimeLog, Tag = tlValid };
        Assert.True(nodeValid.IsLinkedTimeLog);
    }

    [Fact]
    public void DashboardViewModel_CanSyncDataToParentTimelog_EvaluatesCorrectly()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_db_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            var repo = new WellDataRepository(session);
            var vm = new DashboardViewModel(session, repo);

            // No project open
            var unlinkedNode = new WellTreeNode
            {
                Type = WellTreeNodeType.TimeLog,
                Tag = new TimeLog { ObjectID = "TL1", LinkToParent = false }
            };
            var linkedNode = new WellTreeNode
            {
                Type = WellTreeNodeType.TimeLog,
                Tag = new TimeLog
                {
                    ObjectID = "TL2",
                    LinkToParent = true,
                    LinkWellID = "W1",
                    LinkWellboreID = "WB1",
                    LinkLogID = "TL_P"
                }
            };

            Assert.False(vm.CanSyncDataToParentTimelog(linkedNode));

            // Load project
            session.Load(tempDb);

            // Target node unlinked -> cannot sync
            Assert.False(vm.CanSyncDataToParentTimelog(unlinkedNode));
            Assert.False(vm.SyncDataToParentTimelogCommand.CanExecute(unlinkedNode));

            // Target node linked -> can sync
            Assert.True(vm.CanSyncDataToParentTimelog(linkedNode));
            Assert.True(vm.SyncDataToParentTimelogCommand.CanExecute(linkedNode));

            // Null node argument falls back to SelectedNode
            vm.OnTreeNodeSelected(unlinkedNode);
            Assert.False(vm.CanSyncDataToParentTimelog(null));
            Assert.False(vm.SyncDataToParentTimelogCommand.CanExecute(null));

            vm.OnTreeNodeSelected(linkedNode);
            Assert.True(vm.CanSyncDataToParentTimelog(null));
            Assert.True(vm.SyncDataToParentTimelogCommand.CanExecute(null));
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
    public void MainViewModel_DynamicallyUpdatesAvailability_WhenTimelogSelectionChanges()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_dynamic_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var dummyProjectService = new DummyProjectService();
            var mainVm = new MainViewModel(session, dummyProjectService);
            var dashboardVm = (DashboardViewModel)mainVm.CurrentViewModel;

            var unlinkedNode = new WellTreeNode
            {
                Name = "Child_Unlinked",
                Type = WellTreeNodeType.TimeLog,
                Tag = new TimeLog
                {
                    ObjectID = "TL_RAW",
                    nameLog = "Child_Unlinked",
                    LinkToParent = false
                }
            };

            var linkedNode = new WellTreeNode
            {
                Name = "Child_Linked",
                Type = WellTreeNodeType.TimeLog,
                Tag = new TimeLog
                {
                    ObjectID = "TL_SYNC",
                    nameLog = "Child_Linked",
                    LinkToParent = true,
                    LinkWellID = "Well_1",
                    LinkWellboreID = "Wellbore_1",
                    LinkLogID = "TL_PARENT"
                }
            };

            // 1. Select unlinked timelog -> processing menu command is disabled
            dashboardVm.OnTreeNodeSelected(unlinkedNode);
            Assert.False(mainVm.CanSyncDataToParentTimelog);
            Assert.False(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));

            // 2. Select linked timelog -> processing menu command is dynamically enabled
            dashboardVm.OnTreeNodeSelected(linkedNode);
            Assert.True(mainVm.CanSyncDataToParentTimelog);
            Assert.True(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));

            // 3. Switch back to unlinked timelog -> dynamically disabled again
            dashboardVm.OnTreeNodeSelected(unlinkedNode);
            Assert.False(mainVm.CanSyncDataToParentTimelog);
            Assert.False(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));

            // 4. Link the unlinked timelog and notify -> dynamically becomes enabled
            ((TimeLog)unlinkedNode.Tag).LinkToParent = true;
            ((TimeLog)unlinkedNode.Tag).LinkWellID = "Well_1";
            ((TimeLog)unlinkedNode.Tag).LinkWellboreID = "Wellbore_1";
            ((TimeLog)unlinkedNode.Tag).LinkLogID = "TL_PARENT";
            unlinkedNode.NotifyLinkedStatusChanged();

            Assert.True(unlinkedNode.IsLinkedTimeLog);
            Assert.True(mainVm.CanSyncDataToParentTimelog);
            Assert.True(mainVm.SyncDataToParentTimelogCommand.CanExecute(null));
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
    public async Task StartButton_IsEnabled_OnlyWhenTimelogSelectedInDialog()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_btn_enable_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W-1', 'Well1');");
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB-1', 'W-1', 'Wellbore1');");
            conn.Execute("INSERT INTO VMX_TIME_LOG (LOG_ID, WELL_ID, WELLBORE_ID, LOG_NAME) VALUES ('TL-1', 'W-1', 'WB-1', 'Child Log');");
            conn.Execute("INSERT INTO VMX_TIME_LOG (LOG_ID, WELL_ID, WELLBORE_ID, LOG_NAME) VALUES ('TL-2', 'W-1', 'WB-1', 'Parent Log');");

            var repo = new WellDataRepository(session);
            var childTimeLog = new TimeLog
            {
                ObjectID = "TL-1",
                nameLog = "Child Log",
                WellID = "W-1",
                WellboreID = "WB-1",
                LinkToParent = true,
                LinkLogID = "TL-2"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, childTimeLog, repo);
            await vm.InitializeAsync();

            // When initialized with a linked parent log, Timelog is selected -> Start button is enabled
            Assert.True(vm.HasSelectedTimelog);
            Assert.True(vm.CanStart);
            Assert.True(vm.StartSyncCommand.CanExecute(null));

            // When user clears the selection
            vm.SelectedParentTimeLog = null;
            vm.TargetTimeLogName = "";

            Assert.False(vm.HasSelectedTimelog);
            Assert.False(vm.CanStart);
            Assert.False(vm.StartSyncCommand.CanExecute(null));

            // When user selects placeholder options "(None)" or "(Select Timelog)"
            vm.TargetTimeLogName = "(None)";
            Assert.False(vm.HasSelectedTimelog);
            Assert.False(vm.CanStart);
            Assert.False(vm.StartSyncCommand.CanExecute(null));

            vm.TargetTimeLogName = "(Select Timelog)";
            Assert.False(vm.HasSelectedTimelog);
            Assert.False(vm.CanStart);
            Assert.False(vm.StartSyncCommand.CanExecute(null));

            // When user re-selects a Parent Timelog from the dropdown
            var option = vm.AvailableParentTimeLogs.FirstOrDefault(o => o.LogId == "TL-2");
            Assert.NotNull(option);
            vm.SelectedParentTimeLog = option;

            Assert.True(vm.HasSelectedTimelog);
            Assert.True(vm.CanStart);
            Assert.True(vm.StartSyncCommand.CanExecute(null));
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
    public async Task StartButton_IsDisabled_WhileRunning()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_running_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var timeLog = new TimeLog
            {
                ObjectID = "TL-R1",
                nameLog = "Run Log",
                nameWell = "Well1",
                nameWellbore = "WB1"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, timeLog);
            await vm.InitializeAsync();

            Assert.True(vm.CanStart);
            Assert.True(vm.StartSyncCommand.CanExecute(null));
            Assert.False(vm.IsRunning);
            Assert.Equal("Close", vm.CloseButtonText);

            // Simulate running state
            vm.IsRunning = true;
            Assert.False(vm.CanStart);
            Assert.False(vm.StartSyncCommand.CanExecute(null));
            Assert.False(vm.CanSelectDates);
            Assert.Equal("Cancel", vm.CloseButtonText);

            // Finish running state
            vm.IsRunning = false;
            Assert.True(vm.CanStart);
            Assert.True(vm.StartSyncCommand.CanExecute(null));
            Assert.True(vm.CanSelectDates);
            Assert.Equal("Close", vm.CloseButtonText);
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
    public async Task StartSync_ValidatesTimelogSelection_AndShowsError()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_val_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var timeLog = new TimeLog
            {
                ObjectID = "TL-V1",
                nameLog = "Val Log",
                nameWell = "Well1",
                nameWellbore = "WB1"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, timeLog);
            await vm.InitializeAsync();

            // Clear timelog selection
            vm.SelectedParentTimeLog = null;
            vm.TargetTimeLogName = "";

            // Attempt to trigger StartSync directly
            await vm.StartSyncAsync();

            Assert.True(vm.HasError);
            Assert.Contains("Timelog selection is required", vm.ErrorMessage);
            Assert.Contains("Validation failed", vm.ProgressStatus);
            Assert.False(vm.HasSuccess);
            Assert.False(vm.IsCompleted);
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
    public async Task StartSync_HandlesMissingTargetTimelog_Gracefully()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_missing_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var repo = new WellDataRepository(session);
            var timeLog = new TimeLog
            {
                ObjectID = "TL-M1",
                nameLog = "Src Log",
                nameWell = "Well1",
                nameWellbore = "WB1"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, timeLog, repo);
            await vm.InitializeAsync();

            // Set a target timelog name that does not exist in database
            vm.SelectedParentTimeLog = null;
            vm.TargetTimeLogName = "NonExistentParentLog";

            await vm.StartSyncAsync();

            Assert.True(vm.HasError);
            Assert.Contains("could not be found or loaded", vm.ErrorMessage);
            Assert.False(vm.HasSuccess);
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
    public async Task StartSync_HandlesMergeConflict_Gracefully()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_conflict_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W-1', 'Well1');");
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB-1', 'W-1', 'Wellbore1');");
            conn.Execute("INSERT INTO VMX_TIME_LOG (LOG_ID, WELL_ID, WELLBORE_ID, LOG_NAME) VALUES ('TL-SRC', 'W-1', 'WB-1', 'Source Log');");

            var repo = new WellDataRepository(session);
            var timeLog = new TimeLog
            {
                ObjectID = "TL-SRC",
                nameLog = "Source Log",
                WellID = "W-1",
                WellboreID = "WB-1"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, timeLog, repo);
            await vm.InitializeAsync();

            // Create a pseudo option pointing to the same ObjectID but differing TargetTimeLogName
            var option = new TimeLogOption
            {
                LogId = "TL-SRC",
                LogName = "Conflicted Name",
                WellId = "W-1",
                WellboreId = "WB-1"
            };
            vm.SelectedParentTimeLog = option;
            vm.TargetTimeLogName = "Conflicted Name";

            await vm.StartSyncAsync();

            Assert.True(vm.HasError);
            Assert.Contains("Cannot synchronize a timelog into itself", vm.ErrorMessage);
            Assert.Contains("conflict", vm.ProgressStatus, StringComparison.OrdinalIgnoreCase);
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
    public async Task StartSync_MergesChildDataIntoParentTimelog_WithOverwriteDuplicates()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_merge_overwrite_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            // 1. Create physical data tables for child and parent timelogs
            conn.Execute(@"
                CREATE TABLE TL_SRC_DATA (
                    DATETIME TEXT PRIMARY KEY,
                    INDEX_DOUBLE REAL,
                    DEPTH REAL,
                    HDTH REAL,
                    ROP REAL,
                    CUSTOM_CURVE REAL,
                    NEXT_DEPTH REAL,
                    FOOTAGE REAL,
                    NEXT_DATETIME TEXT,
                    TIME_DURATION REAL
                );");

            conn.Execute(@"
                CREATE TABLE TL_PARENT_DATA (
                    DATETIME TEXT PRIMARY KEY,
                    INDEX_DOUBLE REAL,
                    DEPTH REAL,
                    HDTH REAL,
                    ROP REAL,
                    NEXT_DEPTH REAL,
                    FOOTAGE REAL,
                    NEXT_DATETIME TEXT,
                    TIME_DURATION REAL
                );");

            // 2. Populate child and parent data
            conn.Execute("INSERT INTO TL_SRC_DATA (DATETIME, INDEX_DOUBLE, DEPTH, HDTH, ROP, CUSTOM_CURVE) VALUES ('2026-06-01 10:00:00', 46174.4166666667, 1000.0, 1000.0, 50.0, 99.5);");
            conn.Execute("INSERT INTO TL_SRC_DATA (DATETIME, INDEX_DOUBLE, DEPTH, HDTH, ROP, CUSTOM_CURVE) VALUES ('2026-06-01 10:10:00', 46174.4236111111, 1010.0, 1010.0, 55.0, 102.3);");

            conn.Execute("INSERT INTO TL_PARENT_DATA (DATETIME, INDEX_DOUBLE, DEPTH, HDTH, ROP) VALUES ('2026-06-01 09:00:00', 46174.375, 950.0, 950.0, 40.0);");
            conn.Execute("INSERT INTO TL_PARENT_DATA (DATETIME, INDEX_DOUBLE, DEPTH, HDTH, ROP) VALUES ('2026-06-01 10:00:00', 46174.4166666667, 999.0, 999.0, 20.0);");

            // 3. Register timelogs in VMX_TIME_LOG and VMX_TIME_LOG_COLUMNS
            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W1', 'Well1');");
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB1', 'W1', 'Wellbore1');");
            conn.Execute("INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME, DUPLICATE_ACTION) VALUES ('W1', 'WB1', 'TL_SRC', 'ChildLog', 'TL_SRC_DATA', 0);");
            conn.Execute("INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME, DUPLICATE_ACTION) VALUES ('W1', 'WB1', 'TL_PARENT', 'ParentLog', 'TL_PARENT_DATA', 0);");

            conn.Execute("INSERT INTO VMX_TIME_LOG_COLUMNS (WELL_ID, WELLBORE_ID, LOG_ID, MNEMONIC, CHANNEL_NAME, DATA_TYPE) VALUES ('W1', 'WB1', 'TL_SRC', 'CUSTOM_CURVE', 'Custom Curve Channel', 'DOUBLE');");

            var repo = new WellDataRepository(session);
            var childTimeLog = new TimeLog
            {
                ObjectID = "TL_SRC",
                nameLog = "ChildLog",
                WellID = "W1",
                WellboreID = "WB1",
                __dataTableName = "TL_SRC_DATA",
                DuplicateAction = enumDuplicateAction.OverwriteDuplicates,
                LinkToParent = true,
                LinkLogID = "TL_PARENT"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, childTimeLog, repo);
            await vm.InitializeAsync();

            vm.FromDateTime = new DateTime(2026, 6, 1, 9, 30, 0);
            vm.ToDateTime = new DateTime(2026, 6, 1, 11, 0, 0);

            // Execute sync via command
            await vm.StartSyncCommand.ExecuteAsync(null);

            // Verify ViewModel state
            Assert.True(vm.IsCompleted);
            Assert.True(vm.HasSuccess);
            Assert.False(vm.HasError);
            Assert.Equal(100, vm.ProgressPercent);
            Assert.Equal("Data synced successfully to Parent Timelog.", vm.SuccessMessage);
            Assert.StartsWith("Time Elapsed [", vm.ElapsedTimeText);

            // Verify column synchronization: CUSTOM_CURVE column must have been added to parent table
            var parentCols = await conn.QueryAsync<dynamic>("PRAGMA table_info(TL_PARENT_DATA);");
            Assert.Contains(parentCols, c => string.Equals((string)c.name, "CUSTOM_CURVE", StringComparison.OrdinalIgnoreCase));

            // Verify column metadata registered in VMX_TIME_LOG_COLUMNS for parent log
            int colMetaCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VMX_TIME_LOG_COLUMNS WHERE LOG_ID = 'TL_PARENT' AND MNEMONIC = 'CUSTOM_CURVE';");
            Assert.Equal(1, colMetaCount);

            // Verify child data merged into parent table
            var parentRows = (await conn.QueryAsync<dynamic>("SELECT * FROM TL_PARENT_DATA ORDER BY DATETIME;")).ToList();
            Assert.Equal(3, parentRows.Count);

            // First record: 09:00:00 preserved
            Assert.Equal("2026-06-01 09:00:00", (string)parentRows[0].DATETIME);

            // Overwritten record at 10:00:00 has updated DEPTH (1000 instead of 999) and CUSTOM_CURVE (99.5)
            Assert.Equal("2026-06-01 10:00:00", (string)parentRows[1].DATETIME);
            Assert.Equal(1000.0, Convert.ToDouble(parentRows[1].DEPTH));
            Assert.Equal(99.5, Convert.ToDouble(parentRows[1].CUSTOM_CURVE));

            // New record at 10:10:00 inserted
            Assert.Equal("2026-06-01 10:10:00", (string)parentRows[2].DATETIME);
            Assert.Equal(1010.0, Convert.ToDouble(parentRows[2].DEPTH));
            Assert.Equal(102.3, Convert.ToDouble(parentRows[2].CUSTOM_CURVE));

            // Verify record links calculated (NEXT_DEPTH, FOOTAGE, NEXT_DATETIME, TIME_DURATION)
            Assert.NotNull(parentRows[1].NEXT_DATETIME);
            Assert.Equal(1000.0, Convert.ToDouble(parentRows[2].NEXT_DEPTH));
            Assert.Equal(10.0, Convert.ToDouble(parentRows[2].FOOTAGE));

            // Verify indexes updated in VMX_TIME_LOG
            var parentMeta = await conn.QuerySingleAsync<dynamic>("SELECT MIN_DATE, MAX_DATE FROM VMX_TIME_LOG WHERE LOG_ID = 'TL_PARENT';");
            Assert.NotNull(parentMeta.MIN_DATE);
            Assert.NotNull(parentMeta.MAX_DATE);
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
    public async Task StartSync_HandlesSkipDuplicates_Correctly()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_skip_dup_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            // 1. Create physical tables
            conn.Execute(@"
                CREATE TABLE TL_SRC_DATA (
                    DATETIME TEXT PRIMARY KEY,
                    INDEX_DOUBLE REAL,
                    DEPTH REAL,
                    HDTH REAL
                );");

            conn.Execute(@"
                CREATE TABLE TL_PARENT_DATA (
                    DATETIME TEXT PRIMARY KEY,
                    INDEX_DOUBLE REAL,
                    DEPTH REAL,
                    HDTH REAL
                );");

            // Parent already has data up to 10:00:00
            conn.Execute("INSERT INTO TL_PARENT_DATA (DATETIME, INDEX_DOUBLE, DEPTH, HDTH) VALUES ('2026-06-01 09:00:00', 46174.375, 950.0, 950.0);");
            conn.Execute("INSERT INTO TL_PARENT_DATA (DATETIME, INDEX_DOUBLE, DEPTH, HDTH) VALUES ('2026-06-01 10:00:00', 46174.4166666667, 1000.0, 1000.0);");

            // Child has overlapping data at 09:30:00 (value 980) and new data at 10:30:00 (value 1050)
            conn.Execute("INSERT INTO TL_SRC_DATA (DATETIME, INDEX_DOUBLE, DEPTH, HDTH) VALUES ('2026-06-01 09:30:00', 46174.3958333333, 980.0, 980.0);");
            conn.Execute("INSERT INTO TL_SRC_DATA (DATETIME, INDEX_DOUBLE, DEPTH, HDTH) VALUES ('2026-06-01 10:30:00', 46174.4375, 1050.0, 1050.0);");

            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W1', 'Well1');");
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB1', 'W1', 'Wellbore1');");
            conn.Execute("INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME, DUPLICATE_ACTION, MAX_DATE) VALUES ('W1', 'WB1', 'TL_SRC', 'ChildLog', 'TL_SRC_DATA', 1, '2026-06-01 10:30:00');");
            conn.Execute("INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME, DUPLICATE_ACTION, MAX_DATE) VALUES ('W1', 'WB1', 'TL_PARENT', 'ParentLog', 'TL_PARENT_DATA', 1, '2026-06-01 10:00:00');");

            var repo = new WellDataRepository(session);
            var childTimeLog = new TimeLog
            {
                ObjectID = "TL_SRC",
                nameLog = "ChildLog",
                WellID = "W1",
                WellboreID = "WB1",
                __dataTableName = "TL_SRC_DATA",
                DuplicateAction = enumDuplicateAction.SkipDuplicates,
                LinkToParent = true,
                LinkLogID = "TL_PARENT"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, childTimeLog, repo);
            await vm.InitializeAsync();

            vm.FromDateTime = new DateTime(2026, 6, 1, 9, 0, 0);
            vm.ToDateTime = new DateTime(2026, 6, 1, 11, 0, 0);

            await vm.StartSyncCommand.ExecuteAsync(null);

            Assert.True(vm.IsCompleted);
            Assert.True(vm.HasSuccess);

            var parentRows = (await conn.QueryAsync<dynamic>("SELECT * FROM TL_PARENT_DATA ORDER BY DATETIME;")).ToList();
            // Should contain 3 rows: original 09:00:00, original 10:00:00, and new 10:30:00. The duplicate 09:30:00 was skipped!
            Assert.Equal(3, parentRows.Count);
            Assert.Equal("2026-06-01 09:00:00", (string)parentRows[0].DATETIME);
            Assert.Equal("2026-06-01 10:00:00", (string)parentRows[1].DATETIME);
            Assert.Equal("2026-06-01 10:30:00", (string)parentRows[2].DATETIME);
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
    public async Task InitializeAsync_SetsFromAndToDates_AutomaticallyFromDataTable()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_dates_table_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            conn.Execute(@"
                CREATE TABLE TL_AUTO_SRC (
                    DATETIME TEXT PRIMARY KEY,
                    INDEX_DOUBLE REAL,
                    DEPTH REAL,
                    HDTH REAL
                );");

            // Insert records with specific start and end timestamps
            conn.Execute("INSERT INTO TL_AUTO_SRC (DATETIME, INDEX_DOUBLE, DEPTH, HDTH) VALUES ('2026-06-01 08:15:30', 46174.344097, 950.0, 950.0);");
            conn.Execute("INSERT INTO TL_AUTO_SRC (DATETIME, INDEX_DOUBLE, DEPTH, HDTH) VALUES ('2026-06-02 12:00:00', 46175.500000, 980.0, 980.0);");
            conn.Execute("INSERT INTO TL_AUTO_SRC (DATETIME, INDEX_DOUBLE, DEPTH, HDTH) VALUES ('2026-06-05 17:45:15', 46178.739757, 1050.0, 1050.0);");

            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W1', 'Well1');");
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB1', 'W1', 'Wellbore1');");
            conn.Execute("INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME) VALUES ('W1', 'WB1', 'TL_SRC_1', 'AutoLog', 'TL_AUTO_SRC');");

            var repo = new WellDataRepository(session);
            var sourceLog = new TimeLog
            {
                ObjectID = "TL_SRC_1",
                nameLog = "AutoLog",
                WellID = "W1",
                WellboreID = "WB1",
                __dataTableName = "TL_AUTO_SRC"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, sourceLog, repo);
            await vm.InitializeAsync();

            // Verify dates and times are automatically retrieved from the physical table
            Assert.Equal(new DateTime(2026, 6, 1, 8, 15, 30), vm.FromDateTime);
            Assert.Equal(new DateTime(2026, 6, 5, 17, 45, 15), vm.ToDateTime);
            Assert.Equal(new DateTime(2026, 6, 1, 8, 15, 30), vm.DataStartDateTime);
            Assert.Equal(new DateTime(2026, 6, 5, 17, 45, 15), vm.DataEndDateTime);
            Assert.Equal(new DateTime(2026, 6, 1), vm.MinAvailableDate);
            Assert.Equal(new DateTime(2026, 6, 5), vm.MaxAvailableDate);
            Assert.Equal("01/06/2026 08:15", vm.FromDateTimeText);
            Assert.Equal("05/06/2026 17:45", vm.ToDateTimeText);
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
    public async Task InitializeAsync_SetsFromAndToDates_FromVmxTimeLogMetadata_WhenPhysicalTableMissing()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_dates_meta_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W1', 'Well1');");
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB1', 'W1', 'Wellbore1');");
            conn.Execute("INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, MIN_DATE, MAX_DATE) VALUES ('W1', 'WB1', 'TL_META_1', 'MetaLog', '2026-07-10 09:00:00', '2026-07-20 18:30:00');");

            var repo = new WellDataRepository(session);
            var sourceLog = new TimeLog
            {
                ObjectID = "TL_META_1",
                nameLog = "MetaLog",
                WellID = "W1",
                WellboreID = "WB1"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, sourceLog, repo);
            await vm.InitializeAsync();

            Assert.Equal(new DateTime(2026, 7, 10, 9, 0, 0), vm.FromDateTime);
            Assert.Equal(new DateTime(2026, 7, 20, 18, 30, 0), vm.ToDateTime);
            Assert.Equal("10/07/2026 09:00", vm.FromDateTimeText);
            Assert.Equal("20/07/2026 18:30", vm.ToDateTimeText);
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
    public async Task DateRangePreset_AllAvailableData_RestoresExactDataStartAndEndTimestamps()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_preset_restore_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            conn.Execute(@"
                CREATE TABLE TL_PRESET_DATA (
                    DATETIME TEXT PRIMARY KEY,
                    INDEX_DOUBLE REAL
                );");

            conn.Execute("INSERT INTO TL_PRESET_DATA (DATETIME, INDEX_DOUBLE) VALUES ('2026-08-01 11:22:33', 46235.473993);");
            conn.Execute("INSERT INTO TL_PRESET_DATA (DATETIME, INDEX_DOUBLE) VALUES ('2026-08-10 22:44:55', 46244.947859);");

            var sourceLog = new TimeLog
            {
                ObjectID = "TL_P1",
                nameLog = "PresetTimeLog",
                __dataTableName = "TL_PRESET_DATA"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, sourceLog);
            await vm.InitializeAsync();

            Assert.Equal(new DateTime(2026, 8, 1, 11, 22, 33), vm.FromDateTime);
            Assert.Equal(new DateTime(2026, 8, 10, 22, 44, 55), vm.ToDateTime);

            // Change preset to Last 24 Hours
            vm.SelectedDateRangePreset = "Last 24 Hours";
            Assert.Equal(vm.ToDateTime.AddHours(-24), vm.FromDateTime);

            // Change preset back to All Available Data -> must restore exact original timestamps
            vm.SelectedDateRangePreset = "All Available Data";
            Assert.Equal(new DateTime(2026, 8, 1, 11, 22, 33), vm.FromDateTime);
            Assert.Equal(new DateTime(2026, 8, 10, 22, 44, 55), vm.ToDateTime);
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
    public async Task InitializeAsync_PreservesUtcKind_WhenWellDateFormatIsUtc()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_utc_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            conn.Execute(@"
                CREATE TABLE TL_UTC_DATA (
                    DATETIME TEXT PRIMARY KEY
                );");
            conn.Execute("INSERT INTO TL_UTC_DATA (DATETIME) VALUES ('2026-09-01 00:00:00');");
            conn.Execute("INSERT INTO TL_UTC_DATA (DATETIME) VALUES ('2026-09-05 12:00:00');");

            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME, DATE_FORMAT) VALUES ('W_UTC', 'UtcWell', 'UTC');");

            var sourceLog = new TimeLog
            {
                ObjectID = "TL_UTC_LOG",
                nameLog = "UtcLog",
                WellID = "W_UTC",
                __dataTableName = "TL_UTC_DATA",
                __wellDateFormat = "UTC"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, sourceLog);
            await vm.InitializeAsync();

            Assert.Equal(DateTimeKind.Utc, vm.FromDateTime.Kind);
            Assert.Equal(DateTimeKind.Utc, vm.ToDateTime.Kind);
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

    [Theory]
    [InlineData("2026-06-01 08:30:00", 2026, 6, 1, 8, 30, 0)]
    [InlineData("01-Jun-2026 08:30:00", 2026, 6, 1, 8, 30, 0)]
    [InlineData("01/06/2026 08:30:00", 2026, 6, 1, 8, 30, 0)]
    [InlineData("2026-06-01T08:30:00", 2026, 6, 1, 8, 30, 0)]
    public void ParseRowDateTime_ParsesVariousFormatsAccurately(
        string input, int year, int month, int day, int hour, int minute, int second)
    {
        var dt = SyncDataWithParentTimelogViewModel.ParseRowDateTime(input);
        Assert.Equal(new DateTime(year, month, day, hour, minute, second), dt);
    }

    [Fact]
    public async Task SelectedTimelogNode_FromAndToDates_DerivedAutomaticallyFromTreeNode()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_tree_node50_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W1', 'Well1');");
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB1', 'W1', 'Wellbore1');");

            // Timelog 50: Selected from tree. Rows from 01/08/2026 10:00:00 to 15/08/2026 18:30:00
            conn.Execute(@"
                CREATE TABLE TL_50_DATA (
                    DATETIME TEXT PRIMARY KEY,
                    INDEX_DOUBLE REAL
                );");
            conn.Execute("INSERT INTO TL_50_DATA (DATETIME, INDEX_DOUBLE) VALUES ('2026-08-01 10:00:00', 46235.416667);");
            conn.Execute("INSERT INTO TL_50_DATA (DATETIME, INDEX_DOUBLE) VALUES ('2026-08-10 14:15:00', 46244.593750);");
            conn.Execute("INSERT INTO TL_50_DATA (DATETIME, INDEX_DOUBLE) VALUES ('2026-08-15 18:30:00', 46249.770833);");

            // Target Parent Log: TL_PARENT with different dates
            conn.Execute(@"
                CREATE TABLE TL_PARENT_DATA (
                    DATETIME TEXT PRIMARY KEY,
                    INDEX_DOUBLE REAL
                );");
            conn.Execute("INSERT INTO TL_PARENT_DATA (DATETIME, INDEX_DOUBLE) VALUES ('2026-01-01 00:00:00', 46023.0);");
            conn.Execute("INSERT INTO TL_PARENT_DATA (DATETIME, INDEX_DOUBLE) VALUES ('2026-12-31 23:59:59', 46388.0);");

            conn.Execute(@"
                INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME)
                VALUES ('W1', 'WB1', 'TL_PARENT', 'Parent Timelog', 'TL_PARENT_DATA');");

            conn.Execute(@"
                INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME)
                VALUES ('W1', 'WB1', '50', '50', 'TL_50_DATA');");

            var repo = new WellDataRepository(session);
            var selectedTimeLogNode = new TimeLog
            {
                ObjectID = "50",
                nameLog = "50",
                WellID = "W1",
                WellboreID = "WB1",
                LinkToParent = true,
                LinkWellID = "W1",
                LinkWellboreID = "WB1",
                LinkLogID = "TL_PARENT",
                __dataTableName = "TL_50_DATA"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, selectedTimeLogNode, repo);
            await vm.InitializeAsync();

            // 1. Clearly displays the selected Timelog node name
            Assert.Equal("50", vm.SelectedTimelogNodeName);
            Assert.Equal("50", vm.SourceTimeLogName);
            Assert.Equal("Parent Timelog", vm.TargetTimeLogName);

            // 2. From Date & To Date automatically derived from the selected Timelog node (NOT parent)
            Assert.Equal(new DateTime(2026, 8, 1, 10, 0, 0), vm.FromDateTime);
            Assert.Equal(new DateTime(2026, 8, 15, 18, 30, 0), vm.ToDateTime);
            Assert.Equal("01/08/2026 10:00", vm.FromDateTimeText);
            Assert.Equal("15/08/2026 18:30", vm.ToDateTimeText);

            // 3. Confirmation message explaining data will be synced for the given date range
            Assert.Contains("50", vm.ConfirmationMessage);
            Assert.Contains("Parent Timelog", vm.ConfirmationMessage);
            Assert.Contains("01/08/2026 10:00", vm.ConfirmationMessage);
            Assert.Contains("15/08/2026 18:30", vm.ConfirmationMessage);
            Assert.True(vm.CanStart);
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
    public async Task RibbonCommand_WhenTimelog50SelectedInTree_OpensDialogWithNodeDerivedDatesAndConfirmation()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_ribbon_tree50_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W1', 'Main Well');");
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB1', 'W1', 'Main Wellbore');");

            conn.Execute(@"
                CREATE TABLE TL_50_DATA (
                    DATETIME TEXT PRIMARY KEY,
                    INDEX_DOUBLE REAL
                );");
            conn.Execute("INSERT INTO TL_50_DATA (DATETIME, INDEX_DOUBLE) VALUES ('2026-07-01 06:30:00', 46204.270833);");
            conn.Execute("INSERT INTO TL_50_DATA (DATETIME, INDEX_DOUBLE) VALUES ('2026-07-20 16:45:00', 46223.697917);");

            conn.Execute(@"
                INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME)
                VALUES ('W1', 'WB1', '50', '50', 'TL_50_DATA');");

            conn.Execute(@"
                INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME)
                VALUES ('W1', 'WB1', 'TL_PARENT', 'Primary Parent Log');");

            var dummyProjectService = new DummyProjectService();
            var mainVm = new MainViewModel(session, dummyProjectService);
            var dashboardVm = (DashboardViewModel)mainVm.CurrentViewModel;

            var timeLog50 = new TimeLog
            {
                ObjectID = "50",
                nameLog = "50",
                WellID = "W1",
                WellboreID = "WB1",
                LinkToParent = true,
                LinkWellID = "W1",
                LinkWellboreID = "WB1",
                LinkLogID = "TL_PARENT",
                __dataTableName = "TL_50_DATA"
            };

            var treeNode = new WellTreeNode
            {
                Name = "50",
                Type = WellTreeNodeType.TimeLog,
                Tag = timeLog50
            };
            dashboardVm.OnTreeNodeSelected(treeNode);

            SyncDataWithParentTimelogViewModel? dialogVm = null;
            mainVm.OpenSyncDataDialogHandler = vm =>
            {
                dialogVm = vm;
                return true;
            };

            Assert.True(mainVm.CanSyncDataToParentTimelog);
            await mainVm.SyncDataToParentTimelogCommand.ExecuteAsync(null);

            Assert.NotNull(dialogVm);
            // 1. The selected Timelog node name
            Assert.Equal("50", dialogVm.SelectedTimelogNodeName);
            // 2. The From Date and To Date derived from that node
            Assert.Equal(new DateTime(2026, 7, 1, 6, 30, 0), dialogVm.FromDateTime);
            Assert.Equal(new DateTime(2026, 7, 20, 16, 45, 0), dialogVm.ToDateTime);
            Assert.Equal("01/07/2026 06:30", dialogVm.FromDateTimeText);
            Assert.Equal("20/07/2026 16:45", dialogVm.ToDateTimeText);
            // 3. Confirmation message explaining data will be synced for the given date range
            Assert.Equal(
                "Data from selected Timelog '50' will be synchronized into Parent Timelog 'Primary Parent Log' for the date range 01/07/2026 06:30 to 20/07/2026 16:45.",
                dialogVm.ConfirmationMessage);
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
    public async Task ConfirmationMessage_DynamicallyUpdates_WhenDateRangeAdjusted()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"sync_confirm_update_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(tempDb);
        var session = new ProjectSession();

        try
        {
            session.Load(tempDb);
            var conn = session.GetConnection();

            conn.Execute("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W1', 'Well1');");
            conn.Execute("INSERT INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB1', 'W1', 'Wellbore1');");
            conn.Execute(@"
                CREATE TABLE TL_SRC_DATA (
                    DATETIME TEXT PRIMARY KEY
                );");
            conn.Execute("INSERT INTO TL_SRC_DATA (DATETIME) VALUES ('2026-08-01 00:00:00');");
            conn.Execute("INSERT INTO TL_SRC_DATA (DATETIME) VALUES ('2026-08-10 23:59:59');");

            conn.Execute(@"
                INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME)
                VALUES ('W1', 'WB1', 'TL_SRC', 'Timelog 50', 'TL_SRC_DATA');");
            conn.Execute(@"
                INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME)
                VALUES ('W1', 'WB1', 'TL_PARENT', 'ParentLog');");

            var repo = new WellDataRepository(session);
            var sourceLog = new TimeLog
            {
                ObjectID = "TL_SRC",
                nameLog = "Timelog 50",
                WellID = "W1",
                WellboreID = "WB1",
                LinkToParent = true,
                LinkWellID = "W1",
                LinkWellboreID = "WB1",
                LinkLogID = "TL_PARENT",
                __dataTableName = "TL_SRC_DATA"
            };

            var vm = new SyncDataWithParentTimelogViewModel(session, sourceLog, repo);
            await vm.InitializeAsync();

            Assert.Equal(
                "Data from selected Timelog 'Timelog 50' will be synchronized into Parent Timelog 'ParentLog' for the date range 01/08/2026 00:00 to 10/08/2026 23:59.",
                vm.ConfirmationMessage);

            // User adjusts From Date & Time
            vm.FromDateTimeText = "05/08/2026 12:00";
            Assert.Equal(
                "Data from selected Timelog 'Timelog 50' will be synchronized into Parent Timelog 'ParentLog' for the date range 05/08/2026 12:00 to 10/08/2026 23:59.",
                vm.ConfirmationMessage);

            // User adjusts To Date & Time
            vm.ToDateTimeText = "08/08/2026 18:00";
            Assert.Equal(
                "Data from selected Timelog 'Timelog 50' will be synchronized into Parent Timelog 'ParentLog' for the date range 05/08/2026 12:00 to 08/08/2026 18:00.",
                vm.ConfirmationMessage);
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


