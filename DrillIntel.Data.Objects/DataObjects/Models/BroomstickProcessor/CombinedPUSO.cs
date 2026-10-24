using System;

namespace DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor
{
    public class CombinedPUSO : IComparable
    {
        public double Depth = 0d;
        public double DynamicPU = 0d;
        public double StaticPU = 0d;
        public double DynamicSO = 0d;
        public double StaticSO = 0d;
        public double Rotate = 0d;
        public double ROB { get => Rotate; set => Rotate = value; }
        public int PUPumpStatus = 0;
        public int SOPumpStatus = 0;
        public int ROBPumpStatus = 0;
        public double StaticSO2 = 0d;

        public double Circulation = 0d;
        public double PumpPressure = 0d;
        public double BlockMovement = 0;

        public double RPM = 0;
        public string RigStates = "";

        public int CompareTo(object? obj)
        {
            try
            {
                if (obj is CombinedPUSO objNew)
                {
                    if (Depth < objNew.Depth)
                    {
                        return -1;
                    }

                    if (Depth == objNew.Depth)
                    {
                        return 0;
                    }

                    if (Depth > objNew.Depth)
                    {
                        return 1;
                    }
                }
            }
            catch (Exception)
            {
            }

            return 0;
        }
    }
}

