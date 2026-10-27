using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Models.Broomstick;
using DrillIntel.Projects;
using DrillIntel.ViewModels.Broomstick;
using Xunit;

namespace DrillIntel.Tests;

public class BroomstickDocumentManagerViewModelTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly BroomstickTemplateRepository _repo;

    public BroomstickDocumentManagerViewModelTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_bs_mgr_test_{Guid.NewGuid():N}.dintel");
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

    private async Task SeedTemplatesAsync()
    {
        await _repo.SaveTemplateAsync(new BroomstickTemplateItem
        {
            TemplateId = "t1",
            DocumentName = "8.5\" Hookload Drilling",
            WellId = "Etech 420",
            PlanId = "PLAN_8.5_DRILL",
            IsDefault = true
        });

        await _repo.SaveTemplateAsync(new BroomstickTemplateItem
        {
            TemplateId = "t2",
            DocumentName = "12.25\" Casing Run Overview",
            WellId = "Etech 420",
            PlanId = "PLAN_12.25_CSG",
            IsDefault = false
        });

        await _repo.SaveTemplateAsync(new BroomstickTemplateItem
        {
            TemplateId = "t3",
            DocumentName = "6.125\" Deep Lateral",
            WellId = "Well Alpha",
            PlanId = "PLAN_6.125_LATERAL",
            IsDefault = false
        });
    }

    [Fact]
    public async Task InitializeAsync_BuildsHierarchicalTreeByWellAndPlan()
    {
        await SeedTemplatesAsync();

        var vm = new BroomstickDocumentManagerViewModel(_repo, "Etech 420");
        await vm.InitializeAsync();

        Assert.Equal(3, vm.AllDocumentNodes.Count);
        Assert.Equal(2, vm.TreeRoots.Count); // 2 Wells: "Well: Etech 420" and "Well: Well Alpha"

        var etechWell = vm.TreeRoots.FirstOrDefault(w => w.GroupId == "Etech 420");
        Assert.NotNull(etechWell);
        Assert.Equal(2, etechWell.DocumentCount);
        Assert.Equal(2, etechWell.Children.Count); // 2 Plans under Etech 420

        var defaultNode = vm.SelectedDocumentNode;
        Assert.NotNull(defaultNode);
        Assert.Equal("t1", defaultNode.TemplateId);
        Assert.True(defaultNode.IsDefault);
    }

    [Fact]
    public async Task SearchFilter_FiltersNodesDynamically()
    {
        await SeedTemplatesAsync();

        var vm = new BroomstickDocumentManagerViewModel(_repo, "Etech 420");
        await vm.InitializeAsync();

        // Search for "Casing"
        vm.SearchFilter = "Casing";

        var t1 = vm.AllDocumentNodes.First(n => n.TemplateId == "t1");
        var t2 = vm.AllDocumentNodes.First(n => n.TemplateId == "t2");

        Assert.False(t1.IsVisibleInFilter);
        Assert.True(t2.IsVisibleInFilter);

        // Clear filter
        vm.SearchFilter = "";
        Assert.True(t1.IsVisibleInFilter);
        Assert.True(t2.IsVisibleInFilter);
    }

    [Fact]
    public async Task SetDefaultCommand_UpdatesDefaultFlagsInTreeAndRepo()
    {
        await SeedTemplatesAsync();

        var vm = new BroomstickDocumentManagerViewModel(_repo, "Etech 420");
        await vm.InitializeAsync();

        var t2 = vm.AllDocumentNodes.First(n => n.TemplateId == "t2");
        vm.SelectedDocumentNode = t2;

        Assert.True(vm.CanSetDefaultSelection);
        await vm.SetDefaultCommand.ExecuteAsync(null);

        Assert.True(t2.IsDefault);
        var t1 = vm.AllDocumentNodes.First(n => n.TemplateId == "t1");
        Assert.False(t1.IsDefault);

        var repoDef = await _repo.GetDefaultTemplateAsync("Etech 420");
        Assert.NotNull(repoDef);
        Assert.Equal("t2", repoDef.TemplateId);
    }

    [Fact]
    public async Task NewTemplateCommand_CreatesAndAddsToTree()
    {
        await _repo.EnsureTableAsync();

        var vm = new BroomstickDocumentManagerViewModel(_repo, "Etech 420")
        {
            PromptNewNameHandler = (title, msg) => "My New Broomstick Plan"
        };
        await vm.InitializeAsync();

        await vm.NewTemplateCommand.ExecuteAsync(null);

        Assert.Single(vm.AllDocumentNodes);
        Assert.Equal("My New Broomstick Plan", vm.AllDocumentNodes[0].DocumentName);
        Assert.Single(vm.TreeRoots);
    }

    [Fact]
    public async Task DeleteTemplateCommand_RemovesFromTreeAndRepo()
    {
        await SeedTemplatesAsync();

        var vm = new BroomstickDocumentManagerViewModel(_repo, "Etech 420")
        {
            ConfirmDeleteHandler = (t, m) => true
        };
        await vm.InitializeAsync();

        var t2 = vm.AllDocumentNodes.First(n => n.TemplateId == "t2");
        vm.SelectedDocumentNode = t2;

        Assert.True(vm.CanDeleteSelection);
        await vm.DeleteTemplateCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.AllDocumentNodes.Count);
        Assert.Null(await _repo.GetTemplateByIdAsync("t2"));
    }
}

