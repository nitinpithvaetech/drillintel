using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class LogChannel : IComparable
    {
        public enum enValueType
        {
            staticValue = 0,
            queryValue = 1
        }

        public string mnemonic = ""; // Primary Key
        public string classWitsml = "";
        public string unit = "";
        public string mnemAlias = "";
        public string nullValue = "";
        public string minIndex = "";
        public string maxIndex = "";
        public string minIndexUOM = "";
        public string maxIndexUOM = "";
        public string columnIndex = "";
        public string curveDescription = "";
        public string sensorOffset = "";
        public string traceState = "";
        public string typeLogData = "";
        public string startIndex = "";
        public string endIndex = "";
        public string witsmlMnemonic = ""; // Server Mnemonic
        public int valueType = 0;
        public string valueQuery = "";
        public string fieldName = "";
        public int sourceColIndex = 0;
        public int curveID = 0;
        public int sourceColNo = 0;
        public string VuMaxUnitID = "";
        public string UnitID = "";
        public int ColumnOrder = 0;
        public int DoNotInterpolate = 0;
        public bool WriteBack = false;
        public string PiMnemonic = "";
        public int AxisDataCount = 0;

        public bool isStoredProc = false;
        public string StoredProcParams = "";

        public bool processChannel = false;
        public string parentMnemonic = "";

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
                // Note: processChannel is not copied, same as the original VB code.

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
