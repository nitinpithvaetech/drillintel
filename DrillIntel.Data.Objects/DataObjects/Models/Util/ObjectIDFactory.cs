using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrillIntel.Data.Objects.DataObjects.Models.Util
{
    public class ObjectIDFactory
    {
        private Random objRandom = new Random();

        public static string generateAlarmHistoryTableName()
        {
            try
            {
                Random objNewRandom = new Random();

                int part1 = objNewRandom.Next(10000000, 99999999);
                int part2 = objNewRandom.Next(10000000, 99999999);

                string tableName = "VMX_ALARM_HISTORY_" + part1.ToString() + "#" + part2.ToString();

                return tableName;
            }
            catch (Exception)
            {
                return "VMX_ALARM_HISTORY";
            }
        }

        public static string generateTimeLogTableName()
        {
            try
            {
                Random objNewRandom = new Random();

                int part1 = objNewRandom.Next(10000000, 99999999);
                int part2 = objNewRandom.Next(10000000, 99999999);

                string tableName = "timeLog" + part1.ToString() + "#" + part2.ToString();

                return tableName;
            }
            catch (Exception)
            {
                return "timeLog";
            }
        }

        public static string generateDepthLogTableName()
        {
            try
            {
                Random objNewRandom = new Random();

                int part1 = objNewRandom.Next(10000000, 99999999);
                int part2 = objNewRandom.Next(10000000, 99999999);

                string tableName = "depthLog" + part1.ToString() + "#" + part2.ToString();

                return tableName;
            }
            catch (Exception)
            {
                return "depthLog";
            }
        }

        public int getNumericID()
        {
            try
            {
                return objRandom.Next(10000000, 99999999);
            }
            catch (Exception)
            {
                return 0; // VB returned the default value (0) when the function fell off the end
            }
        }

        public int getShortNumericID()
        {
            try
            {
                return objRandom.Next(100, 999);
            }
            catch (Exception)
            {
                return 0; // VB returned the default value (0) when the function fell off the end
            }
        }

        // Returns object ID in the form of xxx-xxx-xxx-xxx-xxx format
        public string getObjectID()
        {
            try
            {
                string part1 = objRandom.Next(100, 999).ToString();
                string part2 = objRandom.Next(100, 999).ToString();
                string part3 = objRandom.Next(100, 999).ToString();
                string part4 = objRandom.Next(100, 999).ToString();
                string part5 = objRandom.Next(100, 999).ToString();

                string objectID = part1 + "-" + part2 + "-" + part3 + "-" + part4 + "-" + part5;

                return objectID;
            }
            catch (Exception)
            {
                return "";
            }
        }

        // Returns object ID in the form of prefix-xxx-xxx-xxx-xxx format
        public string getObjectID(string prefix)
        {
            try
            {
                // VB's Randomize() only seeds VB's Rnd() and had no effect on System.Random, so it is dropped.

                string part1 = prefix;
                string part2 = objRandom.Next(100, 999).ToString();
                string part3 = objRandom.Next(100, 999).ToString();
                string part4 = objRandom.Next(100, 999).ToString();
                string part5 = objRandom.Next(100, 999).ToString();

                string objectID = part1 + "-" + part2 + "-" + part3 + "-" + part4 + "-" + part5;

                return objectID;
            }
            catch (Exception)
            {
                return "";
            }
        }

    }
}
