using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Models.TChart;
using DrillIntel.Projects;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class DocTemplateTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly DocTemplateRepository _repo;

    public DocTemplateTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_template_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(_tempDbPath);
        _session = new ProjectSession();
        _session.Load(_tempDbPath);
        _repo = new DocTemplateRepository(_session);
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
    public async Task EnsureDefaultTemplate_CreatesAndReturnsDefault()
    {
        var template = await _repo.EnsureDefaultRigStateTemplateAsync();

        Assert.NotNull(template);
        Assert.Equal("Default Rig State Overview", template.DocumentName);
        Assert.True(template.IsDefault);
        Assert.NotNull(template.TemplateData);
        Assert.True(template.TemplateData.Length > 0);

        var docData = RigStateDocumentData.FromBytes(template.TemplateData);
        Assert.NotNull(docData);
        Assert.Equal(4, docData.ConsoleModel.Tracks.Count);
        Assert.Equal(enumTrackOrientation.Horizontal, docData.ConsoleModel.TrackOrientation);
    }

    [Fact]
    public async Task SaveTemplate_And_GetTemplates_FullCrudCycle()
    {
        await _repo.EnsureDefaultRigStateTemplateAsync();

        var customDoc = new RigStateDocumentData
        {
            DocumentName = "Tripping Analysis Track",
            Description = "Custom track for tripping in/out of hole.",
            SelectedPreset = "Last 12 Hours",
            MaxPoints = 5000
        };

        var item = new DocTemplateItem
        {
            TemplateType = DocTemplateItem.TypeRigState,
            TemplateId = "tmpl-custom-trip",
            DocumentName = "Tripping Analysis Track",
            Description = "Custom track for tripping in/out of hole.",
            TemplateData = customDoc.ToBytes(),
            IsDefault = false
        };

        await _repo.SaveTemplateAsync(item);

        var all = await _repo.GetTemplatesAsync(DocTemplateItem.TypeRigState);
        Assert.Equal(2, all.Count);

        var retrieved = await _repo.GetTemplateByIdAsync(DocTemplateItem.TypeRigState, "tmpl-custom-trip");
        Assert.NotNull(retrieved);
        Assert.Equal("Tripping Analysis Track", retrieved.DocumentName);

        // Update Document
        retrieved.DocumentName = "Tripping Analysis Track (Updated)";
        await _repo.SaveTemplateAsync(retrieved);

        var updated = await _repo.GetTemplateByIdAsync(DocTemplateItem.TypeRigState, "tmpl-custom-trip");
        Assert.NotNull(updated);
        Assert.Equal("Tripping Analysis Track (Updated)", updated.DocumentName);

        // Delete Document
        await _repo.DeleteTemplateAsync(DocTemplateItem.TypeRigState, "tmpl-custom-trip");
        var afterDelete = await _repo.GetTemplatesAsync(DocTemplateItem.TypeRigState);
        Assert.Single(afterDelete);
    }

    [Fact]
    public async Task SetDefaultTemplate_UpdatesDatabaseCorrectly()
    {
        var def = await _repo.EnsureDefaultRigStateTemplateAsync();

        var secondDoc = new DocTemplateItem
        {
            TemplateType = DocTemplateItem.TypeRigState,
            TemplateId = "tmpl-second",
            DocumentName = "Second View",
            TemplateData = new RigStateDocumentData { DocumentName = "Second View" }.ToBytes(),
            IsDefault = false
        };
        await _repo.SaveTemplateAsync(secondDoc);

        // Set second doc as default
        await _repo.SetDefaultTemplateAsync(DocTemplateItem.TypeRigState, "tmpl-second");

        var currentDefault = await _repo.GetDefaultTemplateAsync(DocTemplateItem.TypeRigState);
        Assert.NotNull(currentDefault);
        Assert.Equal("tmpl-second", currentDefault.TemplateId);
        Assert.True(currentDefault.IsDefault);

        var prevDefault = await _repo.GetTemplateByIdAsync(DocTemplateItem.TypeRigState, def.TemplateId);
        Assert.NotNull(prevDefault);
        Assert.False(prevDefault.IsDefault);
    }

    [Fact]
    public async Task DuplicateTemplate_CreatesExactCopy()
    {
        var original = await _repo.EnsureDefaultRigStateTemplateAsync();

        var duplicate = await _repo.DuplicateTemplateAsync(DocTemplateItem.TypeRigState, original.TemplateId, "Copy of Default");

        Assert.NotNull(duplicate);
        Assert.NotEqual(original.TemplateId, duplicate.TemplateId);
        Assert.Equal("Copy of Default", duplicate.DocumentName);
        Assert.False(duplicate.IsDefault);
        Assert.NotNull(duplicate.TemplateData);
    }

    [Fact]
    public void RigStateDocumentData_SerializationRoundTrip_PreservesAllProperties()
    {
        var original = new RigStateDocumentData
        {
            DocumentName = "Multi-Channel Track View",
            Description = "Test description",
            SelectedPreset = "Last 48 Hours",
            FromDate = new DateTime(2026, 10, 3, 10, 0, 0),
            ToDate = new DateTime(2026, 10, 3, 22, 0, 0),
            MaxPoints = 8000
        };

        var track = new VHTrack { ID = "t1", Title = "Main Track", Width = 2.5 };
        var ch = new VHTrackChannel
        {
            ID = "c1",
            Mnemonic = "RPM",
            Title = "Rotary RPM",
            LineColor = "#FF0000",
            LineWidth = 3,
            ColorCodeAsRigState = true
        };
        track.Channels.Add(ch);
        original.ConsoleModel.Tracks.Add(track);
        original.ConsoleModel.TrackOrientation = enumTrackOrientation.Horizontal;

        byte[] bytes = original.ToBytes();
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        var restored = RigStateDocumentData.FromBytes(bytes);
        Assert.NotNull(restored);
        Assert.Equal("Multi-Channel Track View", restored.DocumentName);
        Assert.Equal("Last 48 Hours", restored.SelectedPreset);
        Assert.Equal(8000, restored.MaxPoints);
        Assert.Equal(enumTrackOrientation.Horizontal, restored.ConsoleModel.TrackOrientation);
        Assert.Single(restored.ConsoleModel.Tracks);

        var restoredTrack = restored.ConsoleModel.Tracks[0];
        Assert.Equal("Main Track", restoredTrack.Title);
        Assert.Equal(2.5, restoredTrack.Width);
        Assert.Single(restoredTrack.Channels);

        var restoredCh = restoredTrack.Channels[0];
        Assert.Equal("RPM", restoredCh.Mnemonic);
        Assert.Equal("#FF0000", restoredCh.LineColor);
        Assert.Equal(3, restoredCh.LineWidth);
        Assert.True(restoredCh.ColorCodeAsRigState);
    }

    [Fact]
    public async Task SaveDocumentAsViewModel_ValidatesNameAndDuplicates()
    {
        await _repo.EnsureDefaultRigStateTemplateAsync();

        var vm = new SaveDocumentAsViewModel(_repo);

        // 1. Empty name error
        vm.DocumentName = "   ";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);
        Assert.Contains("cannot be empty", vm.ErrorMessage);

        // 2. Duplicate name error
        vm.DocumentName = "Default Rig State Overview";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);
        Assert.Contains("already exists", vm.ErrorMessage);

        // 3. Valid name
        vm.DocumentName = "Unique Document Name";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.HasError);
        Assert.True(vm.DialogResult);
    }

    [Fact]
    public async Task RigStateDocumentManagerViewModel_LoadsAndManagesDocuments()
    {
        await _repo.EnsureDefaultRigStateTemplateAsync();

        var mgr = new RigStateDocumentManagerViewModel(_repo);
        await mgr.InitializeAsync();

        Assert.Single(mgr.Documents);
        Assert.NotNull(mgr.SelectedDocument);
        Assert.Equal("Default Rig State Overview", mgr.SelectedDocument.DocumentName);
        Assert.True(mgr.SelectedDocument.IsDefault);

        // Cannot delete only remaining document
        Assert.False(mgr.CanDeleteSelection);
    }

    [Fact]
    public async Task RigStateWorkspaceViewModel_Initialize_LoadsTreeAndOpensDefaultTab()
    {
        await _repo.EnsureDefaultRigStateTemplateAsync();

        var workspace = new RigStateWorkspaceViewModel(_session, _repo);
        await workspace.InitializeAsync();

        Assert.Equal(1, workspace.TotalDocumentCount);
        Assert.Single(workspace.AllDocumentNodes);
        Assert.Single(workspace.RootCategoryNode.Children);
        Assert.Equal(1, workspace.RootCategoryNode.DocumentCount);

        // Default tab opened automatically
        Assert.True(workspace.HasOpenDocuments);
        Assert.Single(workspace.OpenDocuments);
        Assert.NotNull(workspace.ActiveDocument);
        Assert.Equal("Default Rig State Overview", workspace.ActiveDocument.DocumentName);
    }

    [Fact]
    public async Task RigStateWorkspaceViewModel_OpenDocumentNode_PreventsDuplicates()
    {
        var t1 = await _repo.EnsureDefaultRigStateTemplateAsync();
        var t2 = await _repo.DuplicateTemplateAsync(DocTemplateItem.TypeRigState, t1.TemplateId, "Lateral Plot Overview");

        var workspace = new RigStateWorkspaceViewModel(_session, _repo);
        await workspace.InitializeAsync();

        Assert.Equal(2, workspace.TotalDocumentCount);
        Assert.Single(workspace.OpenDocuments); // Only default open initially

        var node2 = workspace.AllDocumentNodes.First(n => n.TemplateId == t2.TemplateId);

        // 1. Double click node 2 -> opens new tab
        await workspace.OpenDocumentNodeAsync(node2);
        Assert.Equal(2, workspace.OpenDocuments.Count);
        Assert.Equal("Lateral Plot Overview", workspace.ActiveDocument?.DocumentName);

        // 2. Double click node 2 again -> activates existing tab, NO duplicate created
        await workspace.OpenDocumentNodeAsync(node2);
        Assert.Equal(2, workspace.OpenDocuments.Count);
        Assert.Equal("Lateral Plot Overview", workspace.ActiveDocument?.DocumentName);

        // 3. Double click node 1 -> switches active tab to node 1, count remains 2
        var node1 = workspace.AllDocumentNodes.First(n => n.TemplateId == t1.TemplateId);
        await workspace.OpenDocumentNodeAsync(node1);
        Assert.Equal(2, workspace.OpenDocuments.Count);
        Assert.Equal("Default Rig State Overview", workspace.ActiveDocument?.DocumentName);
    }

    [Fact]
    public async Task RigStateWorkspaceViewModel_CloseDocumentTab_SelectsAdjacentTab()
    {
        var t1 = await _repo.EnsureDefaultRigStateTemplateAsync();
        var t2 = await _repo.DuplicateTemplateAsync(DocTemplateItem.TypeRigState, t1.TemplateId, "Document 2");

        var workspace = new RigStateWorkspaceViewModel(_session, _repo);
        await workspace.InitializeAsync();

        var node2 = workspace.AllDocumentNodes.First(n => n.TemplateId == t2.TemplateId);
        await workspace.OpenDocumentNodeAsync(node2);
        Assert.Equal(2, workspace.OpenDocuments.Count);

        var doc2Tab = workspace.OpenDocuments.First(d => d.CurrentTemplateId == t2.TemplateId);
        Assert.Same(doc2Tab, workspace.ActiveDocument);

        // Close doc2 tab -> closes and switches active document to remaining tab
        await workspace.CloseDocumentTabAsync(doc2Tab);
        Assert.Single(workspace.OpenDocuments);
        Assert.Equal(t1.TemplateId, workspace.ActiveDocument?.CurrentTemplateId);
    }

    [Fact]
    public async Task RigStateWorkspaceViewModel_MakeDefault_UpdatesBadgesAndRepository()
    {
        var t1 = await _repo.EnsureDefaultRigStateTemplateAsync();
        var t2 = await _repo.DuplicateTemplateAsync(DocTemplateItem.TypeRigState, t1.TemplateId, "New Default Document");

        var workspace = new RigStateWorkspaceViewModel(_session, _repo);
        await workspace.InitializeAsync();

        var node1 = workspace.AllDocumentNodes.First(n => n.TemplateId == t1.TemplateId);
        var node2 = workspace.AllDocumentNodes.First(n => n.TemplateId == t2.TemplateId);

        Assert.True(node1.IsDefault);
        Assert.False(node2.IsDefault);

        // Make node2 default
        await workspace.MakeDefaultAsync(node2);

        Assert.False(node1.IsDefault);
        Assert.True(node2.IsDefault);

        var repoDefault = await _repo.GetDefaultTemplateAsync(DocTemplateItem.TypeRigState);
        Assert.NotNull(repoDefault);
        Assert.Equal(t2.TemplateId, repoDefault.TemplateId);
    }

    [Fact]
    public async Task RigStateWorkspaceViewModel_DeleteWhileDirty_AbortsWhenUserRejects()
    {
        var t1 = await _repo.EnsureDefaultRigStateTemplateAsync();
        var t2 = await _repo.DuplicateTemplateAsync(DocTemplateItem.TypeRigState, t1.TemplateId, "Dirty Test Document");

        var workspace = new RigStateWorkspaceViewModel(_session, _repo);
        await workspace.InitializeAsync();

        var node2 = workspace.AllDocumentNodes.First(n => n.TemplateId == t2.TemplateId);
        await workspace.OpenDocumentNodeAsync(node2);

        var openTab2 = workspace.OpenDocuments.First(d => d.CurrentTemplateId == t2.TemplateId);
        openTab2.IsDirty = true; // Mark as unsaved/dirty

        // Hook handler to simulate user clicking "No" (abort)
        bool dirtyPromptShown = false;
        workspace.ConfirmDeleteDirtyHandler = (title, message) =>
        {
            dirtyPromptShown = true;
            Assert.Contains("unsaved changes", message, StringComparison.OrdinalIgnoreCase);
            return false; // User says NO
        };

        await workspace.DeleteDocumentAsync(node2);

        Assert.True(dirtyPromptShown);
        // Deletion aborted: document remains in repo, tree, and open tabs
        Assert.Equal(2, workspace.TotalDocumentCount);
        Assert.Equal(2, workspace.OpenDocuments.Count);
        Assert.NotNull(await _repo.GetTemplateByIdAsync(DocTemplateItem.TypeRigState, t2.TemplateId));
    }

    [Fact]
    public async Task RigStateWorkspaceViewModel_DeleteWhileDirty_ProceedsWhenUserConfirms()
    {
        var t1 = await _repo.EnsureDefaultRigStateTemplateAsync();
        var t2 = await _repo.DuplicateTemplateAsync(DocTemplateItem.TypeRigState, t1.TemplateId, "Dirty Confirm Document");

        var workspace = new RigStateWorkspaceViewModel(_session, _repo);
        await workspace.InitializeAsync();

        var node2 = workspace.AllDocumentNodes.First(n => n.TemplateId == t2.TemplateId);
        await workspace.OpenDocumentNodeAsync(node2);

        var openTab2 = workspace.OpenDocuments.First(d => d.CurrentTemplateId == t2.TemplateId);
        openTab2.IsDirty = true; // Mark as unsaved/dirty

        // Hook handler to simulate user clicking "Yes"
        workspace.ConfirmDeleteDirtyHandler = (title, message) =>
        {
            Assert.Contains("unsaved changes", message, StringComparison.OrdinalIgnoreCase);
            return true; // User says YES
        };

        await workspace.DeleteDocumentAsync(node2);

        // Deletion succeeds: document removed from repo, tree, and tab closed
        Assert.Equal(1, workspace.TotalDocumentCount);
        Assert.Single(workspace.OpenDocuments);
        Assert.Null(await _repo.GetTemplateByIdAsync(DocTemplateItem.TypeRigState, t2.TemplateId));
    }

    [Fact]
    public async Task RigStateWorkspaceViewModel_SearchFilter_FiltersTreeNodes()
    {
        var t1 = await _repo.EnsureDefaultRigStateTemplateAsync();
        await _repo.DuplicateTemplateAsync(DocTemplateItem.TypeRigState, t1.TemplateId, "Lateral Survey QA");
        await _repo.DuplicateTemplateAsync(DocTemplateItem.TypeRigState, t1.TemplateId, "Drilling Mechanics SPP");

        var workspace = new RigStateWorkspaceViewModel(_session, _repo);
        await workspace.InitializeAsync();

        Assert.Equal(3, workspace.RootCategoryNode.Children.Count);

        // Filter by "Lateral"
        workspace.SearchFilter = "Lateral";
        Assert.Single(workspace.RootCategoryNode.Children);
        Assert.Equal("Lateral Survey QA", workspace.RootCategoryNode.Children[0].DocumentName);

        // Filter by non-existent text
        workspace.SearchFilter = "NonExistentName123";
        Assert.Empty(workspace.RootCategoryNode.Children);

        // Clear filter
        workspace.SearchFilter = string.Empty;
        Assert.Equal(3, workspace.RootCategoryNode.Children.Count);
    }
}

