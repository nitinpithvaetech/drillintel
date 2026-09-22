using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class TimeLogIndex
    {
        public string ID { get; set; } = "";
        public string Prefix { get; set; } = "";
        public string Channels { get; set; } = "";
        public bool Active { get; set; } = true;
    }
}
