using System;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class OffsetWell
    {
        public string OffsetWellID { get; set; } = "";
        public string OffsetWellUWI { get; set; } = "";

        public OffsetWell GetCopy()
        {
            try
            {
                var objNew = new OffsetWell
                {
                    OffsetWellID = this.OffsetWellID,
                    OffsetWellUWI = this.OffsetWellUWI
                };

                return objNew;
            }
            catch (Exception)
            {
                return new OffsetWell();
            }
        }
    }
}

