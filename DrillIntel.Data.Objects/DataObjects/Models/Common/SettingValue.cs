using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrillIntel.Data.Objects.DataObjects.Models.Common
{
    public class SettingValue
    {
        public string SettingID { get; set; } = "";
        public string SettingName { get; set; } = "";
        public string Value { get; set; } = "";
        public string SettingType { get; set; } = "GLOBAL";
        public bool NeedToSave { get; set; } = false;
    }
}
