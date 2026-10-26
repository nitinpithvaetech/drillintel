using System;

namespace DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor
{
    /*
    ====================================================================================================
    ORIGINAL VB.NET SOURCE CODE (FROM VuMaxDR.Data.Objects\BroomStick\BroomStickSetup.vb):
    ====================================================================================================
    <Serializable()> _
    Public Class BroomStickSetup

        Public Enum bmStaticMethod
            Min = 0
            Max = 1
            Avg = 2
        End Enum

        Public Enum pointPlotMethod
            DynamicMethod = 0
            StaticMethod = 1
            Both = 2
        End Enum

        Public Enum bmMultiSelectMethod
            Min = 0
            Max = 1
        End Enum

        Public Enum bmPumpStatus
            Both = 0
            PumpOn = 1
            PumpOff = 2
        End Enum

        Public Enum bmDynamicMethod
            BreakOver = 0
            Average = 1
            BreakOverAverage = 2
        End Enum
        ...
    ====================================================================================================
    */

    [Serializable]
    public class BroomStickSetup
    {
        public enum bmStaticMethod
        {
            Min = 0,
            Max = 1,
            Avg = 2
        }

        public enum pointPlotMethod
        {
            DynamicMethod = 0,
            StaticMethod = 1,
            Both = 2
        }

        public enum bmMultiSelectMethod
        {
            Min = 0,
            Max = 1
        }

        public enum bmPumpStatus
        {
            Both = 0,
            PumpOn = 1,
            PumpOff = 2
        }

        public enum bmDynamicMethod
        {
            BreakOver = 0,
            Average = 1,
            BreakOverAverage = 2
        }

        public string pickupPumpMnemonic { get; set; } = "SPPA";
        public bmPumpStatus pickupPumpStatus { get; set; } = bmPumpStatus.Both;
        public double pickupMaxBlockMovement { get; set; } = 70.0;
        public double pickupMinBlockMovement { get; set; } = 5.0;

        public string slackOffPumpMnemonic { get; set; } = "SPPA";
        public bmPumpStatus slackOffPumpStatus { get; set; } = bmPumpStatus.Both;
        public double slackOffMaxBlockMovement { get; set; } = 70.0;
        public double slackOffMinBlockMovement { get; set; } = 5.0;

        public double pickupRPMCutOff { get; set; } = 12.0;
        public double slackOffRPMCutOff { get; set; } = 12.0;

        public string rotatePumpMnemonic { get; set; } = "SPPA";
        public bmPumpStatus rotatePumpStatus { get; set; } = bmPumpStatus.Both;

        public double pickupCutOffValue { get; set; } = 99.0;
        public double slackOffCutOffValue { get; set; } = 99.0;
        public double rotateCutOffValue { get; set; } = 99.0;

        public bmStaticMethod pickupPointSelectionMethod { get; set; } = bmStaticMethod.Max;
        public bmStaticMethod slackOffPointSelectionMethod { get; set; } = bmStaticMethod.Min;
        public int rotateTime { get; set; } = 120;

        public pointPlotMethod PointToPlot { get; set; } = pointPlotMethod.DynamicMethod;
        public double StringSpeed { get; set; } = 0.0;
        public double OffBottomCircHeight { get; set; } = 10.0;

        public double rotateRPMCutOff { get; set; } = 12.0;
        public double rotateMaxRPM { get; set; } = 30.0;

        public bmDynamicMethod pickupDynamicMethod { get; set; } = bmDynamicMethod.BreakOver;
        public bmDynamicMethod slackOffDynamicMethod { get; set; } = bmDynamicMethod.BreakOver;

        public double ROBMaxHkldTolerance { get; set; } = 1.0;
        public int TolerancePoints { get; set; } = 1;

        public bool CheckPUSOProcedure { get; set; } = false;
        public bool DontPlotROBWithNoStability { get; set; } = false;

        public bool PlotOffBottom { get; set; } = true;
        public bool PlotOnBottom { get; set; } = false;

        public bool PULocalMaxFilter { get; set; } = false;
        public bool SOLocalMinFilter { get; set; } = false;

        public int MaxPauseTime { get; set; } = 1;

        public bmMultiSelectMethod pickupMultiPointMethod { get; set; } = bmMultiSelectMethod.Max;
        public bmMultiSelectMethod slackOffMultiPointMethod { get; set; } = bmMultiSelectMethod.Max;
        public bmMultiSelectMethod ROBMultiPointMethod { get; set; } = bmMultiSelectMethod.Max;
        public bool ShowMultiple { get; set; } = false;
        public bool ShowMultiplePoints { get => ShowMultiple; set => ShowMultiple = value; }

        public string pickupRigStates { get; set; } = string.Empty;
        public string slackOffRigStates { get; set; } = string.Empty;
        public string rotateRigStates { get; set; } = string.Empty;
        public bool EnforceRule { get; set; } = false;

        public BroomStickSetup getCopy()
        {
            var objNew = new BroomStickSetup();
            objNew.pickupCutOffValue = pickupCutOffValue;
            objNew.slackOffCutOffValue = slackOffCutOffValue;
            objNew.pickupPointSelectionMethod = pickupPointSelectionMethod;
            objNew.slackOffPointSelectionMethod = slackOffPointSelectionMethod;
            objNew.rotateTime = rotateTime;
            objNew.PointToPlot = PointToPlot;
            objNew.StringSpeed = StringSpeed;
            objNew.OffBottomCircHeight = OffBottomCircHeight;
            objNew.rotateCutOffValue = rotateCutOffValue;
            objNew.pickupPumpMnemonic = pickupPumpMnemonic;
            objNew.pickupPumpStatus = pickupPumpStatus;
            objNew.pickupMaxBlockMovement = pickupMaxBlockMovement;
            objNew.pickupMinBlockMovement = pickupMinBlockMovement;
            objNew.slackOffPumpMnemonic = slackOffPumpMnemonic;
            objNew.slackOffPumpStatus = slackOffPumpStatus;
            objNew.slackOffMaxBlockMovement = slackOffMaxBlockMovement;
            objNew.slackOffMinBlockMovement = slackOffMinBlockMovement;
            objNew.pickupRPMCutOff = pickupRPMCutOff;
            objNew.slackOffRPMCutOff = slackOffRPMCutOff;
            objNew.rotatePumpMnemonic = rotatePumpMnemonic;
            objNew.rotatePumpStatus = rotatePumpStatus;
            objNew.pickupDynamicMethod = pickupDynamicMethod;
            objNew.slackOffDynamicMethod = slackOffDynamicMethod;
            objNew.rotateRPMCutOff = rotateRPMCutOff;
            objNew.rotateMaxRPM = rotateMaxRPM;
            objNew.ROBMaxHkldTolerance = ROBMaxHkldTolerance;
            objNew.CheckPUSOProcedure = CheckPUSOProcedure;
            objNew.TolerancePoints = TolerancePoints;
            objNew.DontPlotROBWithNoStability = DontPlotROBWithNoStability;
            objNew.PlotOffBottom = PlotOffBottom;
            objNew.PlotOnBottom = PlotOnBottom;
            objNew.PULocalMaxFilter = PULocalMaxFilter;
            objNew.SOLocalMinFilter = SOLocalMinFilter;
            objNew.MaxPauseTime = MaxPauseTime;
            objNew.pickupMultiPointMethod = pickupMultiPointMethod;
            objNew.slackOffMultiPointMethod = slackOffMultiPointMethod;
            objNew.ROBMultiPointMethod = ROBMultiPointMethod;
            objNew.ShowMultiple = ShowMultiple;
            objNew.pickupRigStates = pickupRigStates;
            objNew.slackOffRigStates = slackOffRigStates;
            objNew.rotateRigStates = rotateRigStates;
            objNew.EnforceRule = EnforceRule;
            return objNew;
        }

        public BroomStickSetup Clone() => getCopy();
    }
}

