using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DrillIntel.Services.Readers;
using Xunit;

namespace DrillIntel.Tests;

public class DepthReaderTests
{
    [Fact]
    public void CsvDepthReader_RespectsColumnHeadingRowAndImportFromRow()
    {
        var csvContent = new StringBuilder();
        csvContent.AppendLine("Title: Well Data Export");        // Row 1: comment/title
        csvContent.AppendLine("Created: 2026-09-22");           // Row 2: metadata
        csvContent.AppendLine("Unit: m, degC, kPa");            // Row 3: metadata
        csvContent.AppendLine("DEPTH,TEMPERATURE,PRESSURE");   // Row 4: ColumnHeadingRow (4)
        csvContent.AppendLine("m, degC, kPa");                  // Row 5: units (ignored)
        csvContent.AppendLine("100.0,25.5,101.3");              // Row 6: ImportFromRow (6)
        csvContent.AppendLine("100.5,26.0,102.1");              // Row 7
        csvContent.AppendLine("101.0,26.5,103.0");              // Row 8

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, csvContent.ToString());

            var reader = new CsvDepthReader();
            var headers = reader.GetHeaders(tempFile, columnHeadingRow: 4);

            Assert.Equal(3, headers.Count);
            Assert.Equal("DEPTH", headers[0]);
            Assert.Equal("TEMPERATURE", headers[1]);
            Assert.Equal("PRESSURE", headers[2]);

            var previewRows = reader.GetPreviewRows(tempFile, importFromRow: 6, maxRows: 10);
            Assert.Equal(3, previewRows.Count);
            Assert.Equal("100.0", previewRows[0][0]);
            Assert.Equal("100.5", previewRows[1][0]);
            Assert.Equal("101.0", previewRows[2][0]);

            var allDataRows = reader.StreamDataRows(tempFile, importFromRow: 6).ToList();
            Assert.Equal(3, allDataRows.Count);
            Assert.Equal("100.0", allDataRows[0][0]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void CsvDepthReader_HandlesQuotedFieldsAndCustomDelimiters()
    {
        var csvContent = new StringBuilder();
        csvContent.AppendLine("DEPTH;DESCRIPTION;VALUE");
        csvContent.AppendLine("1000.0;\"High, pressure; zone\";55.2");
        csvContent.AppendLine("1000.5;\"Quoted \"\"with quotes\"\" inside\";56.0");

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, csvContent.ToString());

            var reader = new CsvDepthReader();
            var headers = reader.GetHeaders(tempFile, columnHeadingRow: 1, delimiter: ";");
            Assert.Equal(new[] { "DEPTH", "DESCRIPTION", "VALUE" }, headers);

            var rows = reader.StreamDataRows(tempFile, importFromRow: 2, delimiter: ";").ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal("High, pressure; zone", rows[0][1]);
            Assert.Equal("Quoted \"with quotes\" inside", rows[1][1]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void CsvDepthReader_HandlesMissingAndUnicodeValues()
    {
        var csvContent = new StringBuilder();
        csvContent.AppendLine("DEPTH,ZONE,REMARKS");
        csvContent.AppendLine("2000.0,Sandstone,Ø 15% porosity");
        csvContent.AppendLine("2001.0,,Zone change");
        csvContent.AppendLine("2002.0,Limestone,");

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, csvContent.ToString(), Encoding.UTF8);

            var reader = new CsvDepthReader();
            var rows = reader.StreamDataRows(tempFile, importFromRow: 2).ToList();

            Assert.Equal(3, rows.Count);
            Assert.Equal("Ø 15% porosity", rows[0][2]);
            Assert.Equal(string.Empty, rows[1][1]);
            Assert.Equal(string.Empty, rows[2][2]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void WitsmlDepthReader_ParsesCurvesAndData()
    {
        var witsml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<logs xmlns=""http://www.witsml.org/schemas/1series"">
  <log>
    <nameWell>Test Well</nameWell>
    <nameLog>WITSML Depth Log</nameLog>
    <indexType>measured depth</indexType>
    <indexCurve>DEPTH</indexCurve>
    <stepIncrement uom=""m"">0.2</stepIncrement>
    <startIndex uom=""m"">1000.0</startIndex>
    <endIndex uom=""m"">1001.0</endIndex>
    <nullValue>-999.25</nullValue>
    <logCurveInfo>
      <mnemonic>DEPTH</mnemonic>
      <unit>m</unit>
      <curveDescription>Measured Depth</curveDescription>
      <typeLogData>double</typeLogData>
    </logCurveInfo>
    <logCurveInfo>
      <mnemonic>ROP</mnemonic>
      <unit>m/h</unit>
      <curveDescription>Rate of Penetration</curveDescription>
      <typeLogData>double</typeLogData>
    </logCurveInfo>
    <logData>
      <data>1000.0, 15.2</data>
      <data>1000.2, 16.5</data>
      <data>1000.4, 18.0</data>
    </logData>
  </log>
</logs>";

        var tempBase = Path.GetTempFileName();
        var tempXml = Path.ChangeExtension(tempBase, ".xml");
        try
        {
            File.WriteAllText(tempXml, witsml, Encoding.UTF8);

            var reader = new WitsmlDepthReader();
            Assert.True(reader.CanHandle(tempXml));

            var metadata = reader.ExtractMetadata(tempXml);
            Assert.NotNull(metadata);
            Assert.Equal("DEPTH", metadata.IndexCurve);
            Assert.Equal("1000.0", metadata.StartIndex);
            Assert.Equal("1001.0", metadata.EndIndex);
            Assert.Equal("0.2", metadata.StepIncrement);

            var headers = reader.GetHeaders(tempXml);
            Assert.Equal(new[] { "DEPTH", "ROP" }, headers);

            var preview = reader.GetPreviewRows(tempXml, importFromRow: 1, maxRows: 10);
            Assert.Equal(3, preview.Count);
            Assert.Equal("1000.0", preview[0][0]);
            Assert.Equal("15.2", preview[0][1]);
        }
        finally
        {
            if (File.Exists(tempXml)) File.Delete(tempXml);
            if (File.Exists(tempBase)) File.Delete(tempBase);
        }
    }
}
