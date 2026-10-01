using System;
using System.IO;
using System.Linq;
using DrillIntel.Data;
using DrillIntel.Models;
using Xunit;

namespace DrillIntel.Tests;

public class UnitAndUnitConverterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly IDataServiceDIntel _dataService;

    public UnitAndUnitConverterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"drillintel_unit_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "TestApp.sqlite");

        _dataService = new DataServiceDIntel(_dbPath);
        _dataService.OpenConnection(_dbPath);

        // Initialize schema and default data on test database
        Unit.EnsureTableExists(_dataService);
        Unit.CreateDefaultUnits(_dataService);

        UnitConverter.EnsureTableExists(_dataService);
        UnitConverter.CreateDefaultConversion(_dataService);
    }

    public void Dispose()
    {
        _dataService.CloseConnection();
        _dataService.Dispose();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }

    #region Unit Model & Master Tests

    [Fact]
    public void Unit_CreateDefaultUnits_PopulatesMasterUnits()
    {
        var units = Unit.GetList(_dataService);
        Assert.NotEmpty(units);
        Assert.True(units.Count >= 20);

        var ft = units.FirstOrDefault(u => u.UnitName == "ft");
        Assert.NotNull(ft);
        Assert.Equal("Length", ft.Category);
        Assert.True(ft.IsDefault);

        var m = units.FirstOrDefault(u => u.UnitName == "m");
        Assert.NotNull(m);
        Assert.Equal("Length", m.Category);
        Assert.False(m.IsDefault);

        var psi = units.FirstOrDefault(u => u.UnitName == "psi");
        Assert.NotNull(psi);
        Assert.Equal("Pressure", psi.Category);
        Assert.True(psi.IsDefault);
    }

    [Fact]
    public void Unit_GetList_FilteredByCategory_ReturnsOnlyCategoryUnits()
    {
        var lengthUnits = Unit.GetList(_dataService, "Length");
        Assert.NotEmpty(lengthUnits);
        Assert.All(lengthUnits, u => Assert.Equal("Length", u.Category));
        Assert.Contains(lengthUnits, u => u.UnitName == "ft");
        Assert.Contains(lengthUnits, u => u.UnitName == "m");

        var tempUnits = Unit.GetList(_dataService, "Temperature");
        Assert.NotEmpty(tempUnits);
        Assert.All(tempUnits, u => Assert.Equal("Temperature", u.Category));
        Assert.Contains(tempUnits, u => u.UnitName == "degF");
        Assert.Contains(tempUnits, u => u.UnitName == "degC");
    }

    [Fact]
    public void Unit_GetUnit_ReturnsMatchingUnit_CaseInsensitive()
    {
        var unitLower = Unit.GetUnit(_dataService, "degf");
        Assert.NotNull(unitLower);
        Assert.Equal("degF", unitLower.UnitName);
        Assert.Equal("Temperature", unitLower.Category);

        var nonExistent = Unit.GetUnit(_dataService, "NonExistentUnit");
        Assert.Null(nonExistent);
    }

    [Fact]
    public void Unit_GetById_ReturnsMatchingUnit()
    {
        var ft = Unit.GetUnit(_dataService, "ft")!;
        Assert.NotNull(ft);

        var byId = Unit.GetById(_dataService, ft.ID);
        Assert.NotNull(byId);
        Assert.Equal(ft.ID, byId.ID);
        Assert.Equal("ft", byId.UnitName);
    }

    [Fact]
    public void Unit_GetDefaultUnitForCategory_ReturnsConfiguredDefault()
    {
        var defLength = Unit.GetDefaultUnitForCategory(_dataService, "Length");
        Assert.NotNull(defLength);
        Assert.Equal("ft", defLength.UnitName);
        Assert.True(defLength.IsDefault);

        var defWeight = Unit.GetDefaultUnitForCategory(_dataService, "Weight");
        Assert.NotNull(defWeight);
        Assert.Equal("klb", defWeight.UnitName);
        Assert.True(defWeight.IsDefault);
    }

    [Fact]
    public void Unit_GetCategories_ReturnsDistinctCategories()
    {
        var categories = Unit.GetCategories(_dataService);
        Assert.NotEmpty(categories);
        Assert.Contains("Length", categories);
        Assert.Contains("Weight", categories);
        Assert.Contains("Pressure", categories);
        Assert.Contains("Temperature", categories);
        Assert.Contains("Flow Rate", categories);
        Assert.Contains("Torque", categories);
    }

    [Fact]
    public void Unit_Add_InsertsNewUnit_AndGeneratesID()
    {
        var newUnit = new Unit
        {
            UnitName = "yd",
            Category = "Length",
            Description = "Yards",
            IsDefault = false
        };

        bool added = Unit.Add(_dataService, newUnit);
        Assert.True(added);
        Assert.True(newUnit.ID > 0);

        var retrieved = Unit.GetUnit(_dataService, "yd");
        Assert.NotNull(retrieved);
        Assert.Equal("Yards", retrieved.Description);
        Assert.Equal("Length", retrieved.Category);
    }

    [Fact]
    public void Unit_Add_WithIsDefault_UnsetsPreviousCategoryDefault()
    {
        var initialDefault = Unit.GetDefaultUnitForCategory(_dataService, "Length");
        Assert.NotNull(initialDefault);
        Assert.Equal("ft", initialDefault.UnitName);

        var newDefault = new Unit
        {
            UnitName = "fathoms",
            Category = "Length",
            Description = "Fathoms",
            IsDefault = true
        };

        bool added = Unit.Add(_dataService, newDefault);
        Assert.True(added);

        var currentDefault = Unit.GetDefaultUnitForCategory(_dataService, "Length");
        Assert.NotNull(currentDefault);
        Assert.Equal("fathoms", currentDefault.UnitName);

        var oldDefault = Unit.GetUnit(_dataService, "ft")!;
        Assert.False(oldDefault.IsDefault);
    }

    [Fact]
    public void Unit_Edit_UpdatesExistingUnitProperties()
    {
        var unit = Unit.GetUnit(_dataService, "m")!;
        Assert.NotNull(unit);

        unit.Description = "SI Meter Unit";
        bool updated = Unit.Edit(_dataService, unit);
        Assert.True(updated);

        var reloaded = Unit.GetUnit(_dataService, "m");
        Assert.NotNull(reloaded);
        Assert.Equal("SI Meter Unit", reloaded.Description);
    }

    [Fact]
    public void Unit_Delete_RemovesUnitByIdAndByName()
    {
        var tempUnit = new Unit("temp_u", "Custom", "Temporary", false);
        Unit.Add(_dataService, tempUnit);
        Assert.NotNull(Unit.GetUnit(_dataService, "temp_u"));

        bool deleted = Unit.Delete(_dataService, tempUnit.ID);
        Assert.True(deleted);
        Assert.Null(Unit.GetUnit(_dataService, "temp_u"));

        var tempUnit2 = new Unit("temp_u2", "Custom", "Temporary 2", false);
        Unit.Add(_dataService, tempUnit2);
        Assert.NotNull(Unit.GetUnit(_dataService, "temp_u2"));

        bool deletedByName = Unit.Delete(_dataService, "temp_u2");
        Assert.True(deletedByName);
        Assert.Null(Unit.GetUnit(_dataService, "temp_u2"));
    }

    [Fact]
    public void Unit_GetCopy_CreatesIndependentInstance()
    {
        var original = new Unit("ft", "Length", "Feet", true, 42);
        var copy = original.GetCopy();

        Assert.Equal(original.ID, copy.ID);
        Assert.Equal(original.UnitName, copy.UnitName);
        Assert.Equal(original.Category, copy.Category);
        Assert.Equal(original.Description, copy.Description);
        Assert.Equal(original.IsDefault, copy.IsDefault);

        copy.UnitName = "meters";
        Assert.Equal("ft", original.UnitName);
    }

    #endregion

    #region UnitConverter Model & Conversion Tests

    [Fact]
    public void UnitConverter_CreateDefaultConversion_PopulatesConversions()
    {
        var list = UnitConverter.GetList(_dataService);
        Assert.NotEmpty(list);
        Assert.True(list.Count >= 15);

        Assert.Contains(list, c => c.FromUnit == "ft" && c.ToUnit == "m");
        Assert.Contains(list, c => c.FromUnit == "psi" && c.ToUnit == "kPa");
        Assert.Contains(list, c => c.FromUnit == "degF" && c.ToUnit == "degC");
    }

    [Fact]
    public void UnitConverter_GetUnitConversion_DirectLookup()
    {
        var conv = UnitConverter.GetUnitConversion(_dataService, "ft", "m");
        Assert.NotNull(conv);
        Assert.Equal(0.3048, conv.Multiplier, precision: 4);
        Assert.Equal(0.0, conv.Offset, precision: 4);
    }

    [Fact]
    public void UnitConverter_GetUnitConversion_InverseLookup_WhenDirectNotStored()
    {
        // Add a one-way rule: widget -> gadgets (* 2.5 + 10)
        var rule = new UnitConverter("widget", "gadget", 2.5, 10.0, "Misc");
        UnitConverter.Add(_dataService, rule);

        // Lookup inverse: gadget -> widget
        var invConv = UnitConverter.GetUnitConversion(_dataService, "gadget", "widget");
        Assert.NotNull(invConv);
        // Inverse formula: A = (B - 10) / 2.5 = B * 0.4 - 4.0
        Assert.Equal(0.4, invConv.Multiplier, precision: 4);
        Assert.Equal(-4.0, invConv.Offset, precision: 4);

        // Test numerical conversion with inverse:
        // 35 gadgets -> (35 - 10) / 2.5 = 10 widgets
        double widgets = invConv.Convert(35.0);
        Assert.Equal(10.0, widgets, precision: 4);
    }

    [Fact]
    public void UnitConverter_GetUnitConversion_Identity_ReturnsMultiplierOne()
    {
        var identity = UnitConverter.GetUnitConversion(_dataService, "ft", "ft");
        Assert.NotNull(identity);
        Assert.Equal(1.0, identity.Multiplier);
        Assert.Equal(0.0, identity.Offset);
    }

    [Fact]
    public void UnitConverter_Convert_LengthConversions()
    {
        // 100 ft -> m = 30.48 m
        double m = UnitConverter.Convert(100.0, "ft", "m", _dataService);
        Assert.Equal(30.48, m, precision: 2);

        // 30.48 m -> ft = 100.0 ft
        double ft = UnitConverter.Convert(30.48, "m", "ft", _dataService);
        Assert.Equal(100.0, ft, precision: 1);

        // 1 ft -> 12 in
        double inches = UnitConverter.Convert(1.0, "ft", "in", _dataService);
        Assert.Equal(12.0, inches, precision: 2);
    }

    [Fact]
    public void UnitConverter_Convert_PressureConversions()
    {
        // 100 psi -> kPa (~689.4757 kPa)
        double kpa = UnitConverter.Convert(100.0, "psi", "kPa", _dataService);
        Assert.Equal(689.48, kpa, precision: 1);

        // 14.5038 psi -> bar (~1.0 bar)
        double bar = UnitConverter.Convert(14.50377, "psi", "bar", _dataService);
        Assert.Equal(1.0, bar, precision: 2);
    }

    [Fact]
    public void UnitConverter_Convert_TemperatureConversions()
    {
        // 32 degF -> 0 degC
        double cFreezing = UnitConverter.Convert(32.0, "degF", "degC", _dataService);
        Assert.Equal(0.0, cFreezing, precision: 2);

        // 212 degF -> 100 degC
        double cBoiling = UnitConverter.Convert(212.0, "degF", "degC", _dataService);
        Assert.Equal(100.0, cBoiling, precision: 2);

        // 0 degC -> 32 degF
        double fFreezing = UnitConverter.Convert(0.0, "degC", "degF", _dataService);
        Assert.Equal(32.0, fFreezing, precision: 2);

        // 100 degC -> 212 degF
        double fBoiling = UnitConverter.Convert(100.0, "degC", "degF", _dataService);
        Assert.Equal(212.0, fBoiling, precision: 2);
    }

    [Fact]
    public void UnitConverter_Convert_UnknownUnit_ReturnsOriginalValue()
    {
        double val = UnitConverter.Convert(123.45, "unknown_1", "unknown_2", _dataService);
        Assert.Equal(123.45, val);
    }

    [Fact]
    public void UnitConverter_TryConvert_ReturnsTrueForKnownUnits_FalseForUnknown()
    {
        bool success = UnitConverter.TryConvert(100.0, "ft", "m", out double m, _dataService);
        Assert.True(success);
        Assert.Equal(30.48, m, precision: 2);

        bool unknownSuccess = UnitConverter.TryConvert(100.0, "ft", "non_existent_unit", out double unchanged, _dataService);
        Assert.False(unknownSuccess);
        Assert.Equal(100.0, unchanged);
    }

    [Fact]
    public void UnitConverter_Add_Edit_Remove_PerformsCRUD()
    {
        // Add
        var custom = new UnitConverter("customA", "customB", 4.0, 5.0, "Testing");
        bool added = UnitConverter.Add(_dataService, custom);
        Assert.True(added);
        Assert.True(custom.ID > 0);

        // Verify conversion: 10 * 4 + 5 = 45
        double res = UnitConverter.Convert(10.0, "customA", "customB", _dataService);
        Assert.Equal(45.0, res);

        // Edit
        custom.Multiplier = 5.0;
        custom.Offset = 0.0;
        bool edited = UnitConverter.Edit(_dataService, custom);
        Assert.True(edited);

        double recomputed = UnitConverter.Convert(10.0, "customA", "customB", _dataService);
        Assert.Equal(50.0, recomputed);

        // Remove
        bool removed = UnitConverter.Remove(_dataService, "customA", "customB");
        Assert.True(removed);

        var nonExistent = UnitConverter.GetUnitConversion(_dataService, "customA", "customB");
        Assert.Null(nonExistent);
    }

    [Fact]
    public void UnitConverter_GetCopy_PreservesBackwardCompatibility()
    {
        var original = new UnitConverter("ft", "m", 0.3048, 0.0, "Length", 10);
        var copy = original.GetCopy();

        Assert.Equal(original.ID, copy.ID);
        Assert.Equal(original.FromUnit, copy.FromUnit);
        Assert.Equal(original.ToUnit, copy.ToUnit);
        Assert.Equal(original.Multiplier, copy.Multiplier);
        Assert.Equal(original.Offset, copy.Offset);
        Assert.Equal(original.Category, copy.Category);

        copy.Multiplier = 999.0;
        Assert.Equal(0.3048, original.Multiplier);
    }

    #endregion

    #region AppDatabaseService Integration Tests

    [Fact]
    public void AppDatabaseService_GetUnits_And_ConvertUnit_WorkThroughService()
    {
        var appDb = new AppDatabaseService(_dbPath, _dbPath);
        appDb.Initialize();

        var units = appDb.GetUnits("Length");
        Assert.NotEmpty(units);
        Assert.Contains(units, u => u.UnitName == "ft");

        var conversions = appDb.GetUnitConversions("Length");
        Assert.NotEmpty(conversions);

        double converted = appDb.ConvertUnit(50.0, "ft", "m");
        Assert.Equal(15.24, converted, precision: 2);
    }

    #endregion
}

