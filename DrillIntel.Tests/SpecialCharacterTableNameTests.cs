using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Projects;
using Xunit;

namespace DrillIntel.Tests
{
    public class SpecialCharacterTableNameTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly IDataServiceDIntel _dataService;

        public SpecialCharacterTableNameTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"SpecialTableTest_{Guid.NewGuid():N}.sqlite");
            SchemaInitializer.CreateDatabase(_testDbPath);
            _dataService = new DataServiceDIntel();
            _dataService.OpenConnection(_testDbPath);
        }

        public void Dispose()
        {
            try
            {
                _dataService.CloseConnection();
                _dataService.Dispose();
            }
            catch { }

            try
            {
                if (File.Exists(_testDbPath))
                {
                    File.Delete(_testDbPath);
                }
            }
            catch { }
        }

        [Fact]
        public void TimeLogTableWithHash_CreationAndIndexCreation_SucceedsWithoutError()
        {
            // Table name with hash symbol '#'
            string hashTableName = "timeLog23798241#40580182";

            var timeLog = new TimeLog
            {
                ObjectID = "TL001",
                WellID = "W001",
                WellboreID = "WB001",
                nameLog = "Main TimeLog",
                __dataTableName = hashTableName
            };

            timeLog.logCurves["DATETIME"] = new LogChannel
            {
                Mnemonic = "DATETIME",
                mnemonic = "DATETIME",
                typeLogData = "DATETIME"
            };
            timeLog.logCurves["RIG_STATE"] = new LogChannel
            {
                Mnemonic = "RIG_STATE",
                mnemonic = "RIG_STATE",
                typeLogData = "INTEGER"
            };
            timeLog.logCurves["DEPTH"] = new LogChannel
            {
                Mnemonic = "DEPTH",
                mnemonic = "DEPTH",
                typeLogData = "DOUBLE"
            };
            timeLog.logCurves["STOR"] = new LogChannel
            {
                Mnemonic = "STOR",
                mnemonic = "STOR",
                typeLogData = "DOUBLE"
            };

            string lastError = "";
            bool added = TimeLogService.addTimeLog(_dataService, timeLog, ref lastError);
            Assert.True(added, $"addTimeLog failed: {lastError}");

            // Verify table exists
            bool exists = _dataService.TableExists(hashTableName);
            Assert.True(exists, $"Table [{hashTableName}] should exist.");

            // Insert sample rows into bracketed table
            string insertSql = $"INSERT INTO [{hashTableName}] (DATETIME, RIG_STATE, DEPTH, STOR) VALUES " +
                               "('01-Jan-2024 10:00:00', 0, 1000.0, 150.5), " +
                               "('01-Jan-2024 10:05:00', 1, 1005.0, 160.0);";
            bool inserted = _dataService.ExecuteNonQuery(insertSql);
            Assert.True(inserted, $"Insert into [{hashTableName}] failed: {_dataService.LastError}");

            // Test query with brackets (Broomstick pattern)
            string avgSql = "SELECT AVG(STOR) FROM [" + hashTableName + "] WHERE DATETIME >= @FromDate AND DATETIME <= @ToDate AND RIG_STATE IN (0,1,19)";
            var avgParams = new Dictionary<string, object?>
            {
                ["@FromDate"] = "01-Jan-2024 09:00:00",
                ["@ToDate"] = "01-Jan-2024 11:00:00"
            };
            object? avgObj = _dataService.GetValue(avgSql, avgParams);
            Assert.NotNull(avgObj);
            double avg = Convert.ToDouble(avgObj);
            Assert.True(avg > 150.0);
        }

        [Fact]
        public void VirtualTable_WithHashTableName_LoadsDataSuccessfully()
        {
            string hashTableName = "timeLog99999999#11111111";

            // Create table with brackets
            _dataService.ExecuteNonQuery($"CREATE TABLE [{hashTableName}] (DATETIME TEXT, RIG_STATE INTEGER, STOR REAL);");
            _dataService.ExecuteNonQuery($"INSERT INTO [{hashTableName}] VALUES ('01-Jan-2024 12:00:00', 1, 200.0);");

            var vt = new VirtualTable.VirtualTable
            {
                objDataService = _dataService,
                dataTableName = hashTableName,
                Condition = "DATETIME >= '01-Jan-2024 00:00:00'"
            };

            vt.loadData();
            Assert.Equal(1, vt.RecordCount);
            Assert.NotNull(vt.Table);
            Assert.Equal(200.0, Convert.ToDouble(vt.Table.Rows[0]["STOR"]));
        }

        [Fact]
        public void DepthLogTableWithHash_CreationAndIndex_SucceedsWithoutError()
        {
            string hashDepthTable = "depthLog88888888#22222222";

            var depthLog = new DepthLog
            {
                ObjectID = "DL001",
                WellID = "W001",
                WellboreID = "WB001",
                nameLog = "Main DepthLog",
                __dataTableName = hashDepthTable
            };

            depthLog.LogCurves["DEPTH"] = new LogChannel
            {
                Mnemonic = "DEPTH",
                mnemonic = "DEPTH",
                typeLogData = "DOUBLE"
            };
            depthLog.LogCurves["GR"] = new LogChannel
            {
                Mnemonic = "GR",
                mnemonic = "GR",
                typeLogData = "DOUBLE"
            };

            string lastError = "";
            bool added = DepthLogService.AddDepthLog(_dataService, depthLog, ref lastError);
            Assert.True(added, $"DepthLog AddDepthLog failed: {lastError}");

            bool exists = _dataService.TableExists(hashDepthTable);
            Assert.True(exists, $"Table [{hashDepthTable}] should exist.");

            // Verify select from bracketed table
            DataTable dt = _dataService.GetTable($"SELECT * FROM [{hashDepthTable}];");
            Assert.NotNull(dt);
        }
    }
}

