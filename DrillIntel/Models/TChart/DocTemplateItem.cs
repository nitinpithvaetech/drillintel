using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Domain model representing a saved document template record in VMX_DOC_TEMPLATES (VuMaxDR compatible).
/// </summary>
public class DocTemplateItem
{
    public const string TypeRigState = "RIG_STATE";
    public const string TypeTimeLog = "TIME_LOG";
    public const string TypeDepthLog = "DEPTH_LOG";

    public string TemplateType { get; set; } = TypeRigState;
    public string TemplateId { get; set; } = Guid.NewGuid().ToString();
    public string DocumentName { get; set; } = string.Empty;
    public string? WellId { get; set; }
    public string? WellboreId { get; set; }
    public string? LogId { get; set; }
    public string? Description { get; set; }
    public byte[]? TemplateData { get; set; }
    public bool IsDefault { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public DocTemplateItem Clone(string newName)
    {
        return new DocTemplateItem
        {
            TemplateType = this.TemplateType,
            TemplateId = Guid.NewGuid().ToString(),
            DocumentName = newName,
            WellId = this.WellId,
            WellboreId = this.WellboreId,
            LogId = this.LogId,
            Description = this.Description,
            TemplateData = this.TemplateData != null ? (byte[])this.TemplateData.Clone() : null,
            IsDefault = false,
            CreatedBy = Environment.UserName,
            CreatedDate = DateTime.Now,
            ModifiedBy = Environment.UserName,
            ModifiedDate = DateTime.Now
        };
    }
}

