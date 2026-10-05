using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DrillIntel.Models.TChart;
using DrillIntel.Projects;

namespace DrillIntel.Data;

/// <summary>
/// Data repository for managing document templates in VMX_DOC_TEMPLATES.
/// Provides full CRUD, default management, duplication, and baseline seeding.
/// </summary>
public class DocTemplateRepository : IDocTemplateRepository
{
    private readonly ProjectSession _session;

    public DocTemplateRepository(ProjectSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task<List<DocTemplateItem>> GetTemplatesAsync(string templateType = DocTemplateItem.TypeRigState)
    {
        if (!_session.IsProjectOpen) return new List<DocTemplateItem>();

        var conn = _session.GetConnection();
        string sql = @"
            SELECT 
                TEMPLATE_TYPE   AS TemplateType,
                TEMPLATE_ID     AS TemplateId,
                DOCUMENT_NAME   AS DocumentName,
                WELL_ID         AS WellId,
                WELLBORE_ID     AS WellboreId,
                LOG_ID          AS LogId,
                DESCRIPTION     AS Description,
                TEMPLATE_DATA   AS TemplateData,
                IS_DEFAULT      AS IsDefault,
                CREATED_BY      AS CreatedBy,
                CREATED_DATE    AS CreatedDateRaw,
                MODIFIED_BY     AS ModifiedBy,
                MODIFIED_DATE   AS ModifiedDateRaw
            FROM VMX_DOC_TEMPLATES
            WHERE TEMPLATE_TYPE = @templateType
            ORDER BY IS_DEFAULT DESC, DOCUMENT_NAME ASC;";

        var rows = await conn.QueryAsync<dynamic>(sql, new { templateType });
        var list = new List<DocTemplateItem>();

        foreach (var r in rows)
        {
            var item = new DocTemplateItem
            {
                TemplateType = (string)(r.TemplateType ?? DocTemplateItem.TypeRigState),
                TemplateId = (string)(r.TemplateId ?? string.Empty),
                DocumentName = (string)(r.DocumentName ?? string.Empty),
                WellId = (string?)r.WellId,
                WellboreId = (string?)r.WellboreId,
                LogId = (string?)r.LogId,
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

    public async Task<DocTemplateItem?> GetTemplateByIdAsync(string templateType, string templateId)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(templateId)) return null;

        var all = await GetTemplatesAsync(templateType);
        return all.FirstOrDefault(t => string.Equals(t.TemplateId, templateId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DocTemplateItem?> GetDefaultTemplateAsync(string templateType = DocTemplateItem.TypeRigState)
    {
        if (!_session.IsProjectOpen) return null;

        var all = await GetTemplatesAsync(templateType);
        return all.FirstOrDefault(t => t.IsDefault) ?? all.FirstOrDefault();
    }

    public async Task<bool> TemplateNameExistsAsync(string templateType, string documentName, string? excludeTemplateId = null)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(documentName)) return false;

        var conn = _session.GetConnection();
        string sql = @"
            SELECT COUNT(1) 
            FROM VMX_DOC_TEMPLATES 
            WHERE TEMPLATE_TYPE = @templateType 
              AND LOWER(DOCUMENT_NAME) = LOWER(@documentName)" +
            (!string.IsNullOrWhiteSpace(excludeTemplateId) ? " AND TEMPLATE_ID != @excludeTemplateId" : "") + ";";

        int count = await conn.ExecuteScalarAsync<int>(sql, new { templateType, documentName, excludeTemplateId });
        return count > 0;
    }

    public async Task SaveTemplateAsync(DocTemplateItem item)
    {
        if (!_session.IsProjectOpen || item == null) return;

        var conn = _session.GetConnection();
        string nowIso = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        if (item.CreatedDate == null) item.CreatedDate = DateTime.Now;
        item.ModifiedDate = DateTime.Now;
        if (string.IsNullOrWhiteSpace(item.CreatedBy)) item.CreatedBy = Environment.UserName;
        item.ModifiedBy = Environment.UserName;

        string existsSql = "SELECT COUNT(1) FROM VMX_DOC_TEMPLATES WHERE TEMPLATE_TYPE = @TemplateType AND TEMPLATE_ID = @TemplateId;";
        int exists = await conn.ExecuteScalarAsync<int>(existsSql, new { item.TemplateType, item.TemplateId });

        if (exists > 0)
        {
            string updateSql = @"
                UPDATE VMX_DOC_TEMPLATES SET
                    DOCUMENT_NAME   = @DocumentName,
                    WELL_ID         = @WellId,
                    WELLBORE_ID     = @WellboreId,
                    LOG_ID          = @LogId,
                    DESCRIPTION     = @Description,
                    TEMPLATE_DATA   = @TemplateData,
                    IS_DEFAULT      = @IsDefaultInt,
                    MODIFIED_BY     = @ModifiedBy,
                    MODIFIED_DATE   = @ModifiedDateIso
                WHERE TEMPLATE_TYPE = @TemplateType AND TEMPLATE_ID = @TemplateId;";

            await conn.ExecuteAsync(updateSql, new
            {
                item.DocumentName,
                item.WellId,
                item.WellboreId,
                item.LogId,
                item.Description,
                item.TemplateData,
                IsDefaultInt = item.IsDefault ? 1 : 0,
                item.ModifiedBy,
                ModifiedDateIso = nowIso,
                item.TemplateType,
                item.TemplateId
            });
        }
        else
        {
            string insertSql = @"
                INSERT INTO VMX_DOC_TEMPLATES (
                    TEMPLATE_TYPE, TEMPLATE_ID, DOCUMENT_NAME, WELL_ID, WELLBORE_ID, LOG_ID,
                    DESCRIPTION, TEMPLATE_DATA, IS_DEFAULT, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE
                ) VALUES (
                    @TemplateType, @TemplateId, @DocumentName, @WellId, @WellboreId, @LogId,
                    @Description, @TemplateData, @IsDefaultInt, @CreatedBy, @CreatedDateIso, @ModifiedBy, @ModifiedDateIso
                );";

            await conn.ExecuteAsync(insertSql, new
            {
                item.TemplateType,
                item.TemplateId,
                item.DocumentName,
                item.WellId,
                item.WellboreId,
                item.LogId,
                item.Description,
                item.TemplateData,
                IsDefaultInt = item.IsDefault ? 1 : 0,
                item.CreatedBy,
                CreatedDateIso = item.CreatedDate.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                item.ModifiedBy,
                ModifiedDateIso = nowIso
            });
        }

        // If this template is designated default, reset others of the same type
        if (item.IsDefault)
        {
            await conn.ExecuteAsync(
                "UPDATE VMX_DOC_TEMPLATES SET IS_DEFAULT = 0 WHERE TEMPLATE_TYPE = @TemplateType AND TEMPLATE_ID != @TemplateId;",
                new { item.TemplateType, item.TemplateId });
        }
    }

    public async Task DeleteTemplateAsync(string templateType, string templateId)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(templateId)) return;

        var conn = _session.GetConnection();
        await conn.ExecuteAsync(
            "DELETE FROM VMX_DOC_TEMPLATES WHERE TEMPLATE_TYPE = @templateType AND TEMPLATE_ID = @templateId;",
            new { templateType, templateId });
    }

    public async Task SetDefaultTemplateAsync(string templateType, string templateId)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(templateId)) return;

        var conn = _session.GetConnection();
        await conn.ExecuteAsync("UPDATE VMX_DOC_TEMPLATES SET IS_DEFAULT = 0 WHERE TEMPLATE_TYPE = @templateType;", new { templateType });
        await conn.ExecuteAsync("UPDATE VMX_DOC_TEMPLATES SET IS_DEFAULT = 1 WHERE TEMPLATE_TYPE = @templateType AND TEMPLATE_ID = @templateId;", new { templateType, templateId });
    }

    public async Task<DocTemplateItem> DuplicateTemplateAsync(string templateType, string sourceTemplateId, string newDocumentName)
    {
        var source = await GetTemplateByIdAsync(templateType, sourceTemplateId);
        if (source == null)
            throw new InvalidOperationException($"Source template '{sourceTemplateId}' not found.");

        var clone = source.Clone(newDocumentName);
        await SaveTemplateAsync(clone);
        return clone;
    }

    public async Task<DocTemplateItem> EnsureDefaultRigStateTemplateAsync()
    {
        var existing = await GetTemplatesAsync(DocTemplateItem.TypeRigState);
        if (existing.Count > 0)
        {
            return existing.FirstOrDefault(t => t.IsDefault) ?? existing[0];
        }

        // Seed initial standard template
        var console = new VHTrackConsole
        {
            Name = "Default Rig State Overview",
            IndexType = enumIndexType.TimeLog,
            TrackOrientation = enumTrackOrientation.Horizontal
        };

        // Track 1: Depth & Rig State
        var tDepth = new VHTrack { ID = "track-depth", Title = "Depth & Rig State", Width = 1.6, DisplayOrder = 0 };
        tDepth.Channels.Add(new VHTrackChannel
        {
            ID = "ch-depth",
            Mnemonic = "BIT_DEPTH",
            Title = "Bit Depth",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 2,
            LineColor = "#1976D2",
            ColorCodeAsRigState = true,
            Visible = true
        });
        console.Tracks.Add(tDepth);

        // Track 2: Loads & Forces
        var tLoads = new VHTrack { ID = "track-loads", Title = "Loads & Forces", Width = 2.0, DisplayOrder = 1 };
        tLoads.Channels.Add(new VHTrackChannel
        {
            ID = "ch-hkld",
            Mnemonic = "HKLD",
            Title = "Hookload",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 2,
            LineColor = "#388E3C",
            Visible = true
        });
        tLoads.Channels.Add(new VHTrackChannel
        {
            ID = "ch-wob",
            Mnemonic = "WOB",
            Title = "WOB",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 1.5,
            LineColor = "#F57C00",
            Visible = true
        });
        console.Tracks.Add(tLoads);

        // Track 3: Rotary
        var tRotary = new VHTrack { ID = "track-rotary", Title = "Rotary", Width = 2.0, DisplayOrder = 2 };
        tRotary.Channels.Add(new VHTrackChannel
        {
            ID = "ch-rpm",
            Mnemonic = "RPM",
            Title = "RPM",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 2,
            LineColor = "#D32F2F",
            Visible = true
        });
        tRotary.Channels.Add(new VHTrackChannel
        {
            ID = "ch-torq",
            Mnemonic = "TORQUE",
            Title = "Torque",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 1.5,
            LineColor = "#7B1FA2",
            Visible = true
        });
        console.Tracks.Add(tRotary);

        // Track 4: Hydraulics
        var tHyd = new VHTrack { ID = "track-hyd", Title = "Hydraulics", Width = 2.0, DisplayOrder = 3 };
        tHyd.Channels.Add(new VHTrackChannel
        {
            ID = "ch-spp",
            Mnemonic = "SPP",
            Title = "Standpipe Pressure",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 2,
            LineColor = "#0097A7",
            Visible = true
        });
        tHyd.Channels.Add(new VHTrackChannel
        {
            ID = "ch-flow",
            Mnemonic = "FLOW_IN",
            Title = "Flow In",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 1.5,
            LineColor = "#0288D1",
            Visible = true
        });
        console.Tracks.Add(tHyd);

        var docData = new RigStateDocumentData
        {
            DocumentName = "Default Rig State Overview",
            Description = "Standard operational overview with depth, hookload, rotary, and hydraulics tracks.",
            ConsoleModel = console,
            SelectedPreset = "Last 24 Hours",
            MaxPoints = 10000
        };

        var defaultTemplate = new DocTemplateItem
        {
            TemplateType = DocTemplateItem.TypeRigState,
            TemplateId = "tmpl-rigstate-default",
            DocumentName = "Default Rig State Overview",
            Description = "Standard operational overview with depth, hookload, rotary, and hydraulics tracks.",
            TemplateData = docData.ToBytes(),
            IsDefault = true,
            CreatedBy = "SYSTEM",
            CreatedDate = DateTime.Now,
            ModifiedBy = "SYSTEM",
            ModifiedDate = DateTime.Now
        };

        await SaveTemplateAsync(defaultTemplate);
        return defaultTemplate;
    }
}
