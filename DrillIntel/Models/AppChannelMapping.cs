using System;

namespace DrillIntel.Models;

/// <summary>
/// Represents a channel mnemonic mapping record from the Application Database table APP_CHANNEL_MAPPING.
/// Maps vendor/log mnemonics (e.g. HKLD, DEPT, SPPA) to normalized standard channels (e.g. Hookload, Depth, Pump Pressure).
/// </summary>
public class AppChannelMapping
{
    public int Id { get; set; }
    public string Mnemonic { get; set; } = string.Empty;
    public string StandardChannel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultUnit { get; set; } = string.Empty;
    public string SourceVendor { get; set; } = "Standard";
}

/// <summary>
/// Backward-compatibility class alias for legacy VmxCurveDictionary references.
/// </summary>
public class VmxCurveDictionary : AppChannelMapping
{
}
