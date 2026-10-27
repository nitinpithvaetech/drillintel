using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Models.Broomstick;
using DrillIntel.Projects;
using Xunit;

namespace DrillIntel.Tests;

public class BroomstickTemplateRepositoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly BroomstickTemplateRepository _repo;

    public BroomstickTemplateRepositoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_bs_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(_tempDbPath);
        _session = new ProjectSession();
        _session.Load(_tempDbPath);
        _repo = new BroomstickTemplateRepository(_session);
    }

    public void Dispose()
    {
        _session.Close();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public async Task EnsureTableAsync_CreatesTableWithPlanIdColumn()
    {
        await _repo.EnsureTableAsync();

        var templates = await _repo.GetAllTemplatesAsync();
        Assert.NotNull(templates);
        Assert.Empty(templates);
    }

    [Fact]
    public async Task SaveTemplate_And_GetTemplates_FullCrudWithPlanId()
    {
        var item = new BroomstickTemplateItem
        {
            TemplateId = "tpl-85-drilling",
            DocumentName = "8.5\" Hookload Drilling Overview",
            WellId = "WELL-001",
            WellboreId = "WB-01",
            LogId = "TIME-01",
            PlanId = "PLAN-8.5-DRILL",
            ProfileId = "PROFILE-DEFAULT",
            Description = "Standard 8.5 inch hole section broomstick template",
            IsDefault = true,
            TemplateData = new byte[] { 1, 2, 3, 4, 5 }
        };

        await _repo.SaveTemplateAsync(item);

        // Verify retrieval by well
        var list = await _repo.GetTemplatesAsync("WELL-001");
        Assert.Single(list);
        var loaded = list[0];
        Assert.Equal("tpl-85-drilling", loaded.TemplateId);
        Assert.Equal("8.5\" Hookload Drilling Overview", loaded.DocumentName);
        Assert.Equal("WELL-001", loaded.WellId);
        Assert.Equal("PLAN-8.5-DRILL", loaded.PlanId);
        Assert.Equal("PROFILE-DEFAULT", loaded.ProfileId);
        Assert.True(loaded.IsDefault);
        Assert.NotNull(loaded.TemplateData);
        Assert.Equal(5, loaded.TemplateData.Length);

        // Verify retrieval by ID
        var byId = await _repo.GetTemplateByIdAsync("tpl-85-drilling");
        Assert.NotNull(byId);
        Assert.Equal("PLAN-8.5-DRILL", byId.PlanId);

        // Verify duplication
        var cloned = await _repo.DuplicateTemplateAsync("tpl-85-drilling", "8.5\" Drilling (Cloned)");
        Assert.NotNull(cloned);
        Assert.NotEqual("tpl-85-drilling", cloned.TemplateId);
        Assert.Equal("8.5\" Drilling (Cloned)", cloned.DocumentName);
        Assert.Equal("PLAN-8.5-DRILL", cloned.PlanId);
        Assert.False(cloned.IsDefault);

        var all = await _repo.GetAllTemplatesAsync();
        Assert.Equal(2, all.Count);

        // Verify delete
        await _repo.DeleteTemplateAsync(cloned.TemplateId);
        var afterDelete = await _repo.GetAllTemplatesAsync();
        Assert.Single(afterDelete);
    }

    [Fact]
    public async Task SetDefaultTemplateAsync_SwitchesDefaultFlagWithinWell()
    {
        var item1 = new BroomstickTemplateItem
        {
            TemplateId = "tpl-1",
            DocumentName = "Document 1",
            WellId = "WELL-A",
            PlanId = "PLAN-1",
            IsDefault = true
        };

        var item2 = new BroomstickTemplateItem
        {
            TemplateId = "tpl-2",
            DocumentName = "Document 2",
            WellId = "WELL-A",
            PlanId = "PLAN-2",
            IsDefault = false
        };

        await _repo.SaveTemplateAsync(item1);
        await _repo.SaveTemplateAsync(item2);

        // Currently item1 is default
        var def1 = await _repo.GetDefaultTemplateAsync("WELL-A");
        Assert.NotNull(def1);
        Assert.Equal("tpl-1", def1.TemplateId);

        // Switch default to item2
        await _repo.SetDefaultTemplateAsync("tpl-2", "WELL-A");

        var def2 = await _repo.GetDefaultTemplateAsync("WELL-A");
        Assert.NotNull(def2);
        Assert.Equal("tpl-2", def2.TemplateId);

        var reloaded1 = await _repo.GetTemplateByIdAsync("tpl-1");
        Assert.NotNull(reloaded1);
        Assert.False(reloaded1.IsDefault);
    }

    [Fact]
    public async Task TemplateNameExistsAsync_DetectsDuplicates()
    {
        var item = new BroomstickTemplateItem
        {
            TemplateId = "tpl-abc",
            DocumentName = "Production String",
            WellId = "WELL-100",
            PlanId = "PLAN-100"
        };
        await _repo.SaveTemplateAsync(item);

        Assert.True(await _repo.TemplateNameExistsAsync("WELL-100", "Production String"));
        Assert.True(await _repo.TemplateNameExistsAsync("WELL-100", "production string")); // case-insensitive
        Assert.False(await _repo.TemplateNameExistsAsync("WELL-100", "production string", excludeTemplateId: "tpl-abc"));
        Assert.False(await _repo.TemplateNameExistsAsync("WELL-100", "Unknown Name"));
    }
}

