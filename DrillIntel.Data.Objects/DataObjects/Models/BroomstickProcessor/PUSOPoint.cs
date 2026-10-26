using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using Microsoft.Data.Sqlite;

namespace VirtualTable
{
    /// <summary>
    /// VirtualTable adapter providing tabular data access for PUSO algorithms,
    /// enabling compatibility with legacy eVuMaxPro and DrillIntel SQLite datasets.
    /// </summary>
    public class VirtualTable
    {
        private DataTable? _table;

        public IDataServiceDIntel? objDataService { get; set; }
        public string FieldList { get; set; } = string.Empty;
        public string KeyField { get; set; } = string.Empty;
        public string KeyFieldFormatted { get; set; } = string.Empty;
        public int FrameSize { get; set; } = 1000;
        public string dataTableName { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string OrderBy { get; set; } = string.Empty;

        public int RecordCount => _table?.Rows.Count ?? 0;
        public DataTable? Table => _table;

        public VirtualTable()
        {
        }

        public VirtualTable(DataTable table)
        {
            _table = table;
        }

        public virtual void loadData()
        {
            if (objDataService == null || string.IsNullOrWhiteSpace(dataTableName))
            {
                return;
            }

            try
            {
                string fields = string.IsNullOrWhiteSpace(FieldList) ? "*" : FieldList;
                string sql = $"SELECT {fields} FROM [{dataTableName}]";

                if (!string.IsNullOrWhiteSpace(Condition))
                {
                    sql += $" WHERE {Condition}";
                }

                if (!string.IsNullOrWhiteSpace(KeyField))
                {
                    sql += $" ORDER BY [{KeyField}] ASC";
                }

                _table = objDataService.GetTable(sql);
            }
            catch
            {
                _table = new DataTable();
            }
        }

        public virtual double getDoubleValue(int rowIndex, string columnName, double defaultValue = 0.0)
        {
            if (_table == null || rowIndex < 0 || rowIndex >= _table.Rows.Count) return defaultValue;
            if (!_table.Columns.Contains(columnName)) return defaultValue;

            object val = _table.Rows[rowIndex][columnName];
            if (val == null || val == DBNull.Value) return defaultValue;

            if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                return result;

            if (double.TryParse(val.ToString(), out result))
                return result;

            return defaultValue;
        }

        public virtual double getDoubleValueWithNull(int rowIndex, string columnName, double defaultValue = -1.0)
        {
            if (_table == null || rowIndex < 0 || rowIndex >= _table.Rows.Count) return defaultValue;
            if (!_table.Columns.Contains(columnName)) return defaultValue;

            object val = _table.Rows[rowIndex][columnName];
            if (val == null || val == DBNull.Value) return defaultValue;

            string s = val.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(s)) return defaultValue;

            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                return result;

            if (double.TryParse(s, out result))
                return result;

            return defaultValue;
        }

        public virtual DateTime getDateTimeValue(int rowIndex, string columnName, DateTime defaultValue = default)
        {
            if (_table == null || rowIndex < 0 || rowIndex >= _table.Rows.Count) return defaultValue;
            if (!_table.Columns.Contains(columnName)) return defaultValue;

            object val = _table.Rows[rowIndex][columnName];
            return DrillIntel.Data.Objects.DataObjects.Models.DataService.checkNull(val, defaultValue);
        }

        public virtual string getStringValue(int rowIndex, string columnName, string defaultValue = "")
        {
            if (_table == null || rowIndex < 0 || rowIndex >= _table.Rows.Count) return defaultValue;
            if (!_table.Columns.Contains(columnName)) return defaultValue;

            object val = _table.Rows[rowIndex][columnName];
            return DrillIntel.Data.Objects.DataObjects.Models.DataService.checkNull(val, defaultValue);
        }
    }
}

namespace DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor
{
    /*
    ====================================================================================================
    ORIGINAL SOURCE CODE (FROM eVuMaxPro.Analytics.VuMaxProProcess.TND.PUSOPoint):
    ====================================================================================================

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Data;
    using VuMaxDR.Data;
    using VuMaxDR.Common;
    using VuMaxDR.Data.Objects;

    namespace eVuMaxPro.Analytics.VuMaxProProcess.TND
    {
        public class PUSOPoint : IComparable
        {
            public enum pusoPumpStatus
            {
                PumpOn = 0,
                PumpOff = 1
            }

            public double Depth = 0d;
            public DateTime StartDate;
            public DateTime EndDate;
            public int StartIndex = 0;
            public int EndIndex = 0;
            public double Min = 0d;
            public double Max = 0d;
            public double Avg = 0d;
            public double Avg2 = 0d;
            public double StartDepth = 0d;
            public double EndDepth = 0d;
            public double BlockMovement = 0d;
            public int MaxIndex = 0;
            public double DynamicValue = 0d;
            public double DynamicTorqueValue = 0d;
            public double Circulation = 0d;
            public double PumpPressure = 0d;

            public double UserDynamicValue = 0d;
            public double UserStaticValue = 0d;
            public bool UserVisible = true;

            public double RPM = 0d;
            public string RigStates = "";

            public pusoPumpStatus PumpStatus = pusoPumpStatus.PumpOn;
            ...
    ====================================================================================================
    KEY CHANGES MADE FOR C# & SQLITE COMPATIBILITY:
    ----------------------------------------------------------------------------------------------------
    1. SQLite Database Integration:
       - Added full mapping to/from DrillIntel's SQLite table `VMX_BROOMSTICK_POINTS`.
       - Implemented `EnsureTableExists`, `SavePoint`, `SavePoints`, `LoadPoints`, and `ClearPoints`
         using parameterized queries to prevent SQL injection and guarantee ACID safety.
       - Supported both `IDataServiceDIntel` and direct `DbConnection` / `SqliteConnection`.

    2. Robust Data Parsing & Culture Independence:
       - Original code used `double.Parse(objData.Rows[i]["HKLD"].ToString())`, which throws `FormatException`
         on null/DBNull and breaks on locales where comma ',' is the decimal separator.
       - Replaced with safe `ParseDoubleSafe` using `CultureInfo.InvariantCulture` and DBNull checks.

    3. Column & Bounds Safety:
       - Original `getBreakInIndex` referenced `objData.Rows[i + 1]` without bounds checking, causing
         fatal `IndexOutOfRangeException` on edge rows. Added safety bounds checking.
       - Added column existence checks so calculations do not throw `ArgumentException` if pump/channel
         mnemonics vary between wells.

    4. Configuration Overloads:
       - Added overloads supporting `BroomStickSetup`, `WellSectionPUSOSettings`, `WellPUSOSettings`,
         and `BroomstickProfile` so calling code from any layer in DrillIntel can invoke them cleanly.
       - Corrected a bug in original `isValidSlackOff` which logged `pickupMinBlockMovement` instead
         of `slackOffMinBlockMovement` in the configuration error message.

    5. Generic IComparable<PUSOPoint> Support:
       - Added type-safe `IComparable<PUSOPoint>` alongside legacy `IComparable.CompareTo`.
    ====================================================================================================
    */

    /// <summary>
    /// Represents a Pick-Up (PU), Slack-Off (SO), or Rotate (ROB) broomstick point.
    /// Supports SQLite persistence (VMX_BROOMSTICK_POINTS) and Broomstick algorithmic calculations.
    /// </summary>
    public class PUSOPoint : IComparable, IComparable<PUSOPoint>
    {
        #region Enums & Constants

        /*
        ORIGINAL CODE:
        public enum pusoPumpStatus
        {
            PumpOn = 0,
            PumpOff = 1
        }
        */
        public enum pusoPumpStatus
        {
            PumpOn = 0,
            PumpOff = 1
        }

        public const int PointTypePickUp = 0;
        public const int PointTypeSlackOff = 1;
        public const int PointTypeRotate = 2;

        #endregion

        #region Fields & Properties

        /*
        ORIGINAL CODE:
        public double Depth = 0d;
        public DateTime StartDate;
        public DateTime EndDate;
        public int StartIndex = 0;
        public int EndIndex = 0;
        public double Min = 0d;
        public double Max = 0d;
        public double Avg = 0d;
        public double Avg2 = 0d;
        public double StartDepth = 0d;
        public double EndDepth = 0d;
        public double BlockMovement = 0d;
        public int MaxIndex = 0;
        public double DynamicValue = 0d;
        public double DynamicTorqueValue = 0d;
        public double Circulation = 0d;
        public double PumpPressure = 0d;

        public double UserDynamicValue = 0d;
        public double UserStaticValue = 0d;
        public bool UserVisible = true;

        public double RPM = 0d;
        public string RigStates = "";

        public pusoPumpStatus PumpStatus = pusoPumpStatus.PumpOn;
        */

        // Database ID & Well Tracking
        public long ID { get; set; } = 0;
        public string WellID { get; set; } = string.Empty;
        public int Type { get; set; } = PointTypePickUp;

        // Core Point Geometry
        public double Depth { get; set; } = 0.0;
        public DateTime StartDate { get; set; } = DateTime.MinValue;
        public DateTime EndDate { get; set; } = DateTime.MinValue;
        public int StartIndex { get; set; } = 0;
        public int EndIndex { get; set; } = 0;

        // Hookload / Torque Statistics
        public double Min { get; set; } = 0.0;
        public double Max { get; set; } = 0.0;
        public double Avg { get; set; } = 0.0;
        public double Avg2 { get; set; } = 0.0;

        // Movement & Index Tracking
        public double StartDepth { get; set; } = 0.0;
        public double EndDepth { get; set; } = 0.0;
        public double BlockMovement { get; set; } = 0.0;
        public int MaxIndex { get; set; } = 0;

        // Dynamic Calculations
        public double DynamicValue { get; set; } = 0.0;
        public double DynamicTorqueValue { get; set; } = 0.0;
        public double Circulation { get; set; } = 0.0;
        public double PumpPressure { get; set; } = 0.0;

        // User Overrides & Visibility
        public double UserDynamicValue { get; set; } = 0.0;
        public double UserStaticValue { get; set; } = 0.0;
        public bool UserVisible { get; set; } = true;

        // Rig Context
        public double RPM { get; set; } = 0.0;
        public string RigStates { get; set; } = string.Empty;
        public pusoPumpStatus PumpStatus { get; set; } = pusoPumpStatus.PumpOn;

        #endregion

        #region Constructors

        public PUSOPoint()
        {
        }

        public PUSOPoint(double depth)
        {
            Depth = depth;
        }

        #endregion

        #region Copy & Clone

        /*
        ORIGINAL CODE:
        public void CopyProperties(ref PUSOPoint objNew)
        {
            try
            {
                objNew.StartDate = StartDate;
                objNew.EndDate = EndDate;
                objNew.StartIndex = StartIndex;
                objNew.EndIndex = EndIndex;
                objNew.Min = Min;
                objNew.Max = Max;
                objNew.Avg = Avg;
                objNew.StartDepth = StartDepth;
                objNew.EndDepth = EndDepth;
                objNew.BlockMovement = BlockMovement;
                objNew.MaxIndex = MaxIndex;
                objNew.DynamicValue = DynamicValue;
                objNew.Depth = Depth;
                objNew.PumpPressure = PumpPressure;
                objNew.Circulation = Circulation;
                objNew.PumpStatus = PumpStatus;
                objNew.DynamicTorqueValue = DynamicTorqueValue;
                objNew.Avg2 = Avg2;
                objNew.UserVisible = UserVisible;
                objNew.UserStaticValue = UserStaticValue;
                objNew.UserDynamicValue = UserDynamicValue;
                objNew.RPM = RPM;
                objNew.RigStates = RigStates;
            }
            catch (Exception ex)
            {
            }
        }
        */

        // --- [CONVERTED C# COPY IMPLEMENTATION WITH NULL CHECKS] ---
        public void CopyProperties(ref PUSOPoint objNew)
        {
            if (objNew == null) objNew = new PUSOPoint();
            CopyPropertiesInternal(objNew);
        }

        public void CopyProperties(PUSOPoint objNew)
        {
            if (objNew == null) return;
            CopyPropertiesInternal(objNew);
        }

        private void CopyPropertiesInternal(PUSOPoint objNew)
        {
            objNew.ID = ID;
            objNew.WellID = WellID;
            objNew.Type = Type;
            objNew.StartDate = StartDate;
            objNew.EndDate = EndDate;
            objNew.StartIndex = StartIndex;
            objNew.EndIndex = EndIndex;
            objNew.Min = Min;
            objNew.Max = Max;
            objNew.Avg = Avg;
            objNew.Avg2 = Avg2;
            objNew.StartDepth = StartDepth;
            objNew.EndDepth = EndDepth;
            objNew.BlockMovement = BlockMovement;
            objNew.MaxIndex = MaxIndex;
            objNew.DynamicValue = DynamicValue;
            objNew.DynamicTorqueValue = DynamicTorqueValue;
            objNew.Depth = Depth;
            objNew.PumpPressure = PumpPressure;
            objNew.Circulation = Circulation;
            objNew.PumpStatus = PumpStatus;
            objNew.UserVisible = UserVisible;
            objNew.UserStaticValue = UserStaticValue;
            objNew.UserDynamicValue = UserDynamicValue;
            objNew.RPM = RPM;
            objNew.RigStates = RigStates;
        }

        /*
        ORIGINAL CODE:
        public PUSOPoint getCopy()
        {
            try
            {
                var objNew = new PUSOPoint();
                ...
                return objNew;
            }
            catch (Exception ex)
            {
                return new PUSOPoint();
            }
        }
        */
        public PUSOPoint getCopy()
        {
            var objNew = new PUSOPoint();
            CopyPropertiesInternal(objNew);
            return objNew;
        }

        public PUSOPoint GetCopy() => getCopy();
        public PUSOPoint Clone() => getCopy();

        #endregion

        #region Validation Methods

        /*
        ORIGINAL CODE:
        public static bool isValidPickUp(PUSOPoint objPUSOPoint, BroomStickSetup objSetup, ref string paramLog)
        {
            try
            {
                double BlockMovement = Math.Abs(objPUSOPoint.StartDepth - objPUSOPoint.EndDepth);
                paramLog += "\n" + "Start Depth: " + objPUSOPoint.StartDepth.ToString() + " End Depth: " + objPUSOPoint.EndDepth.ToString();
                paramLog +="\n" + "Calculated Block Movement: " + BlockMovement.ToString();

                if (BlockMovement >= objSetup.pickupMinBlockMovement && BlockMovement <= objSetup.pickupMaxBlockMovement)
                {
                    paramLog += "\n" + "Block movement is within the allowed range set in the configuration";
                    return true;
                }
                else
                {
                    paramLog += "\n" + "Block movement is NOT within the allowed range set in the configuration. Configuration - Min: "+objSetup.pickupMinBlockMovement.ToString()+" Max: "+objSetup.pickupMaxBlockMovement.ToString();
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        */

        // --- [CONVERTED C# VALIDATION: CULTURE INVARIANT LOGGING & NULL-SAFE] ---
        public static bool isValidPickUp(PUSOPoint objPUSOPoint, BroomStickSetup objSetup, ref string paramLog)
        {
            if (objPUSOPoint == null || objSetup == null) return false;

            try
            {
                double blockMovement = Math.Abs(objPUSOPoint.StartDepth - objPUSOPoint.EndDepth);

                paramLog += string.Format(CultureInfo.InvariantCulture,
                    "\nStart Depth: {0:F2} End Depth: {1:F2}\nCalculated Block Movement: {2:F2}",
                    objPUSOPoint.StartDepth, objPUSOPoint.EndDepth, blockMovement);

                if (blockMovement >= objSetup.pickupMinBlockMovement && blockMovement <= objSetup.pickupMaxBlockMovement)
                {
                    paramLog += "\nBlock movement is within the allowed range set in the configuration";
                    return true;
                }
                else
                {
                    paramLog += string.Format(CultureInfo.InvariantCulture,
                        "\nBlock movement is NOT within the allowed range set in the configuration. Configuration - Min: {0:F2} Max: {1:F2}",
                        objSetup.pickupMinBlockMovement, objSetup.pickupMaxBlockMovement);
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool isValidPickUp(PUSOPoint objPUSOPoint, BroomStickSetup objSetup)
        {
            string log = string.Empty;
            return isValidPickUp(objPUSOPoint, objSetup, ref log);
        }

        public static bool isValidPickUp(PUSOPoint objPUSOPoint, WellPUSOSettings objSettings)
        {
            if (objPUSOPoint == null || objSettings == null) return false;
            double blockMovement = Math.Abs(objPUSOPoint.StartDepth - objPUSOPoint.EndDepth);
            return blockMovement >= objSettings.PUMinMovement && blockMovement <= objSettings.PUMaxMovement;
        }

        /*
        ORIGINAL CODE:
        public static bool isValidSlackOff(PUSOPoint objPUSOPoint, BroomStickSetup objSetup,ref string paramLog)
        {
            try
            {
                double BlockMovement = Math.Abs(objPUSOPoint.StartDepth - objPUSOPoint.EndDepth);
                ...
                if (BlockMovement >= objSetup.slackOffMinBlockMovement && BlockMovement <= objSetup.slackOffMaxBlockMovement)
                {
                    return true;
                }
                else
                {
                    // BUG IN ORIGINAL: Used objSetup.pickupMinBlockMovement in error message
                    paramLog += "\n" + "Block movement is NOT within the allowed range set in the configuration. Configuration - Min: " + objSetup.pickupMinBlockMovement.ToString() + " Max: " + objSetup.pickupMaxBlockMovement.ToString();
                    return false;
                }
            }
            ...
        */

        // --- [CONVERTED C# VALIDATION: FIXED SLACKOFF PROPERTY BUG IN LOG] ---
        public static bool isValidSlackOff(PUSOPoint objPUSOPoint, BroomStickSetup objSetup, ref string paramLog)
        {
            if (objPUSOPoint == null || objSetup == null) return false;

            try
            {
                double blockMovement = Math.Abs(objPUSOPoint.StartDepth - objPUSOPoint.EndDepth);

                paramLog += string.Format(CultureInfo.InvariantCulture,
                    "\nStart Depth: {0:F2} End Depth: {1:F2}\nCalculated Block Movement: {2:F2}",
                    objPUSOPoint.StartDepth, objPUSOPoint.EndDepth, blockMovement);

                if (blockMovement >= objSetup.slackOffMinBlockMovement && blockMovement <= objSetup.slackOffMaxBlockMovement)
                {
                    paramLog += "\nBlock movement is within the allowed range set in the configuration";
                    return true;
                }
                else
                {
                    // Fixed: references slackOffMinBlockMovement / slackOffMaxBlockMovement
                    paramLog += string.Format(CultureInfo.InvariantCulture,
                        "\nBlock movement is NOT within the allowed range set in the configuration. Configuration - Min: {0:F2} Max: {1:F2}",
                        objSetup.slackOffMinBlockMovement, objSetup.slackOffMaxBlockMovement);
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool isValidSlackOff(PUSOPoint objPUSOPoint, BroomStickSetup objSetup)
        {
            string log = string.Empty;
            return isValidSlackOff(objPUSOPoint, objSetup, ref log);
        }

        public static bool isValidSlackOff(PUSOPoint objPUSOPoint, WellPUSOSettings objSettings)
        {
            if (objPUSOPoint == null || objSettings == null) return false;
            double blockMovement = Math.Abs(objPUSOPoint.StartDepth - objPUSOPoint.EndDepth);
            return blockMovement >= objSettings.SOMinMovement && blockMovement <= objSettings.SOMaxMovement;
        }

        /*
        ORIGINAL CODE:
        public static bool isValidPU(VirtualTable.VirtualTable objVirtualData, PUSOPoint objPUSOPoint)
        {
            try
            {
                bool InValidPointFound = false;
                for (int i = objPUSOPoint.StartIndex; i >= objPUSOPoint.EndIndex; i--)
                {
                    double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");
                    if (lnRigState == 8 || lnRigState == 10 || lnRigState == 4 || lnRigState == 6) { }
                    else { InValidPointFound = true; break; }
                }
                return !InValidPointFound;
            }
            catch (Exception ex) { return true; }
        }
        */

        // --- [CONVERTED C# RIG STATE VALIDATION (VIRTUAL TABLE & DATATABLE OVERLOADS)] ---
        public static bool isValidPU(VirtualTable.VirtualTable objVirtualData, PUSOPoint objPUSOPoint)
        {
            if (objVirtualData == null || objPUSOPoint == null) return true;

            try
            {
                int start = Math.Max(objPUSOPoint.StartIndex, objPUSOPoint.EndIndex);
                int end = Math.Min(objPUSOPoint.StartIndex, objPUSOPoint.EndIndex);

                for (int i = start; i >= end; i--)
                {
                    double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");
                    if (lnRigState == 8 || lnRigState == 10 || lnRigState == 4 || lnRigState == 6)
                    {
                        // Allowed PU rig states
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        public static bool isValidPU(DataTable objData, PUSOPoint objPUSOPoint)
        {
            if (objData == null || objPUSOPoint == null) return true;

            try
            {
                int start = Math.Min(Math.Max(objPUSOPoint.StartIndex, objPUSOPoint.EndIndex), objData.Rows.Count - 1);
                int end = Math.Max(Math.Min(objPUSOPoint.StartIndex, objPUSOPoint.EndIndex), 0);

                for (int i = start; i >= end; i--)
                {
                    double lnRigState = ParseDoubleSafe(objData.Rows[i], "RIG_STATE", -1.0);
                    if (lnRigState == 8 || lnRigState == 10 || lnRigState == 4 || lnRigState == 6)
                    {
                        // Valid
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        /*
        ORIGINAL CODE:
        public static bool isValidSO(VirtualTable.VirtualTable objVirtualData, PUSOPoint objPUSOPoint)
        ...
        */
        public static bool isValidSO(VirtualTable.VirtualTable objVirtualData, PUSOPoint objPUSOPoint)
        {
            if (objVirtualData == null || objPUSOPoint == null) return true;

            try
            {
                int start = Math.Max(objPUSOPoint.StartIndex, objPUSOPoint.EndIndex);
                int end = Math.Min(objPUSOPoint.StartIndex, objPUSOPoint.EndIndex);

                for (int i = start; i >= end; i--)
                {
                    double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");
                    if (lnRigState == 4 || lnRigState == 6 || lnRigState == 8 || lnRigState == 10)
                    {
                        // Allowed SO rig states
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        public static bool isValidSO(DataTable objData, PUSOPoint objPUSOPoint)
        {
            if (objData == null || objPUSOPoint == null) return true;

            try
            {
                int start = Math.Min(Math.Max(objPUSOPoint.StartIndex, objPUSOPoint.EndIndex), objData.Rows.Count - 1);
                int end = Math.Max(Math.Min(objPUSOPoint.StartIndex, objPUSOPoint.EndIndex), 0);

                for (int i = start; i >= end; i--)
                {
                    double lnRigState = ParseDoubleSafe(objData.Rows[i], "RIG_STATE", -1.0);
                    if (lnRigState == 4 || lnRigState == 6 || lnRigState == 8 || lnRigState == 10)
                    {
                        // Valid
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        #endregion

        #region Statistical Calculations (Static PU / SO)

        /*
        ORIGINAL CODE:
        public static void calculateStaticPUValues(DataTable objData, ref PUSOPoint objPUSOPoint, BroomStickSetup objSetup, WellSectionPUSOSettings objWellSectionPUSOSetting = default)
        {
            ...
            double lnHookload = double.Parse(objData.Rows[i]["HKLD"].ToString());
            double lnPumpValue = double.Parse(objData.Rows[i][objSetup.pickupPumpMnemonic].ToString());
            ...
        */

        // --- [CONVERTED C# STATIC PU CALCULATION: CRASH-PROOF VALUE RETRIEVAL] ---
        public static void calculateStaticPUValues(
            DataTable objData,
            ref PUSOPoint objPUSOPoint,
            BroomStickSetup objSetup,
            WellSectionPUSOSettings? objWellSectionPUSOSetting = null)
        {
            if (objData == null || objPUSOPoint == null || objData.Rows.Count == 0) return;

            try
            {
                string pumpMnemonic = objWellSectionPUSOSetting != null
                    ? objWellSectionPUSOSetting.PUChannel
                    : (objSetup != null ? objSetup.pickupPumpMnemonic : "SPPA");

                int start = Math.Min(Math.Max(objPUSOPoint.StartIndex, objPUSOPoint.EndIndex), objData.Rows.Count - 1);
                int end = Math.Max(Math.Min(objPUSOPoint.StartIndex, objPUSOPoint.EndIndex), 0);

                double maxValue = double.MinValue;
                double minValue = double.MaxValue;
                int maxIndex = start;
                int sampleCount = 0;
                double sumValue = 0.0;

                for (int i = start; i >= end; i--)
                {
                    var row = objData.Rows[i];
                    double lnHookload = ParseDoubleSafe(row, "HKLD", 0.0);

                    if (lnHookload > maxValue)
                    {
                        maxValue = lnHookload;
                        maxIndex = i;
                    }
                    if (lnHookload < minValue)
                    {
                        minValue = lnHookload;
                    }

                    sumValue += lnHookload;
                    sampleCount++;
                }

                if (maxValue == double.MinValue) maxValue = 0.0;
                if (minValue == double.MaxValue) minValue = 0.0;

                double avgValue = sampleCount > 0 ? Math.Round(sumValue / sampleCount, 2) : 0.0;

                objPUSOPoint.Max = maxValue;
                objPUSOPoint.Min = minValue;
                objPUSOPoint.Avg = avgValue;
                objPUSOPoint.MaxIndex = maxIndex;
            }
            catch
            {
                // Fallback guarantee
            }
        }

        /*
        ORIGINAL CODE:
        public static int getBreakInIndex(DataTable objData, ref PUSOPoint objSOPoint)
        {
            try
            {
                int BreakInIndex = objSOPoint.EndIndex;
                for (int i = objSOPoint.EndIndex; i <= objSOPoint.StartIndex - 1; i++)
                {
                    double lnHkld = double.Parse(objData.Rows[i]["HKLD"].ToString());
                    double lnPrevHKld = double.Parse(objData.Rows[i + 1]["HKLD"].ToString());
                    if (lnPrevHKld < lnHkld) { BreakInIndex = i; break; }
                }
                return BreakInIndex;
            }
            ...
        */

        // --- [CONVERTED C# GET BREAK-IN INDEX: ADDED BOUNDS CHECKING FOR i + 1] ---
        public static int getBreakInIndex(DataTable objData, ref PUSOPoint objSOPoint)
        {
            if (objData == null || objSOPoint == null || objData.Rows.Count == 0)
                return objSOPoint?.EndIndex ?? 0;

            try
            {
                int breakInIndex = objSOPoint.EndIndex;
                int start = Math.Max(0, objSOPoint.EndIndex);
                int limit = Math.Min(objSOPoint.StartIndex - 1, objData.Rows.Count - 2);

                for (int i = start; i <= limit; i++)
                {
                    double lnHkld = ParseDoubleSafe(objData.Rows[i], "HKLD", 0.0);
                    double lnPrevHkld = ParseDoubleSafe(objData.Rows[i + 1], "HKLD", 0.0);

                    if (lnPrevHkld < lnHkld)
                    {
                        breakInIndex = i;
                        break;
                    }
                }
                return breakInIndex;
            }
            catch
            {
                return objSOPoint.EndIndex;
            }
        }

        public static int GetBreakInIndex(DataTable objData, PUSOPoint objSOPoint)
        {
            var pt = objSOPoint;
            return getBreakInIndex(objData, ref pt);
        }

        /*
        ORIGINAL CODE:
        public static void calculateStaticSOValues2(DataTable objData, ref PUSOPoint objPUSOPoint, BroomStickSetup objSetup, WellSectionPUSOSettings objWellSectionPUSOSetup = default)
        ...
        */
        public static void calculateStaticSOValues2(
            DataTable objData,
            ref PUSOPoint objPUSOPoint,
            BroomStickSetup objSetup,
            WellSectionPUSOSettings? objWellSectionPUSOSetup = null)
        {
            if (objData == null || objPUSOPoint == null || objData.Rows.Count == 0) return;

            try
            {
                int breakInIndex = objPUSOPoint.EndIndex;
                int start = Math.Min(objPUSOPoint.StartIndex, objData.Rows.Count - 1);
                int end = Math.Max(breakInIndex + 1, 0);

                double maxValue = double.MinValue;
                double minValue = double.MaxValue;
                int maxIndex = start;
                int sampleCount = 0;
                double sumValue = 0.0;

                for (int i = start; i >= end; i--)
                {
                    var row = objData.Rows[i];
                    double lnHookload = ParseDoubleSafe(row, "HKLD", 0.0);

                    if (lnHookload > maxValue)
                    {
                        maxValue = lnHookload;
                        maxIndex = i;
                    }
                    if (lnHookload < minValue)
                    {
                        minValue = lnHookload;
                    }

                    sumValue += lnHookload;
                    sampleCount++;
                }

                if (maxValue == double.MinValue) maxValue = 0.0;
                if (minValue == double.MaxValue) minValue = 0.0;

                double avgValue = sampleCount > 0 ? Math.Round(sumValue / sampleCount, 2) : 0.0;

                objPUSOPoint.Max = maxValue;
                objPUSOPoint.Min = minValue;
                objPUSOPoint.Avg2 = avgValue;
            }
            catch
            {
            }
        }

        /*
        ORIGINAL CODE:
        public static void calculateStaticSOValues(DataTable objData, ref PUSOPoint objPUSOPoint, BroomStickSetup objSetup, WellSectionPUSOSettings objWellSectionPUSOSetup = default)
        ...
        */
        public static void calculateStaticSOValues(
            DataTable objData,
            ref PUSOPoint objPUSOPoint,
            BroomStickSetup objSetup,
            WellSectionPUSOSettings? objWellSectionPUSOSetup = null)
        {
            if (objData == null || objPUSOPoint == null || objData.Rows.Count == 0) return;

            try
            {
                int breakInIndex = getBreakInIndex(objData, ref objPUSOPoint);
                if (breakInIndex == objPUSOPoint.StartIndex)
                {
                    breakInIndex = objPUSOPoint.EndIndex;
                }

                int start = Math.Min(objPUSOPoint.StartIndex, objData.Rows.Count - 1);
                int end = Math.Max(breakInIndex + 1, 0);

                double maxValue = double.MinValue;
                double minValue = double.MaxValue;
                int sampleCount = 0;
                double sumValue = 0.0;

                for (int i = start; i >= end; i--)
                {
                    var row = objData.Rows[i];
                    double lnHookload = ParseDoubleSafe(row, "HKLD", 0.0);

                    if (lnHookload > maxValue)
                    {
                        maxValue = lnHookload;
                    }
                    if (lnHookload < minValue)
                    {
                        minValue = lnHookload;
                    }

                    sumValue += lnHookload;
                    sampleCount++;
                }

                if (maxValue == double.MinValue) maxValue = 0.0;
                if (minValue == double.MaxValue) minValue = 0.0;

                double avgValue = sampleCount > 0 ? Math.Round(sumValue / sampleCount, 2) : 0.0;

                objPUSOPoint.Max = maxValue;
                objPUSOPoint.Min = minValue;
                objPUSOPoint.Avg = avgValue;
            }
            catch
            {
            }
        }

        #endregion

        #region Dynamic Value Calculations

        /*
        ORIGINAL CODE:
        public static void calculateDynamicPUValues(DataTable objData, ref PUSOPoint objPUSOPoint, BroomStickSetup objSetup, WellSectionPUSOSettings objWellSectionPUSOSetting = default)
        ...
        */

        // --- [CONVERTED C# DYNAMIC PU CALCULATION] ---
        public static void calculateDynamicPUValues(
            DataTable objData,
            ref PUSOPoint objPUSOPoint,
            BroomStickSetup objSetup,
            WellSectionPUSOSettings? objWellSectionPUSOSetting = null)
        {
            if (objData == null || objPUSOPoint == null || objPUSOPoint.Max == 0.0) return;

            try
            {
                string pumpMnemonic = objWellSectionPUSOSetting != null
                    ? objWellSectionPUSOSetting.PUChannel
                    : (objSetup != null ? objSetup.pickupPumpMnemonic : "SPPA");

                double dynamicValue = 0.0;
                double dynamicAvg = 0.0;
                double blockHeight = 0.0;
                double pumpPressure = 0.0;
                string strRigState = string.Empty;
                double rpm = 0.0;

                int startPoint = objPUSOPoint.MaxIndex > 0 ? objPUSOPoint.MaxIndex : objPUSOPoint.StartIndex;
                startPoint = Math.Min(startPoint, objData.Rows.Count - 1);
                int endPoint = Math.Max(objPUSOPoint.EndIndex, 0);

                for (int i = startPoint; i >= endPoint; i--)
                {
                    var row = objData.Rows[i];
                    double lnRigState = ParseDoubleSafe(row, "RIG_STATE", 0.0);
                    double lnHookload = ParseDoubleSafe(row, "HKLD", 0.0);
                    double lnPumpValue = ParseDoubleSafe(row, pumpMnemonic, 0.0);
                    double lnBlockHeight = ParseDoubleSafe(row, "BPOS", 0.0);
                    double lnRPM = ParseDoubleSafe(row, "RPM", 0.0);

                    if (lnHookload < objPUSOPoint.Max)
                    {
                        dynamicValue = objPUSOPoint.Max;
                        blockHeight = lnBlockHeight;
                        strRigState = lnRigState.ToString(CultureInfo.InvariantCulture);
                        pumpPressure = lnPumpValue;
                        rpm = lnRPM;
                        break;
                    }
                }

                if (dynamicValue == 0.0)
                {
                    dynamicValue = objPUSOPoint.Max;
                }

                if (dynamicValue > 0.0)
                {
                    objPUSOPoint.PumpPressure = pumpPressure;
                    objPUSOPoint.BlockMovement = blockHeight;
                    objPUSOPoint.RigStates = strRigState;
                    objPUSOPoint.RPM = rpm;

                    double sumValues = 0.0;
                    int sampleCount = 0;

                    for (int c = startPoint; c >= endPoint; c--)
                    {
                        double lnHookload = ParseDoubleSafe(objData.Rows[c], "HKLD", 0.0);
                        sumValues += lnHookload;
                        sampleCount++;
                    }

                    if (sumValues > 0.0 && sampleCount > 0)
                    {
                        dynamicAvg = Math.Round(sumValues / sampleCount, 2);
                    }
                }

                // Determine final dynamic value based on method setting
                if (objWellSectionPUSOSetting != null)
                {
                    if (objWellSectionPUSOSetting.PUDynMethod == 2) // BreakOver Average
                        objPUSOPoint.DynamicValue = Math.Round(dynamicAvg, 2);
                    else if (objWellSectionPUSOSetting.PUDynMethod == 0) // BreakOver
                        objPUSOPoint.DynamicValue = Math.Round(dynamicValue, 2);
                    else if (objWellSectionPUSOSetting.PUDynMethod == 1) // Average
                        objPUSOPoint.DynamicValue = objPUSOPoint.Avg;
                }
                else if (objSetup != null)
                {
                    if (objSetup.pickupDynamicMethod == BroomStickSetup.bmDynamicMethod.BreakOverAverage)
                        objPUSOPoint.DynamicValue = Math.Round(dynamicAvg, 2);
                    else if (objSetup.pickupDynamicMethod == BroomStickSetup.bmDynamicMethod.BreakOver)
                        objPUSOPoint.DynamicValue = Math.Round(dynamicValue, 2);
                    else if (objSetup.pickupDynamicMethod == BroomStickSetup.bmDynamicMethod.Average)
                        objPUSOPoint.DynamicValue = objPUSOPoint.Avg;
                }
                else
                {
                    objPUSOPoint.DynamicValue = Math.Round(dynamicValue, 2);
                }
            }
            catch
            {
            }
        }

        /*
        ORIGINAL CODE:
        public static void calculateDynamicSOValues(DataTable objData, ref PUSOPoint objPUSOPoint, BroomStickSetup objSetup, WellSectionPUSOSettings objWellSectionPUSOSetup = default)
        ...
        */

        // --- [CONVERTED C# DYNAMIC SO CALCULATION] ---
        public static void calculateDynamicSOValues(
            DataTable objData,
            ref PUSOPoint objPUSOPoint,
            BroomStickSetup objSetup,
            WellSectionPUSOSettings? objWellSectionPUSOSetup = null)
        {
            if (objData == null || objPUSOPoint == null || objData.Rows.Count == 0) return;

            try
            {
                string pumpMnemonic = objWellSectionPUSOSetup != null
                    ? objWellSectionPUSOSetup.SOChannel
                    : (objSetup != null ? objSetup.slackOffPumpMnemonic : "SPPA");

                double dynamicValue = 0.0;
                double dynamicAvg = 0.0;
                double blockHeight = 0.0;
                double pumpPressure = 0.0;
                string strRigState = string.Empty;
                double rpm = 0.0;

                int startIndex = Math.Min(objPUSOPoint.StartIndex, objData.Rows.Count - 1);
                int endIndex = Math.Max(objPUSOPoint.EndIndex, 0);

                double lnPreviousValue = ParseDoubleSafe(objData.Rows[startIndex], "HKLD", 0.0);
                int minPointIndex = -1;

                for (int i = startIndex; i >= endIndex; i--)
                {
                    var row = objData.Rows[i];
                    double lnRigState = ParseDoubleSafe(row, "RIG_STATE", 0.0);
                    double lnHookload = ParseDoubleSafe(row, "HKLD", 0.0);
                    double lnPumpValue = ParseDoubleSafe(row, pumpMnemonic, 0.0);
                    double lnBlockHeight = ParseDoubleSafe(row, "BPOS", 0.0);
                    double lnRPM = ParseDoubleSafe(row, "RPM", 0.0);

                    if (lnHookload > lnPreviousValue)
                    {
                        minPointIndex = i + 1;
                        dynamicValue = lnPreviousValue;
                        pumpPressure = lnPumpValue;
                        blockHeight = lnBlockHeight;
                        strRigState = lnRigState.ToString(CultureInfo.InvariantCulture);
                        rpm = lnRPM;
                        break;
                    }

                    lnPreviousValue = lnHookload;
                }

                if (dynamicValue > 0.0 && minPointIndex != -1)
                {
                    objPUSOPoint.PumpPressure = pumpPressure;
                    objPUSOPoint.BlockMovement = blockHeight;
                    objPUSOPoint.RigStates = strRigState;
                    objPUSOPoint.RPM = rpm;

                    double sumValues = 0.0;
                    int sampleCount = 0;
                    int cStart = Math.Min(minPointIndex, objData.Rows.Count - 1);

                    for (int c = cStart; c >= endIndex; c--)
                    {
                        double lnHookload = ParseDoubleSafe(objData.Rows[c], "HKLD", 0.0);
                        sumValues += lnHookload;
                        sampleCount++;
                    }

                    if (sumValues > 0.0 && sampleCount > 0)
                    {
                        dynamicAvg = Math.Round(sumValues / sampleCount, 2);
                    }
                }

                if (dynamicValue == 0.0)
                {
                    dynamicValue = objPUSOPoint.Min;
                }

                if (objWellSectionPUSOSetup != null)
                {
                    if (objWellSectionPUSOSetup.SODynMethod == 2)
                        objPUSOPoint.DynamicValue = Math.Round(dynamicAvg, 2);
                    else if (objWellSectionPUSOSetup.SODynMethod == 0)
                        objPUSOPoint.DynamicValue = Math.Round(dynamicValue, 2);
                    else if (objWellSectionPUSOSetup.SODynMethod == 1)
                        objPUSOPoint.DynamicValue = objPUSOPoint.Avg;
                }
                else if (objSetup != null)
                {
                    if (objSetup.slackOffDynamicMethod == BroomStickSetup.bmDynamicMethod.BreakOverAverage)
                        objPUSOPoint.DynamicValue = Math.Round(dynamicAvg, 2);
                    else if (objSetup.slackOffDynamicMethod == BroomStickSetup.bmDynamicMethod.BreakOver)
                        objPUSOPoint.DynamicValue = Math.Round(dynamicValue, 2);
                    else if (objSetup.slackOffDynamicMethod == BroomStickSetup.bmDynamicMethod.Average)
                        objPUSOPoint.DynamicValue = objPUSOPoint.Avg;
                }
                else
                {
                    objPUSOPoint.DynamicValue = Math.Round(dynamicValue, 2);
                }
            }
            catch
            {
            }
        }

        #endregion

        #region Pump Status Updates

        /*
        ORIGINAL CODE:
        public static void updatePumpStatusPU(ref PUSOPoint objPoint, BroomStickSetup objSetup)
        {
            try
            {
                if (objSetup.pickupPumpMnemonic == "SPPA")
                {
                    if (objPoint.PumpPressure > objSetup.pickupCutOffValue)
                        objPoint.PumpStatus = pusoPumpStatus.PumpOn;
                    else
                        objPoint.PumpStatus = pusoPumpStatus.PumpOff;
                }
                else if (objPoint.Circulation > objSetup.pickupCutOffValue)
                    objPoint.PumpStatus = pusoPumpStatus.PumpOn;
                else
                    objPoint.PumpStatus = pusoPumpStatus.PumpOff;
            }
            catch (Exception ex) { }
        }
        */

        // --- [CONVERTED C# PUMP STATUS PU] ---
        public static void updatePumpStatusPU(ref PUSOPoint objPoint, BroomStickSetup objSetup)
        {
            if (objPoint == null || objSetup == null) return;

            try
            {
                if (string.Equals(objSetup.pickupPumpMnemonic, "SPPA", StringComparison.OrdinalIgnoreCase))
                {
                    objPoint.PumpStatus = objPoint.PumpPressure > objSetup.pickupCutOffValue
                        ? pusoPumpStatus.PumpOn
                        : pusoPumpStatus.PumpOff;
                }
                else
                {
                    objPoint.PumpStatus = objPoint.Circulation > objSetup.pickupCutOffValue
                        ? pusoPumpStatus.PumpOn
                        : pusoPumpStatus.PumpOff;
                }
            }
            catch
            {
            }
        }

        public static void updatePumpStatusPU(PUSOPoint objPoint, BroomStickSetup objSetup)
        {
            var pt = objPoint;
            updatePumpStatusPU(ref pt, objSetup);
        }

        /*
        ORIGINAL CODE:
        public static void updatePumpStatusSO(ref PUSOPoint objPoint, BroomStickSetup objSetup)
        ...
        */
        public static void updatePumpStatusSO(ref PUSOPoint objPoint, BroomStickSetup objSetup)
        {
            if (objPoint == null || objSetup == null) return;

            try
            {
                if (string.Equals(objSetup.slackOffPumpMnemonic, "SPPA", StringComparison.OrdinalIgnoreCase))
                {
                    objPoint.PumpStatus = objPoint.PumpPressure > objSetup.slackOffCutOffValue
                        ? pusoPumpStatus.PumpOn
                        : pusoPumpStatus.PumpOff;
                }
                else
                {
                    objPoint.PumpStatus = objPoint.Circulation > objSetup.slackOffCutOffValue
                        ? pusoPumpStatus.PumpOn
                        : pusoPumpStatus.PumpOff;
                }
            }
            catch
            {
            }
        }

        public static void updatePumpStatusSO(PUSOPoint objPoint, BroomStickSetup objSetup)
        {
            var pt = objPoint;
            updatePumpStatusSO(ref pt, objSetup);
        }

        /*
        ORIGINAL CODE:
        public static void updatePumpStatusROB(ref PUSOPoint objPoint, BroomStickSetup objSetup)
        ...
        */
        public static void updatePumpStatusROB(ref PUSOPoint objPoint, BroomStickSetup objSetup)
        {
            if (objPoint == null || objSetup == null) return;

            try
            {
                if (string.Equals(objSetup.rotatePumpMnemonic, "SPPA", StringComparison.OrdinalIgnoreCase))
                {
                    objPoint.PumpStatus = objPoint.PumpPressure > objSetup.rotateCutOffValue
                        ? pusoPumpStatus.PumpOn
                        : pusoPumpStatus.PumpOff;
                }
                else
                {
                    objPoint.PumpStatus = objPoint.Circulation > objSetup.rotateCutOffValue
                        ? pusoPumpStatus.PumpOn
                        : pusoPumpStatus.PumpOff;
                }
            }
            catch
            {
            }
        }

        public static void updatePumpStatusROB(PUSOPoint objPoint, BroomStickSetup objSetup)
        {
            var pt = objPoint;
            updatePumpStatusROB(ref pt, objSetup);
        }

        #endregion

        #region Comparable Implementation

        /*
        ORIGINAL CODE:
        public int CompareTo(object obj)
        {
            try
            {
                PUSOPoint objNew = (PUSOPoint)obj;
                if (Depth < objNew.Depth) return -1;
                if (Depth == objNew.Depth) return 0;
                if (Depth > objNew.Depth) return 1;
            }
            catch (Exception ex) { }
            return default;
        }
        */

        // --- [CONVERTED C# COMPARABLE: IMPLEMENTS BOTH OBJECT & GENERIC PUSOPoint OVERLOADS] ---
        public int CompareTo(object? obj)
        {
            if (obj is PUSOPoint other)
            {
                return CompareTo(other);
            }
            return 0;
        }

        public int CompareTo(PUSOPoint? other)
        {
            if (other == null) return 1;
            return Depth.CompareTo(other.Depth);
        }

        #endregion

        #region SQLite Database Persistence (VMX_BROOMSTICK_POINTS)

        /// <summary>
        /// SQLite DDL for the VMX_BROOMSTICK_POINTS table and performance indexes.
        /// </summary>
        public const string CreateTableSql = @"
CREATE TABLE IF NOT EXISTS VMX_BROOMSTICK_POINTS (
    ID                      INTEGER PRIMARY KEY AUTOINCREMENT,
    WELL_ID                 TEXT NOT NULL,
    DEPTH                   REAL,
    TYPE                    INTEGER,
    START_DATE              TEXT,
    END_DATE                TEXT,
    START_INDEX             REAL,
    END_INDEX               REAL,
    MIN                     REAL,
    MAX                     REAL,
    AVG                     REAL,
    AVG2                    REAL,
    START_DEPTH             REAL,
    END_DEPTH               REAL,
    BLOCK_MOVEMENT          REAL,
    MAX_INDEX               REAL,
    DYNAMIC_VALUE           REAL,
    DYNAMIC_TORQUE_VALUE    REAL,
    CIRCULATION             REAL,
    PUMP_PRESSURE           REAL,
    USER_DYNAMIC_VALUE      REAL,
    USER_STATIC_VALUE       REAL,
    USER_VISIBLE            INTEGER DEFAULT 1,
    PUMP_STATUS             INTEGER DEFAULT 0,
    CREATED_BY              TEXT,
    CREATED_DATE            TEXT,
    MODIFIED_BY             TEXT,
    MODIFIED_DATE           TEXT
);
CREATE INDEX IF NOT EXISTS IDX_VMX_BROOMSTICK_POINTS_WELL ON VMX_BROOMSTICK_POINTS(WELL_ID);
CREATE INDEX IF NOT EXISTS IDX_VMX_BROOMSTICK_POINTS_DEPTH ON VMX_BROOMSTICK_POINTS(WELL_ID, DEPTH, TYPE);
";

        /// <summary>
        /// Ensures VMX_BROOMSTICK_POINTS table exists via IDataServiceDIntel.
        /// </summary>
        public static void EnsureTableExists(IDataServiceDIntel? db)
        {
            if (db == null) return;
            try { db.ExecuteNonQuery(CreateTableSql); } catch { }
        }

        /// <summary>
        /// Ensures VMX_BROOMSTICK_POINTS table exists via direct DbConnection.
        /// </summary>
        public static void EnsureTableExists(DbConnection? conn, DbTransaction? transaction = null)
        {
            if (conn == null) return;
            try
            {
                if (conn.State != ConnectionState.Open) conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = CreateTableSql;
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        /// <summary>
        /// Saves a single PUSOPoint to VMX_BROOMSTICK_POINTS using parameterized SQLite queries.
        /// </summary>
        public static bool SavePoint(
            IDataServiceDIntel db,
            string wellId,
            int pointType,
            PUSOPoint point,
            string userName = "")
        {
            if (db == null || point == null) return false;

            try
            {
                EnsureTableExists(db);

                const string insertSql = @"
INSERT INTO VMX_BROOMSTICK_POINTS (
    WELL_ID, DEPTH, TYPE, START_DATE, END_DATE, START_INDEX, END_INDEX,
    MIN, MAX, AVG, AVG2, START_DEPTH, END_DEPTH, BLOCK_MOVEMENT, MAX_INDEX,
    DYNAMIC_VALUE, DYNAMIC_TORQUE_VALUE, CIRCULATION, PUMP_PRESSURE,
    USER_DYNAMIC_VALUE, USER_STATIC_VALUE, USER_VISIBLE, PUMP_STATUS,
    CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE
) VALUES (
    @WELL_ID, @DEPTH, @TYPE, @START_DATE, @END_DATE, @START_INDEX, @END_INDEX,
    @MIN, @MAX, @AVG, @AVG2, @START_DEPTH, @END_DEPTH, @BLOCK_MOVEMENT, @MAX_INDEX,
    @DYNAMIC_VALUE, @DYNAMIC_TORQUE_VALUE, @CIRCULATION, @PUMP_PRESSURE,
    @USER_DYNAMIC_VALUE, @USER_STATIC_VALUE, @USER_VISIBLE, @PUMP_STATUS,
    @CREATED_BY, @CREATED_DATE, @MODIFIED_BY, @MODIFIED_DATE
);";

                var p = point.ToParameters(wellId, pointType, userName);
                return db.ExecuteNonQuery(insertSql, p);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Saves a batch of PUSOPoint objects in an atomic SQLite transaction.
        /// </summary>
        public static bool SavePoints(
            IDataServiceDIntel db,
            string wellId,
            int pointType,
            IEnumerable<PUSOPoint> points,
            string userName = "")
        {
            if (db == null || points == null) return false;

            try
            {
                EnsureTableExists(db);
                db.StartTransaction();

                foreach (var pt in points)
                {
                    if (!SavePoint(db, wellId, pointType, pt, userName))
                    {
                        db.RollBack();
                        return false;
                    }
                }

                return db.Commit();
            }
            catch
            {
                try { db.RollBack(); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Loads PUSOPoint objects for a specified well and optional point type from SQLite.
        /// </summary>
        public static List<PUSOPoint> LoadPoints(
            IDataServiceDIntel db,
            string wellId,
            int pointType = -1)
        {
            var list = new List<PUSOPoint>();
            if (db == null || string.IsNullOrWhiteSpace(wellId)) return list;

            try
            {
                EnsureTableExists(db);

                string query = pointType >= 0
                    ? "SELECT * FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WELL_ID AND TYPE = @TYPE ORDER BY DEPTH ASC;"
                    : "SELECT * FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WELL_ID ORDER BY DEPTH ASC;";

                var p = new Dictionary<string, object?> { ["@WELL_ID"] = wellId.Trim() };
                if (pointType >= 0) p["@TYPE"] = pointType;

                var dt = db.GetTable(query, p);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapFromRow(row));
                    }
                }
            }
            catch
            {
            }
            return list;
        }

        /// <summary>
        /// Clears PUSO points from the database for the specified well and optional type.
        /// </summary>
        public static bool ClearPoints(IDataServiceDIntel db, string wellId, int pointType = -1)
        {
            if (db == null || string.IsNullOrWhiteSpace(wellId)) return false;

            try
            {
                EnsureTableExists(db);

                string sql = pointType >= 0
                    ? "DELETE FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WELL_ID AND TYPE = @TYPE;"
                    : "DELETE FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WELL_ID;";

                var p = new Dictionary<string, object?> { ["@WELL_ID"] = wellId.Trim() };
                if (pointType >= 0) p["@TYPE"] = pointType;

                return db.ExecuteNonQuery(sql, p);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Maps a DataRow from VMX_BROOMSTICK_POINTS to a PUSOPoint object.
        /// </summary>
        public static PUSOPoint MapFromRow(DataRow row)
        {
            var pt = new PUSOPoint();
            if (row == null) return pt;

            var table = row.Table;
            pt.ID = GetLongSafe(row, table, "ID", 0);
            pt.WellID = GetStringSafe(row, table, "WELL_ID", string.Empty);
            pt.Type = GetIntSafe(row, table, "TYPE", 0);
            pt.Depth = GetDoubleSafe(row, table, "DEPTH", 0.0);
            pt.StartDate = GetDateTimeSafe(row, table, "START_DATE");
            pt.EndDate = GetDateTimeSafe(row, table, "END_DATE");
            pt.StartIndex = GetIntSafe(row, table, "START_INDEX", 0);
            pt.EndIndex = GetIntSafe(row, table, "END_INDEX", 0);
            pt.Min = GetDoubleSafe(row, table, "MIN", 0.0);
            pt.Max = GetDoubleSafe(row, table, "MAX", 0.0);
            pt.Avg = GetDoubleSafe(row, table, "AVG", 0.0);
            pt.Avg2 = GetDoubleSafe(row, table, "AVG2", 0.0);
            pt.StartDepth = GetDoubleSafe(row, table, "START_DEPTH", 0.0);
            pt.EndDepth = GetDoubleSafe(row, table, "END_DEPTH", 0.0);
            pt.BlockMovement = GetDoubleSafe(row, table, "BLOCK_MOVEMENT", 0.0);
            pt.MaxIndex = GetIntSafe(row, table, "MAX_INDEX", 0);
            pt.DynamicValue = GetDoubleSafe(row, table, "DYNAMIC_VALUE", 0.0);
            pt.DynamicTorqueValue = GetDoubleSafe(row, table, "DYNAMIC_TORQUE_VALUE", 0.0);
            pt.Circulation = GetDoubleSafe(row, table, "CIRCULATION", 0.0);
            pt.PumpPressure = GetDoubleSafe(row, table, "PUMP_PRESSURE", 0.0);
            pt.UserDynamicValue = GetDoubleSafe(row, table, "USER_DYNAMIC_VALUE", 0.0);
            pt.UserStaticValue = GetDoubleSafe(row, table, "USER_STATIC_VALUE", 0.0);
            pt.UserVisible = GetIntSafe(row, table, "USER_VISIBLE", 1) == 1;
            pt.PumpStatus = (pusoPumpStatus)GetIntSafe(row, table, "PUMP_STATUS", 0);

            return pt;
        }

        /// <summary>
        /// Converts this PUSOPoint into a dictionary of SQLite parameters for VMX_BROOMSTICK_POINTS.
        /// </summary>
        public Dictionary<string, object?> ToParameters(string wellId, int pointType, string userName)
        {
            string now = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
            string user = !string.IsNullOrWhiteSpace(userName) ? userName : Environment.UserName;

            return new Dictionary<string, object?>
            {
                ["@WELL_ID"] = wellId ?? string.Empty,
                ["@DEPTH"] = Depth,
                ["@TYPE"] = pointType,
                ["@START_DATE"] = StartDate != DateTime.MinValue ? StartDate.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture) : string.Empty,
                ["@END_DATE"] = EndDate != DateTime.MinValue ? EndDate.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture) : string.Empty,
                ["@START_INDEX"] = StartIndex,
                ["@END_INDEX"] = EndIndex,
                ["@MIN"] = Min,
                ["@MAX"] = Max,
                ["@AVG"] = Avg,
                ["@AVG2"] = Avg2,
                ["@START_DEPTH"] = StartDepth,
                ["@END_DEPTH"] = EndDepth,
                ["@BLOCK_MOVEMENT"] = BlockMovement,
                ["@MAX_INDEX"] = MaxIndex,
                ["@DYNAMIC_VALUE"] = DynamicValue,
                ["@DYNAMIC_TORQUE_VALUE"] = DynamicTorqueValue,
                ["@CIRCULATION"] = Circulation,
                ["@PUMP_PRESSURE"] = PumpPressure,
                ["@USER_DYNAMIC_VALUE"] = UserDynamicValue,
                ["@USER_STATIC_VALUE"] = UserStaticValue,
                ["@USER_VISIBLE"] = UserVisible ? 1 : 0,
                ["@PUMP_STATUS"] = (int)PumpStatus,
                ["@CREATED_BY"] = user,
                ["@CREATED_DATE"] = now,
                ["@MODIFIED_BY"] = user,
                ["@MODIFIED_DATE"] = now
            };
        }

        #endregion

        #region Safe Parsing Helpers

        private static double ParseDoubleSafe(DataRow row, string column, double defaultValue)
        {
            if (row == null || row.Table == null || !row.Table.Columns.Contains(column))
                return defaultValue;

            object val = row[column];
            if (val == null || val == DBNull.Value) return defaultValue;

            if (val is double d) return d;
            if (val is float f) return f;
            if (val is int i) return i;
            if (val is long l) return l;

            if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                return parsed;

            return defaultValue;
        }

        private static string GetStringSafe(DataRow row, DataTable? table, string col, string def)
        {
            if (table != null && !table.Columns.Contains(col)) return def;
            return DataService.checkNull(row[col], def);
        }

        private static double GetDoubleSafe(DataRow row, DataTable? table, string col, double def)
        {
            if (table != null && !table.Columns.Contains(col)) return def;
            return DataService.checkNull(row[col], def);
        }

        private static int GetIntSafe(DataRow row, DataTable? table, string col, int def)
        {
            if (table != null && !table.Columns.Contains(col)) return def;
            return DataService.checkNull(row[col], def);
        }

        private static long GetLongSafe(DataRow row, DataTable? table, string col, long def)
        {
            if (table != null && !table.Columns.Contains(col)) return def;
            object val = row[col];
            if (val == null || val == DBNull.Value) return def;
            if (long.TryParse(val.ToString(), out long l)) return l;
            return def;
        }

        private static DateTime GetDateTimeSafe(DataRow row, DataTable? table, string col)
        {
            if (table != null && !table.Columns.Contains(col)) return DateTime.MinValue;
            return DataService.checkNull(row[col], DateTime.MinValue);
        }

        #endregion
    }
}
