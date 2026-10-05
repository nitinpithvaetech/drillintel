using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Serializable payload stored as binary (BLOB / varbinary(max)) in VMX_DOC_TEMPLATES.TEMPLATE_DATA.
/// Preserves complete track console structure, orientation, channel styling, scales, and view settings.
/// </summary>
public class RigStateDocumentData
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public int SchemaVersion { get; set; } = 1;
    public string DocumentName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Master Track Console Model
    public VHTrackConsole ConsoleModel { get; set; } = new();

    // View Navigation & Filtering State
    public string SelectedPreset { get; set; } = "Last 24 Hours";
    public DateTime? FromDate { get; set; }
    public DateTime? FromTime { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? ToTime { get; set; }
    public double? FromDepth { get; set; }
    public double? ToDepth { get; set; }
    public int MaxPoints { get; set; } = 10000;

    // Optional Context Bindings
    public string? SelectedWellId { get; set; }
    public string? SelectedWellboreId { get; set; }
    public string? SelectedLogId { get; set; }

    /// <summary>
    /// Serializes this configuration to UTF-8 JSON bytes for storage in TEMPLATE_DATA.
    /// </summary>
    public byte[] ToBytes()
    {
        string json = JsonSerializer.Serialize(this, JsonOpts);
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// Deserializes binary TEMPLATE_DATA into a RigStateDocumentData instance with fallback safety.
    /// </summary>
    public static RigStateDocumentData? FromBytes(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0) return null;

        try
        {
            string json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<RigStateDocumentData>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }
}

