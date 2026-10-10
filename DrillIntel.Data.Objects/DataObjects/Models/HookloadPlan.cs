using System;
using System.Collections.Generic;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class HookloadPlan
    {
        public const string cnPickup = "PKUP";
        public const string cnSlackOff = "SLOF";
        public const string cnRotate = "ROT";
        public const string cnTorque = "TOR";
        public const string cnCustom = "CUSTOM";
        public const string cnOnTorque = "ONTOR";
        public const string cnMkTorque = "MKTOR";
        public const string cnTorqueLimit = "TQLMT";
        public const string cnSinRot = "SNROT";

        public string WellID { get; set; } = "";
        public string WellboreID { get; set; } = "";
        public string LogID { get; set; } = "";
        public string Name { get; set; } = "";
        public string PlanType { get; set; } = "";

        public Dictionary<int, HookloadPlanData> pickup { get; set; } = new Dictionary<int, HookloadPlanData>();
        public Dictionary<int, HookloadPlanData> slackoff { get; set; } = new Dictionary<int, HookloadPlanData>();
        public Dictionary<int, HookloadPlanData> rotate { get; set; } = new Dictionary<int, HookloadPlanData>();

        public HookloadPlan GetCopy() => new HookloadPlan();
    }
}
