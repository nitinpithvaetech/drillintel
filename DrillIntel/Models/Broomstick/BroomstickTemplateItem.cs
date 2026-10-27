using System;

namespace DrillIntel.Models.Broomstick;

/// <summary>
/// Domain model representing a saved Broomstick document template in VMX_BROOMSTICK_TEMPLATES.
/// Includes PLAN_ID linking directly to theoretical hookload plans in VMX_ADNL_HKLD_PLAN.
/// </summary>
public class BroomstickTemplateItem
{
    public string TemplateId { get; set; } = Guid.NewGuid().ToString();
    public string DocumentName { get; set; } = string.Empty;
    public string WellId { get; set; } = string.Empty;
    public string? WellboreId { get; set; }
    public string? LogId { get; set; }
    public string? PlanId { get; set; }
    public string? ProfileId { get; set; }
    public string? Description { get; set; }
    public byte[]? TemplateData { get; set; }
    public bool IsDefault { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public BroomstickTemplateItem Clone(string newName)
    {
        return new BroomstickTemplateItem
        {
            TemplateId = Guid.NewGuid().ToString(),
            DocumentName = newName,
            WellId = this.WellId,
            WellboreId = this.WellboreId,
            LogId = this.LogId,
            PlanId = this.PlanId,
            ProfileId = this.ProfileId,
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

