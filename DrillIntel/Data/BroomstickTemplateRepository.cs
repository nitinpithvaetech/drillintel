using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DrillIntel.Models.Broomstick;
using DrillIntel.Projects;

namespace DrillIntel.Data;

/// <summary>
/// Data repository for managing Broomstick document templates in VMX_BROOMSTICK_TEMPLATES.
/// Provides full CRUD, default management, duplication, and SQLite indexing.
/// </summary>
public class BroomstickTemplateRepository : IBroomstickTemplateRepository
{
    private readonly ProjectSession? _session;
    private readonly IDbConnection? _explicitConnection;
    private bool _tableEnsured;

    public BroomstickTemplateRepository(ProjectSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public BroomstickTemplateRepository(IDbConnection explicitConnection)
    {
        _explicitConnection = explicitConnection ?? throw new ArgumentNullException(nameof(explicitConnection));
    }

    private IDbConnection? GetConnection()
    {
        if (_explicitConnection != null) return _explicitConnection;
        if (_session != null && _session.IsProjectOpen) return _session.GetConnection();
        return null;
    }

    private async Task EnsureTableAsync(IDbConnection conn)
    {
        if (_tableEnsured) return;
        try
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS VMX_BROOMSTICK_TEMPLATES (
    TEMPLATE_ID     TEXT NOT NULL PRIMARY KEY,
    DOCUMENT_NAME   TEXT NOT NULL DEFAULT '',
    WELL_ID         TEXT NOT NULL,
    WELLBORE_ID     TEXT,
    LOG_ID          TEXT,
    PLAN_ID         TEXT,
    PROFILE_ID      TEXT,
    DESCRIPTION     TEXT,
    TEMPLATE_DATA   BLOB,
    IS_DEFAULT      INTEGER NOT NULL DEFAULT 0,
    CREATED_BY      TEXT,
    CREATED_DATE    TEXT,
    MODIFIED_BY     TEXT,
    MODIFIED_DATE   TEXT
);
CREATE INDEX IF NOT EXISTS IX_VMX_BS_TEMPLATES_WELL ON VMX_BROOMSTICK_TEMPLATES(WELL_ID);
CREATE INDEX IF NOT EXISTS IX_VMX_BS_TEMPLATES_PLAN ON VMX_BROOMSTICK_TEMPLATES(PLAN_ID);
CREATE INDEX IF NOT EXISTS IX_VMX_BS_TEMPLATES_NAME ON VMX_BROOMSTICK_TEMPLATES(DOCUMENT_NAME);";

            await conn.ExecuteAsync(sql);
            _tableEnsured = true;
        }
        catch
        {
            // Non-fatal if table already exists or read-only
        }
    }

    public async Task EnsureTableAsync()
    {
        var conn = GetConnection();
        if (conn == null) return;
        await EnsureTableAsync(conn);
    }

    public async Task<List<BroomstickTemplateItem>> GetTemplatesAsync(string wellId, string? logId = null)
    {
        var conn = GetConnection();
        if (conn == null) return new List<BroomstickTemplateItem>();

        await EnsureTableAsync(conn);

        string sql = @"
            SELECT 
                TEMPLATE_ID     AS TemplateId,
                DOCUMENT_NAME   AS DocumentName,
                WELL_ID         AS WellId,
                WELLBORE_ID     AS WellboreId,
                LOG_ID          AS LogId,
                PLAN_ID         AS PlanId,
                PROFILE_ID      AS ProfileId,
                DESCRIPTION     AS Description,
                TEMPLATE_DATA   AS TemplateData,
                IS_DEFAULT      AS IsDefault,
                CREATED_BY      AS CreatedBy,
                CREATED_DATE    AS CreatedDateRaw,
                MODIFIED_BY     AS ModifiedBy,
                MODIFIED_DATE   AS ModifiedDateRaw
            FROM VMX_BROOMSTICK_TEMPLATES
            WHERE WELL_ID = @wellId" +
            (!string.IsNullOrWhiteSpace(logId) ? " AND (LOG_ID = @logId OR LOG_ID IS NULL OR LOG_ID = '')" : "") + @"
            ORDER BY IS_DEFAULT DESC, DOCUMENT_NAME ASC;";

        var rows = await conn.QueryAsync<dynamic>(sql, new { wellId, logId });
        var list = new List<BroomstickTemplateItem>();

        foreach (var r in rows)
        {
            var item = new BroomstickTemplateItem
            {
                TemplateId = (string)(r.TemplateId ?? string.Empty),
                DocumentName = (string)(r.DocumentName ?? string.Empty),
                WellId = (string)(r.WellId ?? string.Empty),
                WellboreId = (string?)r.WellboreId,
                LogId = (string?)r.LogId,
                PlanId = (string?)r.PlanId,
                ProfileId = (string?)r.ProfileId,
                Description = (string?)r.Description,
                TemplateData = (byte[]?)r.TemplateData,
                IsDefault = Convert.ToInt32(r.IsDefault ?? 0) == 1,
                CreatedBy = (string?)r.CreatedBy,
                ModifiedBy = (string?)r.ModifiedBy
            };

            if (DateTime.TryParse((string?)r.CreatedDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var cd))
                item.CreatedDate = cd;
            if (DateTime.TryParse((string?)r.ModifiedDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var md))
                item.ModifiedDate = md;

            list.Add(item);
        }

        return list;
    }

    public async Task<List<BroomstickTemplateItem>> GetAllTemplatesAsync()
    {
        var conn = GetConnection();
        if (conn == null) return new List<BroomstickTemplateItem>();

        await EnsureTableAsync(conn);

        const string sql = @"
            SELECT 
                TEMPLATE_ID     AS TemplateId,
                DOCUMENT_NAME   AS DocumentName,
                WELL_ID         AS WellId,
                WELLBORE_ID     AS WellboreId,
                LOG_ID          AS LogId,
                PLAN_ID         AS PlanId,
                PROFILE_ID      AS ProfileId,
                DESCRIPTION     AS Description,
                TEMPLATE_DATA   AS TemplateData,
                IS_DEFAULT      AS IsDefault,
                CREATED_BY      AS CreatedBy,
                CREATED_DATE    AS CreatedDateRaw,
                MODIFIED_BY     AS ModifiedBy,
                MODIFIED_DATE   AS ModifiedDateRaw
            FROM VMX_BROOMSTICK_TEMPLATES
            ORDER BY WELL_ID ASC, IS_DEFAULT DESC, DOCUMENT_NAME ASC;";

        var rows = await conn.QueryAsync<dynamic>(sql);
        var list = new List<BroomstickTemplateItem>();

        foreach (var r in rows)
        {
            var item = new BroomstickTemplateItem
            {
                TemplateId = (string)(r.TemplateId ?? string.Empty),
                DocumentName = (string)(r.DocumentName ?? string.Empty),
                WellId = (string)(r.WellId ?? string.Empty),
                WellboreId = (string?)r.WellboreId,
                LogId = (string?)r.LogId,
                PlanId = (string?)r.PlanId,
                ProfileId = (string?)r.ProfileId,
                Description = (string?)r.Description,
                TemplateData = (byte[]?)r.TemplateData,
                IsDefault = Convert.ToInt32(r.IsDefault ?? 0) == 1,
                CreatedBy = (string?)r.CreatedBy,
                ModifiedBy = (string?)r.ModifiedBy
            };

            if (DateTime.TryParse((string?)r.CreatedDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var cd))
                item.CreatedDate = cd;
            if (DateTime.TryParse((string?)r.ModifiedDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var md))
                item.ModifiedDate = md;

            list.Add(item);
        }

        return list;
    }

    public async Task<BroomstickTemplateItem?> GetTemplateByIdAsync(string templateId)
    {
        var conn = GetConnection();
        if (conn == null || string.IsNullOrWhiteSpace(templateId)) return null;

        await EnsureTableAsync(conn);

        const string sql = @"
            SELECT 
                TEMPLATE_ID     AS TemplateId,
                DOCUMENT_NAME   AS DocumentName,
                WELL_ID         AS WellId,
                WELLBORE_ID     AS WellboreId,
                LOG_ID          AS LogId,
                PLAN_ID         AS PlanId,
                PROFILE_ID      AS ProfileId,
                DESCRIPTION     AS Description,
                TEMPLATE_DATA   AS TemplateData,
                IS_DEFAULT      AS IsDefault,
                CREATED_BY      AS CreatedBy,
                CREATED_DATE    AS CreatedDateRaw,
                MODIFIED_BY     AS ModifiedBy,
                MODIFIED_DATE   AS ModifiedDateRaw
            FROM VMX_BROOMSTICK_TEMPLATES
            WHERE TEMPLATE_ID = @templateId;";

        var r = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { templateId });
        if (r == null) return null;

        var item = new BroomstickTemplateItem
        {
            TemplateId = (string)(r.TemplateId ?? string.Empty),
            DocumentName = (string)(r.DocumentName ?? string.Empty),
            WellId = (string)(r.WellId ?? string.Empty),
            WellboreId = (string?)r.WellboreId,
            LogId = (string?)r.LogId,
            PlanId = (string?)r.PlanId,
            ProfileId = (string?)r.ProfileId,
            Description = (string?)r.Description,
            TemplateData = (byte[]?)r.TemplateData,
            IsDefault = Convert.ToInt32(r.IsDefault ?? 0) == 1,
            CreatedBy = (string?)r.CreatedBy,
            ModifiedBy = (string?)r.ModifiedBy
        };

        if (DateTime.TryParse((string?)r.CreatedDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var cd))
            item.CreatedDate = cd;
        if (DateTime.TryParse((string?)r.ModifiedDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var md))
            item.ModifiedDate = md;

        return item;
    }

    public async Task<BroomstickTemplateItem?> GetDefaultTemplateAsync(string wellId)
    {
        var list = await GetTemplatesAsync(wellId);
        return list.FirstOrDefault(t => t.IsDefault) ?? list.FirstOrDefault();
    }

    public async Task<bool> TemplateNameExistsAsync(string wellId, string documentName, string? excludeTemplateId = null)
    {
        var conn = GetConnection();
        if (conn == null || string.IsNullOrWhiteSpace(documentName)) return false;

        await EnsureTableAsync(conn);

        string sql = @"
            SELECT COUNT(1) 
            FROM VMX_BROOMSTICK_TEMPLATES 
            WHERE WELL_ID = @wellId 
              AND LOWER(DOCUMENT_NAME) = LOWER(@documentName)" +
            (!string.IsNullOrWhiteSpace(excludeTemplateId) ? " AND TEMPLATE_ID != @excludeTemplateId" : "") + ";";

        int count = await conn.ExecuteScalarAsync<int>(sql, new { wellId, documentName, excludeTemplateId });
        return count > 0;
    }

    public async Task SaveTemplateAsync(BroomstickTemplateItem item)
    {
        var conn = GetConnection();
        if (conn == null || item == null) return;

        await EnsureTableAsync(conn);

        string nowIso = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        if (item.CreatedDate == null) item.CreatedDate = DateTime.Now;
        item.ModifiedDate = DateTime.Now;
        if (string.IsNullOrWhiteSpace(item.CreatedBy)) item.CreatedBy = Environment.UserName;
        item.ModifiedBy = Environment.UserName;

        string existsSql = "SELECT COUNT(1) FROM VMX_BROOMSTICK_TEMPLATES WHERE TEMPLATE_ID = @TemplateId;";
        int exists = await conn.ExecuteScalarAsync<int>(existsSql, new { item.TemplateId });

        if (exists > 0)
        {
            string updateSql = @"
                UPDATE VMX_BROOMSTICK_TEMPLATES SET
                    DOCUMENT_NAME   = @DocumentName,
                    WELL_ID         = @WellId,
                    WELLBORE_ID     = @WellboreId,
                    LOG_ID          = @LogId,
                    PLAN_ID         = @PlanId,
                    PROFILE_ID      = @ProfileId,
                    DESCRIPTION     = @Description,
                    TEMPLATE_DATA   = @TemplateData,
                    IS_DEFAULT      = @IsDefaultInt,
                    MODIFIED_BY     = @ModifiedBy,
                    MODIFIED_DATE   = @ModifiedDateIso
                WHERE TEMPLATE_ID = @TemplateId;";

            await conn.ExecuteAsync(updateSql, new
            {
                item.DocumentName,
                item.WellId,
                item.WellboreId,
                item.LogId,
                item.PlanId,
                item.ProfileId,
                item.Description,
                item.TemplateData,
                IsDefaultInt = item.IsDefault ? 1 : 0,
                item.ModifiedBy,
                ModifiedDateIso = nowIso,
                item.TemplateId
            });
        }
        else
        {
            string insertSql = @"
                INSERT INTO VMX_BROOMSTICK_TEMPLATES (
                    TEMPLATE_ID, DOCUMENT_NAME, WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PROFILE_ID,
                    DESCRIPTION, TEMPLATE_DATA, IS_DEFAULT, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE
                ) VALUES (
                    @TemplateId, @DocumentName, @WellId, @WellboreId, @LogId, @PlanId, @ProfileId,
                    @Description, @TemplateData, @IsDefaultInt, @CreatedBy, @CreatedDateIso, @ModifiedBy, @ModifiedDateIso
                );";

            await conn.ExecuteAsync(insertSql, new
            {
                item.TemplateId,
                item.DocumentName,
                item.WellId,
                item.WellboreId,
                item.LogId,
                item.PlanId,
                item.ProfileId,
                item.Description,
                item.TemplateData,
                IsDefaultInt = item.IsDefault ? 1 : 0,
                item.CreatedBy,
                CreatedDateIso = item.CreatedDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? nowIso,
                item.ModifiedBy,
                ModifiedDateIso = nowIso
            });
        }
    }

    public async Task DeleteTemplateAsync(string templateId)
    {
        var conn = GetConnection();
        if (conn == null || string.IsNullOrWhiteSpace(templateId)) return;

        await EnsureTableAsync(conn);

        const string sql = "DELETE FROM VMX_BROOMSTICK_TEMPLATES WHERE TEMPLATE_ID = @templateId;";
        await conn.ExecuteAsync(sql, new { templateId });
    }

    public async Task SetDefaultTemplateAsync(string templateId, string wellId)
    {
        var conn = GetConnection();
        if (conn == null || string.IsNullOrWhiteSpace(templateId)) return;

        await EnsureTableAsync(conn);

        const string clearSql = "UPDATE VMX_BROOMSTICK_TEMPLATES SET IS_DEFAULT = 0 WHERE WELL_ID = @wellId;";
        await conn.ExecuteAsync(clearSql, new { wellId });

        const string setSql = "UPDATE VMX_BROOMSTICK_TEMPLATES SET IS_DEFAULT = 1 WHERE TEMPLATE_ID = @templateId;";
        await conn.ExecuteAsync(setSql, new { templateId });
    }

    public async Task<BroomstickTemplateItem> DuplicateTemplateAsync(string sourceTemplateId, string newDocumentName)
    {
        var source = await GetTemplateByIdAsync(sourceTemplateId);
        if (source == null)
            throw new InvalidOperationException($"Source template '{sourceTemplateId}' not found.");

        var clone = source.Clone(newDocumentName);
        await SaveTemplateAsync(clone);
        return clone;
    }
}

