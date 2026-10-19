using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DrillIntel.Projects;
using DrillIntel.Services;

namespace DrillIntel.Data;

/// <summary>
/// Contract for Broomstick data operations against the project database.
/// </summary>
public interface IBroomstickDataService
{
    /// <summary>
    /// Clears existing broomstick points and processing info for a specified well using parameterized queries.
    /// </summary>
    Task<bool> ClearBroomstickPointsAsync(string wellId, CancellationToken ct = default);

    /// <summary>
    /// Synchronously clears existing broomstick points and processing info for a specified well.
    /// </summary>
    bool ClearBroomstickPoints(string wellId);

    /// <summary>
    /// Ensures that VMX_BROOMSTICK_POINTS and VMX_BROOMSTICK_PRC_INFO tables exist in the database.
    /// </summary>
    Task EnsureTablesExistAsync(CancellationToken ct = default);
}

/// <summary>
/// Data service providing parameterized access to Broomstick tables in SQLite.
/// </summary>
public class BroomstickDataService : IBroomstickDataService
{
    private readonly ProjectSession _session;
    private readonly IDataServiceDIntel? _dataService;
    private readonly IAppLogger _logger;

    public const string TableBroomstickPoints = "VMX_BROOMSTICK_POINTS";
    public const string TableBroomstickPrcInfo = "VMX_BROOMSTICK_PRC_INFO";

    public BroomstickDataService(ProjectSession session, IAppLogger? logger = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? new AppLogger(nameof(BroomstickDataService));
    }

    public BroomstickDataService(IDataServiceDIntel dataService, IAppLogger? logger = null)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _session = null!;
        _logger = logger ?? new AppLogger(nameof(BroomstickDataService));
    }

    private IDataServiceDIntel GetEffectiveDataService()
    {
        if (_dataService != null) return _dataService;
        if (_session != null && _session.IsProjectOpen)
            return _session.GetDataService();
        throw new InvalidOperationException("No database connection or active project session is available.");
    }

    private DbConnection GetEffectiveDbConnection()
    {
        if (_session != null && _session.IsProjectOpen)
            return _session.GetConnection();

        var ds = GetEffectiveDataService();
        return ds.GetDbConnection();
    }

    public async Task EnsureTablesExistAsync(CancellationToken ct = default)
    {
        try
        {
            var connection = GetEffectiveDbConnection();

            const string createPointsTableSql = @"
                CREATE TABLE IF NOT EXISTS VMX_BROOMSTICK_POINTS (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    WELL_ID TEXT NOT NULL,
                    DEPTH REAL,
                    TYPE INTEGER,
                    START_DATE TEXT,
                    END_DATE TEXT,
                    START_INDEX REAL,
                    END_INDEX REAL,
                    MIN REAL,
                    MAX REAL,
                    AVG REAL,
                    AVG2 REAL,
                    START_DEPTH REAL,
                    END_DEPTH REAL,
                    BLOCK_MOVEMENT REAL,
                    MAX_INDEX REAL,
                    DYNAMIC_VALUE REAL,
                    DYNAMIC_TORQUE_VALUE REAL,
                    CIRCULATION REAL,
                    PUMP_PRESSURE REAL,
                    USER_DYNAMIC_VALUE REAL,
                    USER_STATIC_VALUE REAL,
                    USER_VISIBLE INTEGER DEFAULT 1,
                    PUMP_STATUS INTEGER DEFAULT 0,
                    CREATED_BY TEXT,
                    CREATED_DATE TEXT,
                    MODIFIED_BY TEXT,
                    MODIFIED_DATE TEXT
                );
                CREATE INDEX IF NOT EXISTS IDX_VMX_BROOMSTICK_POINTS_WELL ON VMX_BROOMSTICK_POINTS(WELL_ID);
                CREATE INDEX IF NOT EXISTS IDX_VMX_BROOMSTICK_POINTS_DEPTH ON VMX_BROOMSTICK_POINTS(WELL_ID, DEPTH, TYPE);";

            const string createPrcInfoTableSql = @"
                CREATE TABLE IF NOT EXISTS VMX_BROOMSTICK_PRC_INFO (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    WELL_ID TEXT NOT NULL,
                    TYPE INTEGER,
                    PRC_LAST_DATE TEXT
                );
                CREATE INDEX IF NOT EXISTS IDX_VMX_BROOMSTICK_PRC_INFO_WELL ON VMX_BROOMSTICK_PRC_INFO(WELL_ID);";

            await connection.ExecuteAsync(new CommandDefinition(createPointsTableSql, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition(createPrcInfoTableSql, cancellationToken: ct));

            _logger.LogInfo("Broomstick database tables verified / created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to ensure Broomstick tables exist: {ex.Message}", ex);
            throw;
        }
    }

    public async Task<bool> ClearBroomstickPointsAsync(string wellId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(wellId))
        {
            throw new ArgumentException("WellID cannot be null or empty.", nameof(wellId));
        }

        try
        {
            await EnsureTablesExistAsync(ct);

            var connection = GetEffectiveDbConnection();

            // Parameterized delete queries to eliminate SQL injection vulnerabilities
            const string deletePointsSql = "DELETE FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID;";
            const string deletePrcInfoSql = "DELETE FROM VMX_BROOMSTICK_PRC_INFO WHERE WELL_ID = @WellID;";

            var parameters = new { WellID = wellId.Trim() };

            _logger.LogInfo($"Executing parameterized deletion of broomstick points for WellID='{wellId}'...");

            int pointsDeleted = await connection.ExecuteAsync(new CommandDefinition(deletePointsSql, parameters, cancellationToken: ct));
            int prcInfoDeleted = await connection.ExecuteAsync(new CommandDefinition(deletePrcInfoSql, parameters, cancellationToken: ct));

            _logger.LogInfo($"Successfully cleared Broomstick database entries for WellID='{wellId}'. Rows affected: Points={pointsDeleted}, PrcInfo={prcInfoDeleted}.");

            _session?.NotifyDataChanged();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error clearing broomstick database entries for WellID '{wellId}': {ex.Message}", ex);
            throw;
        }
    }

    public bool ClearBroomstickPoints(string wellId)
    {
        return ClearBroomstickPointsAsync(wellId, CancellationToken.None).GetAwaiter().GetResult();
    }
}
