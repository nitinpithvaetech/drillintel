using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;

namespace DrillIntel.Data.Objects.DataObjects.Services
{
    public class WellboreService
    {
        public static bool IsWellboreExist(IDataServiceDIntel objDataService, string wellID, string wellboreID)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(wellID) || string.IsNullOrWhiteSpace(wellboreID))
                    return false;

                string sql = "SELECT WELLBORE_ID FROM VMX_WELLBORE WHERE WELL_ID='"
                    + wellID.Replace("'", "''") + "' AND WELLBORE_ID='"
                    + wellboreID.Replace("'", "''") + "'";
                return objDataService.IsRecordExist(sql);
            }
            catch
            {
                return false;
            }
        }

        public static bool AddWellbore(IDataServiceDIntel objDataService, Wellbore objWellbore, ref string lastError)
        {
            try
            {
                if (objDataService == null)
                {
                    lastError = "Data service is not initialized.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(objWellbore.WellID))
                {
                    lastError = "Well ID cannot be empty.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(objWellbore.ObjectID))
                {
                    objWellbore.ObjectID = Guid.NewGuid().ToString();
                }

                if (IsWellboreExist(objDataService, objWellbore.WellID, objWellbore.ObjectID))
                {
                    lastError = "Wellbore already exists.";
                    return false;
                }

                string nowStr = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");
                string userName = objDataService.UserName ?? string.Empty;

                string strSQL = "INSERT INTO VMX_WELLBORE ("
                    + "WELL_ID, WELLBORE_ID, WELLBORE_NAME, WELLBORE_TYPE, WELLBORE_NO, GOVT_NO, "
                    + "SHAPE, STATUS, PURPOSE, KICKOFF_DATE, DAY_TARGET, MD_CURRENT, TVD_CURRENT, "
                    + "MD_KICKOFF, TVD_KICKOFF, MD_PLANNED, TVD_PLANNED, MD_SS_PLANNED, TVD_SS_PLANNED, "
                    + "WMLS_URL, WMLP_URL, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE) VALUES (";

                strSQL += "'" + objWellbore.WellID.Replace("'", "''") + "',";
                strSQL += "'" + objWellbore.ObjectID.Replace("'", "''") + "',";
                strSQL += "'" + (objWellbore.name ?? string.Empty).Replace("'", "''") + "',";
                strSQL += "'" + (objWellbore.typeWellbore ?? string.Empty).Replace("'", "''") + "',";
                strSQL += "'" + (objWellbore.number ?? string.Empty).Replace("'", "''") + "',";
                strSQL += "'" + (objWellbore.numGovt ?? string.Empty).Replace("'", "''") + "',";
                strSQL += "'" + (objWellbore.shape ?? string.Empty).Replace("'", "''") + "',";
                strSQL += "'" + (objWellbore.statusWellbore ?? string.Empty).Replace("'", "''") + "',";
                strSQL += "'" + (objWellbore.purposeWellbore ?? string.Empty).Replace("'", "''") + "',";
                strSQL += "'" + (objWellbore.dTimeKickoff ?? string.Empty).Replace("'", "''") + "',";
                strSQL += "" + Convert.ToInt32(objWellbore.dayTarget).ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWellbore.mdCurrent.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWellbore.tvdCurrent.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWellbore.mdKickoff.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWellbore.tvdKickoff.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWellbore.mdPlanned.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWellbore.tvdPlanned.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWellbore.mdSubSeaPlanned.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWellbore.tvdSubSeaPlanned.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "'" + (objWellbore.wmlsurl ?? string.Empty).Replace("'", "''") + "',";
                strSQL += "'" + (objWellbore.wmlpurl ?? string.Empty).Replace("'", "''") + "',";
                strSQL += "'" + userName.Replace("'", "''") + "',";
                strSQL += "'" + nowStr + "',";
                strSQL += "'" + userName.Replace("'", "''") + "',";
                strSQL += "'" + nowStr + "')";

                if (objDataService.ExecuteNonQuery(strSQL))
                {
                    return true;
                }
                else
                {
                    lastError = objDataService.LastError;
                    return false;
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ":" + ex.StackTrace;
                return false;
            }
        }

        public static bool UpdateWellbore(IDataServiceDIntel objDataService, Wellbore objWellbore, ref string lastError)
        {
            try
            {
                if (objDataService == null)
                {
                    lastError = "Data service is not initialized.";
                    return false;
                }

                string nowStr = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");
                string userName = objDataService.UserName ?? string.Empty;

                string strSQL = "UPDATE VMX_WELLBORE SET "
                    + "WELLBORE_NAME='" + (objWellbore.name ?? string.Empty).Replace("'", "''") + "',"
                    + "WELLBORE_TYPE='" + (objWellbore.typeWellbore ?? string.Empty).Replace("'", "''") + "',"
                    + "WELLBORE_NO='" + (objWellbore.number ?? string.Empty).Replace("'", "''") + "',"
                    + "GOVT_NO='" + (objWellbore.numGovt ?? string.Empty).Replace("'", "''") + "',"
                    + "SHAPE='" + (objWellbore.shape ?? string.Empty).Replace("'", "''") + "',"
                    + "STATUS='" + (objWellbore.statusWellbore ?? string.Empty).Replace("'", "''") + "',"
                    + "PURPOSE='" + (objWellbore.purposeWellbore ?? string.Empty).Replace("'", "''") + "',"
                    + "KICKOFF_DATE='" + (objWellbore.dTimeKickoff ?? string.Empty).Replace("'", "''") + "',"
                    + "DAY_TARGET=" + Convert.ToInt32(objWellbore.dayTarget).ToString(CultureInfo.InvariantCulture) + ","
                    + "MD_CURRENT=" + objWellbore.mdCurrent.ToString(CultureInfo.InvariantCulture) + ","
                    + "TVD_CURRENT=" + objWellbore.tvdCurrent.ToString(CultureInfo.InvariantCulture) + ","
                    + "MD_KICKOFF=" + objWellbore.mdKickoff.ToString(CultureInfo.InvariantCulture) + ","
                    + "TVD_KICKOFF=" + objWellbore.tvdKickoff.ToString(CultureInfo.InvariantCulture) + ","
                    + "MD_PLANNED=" + objWellbore.mdPlanned.ToString(CultureInfo.InvariantCulture) + ","
                    + "TVD_PLANNED=" + objWellbore.tvdPlanned.ToString(CultureInfo.InvariantCulture) + ","
                    + "MD_SS_PLANNED=" + objWellbore.mdSubSeaPlanned.ToString(CultureInfo.InvariantCulture) + ","
                    + "TVD_SS_PLANNED=" + objWellbore.tvdSubSeaPlanned.ToString(CultureInfo.InvariantCulture) + ","
                    + "WMLS_URL='" + (objWellbore.wmlsurl ?? string.Empty).Replace("'", "''") + "',"
                    + "WMLP_URL='" + (objWellbore.wmlpurl ?? string.Empty).Replace("'", "''") + "',"
                    + "MODIFIED_BY='" + userName.Replace("'", "''") + "',"
                    + "MODIFIED_DATE='" + nowStr + "' "
                    + "WHERE WELL_ID='" + objWellbore.WellID.Replace("'", "''") + "' AND WELLBORE_ID='" + objWellbore.ObjectID.Replace("'", "''") + "'";

                if (objDataService.ExecuteNonQuery(strSQL))
                {
                    return true;
                }
                else
                {
                    lastError = objDataService.LastError;
                    return false;
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ":" + ex.StackTrace;
                return false;
            }
        }

        public static Wellbore? LoadObject(IDataServiceDIntel objDataService, string wellID, string wellboreID, ref string lastError)
        {
            try
            {
                DataTable dt = objDataService.GetTable("SELECT * FROM VMX_WELLBORE WHERE WELL_ID='"
                    + wellID.Replace("'", "''") + "' AND WELLBORE_ID='" + wellboreID.Replace("'", "''") + "'");

                if (dt != null && dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    return MapRowToWellbore(row, wellID);
                }

                return null;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ":" + ex.StackTrace;
                return null;
            }
        }

        public static List<Wellbore> LoadWellbores(IDataServiceDIntel objDataService, string wellID, ref string lastError)
        {
            var list = new List<Wellbore>();
            try
            {
                DataTable dt = objDataService.GetTable("SELECT * FROM VMX_WELLBORE WHERE WELL_ID='"
                    + wellID.Replace("'", "''") + "'");

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapRowToWellbore(row, wellID));
                    }
                    dt.Dispose();
                }

                return list;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ":" + ex.StackTrace;
                return list;
            }
        }

        private static Wellbore MapRowToWellbore(DataRow row, string wellID)
        {
            var wb = new Wellbore
            {
                WellID = wellID,
                ObjectID = DataService.checkNull(row["WELLBORE_ID"], ""),
                name = DataService.checkNull(row["WELLBORE_NAME"], ""),
                typeWellbore = DataService.checkNull(row["WELLBORE_TYPE"], ""),
                number = DataService.checkNull(row["WELLBORE_NO"], ""),
                numGovt = DataService.checkNull(row["GOVT_NO"], ""),
                shape = DataService.checkNull(row["SHAPE"], ""),
                statusWellbore = DataService.checkNull(row["STATUS"], ""),
                purposeWellbore = DataService.checkNull(row["PURPOSE"], ""),
                dTimeKickoff = DataService.checkNull(row["KICKOFF_DATE"], ""),
                dayTarget = Convert.ToDouble(DataService.checkNull(row["DAY_TARGET"], 0)),
                mdCurrent = DataService.checkNull(row["MD_CURRENT"], 0.0),
                tvdCurrent = DataService.checkNull(row["TVD_CURRENT"], 0.0),
                mdKickoff = DataService.checkNull(row["MD_KICKOFF"], 0.0),
                tvdKickoff = DataService.checkNull(row["TVD_KICKOFF"], 0.0),
                mdPlanned = DataService.checkNull(row["MD_PLANNED"], 0.0),
                tvdPlanned = DataService.checkNull(row["TVD_PLANNED"], 0.0),
                mdSubSeaPlanned = DataService.checkNull(row["MD_SS_PLANNED"], 0.0),
                tvdSubSeaPlanned = DataService.checkNull(row["TVD_SS_PLANNED"], 0.0),
                wmlsurl = DataService.checkNull(row["WMLS_URL"], ""),
                wmlpurl = DataService.checkNull(row["WMLP_URL"], "")
            };
            return wb;
        }
    }
}

