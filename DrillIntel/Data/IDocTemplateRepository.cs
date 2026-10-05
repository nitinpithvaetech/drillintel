using System.Collections.Generic;
using System.Threading.Tasks;
using DrillIntel.Models.TChart;

namespace DrillIntel.Data;

/// <summary>
/// Data repository for managing document templates in VMX_DOC_TEMPLATES.
/// </summary>
public interface IDocTemplateRepository
{
    Task<List<DocTemplateItem>> GetTemplatesAsync(string templateType = DocTemplateItem.TypeRigState);
    Task<DocTemplateItem?> GetTemplateByIdAsync(string templateType, string templateId);
    Task<DocTemplateItem?> GetDefaultTemplateAsync(string templateType = DocTemplateItem.TypeRigState);
    Task<bool> TemplateNameExistsAsync(string templateType, string documentName, string? excludeTemplateId = null);
    Task SaveTemplateAsync(DocTemplateItem item);
    Task DeleteTemplateAsync(string templateType, string templateId);
    Task SetDefaultTemplateAsync(string templateType, string templateId);
    Task<DocTemplateItem> DuplicateTemplateAsync(string templateType, string sourceTemplateId, string newDocumentName);
    Task<DocTemplateItem> EnsureDefaultRigStateTemplateAsync();
}

