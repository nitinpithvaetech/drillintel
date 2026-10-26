using System;
using System.Collections.Generic;
using System.Data;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class wellSection : IComparable
    {
        public string WellID { get; set; } = "";
        public int EntryID { get; set; } = 0;
        public string SectionName { get; set; } = "";
        public double StartDepth { get; set; } = 0;
        public double EndDepth { get; set; } = 0;
        public double InnerDia { get; set; } = 0;
        public double OuterDia { get; set; } = 0;
        public string HoleType { get; set; } = "";
        public double HoleDiameter { get; set; } = 0;
        public double DepthOfInvestigation { get; set; } = 0;
        public string BHADetails { get; set; } = "";

        public WellSectionPUSOSettings PUSOSettings { get; set; } = new WellSectionPUSOSettings();

        public double DrlgConnTrgTime { get; set; } = 0;
        public double TripConnTrgTime { get; set; } = 0;
        public double BTSTrgTime { get; set; } = 0;
        public double STSTrgTime { get; set; } = 0;
        public double STBTrgTime { get; set; } = 0;
        public string AlarmProfileID { get; set; } = "";

        public double ROPBenchmark { get; set; } = 0;
        public double PipeMoveBenchmark { get; set; } = 0;
        public double TripStandBenchmark { get; set; } = 0;
        public double DrlgConnDeviation { get; set; } = 0;
        public double DrlgSTSDeviation { get; set; } = 0;
        public double ROPDeviation { get; set; } = 0;
        public double TripConnDeviation { get; set; } = 0;
        public double PipeMoveDeviation { get; set; } = 0;
        public double TripStandDeviation { get; set; } = 0;

        public wellSection getCopy()
        {
            var objNew = new wellSection
            {
                WellID = this.WellID,
                EntryID = this.EntryID,
                SectionName = this.SectionName,
                StartDepth = this.StartDepth,
                EndDepth = this.EndDepth,
                InnerDia = this.InnerDia,
                OuterDia = this.OuterDia,
                HoleType = this.HoleType,
                HoleDiameter = this.HoleDiameter,
                DepthOfInvestigation = this.DepthOfInvestigation,
                BHADetails = this.BHADetails,
                PUSOSettings = this.PUSOSettings != null ? this.PUSOSettings.GetCopy() : new WellSectionPUSOSettings(),
                DrlgConnTrgTime = this.DrlgConnTrgTime,
                TripConnTrgTime = this.TripConnTrgTime,
                AlarmProfileID = this.AlarmProfileID,
                BTSTrgTime = this.BTSTrgTime,
                STSTrgTime = this.STSTrgTime,
                STBTrgTime = this.STBTrgTime,
                ROPBenchmark = this.ROPBenchmark,
                PipeMoveBenchmark = this.PipeMoveBenchmark,
                TripStandBenchmark = this.TripStandBenchmark,
                DrlgConnDeviation = this.DrlgConnDeviation,
                DrlgSTSDeviation = this.DrlgSTSDeviation,
                ROPDeviation = this.ROPDeviation,
                TripConnDeviation = this.TripConnDeviation,
                PipeMoveDeviation = this.PipeMoveDeviation,
                TripStandDeviation = this.TripStandDeviation
            };
            return objNew;
        }

        public wellSection GetCopy() => getCopy();

        public static Dictionary<int, wellSection> getList(IDataServiceDIntel objDataService, string paramWellID)
        {
            var list = new Dictionary<int, wellSection>();
            if (objDataService == null || string.IsNullOrWhiteSpace(paramWellID)) return list;

            try
            {
                string sql = "SELECT * FROM VMX_WELL_SECTIONS WHERE WELL_ID = @WellID ORDER BY START_DEPTH";
                var parameters = new Dictionary<string, object?> { ["@WellID"] = paramWellID.Trim() };
                DataTable objData = objDataService.GetTable(sql, parameters);

                if (objData != null)
                {
                    foreach (DataRow objRow in objData.Rows)
                    {
                        var objItem = new wellSection();
                        objItem.WellID = paramWellID;
                        objItem.EntryID = Convert.ToInt32(DataService.checkNull(objRow["ENTRY_ID"], 0));
                        objItem.SectionName = DataService.checkNull(objRow["SECTION_NAME"], "");
                        objItem.StartDepth = DataService.checkNull(objRow["START_DEPTH"], 0.0);
                        objItem.EndDepth = DataService.checkNull(objRow["END_DEPTH"], 0.0);
                        objItem.InnerDia = DataService.checkNull(objRow["INNER_DIA"], 0.0);
                        objItem.OuterDia = DataService.checkNull(objRow["OUTER_DIA"], 0.0);
                        objItem.HoleType = DataService.checkNull(objRow["HOLE_TYPE"], "");
                        objItem.HoleDiameter = DataService.checkNull(objRow["HOLE_DIAMETER"], 0.0);
                        objItem.DepthOfInvestigation = DataService.checkNull(objRow["DEPTH_INVEST"], 0.0);
                        objItem.BHADetails = DataService.checkNull(objRow["BHA_DETAILS"], "");
                        objItem.DrlgConnTrgTime = DataService.checkNull(objRow["DRILL_CONN_TRG_TIME"], 0.0);
                        objItem.TripConnTrgTime = DataService.checkNull(objRow["TRIP_CONN_TRG_TIME"], 0.0);
                        objItem.AlarmProfileID = DataService.checkNull(objRow["ALARM_PROFILE_ID"], "");

                        objItem.PUSOSettings = WellSectionPUSOSettings.LoadSettings(objDataService, paramWellID, objItem.EntryID.ToString());

                        if (!list.ContainsKey(objItem.EntryID))
                        {
                            list.Add(objItem.EntryID, objItem);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return list;
        }

        public static Dictionary<int, wellSection> GetList(IDataServiceDIntel objDataService, string paramWellID)
            => getList(objDataService, paramWellID);

        public int CompareTo(object? obj)
        {
            if (obj is wellSection other)
            {
                return StartDepth.CompareTo(other.StartDepth);
            }
            return 0;
        }
    }
}

