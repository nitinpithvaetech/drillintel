using System.Collections.Generic;
using System.Threading.Tasks;
using DrillIntel.Models.Broomstick;

namespace DrillIntel.Data;

/// <summary>
/// Data repository contract for managing Broomstick document templates in VMX_BROOMSTICK_TEMPLATES.
/// </summary>
public interface IBroomstickTemplateRepository
{
    Task<List<BroomstickTemplateItem>> GetTemplatesAsync(string wellId, string? logId = null);
    Task<List<BroomstickTemplateItem>> GetAllTemplatesAsync();
    Task<BroomstickTemplateItem?> GetTemplateByIdAsync(string templateId);
    Task<BroomstickTemplateItem?> GetDefaultTemplateAsync(string wellId);
    Task<bool> TemplateNameExistsAsync(string wellId, string documentName, string? excludeTemplateId = null);
    Task SaveTemplateAsync(BroomstickTemplateItem item);
    Task DeleteTemplateAsync(string templateId);
    Task SetDefaultTemplateAsync(string templateId, string wellId);
    Task<BroomstickTemplateItem> DuplicateTemplateAsync(string sourceTemplateId, string newDocumentName);
    Task EnsureTableAsync();
}

