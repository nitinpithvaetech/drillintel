using System;

namespace DrillIntel.Data.Objects.DataObjects.DQI
{
    public class DQIChannel
    {
        public string Mnemonic { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public DQIChannel() { }

        public DQIChannel(string mnemonic, string name)
        {
            Mnemonic = mnemonic;
            Name = name;
        }

        public DQIChannel Clone() => new DQIChannel
        {
            Mnemonic = Mnemonic,
            Name = Name
        };
    }
}

