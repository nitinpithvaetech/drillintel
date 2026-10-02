using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    // --- [OLD LOGIC (LogChannel: without INotifyPropertyChanged)] ---
    // public class LogChannel : IComparable

    // --- [NEW LOGIC (LogChannel: implements INotifyPropertyChanged for WPF two-way data binding)] ---
    public class LogChannel : IComparable, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public enum enValueType
        {
            staticValue = 0,
            queryValue = 1
        }

        public string mnemonic { get; set; } = ""; // Primary Key
        public string classWitsml { get; set; } = "";
        public string unit { get; set; } = "";
        public string mnemAlias { get; set; } = "";
        public string nullValue { get; set; }  = "";
        public string minIndex { get; set; } = "";
        public string maxIndex { get; set; } = "";
        public string minIndexUOM { get; set; } = "";
        public string maxIndexUOM { get; set; } = "";
        public string  columnIndex { get; set; } = "";
        public string curveDescription { get; set; } = "";
        public string sensorOffset { get; set; } = "";
        public string traceState { get; set; } = "";
        public string typeLogData { get; set; } = "";
        public string startIndex { get; set; } = "";
        public string endIndex { get; set; } = "";
        public string witsmlMnemonic { get; set; } = ""; // Server Mnemonic
        public int valueType { get; set; } = 0;
        public string valueQuery { get; set; } = "";
        public string fieldName { get; set; } = "";
        public int sourceColIndex { get; set; } = 0;
        public int curveID { get; set; } = 0;
        public int sourceColNo { get; set; } = 0;
        public string VuMaxUnitID { get; set; } = "";
        public string UnitID { get; set; } = "";
        public int ColumnOrder { get; set; } = 0;
        public int DoNotInterpolate { get; set; } = 0;
        public bool WriteBack { get; set; } = false;
        public string PiMnemonic { get; set; } = "";
        public int AxisDataCount { get; set; } = 0;

        public bool isStoredProc { get; set; } = false;
        public string StoredProcParams { get; set; }  = "";

        public bool processChannel { get; set; } = false;
        public string parentMnemonic { get; set; } = "";

        // --- [NEW LOGIC (WPF Data Binding & Model Compatibility Properties with INotifyPropertyChanged)] ---
        public string? OriginalMnemonic { get; set; }

        public bool Upload
        {
            get => WriteBack || processChannel;
            set
            {
                if (WriteBack != value || processChannel != value)
                {
                    WriteBack = value;
                    processChannel = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Mnemonic
        {
            get => mnemonic;
            set
            {
                if (mnemonic != value)
                {
                    mnemonic = value ?? "";
                    OnPropertyChanged();
                }
            }
        }

        public string Unit
        {
            get => unit;
            set
            {
                if (unit != value)
                {
                    unit = value ?? "";
                    if (string.IsNullOrEmpty(UnitID)) UnitID = unit;
                    OnPropertyChanged();
                }
            }
        }

        public string VuMaxUnitId
        {
            get => !string.IsNullOrEmpty(VuMaxUnitID) ? VuMaxUnitID : UnitID;
            set
            {
                if (VuMaxUnitID != value)
                {
                    VuMaxUnitID = value ?? "";
                    if (string.IsNullOrEmpty(UnitID)) UnitID = VuMaxUnitID;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => curveDescription;
            set
            {
                if (curveDescription != value)
                {
                    curveDescription = value ?? "";
                    OnPropertyChanged();
                }
            }
        }

        public string UploadMnemonic
        {
            get => witsmlMnemonic;
            set
            {
                if (witsmlMnemonic != value)
                {
                    witsmlMnemonic = value ?? "";
                    OnPropertyChanged();
                }
            }
        }

        public string ValueType
        {
            get => valueType.ToString();
            set
            {
                int parsed = int.TryParse(value, out int v) ? v : 0;
                if (valueType != parsed)
                {
                    valueType = parsed;
                    OnPropertyChanged();
                }
            }
        }

        public string Expression
        {
            get => valueQuery;
            set
            {
                if (valueQuery != value)
                {
                    valueQuery = value ?? "";
                    OnPropertyChanged();
                }
            }
        }

        public bool DoNotInterpol
        {
            get => DoNotInterpolate != 0;
            set
            {
                int val = value ? 1 : 0;
                if (DoNotInterpolate != val)
                {
                    DoNotInterpolate = val;
                    OnPropertyChanged();
                }
            }
        }

        public string DataType
        {
            get => string.IsNullOrEmpty(typeLogData) ? "Double" : typeLogData;
            set
            {
                if (typeLogData != value)
                {
                    typeLogData = value ?? "Double";
                    OnPropertyChanged();
                }
            }
        }

        public LogChannel Clone()
        {
            var copy = GetCopy();
            copy.OriginalMnemonic = this.OriginalMnemonic;
            copy.processChannel = this.processChannel;
            return copy;
        }

        public LogChannel GetCopy()
        {
            try
            {
                LogChannel objNew = new LogChannel();
                objNew.mnemonic = this.mnemonic;
                objNew.classWitsml = this.classWitsml;
                objNew.unit = this.unit;
                objNew.mnemAlias = this.mnemAlias;
                objNew.nullValue = this.nullValue;
                objNew.minIndex = this.minIndex;
                objNew.maxIndex = this.maxIndex;
                objNew.minIndexUOM = this.minIndexUOM;
                objNew.maxIndexUOM = this.maxIndexUOM;
                objNew.columnIndex = this.columnIndex;
                objNew.curveDescription = this.curveDescription;
                objNew.sensorOffset = this.sensorOffset;
                objNew.traceState = this.traceState;
                objNew.typeLogData = this.typeLogData;
                objNew.startIndex = this.startIndex;
                objNew.endIndex = this.endIndex;
                objNew.witsmlMnemonic = this.witsmlMnemonic;
                objNew.valueType = this.valueType;
                objNew.valueQuery = this.valueQuery;
                objNew.fieldName = this.fieldName;
                objNew.sourceColIndex = this.sourceColIndex;
                objNew.curveID = this.curveID;
                objNew.sourceColNo = this.sourceColNo;
                objNew.VuMaxUnitID = this.VuMaxUnitID;
                objNew.UnitID = this.UnitID;
                objNew.ColumnOrder = this.ColumnOrder;
                objNew.DoNotInterpolate = this.DoNotInterpolate;
                objNew.WriteBack = this.WriteBack;
                objNew.PiMnemonic = this.PiMnemonic;
                objNew.isStoredProc = this.isStoredProc;
                objNew.StoredProcParams = this.StoredProcParams;
                objNew.AxisDataCount = this.AxisDataCount;
                objNew.parentMnemonic = this.parentMnemonic;
                objNew.OriginalMnemonic = this.OriginalMnemonic;
                objNew.processChannel = this.processChannel;

                return objNew;
            }
            catch (Exception)
            {
                return new LogChannel();
            }
        }

        public int CompareTo(object? obj)
        {
            try
            {
                if (obj is LogChannel objItem)
                {
                    if (this.ColumnOrder < objItem.ColumnOrder)
                    {
                        return -1;
                    }

                    if (this.ColumnOrder > objItem.ColumnOrder)
                    {
                        return 1;
                    }

                    return 0;
                }

                return 1;
            }
            catch (Exception)
            {
                return 0; // VB returned the default value (0) when the function fell off the end
            }
        }

    }
}
