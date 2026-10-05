using System;
using System.Collections.Generic;
using System.Linq;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Represents a track (strip chart column) within the VHTrackConsole.
/// Contains channels, axes, layout metrics, and display parameters.
/// </summary>
public class VHTrack
{
    public string ID { get; set; } = string.Empty;
    public enumRTTrackType TrackType { get; set; } = enumRTTrackType.Regular;
    //Not to Use dataSource
    public RTDataSource DataSource { get; set; } = new RTDataSource();
    public bool ShowIndexDateTimeTrack { get; set; } = false;

    public string Title { get; set; } = string.Empty;
    public bool TitleVisible { get; set; } = false;
    public double Width { get; set; } = 2;
    public int DisplayOrder { get; set; } = 0;
    public List<VHTrackChannel> Channels { get; set; } = new List<VHTrackChannel>();
    //public string BHAProfileId { get; set; } = string.Empty;

    // Track Font
    public string FontName { get; set; } = "Tahoma";
    public double FontSize { get; set; } = 8;
    public string FontColor { get; set; } = "green";
    public bool FontBold { get; set; } = true;
    public bool FontItalic { get; set; } = false;
    public bool FontUnderline { get; set; } = false;

    public bool Visible { get; set; } = true;
    public List<RTAxis> axisList { get; set; } = new List<RTAxis>();

    // Runtime layout rectangles
    [System.Text.Json.Serialization.JsonIgnore]
    public RTRectangle? headerRect { get; set; } = new RTRectangle();
    [System.Text.Json.Serialization.JsonIgnore]
    public RTRectangle? contentRect { get; set; } = new RTRectangle();

    public VHTrack()
    {
    }

    public VHTrack GetCopy()
    {
        return GetCopy(this);
    }

    public static VHTrack GetBlankCopy(VHTrack paramSource, RTDataSource paramRootDataSource)
    {
        try
        {
            var objNew = new VHTrack
            {
                ID = paramSource.ID,
                TrackType = paramSource.TrackType,
                DataSource = RTDataSource.GetCopy(paramRootDataSource),
                Title = paramSource.Title,
                TitleVisible = paramSource.TitleVisible,
                Width = paramSource.Width,
                DisplayOrder = paramSource.DisplayOrder,
                Channels = paramSource.Channels.Select(ch => ch.GetCopy()).ToList(),
                FontName = paramSource.FontName,
                FontSize = paramSource.FontSize,
                FontColor = paramSource.FontColor,
                FontBold = paramSource.FontBold,
                FontItalic = paramSource.FontItalic,
                FontUnderline = paramSource.FontUnderline,
                Visible = paramSource.Visible,
                //BHAProfileId = paramSource.BHAProfileId,
                axisList = paramSource.axisList.Select(ax => ax.GetCopy()).ToList()
            };

            return objNew;
        }
        catch
        {
            return paramSource;
        }
    }

    public static VHTrack GetCopy(VHTrack? paramSource)
    {
        if (paramSource == null) return new VHTrack();

        try
        {
            var objNew = new VHTrack
            {
                ID = paramSource.ID,
                TrackType = paramSource.TrackType,
                DataSource = RTDataSource.GetCopy(paramSource.DataSource),
                Title = paramSource.Title,
                TitleVisible = paramSource.TitleVisible,
                Width = paramSource.Width,
                DisplayOrder = paramSource.DisplayOrder,
                Channels = paramSource.Channels.Select(ch => ch.GetCopy()).ToList(),
                FontName = paramSource.FontName,
                FontSize = paramSource.FontSize,
                FontColor = paramSource.FontColor,
                FontBold = paramSource.FontBold,
                FontItalic = paramSource.FontItalic,
                FontUnderline = paramSource.FontUnderline,
                Visible = paramSource.Visible,
                //BHAProfileId = paramSource.BHAProfileId,
                axisList = paramSource.axisList.Select(ax => ax.GetCopy()).ToList(),
                ShowIndexDateTimeTrack = paramSource.ShowIndexDateTimeTrack,
                headerRect = paramSource.headerRect?.GetCopy(),
                contentRect = paramSource.contentRect?.GetCopy()
            };

            return objNew;
        }
        catch
        {
            return paramSource;
        }
    }

    public static List<VHTrack> GetSystemDefaultMultiWellTimeTracks()
    {
        try
        {
            var tracks = new List<VHTrack>();

            var objIndextrack = new VHTrack
            {
                ID = Guid.NewGuid().ToString(),
                TrackType = enumRTTrackType.Index,
                Title = "Index",
                Visible = true
            };

            var objDepthTrack = new VHTrack
            {
                ID = Guid.NewGuid().ToString(),
                TrackType = enumRTTrackType.Regular,
                Title = "Depth",
                Visible = true
            };

            var depthChannel = new VHTrackChannel
            {
                ID = Guid.NewGuid().ToString(),
                Mnemonic = "DEPTH",
                Title = "Depth",
                Name = "Depth",
                TrackID = objDepthTrack.ID
            };

            objDepthTrack.Channels.Add(depthChannel);

            tracks.Add(objIndextrack);
            tracks.Add(objDepthTrack);

            return tracks;
        }
        catch
        {
            return new List<VHTrack>();
        }
    }
}

