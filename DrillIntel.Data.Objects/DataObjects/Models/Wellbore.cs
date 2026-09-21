using System;
using System.Collections.Generic;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class Wellbore
    {
        public string ObjectID { get; set; } = "";
        public string WellID { get; set; } = ""; // Reference to the well object
        public string nameWell { get; set; } = "";
        public string name { get; set; } = "";
        public string number { get; set; } = "";
        public string numGovt { get; set; } = "";
        public string statusWellbore { get; set; } = "";
        public string purposeWellbore { get; set; } = "";
        public string typeWellbore { get; set; } = "";
        public string shape { get; set; } = "";
        public string dTimeKickoff { get; set; } = "";
        public double mdCurrent { get; set; } = 0;
        public double tvdCurrent { get; set; } = 0;
        public double mdKickoff { get; set; } = 0;
        public double tvdKickoff { get; set; } = 0;
        public double mdPlanned { get; set; } = 0;
        public double tvdPlanned { get; set; } = 0;
        public double mdSubSeaPlanned { get; set; } = 0;
        public double tvdSubSeaPlanned { get; set; } = 0;
        public double dayTarget { get; set; } = 0;
        public string ServerKey { get; set; } = "";
        public string wmlsurl { get; set; } = "";
        public string wmlpurl { get; set; } = "";
        public DateTime lastDataReceived { get; set; }
        public DateTime lastRestartStarted { get; set; }

        public Dictionary<string, TimeLog> timeLogs { get; set; } = new Dictionary<string, TimeLog>();
        public Dictionary<string, DepthLog> depthLogs { get; set; } = new Dictionary<string, DepthLog>();
        public Dictionary<string, Trajectory> trajectories { get; set; } = new Dictionary<string, Trajectory>();
        public Dictionary<string, opsReport> opsReports { get; set; } = new Dictionary<string, opsReport>();
        public Dictionary<string, bhaRun> bhaRuns { get; set; } = new Dictionary<string, bhaRun>();
        public Dictionary<string, mudLog> mudLogs { get; set; } = new Dictionary<string, mudLog>();

        public Dictionary<string, TimeLog> newTimeLogs { get; set; } = new Dictionary<string, TimeLog>();
        public Dictionary<string, DepthLog> newDepthLogs { get; set; } = new Dictionary<string, DepthLog>();
        public Dictionary<string, Trajectory> newTrajectories { get; set; } = new Dictionary<string, Trajectory>();
        public Dictionary<string, mudLog> newMudLogs { get; set; } = new Dictionary<string, mudLog>();

        public Wellbore GetCopy()
        {
            try
            {
                var objNew = new Wellbore();

                objNew.ObjectID = this.ObjectID;
                objNew.WellID = this.WellID;
                objNew.nameWell = this.nameWell;
                objNew.name = this.name;
                objNew.number = this.number;
                objNew.numGovt = this.numGovt;
                objNew.statusWellbore = this.statusWellbore;
                objNew.purposeWellbore = this.purposeWellbore;
                objNew.typeWellbore = this.typeWellbore;
                objNew.shape = this.shape;
                objNew.dTimeKickoff = this.dTimeKickoff;
                objNew.mdCurrent = this.mdCurrent;
                objNew.tvdCurrent = this.tvdCurrent;
                objNew.mdKickoff = this.mdKickoff;
                objNew.tvdKickoff = this.tvdKickoff;
                objNew.mdPlanned = this.mdPlanned;
                objNew.tvdPlanned = this.tvdPlanned;
                objNew.mdSubSeaPlanned = this.mdSubSeaPlanned;
                objNew.tvdSubSeaPlanned = this.tvdSubSeaPlanned;
                objNew.dayTarget = this.dayTarget;
                objNew.ServerKey = this.ServerKey;
                objNew.wmlsurl = this.wmlsurl;
                objNew.wmlpurl = this.wmlpurl;
                objNew.lastDataReceived = this.lastDataReceived;
                objNew.lastRestartStarted = this.lastRestartStarted;

                if (this.timeLogs != null)
                {
                    foreach (string objKey in this.timeLogs.Keys)
                    {
                        if (this.timeLogs[objKey] != null)
                        {
                            objNew.timeLogs.Add(objKey, this.timeLogs[objKey].GetCopy());
                        }
                    }
                }

                if (this.depthLogs != null)
                {
                    foreach (string objKey in this.depthLogs.Keys)
                    {
                        if (this.depthLogs[objKey] != null)
                        {
                            objNew.depthLogs.Add(objKey, this.depthLogs[objKey].GetCopy());
                        }
                    }
                }

                if (this.trajectories != null)
                {
                    foreach (string objKey in this.trajectories.Keys)
                    {
                        if (this.trajectories[objKey] != null)
                        {
                            objNew.trajectories.Add(objKey, this.trajectories[objKey].GetCopy());
                        }
                    }
                }

                if (this.mudLogs != null)
                {
                    foreach (string objKey in this.mudLogs.Keys)
                    {
                        if (this.mudLogs[objKey] != null)
                        {
                            objNew.mudLogs.Add(objKey, this.mudLogs[objKey].GetCopy());
                        }
                    }
                }

                if (this.newTimeLogs == null)
                {
                    this.newTimeLogs = new Dictionary<string, TimeLog>();
                }

                if (this.newDepthLogs == null)
                {
                    this.newDepthLogs = new Dictionary<string, DepthLog>();
                }

                if (this.newTrajectories == null)
                {
                    this.newTrajectories = new Dictionary<string, Trajectory>();
                }

                if (this.newMudLogs == null)
                {
                    this.newMudLogs = new Dictionary<string, mudLog>();
                }

                foreach (string objKey in this.newTimeLogs.Keys)
                {
                    if (this.newTimeLogs[objKey] != null)
                    {
                        objNew.newTimeLogs.Add(objKey, this.newTimeLogs[objKey].GetCopy());
                    }
                }

                foreach (string objKey in this.newDepthLogs.Keys)
                {
                    if (this.newDepthLogs[objKey] != null)
                    {
                        objNew.newDepthLogs.Add(objKey, this.newDepthLogs[objKey].GetCopy());
                    }
                }

                foreach (string objKey in this.newTrajectories.Keys)
                {
                    if (this.newTrajectories[objKey] != null)
                    {
                        objNew.newTrajectories.Add(objKey, this.newTrajectories[objKey].GetCopy());
                    }
                }

                foreach (string objKey in this.newMudLogs.Keys)
                {
                    if (this.newMudLogs[objKey] != null)
                    {
                        objNew.newMudLogs.Add(objKey, this.newMudLogs[objKey].GetCopy());
                    }
                }

                return objNew;
            }
            catch (Exception)
            {
                return new Wellbore();
            }
        }
    }
}

