using System;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class rigStateItem
    {
        public int Number { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public double Color { get; set; } = 0;
        public string ColorHex { get; set; } = string.Empty;

        public rigStateItem GetCopy()
        {
            return new rigStateItem
            {
                Number = this.Number,
                Name = this.Name,
                Color = this.Color,
                ColorHex = this.ColorHex
            };
        }
    }
}

