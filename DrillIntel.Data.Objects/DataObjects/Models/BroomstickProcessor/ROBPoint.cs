using System;
using System.Collections.Generic;
using System.Linq;

namespace DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor
{
    public class ROBPoint : IComparable
    {
        public DateTime RecordDate;
        public double Hkld = 0d;
        public double Torque = 0d;
        public double RPM = 0d;
        public double Depth = 0d;
        public double Circulation = 0d;
        public double PumpPressure = 0d;
        public string RigStates = "";

        public PUSOPoint.pusoPumpStatus pumpStatus = PUSOPoint.pusoPumpStatus.PumpOn;

        public ROBPoint getCopy()
        {
            try
            {
                var objNew = new ROBPoint();
                objNew.RecordDate = RecordDate;
                objNew.Hkld = Hkld;
                objNew.Torque = Torque;
                objNew.RPM = RPM;
                objNew.Depth = Depth;
                objNew.Circulation = Circulation;
                objNew.PumpPressure = PumpPressure;
                objNew.pumpStatus = pumpStatus;
                objNew.RigStates = RigStates;

                return objNew;
            }
            catch (Exception)
            {
                return new ROBPoint();
            }
        }

        public ROBPoint GetCopy() => getCopy();

        public static Dictionary<int, ROBPoint> filterROBListByTime(Dictionary<int, ROBPoint> list)
        {
            try
            {
                var newList = new Dictionary<int, ROBPoint>();
                var arrPoints = list.Values.ToArray();
                if (arrPoints.Length == 0) return newList;

                int MAX_THRESHOLD = 5;
                int MIN_DATAAMOUNT = 8;
                int MAX_DEPTH_THRESHOLD = 3;

                var startDate = arrPoints[0].RecordDate;

                var subList = new Dictionary<int, ROBPoint>();
                subList.Add(subList.Count + 1, arrPoints[0].getCopy());

                for (int i = 1; i <= arrPoints.Length - 1; i++)
                {
                    var lnDate = arrPoints[i].RecordDate;
                    var lnPrevDate = arrPoints[i - 1].RecordDate;

                    int secDiff = (int)Math.Abs((lnPrevDate - lnDate).TotalSeconds);

                    if (secDiff > MAX_THRESHOLD)
                    {
                        // Break Found ...
                        double lnMaxDepth = 0d;
                        double lnMinDepth = 0d;

                        foreach (ROBPoint objPoint in subList.Values)
                        {
                            if (objPoint.Depth > lnMaxDepth)
                            {
                                lnMaxDepth = objPoint.Depth;
                            }
                        }

                        lnMinDepth = lnMaxDepth;

                        foreach (ROBPoint objPoint in subList.Values)
                        {
                            if (objPoint.Depth < lnMinDepth)
                            {
                                lnMinDepth = objPoint.Depth;
                            }
                        }

                        lnMaxDepth = lnMinDepth;

                        foreach (ROBPoint objPoint in subList.Values)
                        {
                            if (objPoint.Depth > lnMaxDepth)
                            {
                                lnMaxDepth = objPoint.Depth;
                            }
                        }

                        double depthDiff = lnMaxDepth - lnMinDepth;

                        var subArray = subList.Values.ToArray();
                        Array.Sort(subArray);

                        int timeROB = 0;

                        if (subArray is Array && subArray.Length > 0)
                        {
                            timeROB = (int)Math.Abs((subArray[0].RecordDate - subArray[subArray.Length - 1].RecordDate).TotalSeconds);
                        }

                        // Check two conditions to make sure that sub list is good ...
                        bool validSubList = false;

                        if (timeROB > MIN_DATAAMOUNT && depthDiff < MAX_DEPTH_THRESHOLD)
                        {
                            validSubList = true;
                        }

                        if (validSubList)
                        {
                            // Add it to the main list ...
                            foreach (ROBPoint objPoint in subList.Values)
                            {
                                newList.Add(newList.Count + 1, objPoint.getCopy());
                            }
                        }

                        // Clear the sub list
                        subList.Clear();

                        // Process sub list ... and start over
                        startDate = lnDate;
                    }
                    else
                    {
                        // Push it to the temp list ...
                        subList.Add(subList.Count + 1, arrPoints[i].getCopy());
                    }
                }

                // Handle the rest of the buffered points ...
                if (subList.Count > 0)
                {
                    double lnMaxDepth = 0d;
                    double lnMinDepth = 0d;

                    foreach (ROBPoint objPoint in subList.Values)
                    {
                        if (objPoint.Depth > lnMaxDepth)
                        {
                            lnMaxDepth = objPoint.Depth;
                        }
                    }

                    lnMinDepth = lnMaxDepth;

                    foreach (ROBPoint objPoint in subList.Values)
                    {
                        if (objPoint.Depth < lnMinDepth)
                        {
                            lnMinDepth = objPoint.Depth;
                        }
                    }

                    lnMaxDepth = lnMinDepth;

                    foreach (ROBPoint objPoint in subList.Values)
                    {
                        if (objPoint.Depth > lnMaxDepth)
                        {
                            lnMaxDepth = objPoint.Depth;
                        }
                    }

                    double depthDiff = lnMaxDepth - lnMinDepth;

                    var subArray = subList.Values.ToArray();
                    Array.Sort(subArray);

                    int timeROB = 0;

                    if (subArray is Array && subArray.Length > 0)
                    {
                        timeROB = (int)Math.Abs((subArray[0].RecordDate - subArray[subArray.Length - 1].RecordDate).TotalSeconds);
                    }

                    // Check two conditions to make sure that sub list is good ...
                    bool validSubList = false;

                    if (timeROB > MIN_DATAAMOUNT && depthDiff < MAX_DEPTH_THRESHOLD)
                    {
                        validSubList = true;
                    }

                    if (validSubList)
                    {
                        // Add it to the main list ...
                        foreach (ROBPoint objPoint in subList.Values)
                        {
                            newList.Add(newList.Count + 1, objPoint.getCopy());
                        }
                    }

                    // Clear the sub list
                    subList.Clear();
                }

                return newList;
            }
            catch (Exception)
            {
                return new Dictionary<int, ROBPoint>();
            }
        }

        public static bool calculateROB(
            Dictionary<int, ROBPoint> plist,
            BroomStickSetup objSetup,
            ref double paramHkld,
            ref double paramTorque,
            ref bool paramStabilityFound,
            ref double paramSPPA,
            ref double paramCirc,
            ref double paramRPM)
        {
            try
            {
                var filteredList = plist;

                // Find the stability point from the data
                if (filteredList.Count <= objSetup.TolerancePoints)
                {
                    // Simply calculate the avg. of all points and return ...
                    double sumHkld = 0d;
                    double sumTorque = 0d;
                    int sampleCount = 0;

                    foreach (ROBPoint objItem in filteredList.Values)
                    {
                        sumHkld += objItem.Hkld;
                        sumTorque += objItem.Torque;
                        sampleCount += 1;
                    }

                    if (sampleCount > 0 && sumHkld > 0d)
                    {
                        paramHkld = Math.Round(sumHkld / sampleCount, 2);
                    }

                    if (sampleCount > 0 && sumTorque > 0d)
                    {
                        paramTorque = Math.Round(sumTorque / sampleCount, 2);
                    }

                    paramStabilityFound = false;
                    return true;
                }

                var arrValues = filteredList.Values.ToArray();

                bool stabilityFound = false;
                int stabilityStartIndex = 0;
                int startPoint = 0;

                if (objSetup.TolerancePoints <= 0)
                {
                    objSetup.TolerancePoints = 1;
                }

                if (objSetup.ROBMaxHkldTolerance <= 0)
                {
                    objSetup.ROBMaxHkldTolerance = 1;
                }

                startPoint = objSetup.TolerancePoints;

                for (int i = startPoint, loopTo = arrValues.Length - 1; i <= loopTo; i++)
                {
                    double minValue = 0d;
                    double maxValue = 0d;

                    for (int j = i, loopTo1 = i - objSetup.TolerancePoints; j >= loopTo1; j -= 1)
                    {
                        if (arrValues[j].Hkld > maxValue)
                        {
                            maxValue = arrValues[j].Hkld;
                        }
                    }

                    minValue = maxValue;

                    for (int j = i, loopTo2 = i - objSetup.TolerancePoints; j >= loopTo2; j -= 1)
                    {
                        if (arrValues[j].Hkld < minValue)
                        {
                            minValue = arrValues[j].Hkld;
                        }
                    }

                    maxValue = minValue;

                    for (int j = i, loopTo3 = i - objSetup.TolerancePoints; j >= loopTo3; j -= 1)
                    {
                        if (arrValues[j].Hkld > maxValue)
                        {
                            maxValue = arrValues[j].Hkld;
                        }
                    }

                    double Diff = Math.Abs(maxValue - minValue);

                    if (Diff < objSetup.ROBMaxHkldTolerance)
                    {
                        // Stability Found ...
                        stabilityFound = true;
                        stabilityStartIndex = i;
                        break;
                    }
                }

                if (stabilityFound)
                {
                    double sumHkld = 0d;
                    double sumTorque = 0d;
                    int sampleCount = 0;
                    double sumSPPA = 0d;
                    double sumCirc = 0d;
                    double sumRPM = 0d;

                    for (int i = stabilityStartIndex, loopTo4 = arrValues.Length - 1; i <= loopTo4; i++)
                    {
                        sumHkld += arrValues[i].Hkld;
                        sumTorque += arrValues[i].Torque;
                        sumSPPA += arrValues[i].PumpPressure;
                        sumCirc += arrValues[i].Circulation;
                        sumRPM += arrValues[i].RPM;

                        sampleCount += 1;
                        stabilityFound = true;
                    }

                    if (sampleCount > 0 && sumHkld > 0d)
                    {
                        paramHkld = Math.Round(sumHkld / sampleCount, 2);
                    }

                    if (sampleCount > 0 && sumTorque > 0d)
                    {
                        paramTorque = Math.Round(sumTorque / sampleCount, 2);
                    }

                    if (sampleCount > 0 && sumSPPA > 0d)
                    {
                        paramSPPA = Math.Round(sumSPPA / sampleCount, 2);
                    }

                    if (sampleCount > 0 && sumCirc > 0d)
                    {
                        paramCirc = Math.Round(sumCirc / sampleCount, 2);
                    }

                    if (sampleCount > 0 && sumRPM > 0d)
                    {
                        paramRPM = Math.Round(sumRPM / sampleCount, 2);
                    }
                }
                else
                {
                    // Take the last value ...
                    paramHkld = arrValues[arrValues.Length - 1].Hkld;
                    paramTorque = arrValues[arrValues.Length - 1].Torque;
                    paramCirc = arrValues[arrValues.Length - 1].Circulation;
                    paramSPPA = arrValues[arrValues.Length - 1].PumpPressure;
                    paramRPM = arrValues[arrValues.Length - 1].RPM;
                }

                paramStabilityFound = stabilityFound;
                return true;
            }
            catch (Exception)
            {
                return true;
            }
        }

        public int CompareTo(object? obj)
        {
            try
            {
                if (obj is ROBPoint objNew)
                {
                    if (RecordDate < objNew.RecordDate)
                    {
                        return -1;
                    }

                    if (RecordDate == objNew.RecordDate)
                    {
                        return 0;
                    }

                    if (RecordDate > objNew.RecordDate)
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

        public int CompareTo(ROBPoint? other)
        {
            if (other == null) return 1;
            return RecordDate.CompareTo(other.RecordDate);
        }
    }
}

