using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    /// <summary>
    /// Represents Additional Hookload and Torque Plan data and database operations.
    /// Converted from AdnlHookloadPlan.vb to C# with SQLite syntax.
    /// </summary>
    public class AdnlHookloadPlan
    {
        #region Constants
        // --- [ORIGINAL VB CONSTANTS] ---
        // Public Const cnPickup As String = "PKUP"
        // Public Const cnSlackOff As String = "SLOF"
        // Public Const cnRotate As String = "ROT"
        // Public Const cnTorque As String = "TOR"
        // Public Const cnOnTorque As String = "ONTOR"
        // Public Const cnMkTorque As String = "MKTOR"
        // Public Const cnTypeHkldPlan As String = "HKLDP"
        // Public Const cnTypeActualHkld As String = "HKLDA"
        // Public Const cnTypeTorquePlan As String = "TORP"
        // Public Const cnTypeActualTorque As String = "TORA"
        // Public Const cnTorqueLimit As String = "TQLMT"
        // Public Const cnSinRot As String = "SNROT"
        public const string cnPickup = "PKUP";
        public const string cnSlackOff = "SLOF";
        public const string cnRotate = "ROT";
        public const string cnTorque = "TOR";
        public const string cnOnTorque = "ONTOR";
        public const string cnMkTorque = "MKTOR";
        public const string cnTypeHkldPlan = "HKLDP";
        public const string cnTypeActualHkld = "HKLDA";
        public const string cnTypeTorquePlan = "TORP";
        public const string cnTypeActualTorque = "TORA";
        public const string cnTorqueLimit = "TQLMT";
        public const string cnSinRot = "SNROT";
        #endregion

        #region Public Properties
        // --- [ORIGINAL VB FIELDS] ---
        // Public WellID As String = ""
        // Public WellboreID As String = ""
        // Public LogID As String = ""
        // Public PlanID As String = ""
        // Public Name As String = ""
        // Public PlanType As String = ""
        // Public RunNo As String = ""
        // Public Type As Integer = 0
        // Public pickup As New Dictionary(Of Integer, HookloadPlanData)
        // Public slackoff As New Dictionary(Of Integer, HookloadPlanData)
        // Public rotate As New Dictionary(Of Integer, HookloadPlanData)
        // Public torque As New Dictionary(Of Integer, HookloadPlanData)
        // Public onTorque As New Dictionary(Of Integer, HookloadPlanData)
        // Public mkTorque As New Dictionary(Of Integer, HookloadPlanData)
        // Public tqLimit As New Dictionary(Of Integer, HookloadPlanData)
        // Public sinRot As New Dictionary(Of Integer, HookloadPlanData)
        public string WellID { get; set; } = "";
        public string WellboreID { get; set; } = "";
        public string LogID { get; set; } = "";
        public string PlanID { get; set; } = "";
        public string Name { get; set; } = "";
        public string PlanType { get; set; } = "";
        public string RunNo { get; set; } = "";
        public int Type { get; set; } = 0;

        public Dictionary<int, HookloadPlanData> pickup { get; set; } = new Dictionary<int, HookloadPlanData>();
        public Dictionary<int, HookloadPlanData> slackoff { get; set; } = new Dictionary<int, HookloadPlanData>();
        public Dictionary<int, HookloadPlanData> rotate { get; set; } = new Dictionary<int, HookloadPlanData>();
        public Dictionary<int, HookloadPlanData> torque { get; set; } = new Dictionary<int, HookloadPlanData>();
        public Dictionary<int, HookloadPlanData> onTorque { get; set; } = new Dictionary<int, HookloadPlanData>();
        public Dictionary<int, HookloadPlanData> mkTorque { get; set; } = new Dictionary<int, HookloadPlanData>();
        public Dictionary<int, HookloadPlanData> tqLimit { get; set; } = new Dictionary<int, HookloadPlanData>();
        public Dictionary<int, HookloadPlanData> sinRot { get; set; } = new Dictionary<int, HookloadPlanData>();
        #endregion

        #region Clone Method
        public AdnlHookloadPlan GetCopy()
        {
            try
            {
                var objNew = new AdnlHookloadPlan
                {
                    WellID = this.WellID,
                    WellboreID = this.WellboreID,
                    LogID = this.LogID,
                    PlanID = this.PlanID,
                    Name = this.Name,
                    PlanType = this.PlanType,
                    RunNo = this.RunNo,
                    Type = this.Type
                };

                if (this.pickup != null)
                {
                    foreach (var kvp in this.pickup)
                    {
                        objNew.pickup.Add(kvp.Key, kvp.Value != null ? kvp.Value.GetCopy() : new HookloadPlanData());
                    }
                }

                if (this.slackoff != null)
                {
                    foreach (var kvp in this.slackoff)
                    {
                        objNew.slackoff.Add(kvp.Key, kvp.Value != null ? kvp.Value.GetCopy() : new HookloadPlanData());
                    }
                }

                if (this.rotate != null)
                {
                    foreach (var kvp in this.rotate)
                    {
                        objNew.rotate.Add(kvp.Key, kvp.Value != null ? kvp.Value.GetCopy() : new HookloadPlanData());
                    }
                }

                if (this.torque != null)
                {
                    foreach (var kvp in this.torque)
                    {
                        objNew.torque.Add(kvp.Key, kvp.Value != null ? kvp.Value.GetCopy() : new HookloadPlanData());
                    }
                }

                if (this.onTorque != null)
                {
                    foreach (var kvp in this.onTorque)
                    {
                        objNew.onTorque.Add(kvp.Key, kvp.Value != null ? kvp.Value.GetCopy() : new HookloadPlanData());
                    }
                }

                if (this.mkTorque != null)
                {
                    foreach (var kvp in this.mkTorque)
                    {
                        objNew.mkTorque.Add(kvp.Key, kvp.Value != null ? kvp.Value.GetCopy() : new HookloadPlanData());
                    }
                }

                if (this.tqLimit != null)
                {
                    foreach (var kvp in this.tqLimit)
                    {
                        objNew.tqLimit.Add(kvp.Key, kvp.Value != null ? kvp.Value.GetCopy() : new HookloadPlanData());
                    }
                }

                if (this.sinRot != null)
                {
                    foreach (var kvp in this.sinRot)
                    {
                        objNew.sinRot.Add(kvp.Key, kvp.Value != null ? kvp.Value.GetCopy() : new HookloadPlanData());
                    }
                }

                return objNew;
            }
            catch
            {
                return new AdnlHookloadPlan();
            }
        }
        #endregion

        #region getList
        // --- [ORIGINAL VB CODE: getList] ---
        // Public Shared Function getList(ByRef objDataService As DataService, ByVal WellID As String, ByVal WellboreID As String, ByVal LogID As String, ByVal PlanType As String) As Dictionary(Of String, AdnlHookloadPlan)
        //     Try
        //         Dim list As New Dictionary(Of String, AdnlHookloadPlan)
        //         Dim objData As DataTable = objDataService.getTable("SELECT PLAN_ID FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + WellID + "' AND WELLBORE_ID='" + WellboreID + "' AND LOG_ID='" + LogID + "' AND PLAN_TYPE='" + PlanType + "'")
        //         For Each objRow As DataRow In objData.Rows
        //             Dim PlanID As String = DataService.checkNull(objRow("PLAN_ID"), "")
        //             Dim objPlan As AdnlHookloadPlan = AdnlHookloadPlan.loadObject(objDataService, WellID, WellboreID, LogID, PlanID)
        //             If Not objPlan Is Nothing Then
        //                 list.Add(objPlan.PlanID, objPlan)
        //             End If
        //         Next
        //         Return list
        //     Catch ex As Exception
        //         Return New Dictionary(Of String, AdnlHookloadPlan)
        //     End Try
        // End Function

        // --- [NEW C# / SQLITE LOGIC: getList] ---
        // Changes:
        // 1. Used IDataServiceDIntel for database service parameter (DrillIntel SQLite abstraction).
        // 2. Added single-quote escaping (.Replace("'", "''")) for SQLite query safety.
        // 3. Changed objDataService.getTable to objDataService.GetTable.
        // 4. Handled dictionary duplicate keys gracefully using indexer.
        public static Dictionary<string, AdnlHookloadPlan> getList(IDataServiceDIntel objDataService, string WellID, string WellboreID, string LogID, string PlanType)
        {
            try
            {
                var list = new Dictionary<string, AdnlHookloadPlan>();
                if (objDataService == null) return list;

                string query = "SELECT PLAN_ID FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='"
                    + (WellID ?? "").Replace("'", "''") + "' AND WELLBORE_ID='"
                    + (WellboreID ?? "").Replace("'", "''") + "' AND LOG_ID='"
                    + (LogID ?? "").Replace("'", "''") + "' AND PLAN_TYPE='"
                    + (PlanType ?? "").Replace("'", "''") + "'";

                DataTable objData = objDataService.GetTable(query);

                if (objData != null && objData.Rows != null)
                {
                    foreach (DataRow objRow in objData.Rows)
                    {
                        string planId = DataService.checkNull(objRow["PLAN_ID"], "");
                        AdnlHookloadPlan? objPlan = loadObject(objDataService, WellID ?? "", WellboreID ?? "", LogID ?? "", planId);
                        if (objPlan != null && !string.IsNullOrEmpty(objPlan.PlanID))
                        {
                            list[objPlan.PlanID] = objPlan;
                        }
                    }
                }

                return list;
            }
            catch
            {
                return new Dictionary<string, AdnlHookloadPlan>();
            }
        }

        #region getPlanHeaders
        /// <summary>
        /// Fast lightweight query that returns only plan header metadata (ID, Name, Well, Wellbore, Log)
        /// WITHOUT loading the 9 heavy curve datasets. Ideal for populating ComboBoxes and navigation trees in milliseconds.
        /// </summary>
        public static List<AdnlHookloadPlan> getPlanHeaders(IDataServiceDIntel objDataService, string? wellId = null, string? wellboreId = null, string? logId = null)
        {
            var list = new List<AdnlHookloadPlan>();
            if (objDataService == null) return list;

            try
            {
                CreateTables(objDataService);
                string query = "SELECT DISTINCT WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PLAN_NAME FROM VMX_ADNL_HKLD_PLAN WHERE 1=1";
                if (!string.IsNullOrEmpty(wellId))
                    query += " AND WELL_ID='" + wellId.Replace("'", "''") + "'";
                if (!string.IsNullOrEmpty(wellboreId))
                    query += " AND WELLBORE_ID='" + wellboreId.Replace("'", "''") + "'";
                if (!string.IsNullOrEmpty(logId))
                    query += " AND LOG_ID='" + logId.Replace("'", "''") + "'";
                query += " ORDER BY PLAN_NAME";

                DataTable objData = objDataService.GetTable(query);

                // Fallback 1: If filtering with wellboreId or logId returned 0 rows, try with wellId only
                if ((objData == null || objData.Rows == null || objData.Rows.Count == 0) && (!string.IsNullOrEmpty(wellboreId) || !string.IsNullOrEmpty(logId)) && !string.IsNullOrEmpty(wellId))
                {
                    string fallbackQuery = "SELECT DISTINCT WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PLAN_NAME FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + wellId.Replace("'", "''") + "' ORDER BY PLAN_NAME";
                    objData = objDataService.GetTable(fallbackQuery);
                }

                // Fallback 2: If still 0 rows, load all plans across the project
                if (objData == null || objData.Rows == null || objData.Rows.Count == 0)
                {
                    string allPlansQuery = "SELECT DISTINCT WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PLAN_NAME FROM VMX_ADNL_HKLD_PLAN ORDER BY PLAN_NAME";
                    objData = objDataService.GetTable(allPlansQuery);
                }

                if (objData != null && objData.Rows != null)
                {
                    foreach (DataRow row in objData.Rows)
                    {
                        string wId = DataService.checkNull(row["WELL_ID"], "");
                        string wbId = DataService.checkNull(row["WELLBORE_ID"], "");
                        string lId = DataService.checkNull(row["LOG_ID"], "");
                        string pId = DataService.checkNull(row["PLAN_ID"], "");
                        string pName = DataService.checkNull(row["PLAN_NAME"], pId);

                        var plan = new AdnlHookloadPlan
                        {
                            WellID = wId,
                            WellboreID = wbId,
                            LogID = lId,
                            PlanID = pId,
                            Name = !string.IsNullOrWhiteSpace(pName) ? pName : pId
                        };
                        list.Add(plan);
                    }
                }

                return list;
            }
            catch
            {
                return new List<AdnlHookloadPlan>();
            }
        }

        /// <summary>
        /// Returns distinct Main Plans (grouped by RUN_NO or main plan name prefix)
        /// for displaying in the Broomstick Document "Select Plan" dropdown.
        /// </summary>
        public static List<AdnlHookloadPlan> GetMainPlanHeaders(IDataServiceDIntel objDataService, string? wellId = null, string? wellboreId = null, string? logId = null)
        {
            var list = new List<AdnlHookloadPlan>();
            if (objDataService == null) return list;

            try
            {
                CreateTables(objDataService);
                string query = "SELECT DISTINCT WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PLAN_NAME, RUN_NO, PLAN_TYPE FROM VMX_ADNL_HKLD_PLAN WHERE 1=1";
                if (!string.IsNullOrEmpty(wellId))
                    query += " AND WELL_ID='" + wellId.Replace("'", "''") + "'";
                if (!string.IsNullOrEmpty(wellboreId))
                    query += " AND WELLBORE_ID='" + wellboreId.Replace("'", "''") + "'";
                if (!string.IsNullOrEmpty(logId))
                    query += " AND LOG_ID='" + logId.Replace("'", "''") + "'";
                query += " ORDER BY RUN_NO, PLAN_NAME";

                DataTable objData = objDataService.GetTable(query);

                // Fallback 1: If filtering with wellboreId or logId returned 0 rows, try with wellId only
                if ((objData == null || objData.Rows == null || objData.Rows.Count == 0) && (!string.IsNullOrEmpty(wellboreId) || !string.IsNullOrEmpty(logId)) && !string.IsNullOrEmpty(wellId))
                {
                    string fallbackQuery = "SELECT DISTINCT WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PLAN_NAME, RUN_NO, PLAN_TYPE FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + wellId.Replace("'", "''") + "' ORDER BY RUN_NO, PLAN_NAME";
                    objData = objDataService.GetTable(fallbackQuery);
                }

                // Fallback 2: If still 0 rows, load all plans across the project
                if (objData == null || objData.Rows == null || objData.Rows.Count == 0)
                {
                    string allPlansQuery = "SELECT DISTINCT WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PLAN_NAME, RUN_NO, PLAN_TYPE FROM VMX_ADNL_HKLD_PLAN ORDER BY RUN_NO, PLAN_NAME";
                    objData = objDataService.GetTable(allPlansQuery);
                }

                if (objData != null && objData.Rows != null)
                {
                    var groups = new Dictionary<string, AdnlHookloadPlan>(StringComparer.OrdinalIgnoreCase);

                    foreach (DataRow row in objData.Rows)
                    {
                        string wId = DataService.checkNull(row["WELL_ID"], "");
                        string wbId = DataService.checkNull(row["WELLBORE_ID"], "");
                        string lId = DataService.checkNull(row["LOG_ID"], "");
                        string pId = DataService.checkNull(row["PLAN_ID"], "");
                        string pName = DataService.checkNull(row["PLAN_NAME"], pId);
                        string runNo = DataService.checkNull(row["RUN_NO"], "");
                        string pType = DataService.checkNull(row["PLAN_TYPE"], "");
                        int typeVal = row.Table.Columns.Contains("TYPE") ? DataService.checkNull(row["TYPE"], 0) : 0;

                        string mainKey = ExtractMainPlanKey(pName, runNo);
                        if (string.IsNullOrWhiteSpace(mainKey))
                            mainKey = !string.IsNullOrWhiteSpace(pName) ? pName : pId;

                        if (!groups.ContainsKey(mainKey))
                        {
                            var mainPlan = new AdnlHookloadPlan
                            {
                                WellID = wId,
                                WellboreID = wbId,
                                LogID = lId,
                                PlanID = mainKey,
                                Name = mainKey,
                                RunNo = !string.IsNullOrWhiteSpace(runNo) ? runNo : mainKey,
                                PlanType = pType,
                                Type = typeVal
                            };
                            groups[mainKey] = mainPlan;
                        }
                    }

                    list.AddRange(groups.Values);
                }

                return list;
            }
            catch
            {
                return new List<AdnlHookloadPlan>();
            }
        }

        public static string ExtractMainPlanKey(string planName, string runNo)
        {
            var isFfOnly = new System.Text.RegularExpressions.Regex(@"^0?\.\d+\s*(?:OHFF|FF)?\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var regexFf = new System.Text.RegularExpressions.Regex(@"(?:^|\s+)0?\.\d+\s*(?:OHFF|FF)?\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // 1. If planName is provided and is NOT just a friction factor like "0.4 OHFF"
            if (!string.IsNullOrWhiteSpace(planName) && !isFfOnly.IsMatch(planName.Trim()))
            {
                string stripped = regexFf.Replace(planName.Trim(), "").Trim();
                if (!string.IsNullOrWhiteSpace(stripped))
                    return stripped;
                return planName.Trim();
            }

            // 2. If runNo is provided and is NOT just a friction factor like "0.4 OHFF"
            if (!string.IsNullOrWhiteSpace(runNo) && !isFfOnly.IsMatch(runNo.Trim()) && !int.TryParse(runNo.Trim(), out _))
            {
                string stripped = regexFf.Replace(runNo.Trim(), "").Trim();
                if (!string.IsNullOrWhiteSpace(stripped))
                    return stripped;
                return runNo.Trim();
            }

            // 3. If planName exists, strip FF
            if (!string.IsNullOrWhiteSpace(planName))
            {
                string stripped = regexFf.Replace(planName.Trim(), "").Trim();
                return !string.IsNullOrWhiteSpace(stripped) ? stripped : planName.Trim();
            }

            // 4. Fallback to runNo or default
            if (!string.IsNullOrWhiteSpace(runNo))
            {
                return runNo.Trim();
            }

            return "Default Plan";
        }

        public static double ExtractFrictionFactor(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0.0;
            var match = System.Text.RegularExpressions.Regex.Match(text, @"0\.\d+");
            if (match.Success && double.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                return val;
            }
            return 0.0;
        }

        public static double ExtractFrictionFactor(AdnlHookloadPlan? plan)
        {
            if (plan == null) return 0.0;
            double ff = ExtractFrictionFactor(plan.RunNo);
            if (ff > 0.0) return ff;
            return ExtractFrictionFactor(plan.Name);
        }
        #endregion

        public static List<AdnlHookloadPlan> getAllPlans(IDataServiceDIntel objDataService, string? wellId = null, string? wellboreId = null, string? logId = null)
        {
            var list = new List<AdnlHookloadPlan>();
            if (objDataService == null) return list;

            try
            {
                CreateTables(objDataService);
                string query = "SELECT DISTINCT WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PLAN_NAME FROM VMX_ADNL_HKLD_PLAN WHERE 1=1";
                if (!string.IsNullOrEmpty(wellId))
                    query += " AND WELL_ID='" + wellId.Replace("'", "''") + "'";
                if (!string.IsNullOrEmpty(wellboreId))
                    query += " AND WELLBORE_ID='" + wellboreId.Replace("'", "''") + "'";
                if (!string.IsNullOrEmpty(logId))
                    query += " AND LOG_ID='" + logId.Replace("'", "''") + "'";
                query += " ORDER BY PLAN_NAME";

                DataTable objData = objDataService.GetTable(query);

                // Fallback 1: If filtering with wellboreId or logId returned 0 rows, try with wellId only
                if ((objData == null || objData.Rows == null || objData.Rows.Count == 0) && (!string.IsNullOrEmpty(wellboreId) || !string.IsNullOrEmpty(logId)) && !string.IsNullOrEmpty(wellId))
                {
                    string fallbackQuery = "SELECT DISTINCT WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PLAN_NAME FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + wellId.Replace("'", "''") + "' ORDER BY PLAN_NAME";
                    objData = objDataService.GetTable(fallbackQuery);
                }

                // Fallback 2: If still 0 rows, load all plans across the project
                if (objData == null || objData.Rows == null || objData.Rows.Count == 0)
                {
                    string allPlansQuery = "SELECT DISTINCT WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PLAN_NAME FROM VMX_ADNL_HKLD_PLAN ORDER BY PLAN_NAME";
                    objData = objDataService.GetTable(allPlansQuery);
                }
                if (objData != null && objData.Rows != null)
                {
                    foreach (DataRow row in objData.Rows)
                    {
                        string wId = DataService.checkNull(row["WELL_ID"], "");
                        string wbId = DataService.checkNull(row["WELLBORE_ID"], "");
                        string lId = DataService.checkNull(row["LOG_ID"], "");
                        string pId = DataService.checkNull(row["PLAN_ID"], "");
                        var plan = loadObject(objDataService, wId, wbId, lId, pId);
                        if (plan != null)
                        {
                            if (string.IsNullOrWhiteSpace(plan.Name))
                            {
                                plan.Name = DataService.checkNull(row["PLAN_NAME"], pId);
                            }
                            list.Add(plan);
                        }
                    }
                }
            }
            catch
            {
            }

            return list;
        }
        #endregion

        #region loadObject
        // --- [ORIGINAL VB CODE: loadObject] ---
        // Public Shared Function loadObject(ByRef objDataService As DataService, ByVal WellID As String, ByVal WellboreID As String, ByVal LogID As String, ByVal PlanID As String) As AdnlHookloadPlan
        //     Try
        //         Dim objPlan As New AdnlHookloadPlan
        //         Dim objData As DataTable = objDataService.getTable("SELECT * FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + WellID + "' AND WELLBORE_ID='" + WellboreID + "' AND LOG_ID='" + LogID + "' AND PLAN_ID='" + PlanID + "'")
        //         If objData.Rows.Count > 0 Then
        //             ... loads header fields and iterates pickup, slackoff, rotate, torque, onTorque, mkTorque, tqLimit, sinRot ...
        // --- [NEW C# / SQLITE LOGIC: loadObject] ---
        // Changes:
        // 1. Used IDataServiceDIntel for database service parameter.
        // 2. Added single-quote escaping for SQLite query parameters.
        // 3. Replaced DataService.checkNumericNull with DataService.checkNull(..., 0).
        // 4. Safely check column existence before reading "TYPE" column.
        public static AdnlHookloadPlan? loadObject(IDataServiceDIntel objDataService, string WellID, string WellboreID, string LogID, string PlanID)
        {
            try
            {
                if (objDataService == null) return null;

                var objPlan = new AdnlHookloadPlan();

                string safeWellId = WellID ?? "";
                string safeWellboreId = WellboreID ?? "";
                string safeLogId = LogID ?? "";
                string safePlanId = PlanID ?? "";

                string planSql = "SELECT * FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='"
                    + safeWellId.Replace("'", "''") + "' AND WELLBORE_ID='"
                    + safeWellboreId.Replace("'", "''") + "' AND LOG_ID='"
                    + safeLogId.Replace("'", "''") + "' AND PLAN_ID='"
                    + safePlanId.Replace("'", "''") + "'";

                DataTable objData = objDataService.GetTable(planSql);

                if ((objData == null || objData.Rows.Count == 0) && !string.IsNullOrEmpty(safePlanId))
                {
                    planSql = "SELECT * FROM VMX_ADNL_HKLD_PLAN WHERE PLAN_ID='" + safePlanId.Replace("'", "''") + "' LIMIT 1";
                    objData = objDataService.GetTable(planSql);
                }

                if (objData != null && objData.Rows.Count > 0)
                {
                    DataRow headerRow = objData.Rows[0];
                    string actualWellId = DataService.checkNull(headerRow["WELL_ID"], safeWellId);
                    string actualWellboreId = DataService.checkNull(headerRow["WELLBORE_ID"], safeWellboreId);
                    string actualLogId = DataService.checkNull(headerRow["LOG_ID"], safeLogId);
                    string actualPlanId = DataService.checkNull(headerRow["PLAN_ID"], safePlanId);

                    objPlan.WellID = actualWellId;
                    objPlan.WellboreID = actualWellboreId;
                    objPlan.LogID = actualLogId;
                    objPlan.PlanID = actualPlanId;
                    objPlan.Name = DataService.checkNull(headerRow["PLAN_NAME"], "");
                    objPlan.PlanType = DataService.checkNull(headerRow["PLAN_TYPE"], "");
                    objPlan.RunNo = DataService.checkNull(headerRow["RUN_NO"], "");
                    if (headerRow.Table.Columns.Contains("TYPE"))
                    {
                        objPlan.Type = DataService.checkNull(headerRow["TYPE"], 0);
                    }

                    // Load all 8 curve datasets in a single batch query for maximum performance
                    LoadAllPlanData(objDataService, actualPlanId, objPlan);

                    return objPlan;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void LoadAllPlanData(
            IDataServiceDIntel objDataService,
            string PlanID,
            AdnlHookloadPlan objPlan)
        {
            string pId = (PlanID ?? "").Replace("'", "''");
            string sql = $"SELECT PLAN_TYPE, DEPTH, WEIGHT, MAX_COMPRESS, MAX_TENSION, MIN_COMPRESS, MIN_TENSION FROM VMX_ADNL_HKLD_PLAN_DATA WHERE PLAN_ID='{pId}' ORDER BY PLAN_TYPE, DEPTH";

            DataTable dt = objDataService.GetTable(sql);
            if (dt != null && dt.Rows != null)
            {
                foreach (DataRow row in dt.Rows)
                {
                    string pt = DataService.checkNull(row["PLAN_TYPE"], "").ToUpperInvariant();
                    var item = new HookloadPlanData
                    {
                        Depth = DataService.checkNull(row["DEPTH"], 0.0),
                        Weight = DataService.checkNull(row["WEIGHT"], 0.0),
                        MaxCompress = DataService.checkNull(row["MAX_COMPRESS"], 0.0),
                        MaxTension = DataService.checkNull(row["MAX_TENSION"], 0.0),
                        MinCompress = DataService.checkNull(row["MIN_COMPRESS"], 0.0),
                        MinTension = DataService.checkNull(row["MIN_TENSION"], 0.0)
                    };

                    switch (pt)
                    {
                        case HookloadPlan.cnPickup:
                            objPlan.pickup.Add(objPlan.pickup.Count + 1, item);
                            break;
                        case HookloadPlan.cnSlackOff:
                            objPlan.slackoff.Add(objPlan.slackoff.Count + 1, item);
                            break;
                        case HookloadPlan.cnRotate:
                            objPlan.rotate.Add(objPlan.rotate.Count + 1, item);
                            break;
                        case HookloadPlan.cnTorque:
                            objPlan.torque.Add(objPlan.torque.Count + 1, item);
                            break;
                        case HookloadPlan.cnOnTorque:
                            objPlan.onTorque.Add(objPlan.onTorque.Count + 1, item);
                            break;
                        case HookloadPlan.cnMkTorque:
                            objPlan.mkTorque.Add(objPlan.mkTorque.Count + 1, item);
                            break;
                        case HookloadPlan.cnTorqueLimit:
                            objPlan.tqLimit.Add(objPlan.tqLimit.Count + 1, item);
                            break;
                        case HookloadPlan.cnSinRot:
                            objPlan.sinRot.Add(objPlan.sinRot.Count + 1, item);
                            break;
                    }
                }
            }
        }
        #endregion

        #region Table Creation (SQLite DDL)
        /// <summary>
        /// Creates VMX_ADNL_HKLD_PLAN and VMX_ADNL_HKLD_PLAN_DATA tables and indexes in SQLite if they do not exist.
        /// Converted from SQL Server DDL to SQLite syntax.
        /// </summary>
        public static bool CreateTables(IDataServiceDIntel objDataService)
        {
            try
            {
                if (objDataService == null) return false;

                const string createPlanTableSql = @"
CREATE TABLE IF NOT EXISTS VMX_ADNL_HKLD_PLAN (
    WELL_ID         TEXT NOT NULL,
    WELLBORE_ID     TEXT NOT NULL,
    LOG_ID          TEXT NOT NULL,
    PLAN_ID         TEXT NOT NULL,
    PLAN_NAME       TEXT,
    CREATED_BY      TEXT,
    CREATED_DATE    TEXT,
    MODIFIED_BY     TEXT,
    MODIFIED_DATE   TEXT,
    PLAN_TYPE       TEXT,
    RUN_NO          TEXT,
    TYPE            INTEGER,
    PRIMARY KEY (LOG_ID, WELLBORE_ID, WELL_ID, PLAN_ID)
);

CREATE INDEX IF NOT EXISTS IX_VMX_ADNL_HKLD_PLAN_LOOKUP 
ON VMX_ADNL_HKLD_PLAN(WELL_ID, WELLBORE_ID, LOG_ID, PLAN_TYPE);
";

                const string createPlanDataTableSql = @"
CREATE TABLE IF NOT EXISTS VMX_ADNL_HKLD_PLAN_DATA (
    WELL_ID         TEXT NOT NULL,
    WELLBORE_ID     TEXT NOT NULL,
    LOG_ID          TEXT NOT NULL,
    PLAN_ID         TEXT NOT NULL,
    PLAN_TYPE       TEXT NOT NULL,
    SR_NO           INTEGER NOT NULL,
    DEPTH           REAL,
    WEIGHT          REAL,
    MAX_COMPRESS    REAL,
    MAX_TENSION     REAL,
    MIN_COMPRESS    REAL,
    MIN_TENSION     REAL,
    CREATED_BY      TEXT,
    CREATED_DATE    TEXT,
    MODIFIED_BY     TEXT,
    MODIFIED_DATE   TEXT,
    PRIMARY KEY (LOG_ID, SR_NO, WELLBORE_ID, WELL_ID, PLAN_TYPE, PLAN_ID)
);

CREATE INDEX IF NOT EXISTS IX_VMX_ADNL_HKLD_PLAN_DATA_LOOKUP 
ON VMX_ADNL_HKLD_PLAN_DATA(WELL_ID, WELLBORE_ID, LOG_ID, PLAN_ID, PLAN_TYPE, DEPTH);
";

                objDataService.ExecuteNonQuery(createPlanTableSql);
                objDataService.ExecuteNonQuery(createPlanDataTableSql);
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region savePlan & removePlan
        // --- [ORIGINAL VB CODE: removePlan] ---
        // Public Shared Function removePlan(ByRef objDataService As DataService, ByVal WellID As String, ByVal WellboreID As String, ByVal LogID As String, ByVal PlanID As String) As Boolean
        public static bool removePlan(IDataServiceDIntel objDataService, string WellID, string WellboreID, string LogID, string PlanID)
        {
            try
            {
                if (objDataService == null) return false;

                string wId = (WellID ?? "").Replace("'", "''");
                string wbId = (WellboreID ?? "").Replace("'", "''");
                string lId = (LogID ?? "").Replace("'", "''");
                string pId = (PlanID ?? "").Replace("'", "''");

                if (string.IsNullOrWhiteSpace(pId)) return false;

                if (!string.IsNullOrEmpty(wId) && !string.IsNullOrEmpty(wbId) && !string.IsNullOrEmpty(lId))
                {
                    objDataService.ExecuteNonQuery($"DELETE FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wId}' AND WELLBORE_ID='{wbId}' AND LOG_ID='{lId}' AND PLAN_ID='{pId}';");
                    objDataService.ExecuteNonQuery($"DELETE FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='{wId}' AND WELLBORE_ID='{wbId}' AND LOG_ID='{lId}' AND PLAN_ID='{pId}';");
                }

                // PLAN_ID is uniquely generated per plan; ensure all data points and header records are removed
                objDataService.ExecuteNonQuery($"DELETE FROM VMX_ADNL_HKLD_PLAN_DATA WHERE PLAN_ID='{pId}';");
                objDataService.ExecuteNonQuery($"DELETE FROM VMX_ADNL_HKLD_PLAN WHERE PLAN_ID='{pId}';");

                return true;
            }
            catch
            {
                return false;
            }
        }

        // --- [ORIGINAL VB CODE: savePlan] ---
        // Public Shared Function savePlan(ByRef objDataService As DataService, ByVal objPlanData As AdnlHookloadPlan, ByRef LastError As String) As Boolean
        public static bool savePlan(IDataServiceDIntel objDataService, AdnlHookloadPlan objPlanData, ref string LastError)
        {
            return savePlanEx(objDataService, objPlanData, ref LastError, false, 0, 0);
        }
        #endregion

        #region savePlanEx
        // --- [ORIGINAL VB CODE: savePlanEx] ---
        // Public Shared Function savePlanEx(ByRef objDataService As DataService, ByVal objPlanData As AdnlHookloadPlan, ByRef LastError As String, ByVal importByRange As Boolean, ByVal fromDepth As Double, ByVal toDepth As Double) As Boolean
        //     Try
        //         ''Check if any plan with selected run no. is present ... 
        //         If objPlanData.RunNo.Trim <> "" Then
        //             If objDataService.IsRecordExist("SELECT PLAN_ID FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND LTRIM(RTRIM(PLAN_NAME))='" + objPlanData.Name + "' AND RUN_NO='" + objPlanData.RunNo.Trim + "'") Then
        //                 ''Replace current plan id with the run no ...
        //                 objPlanData.PlanID = objDataService.getValueFromDatabase("SELECT PLAN_ID FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND LTRIM(RTRIM(PLAN_NAME))='" + objPlanData.Name + "' AND RUN_NO='" + objPlanData.RunNo.Trim + "'")
        //             End If
        //         End If
        //         Dim strSQL As String = ""
        //         ''Delete existing data
        //         If Not objDataService.IsRecordExist("SELECT PLAN_ID FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "'") Then
        //             strSQL = "INSERT INTO VMX_ADNL_HKLD_PLAN (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_NAME,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE,PLAN_TYPE,RUN_NO) VALUES("
        //             ...
        //         Else
        //             ...
        //         End If
        //         If importByRange Then
        //             objDataService.executeNonQuery("DELETE FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellBORE_ID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "' AND DEPTH>=" + fromDepth.ToString + " AND DEPTH<=" + toDepth.ToString + " ")
        //         Else
        //             objDataService.executeNonQuery("DELETE FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "'")
        //         End If
        //         ... 8 loops for pickup, slackoff, rotate, torque, onTorque, mkTorque, tqLimit, sinRot ...
        //         Return True
        //     Catch ex As Exception
        //         LastError = ex.Message + ex.StackTrace
        //         Return False
        //     End Try
        // End Function

        // --- [NEW C# / SQLITE LOGIC: savePlanEx] ---
        // Key changes applied for SQLite and C# stability:
        // 1. Used IDataServiceDIntel for data service and ref string LastError.
        // 2. Used TRIM(PLAN_NAME) for idiomatic SQLite string trimming instead of LTRIM(RTRIM(...)).
        // 3. String escaping: Escaped all string values (.Replace("'", "''")) to prevent SQLite syntax errors.
        // 4. Invariant Culture: Formatted numbers (DEPTH, WEIGHT, MAX_COMPRESS, etc.) and dates using CultureInfo.InvariantCulture
        //    to avoid decimal comma issues (e.g. "123,45") in non-English locales which breaks SQLite syntax.
        // 5. Safe MAX(SR_NO): Handled nullability using COALESCE(MAX(SR_NO), 0) in SQLite and DataService.checkNull(..., 0).
        // 6. Typo Fix: Corrected 'Dim doContinue conscience As Boolean = True' from original VB snippet to 'bool doContinue = true;'.
        // 7. Case Correction: Mapped original VB property 'WellBORE_ID' to C# 'WellboreID'.
        // 8. Performance: Wrapped multiple INSERT statements in a SQLite transaction (BeginTransaction / Commit)
        //    to prevent severe disk fsync latency on SQLite bulk operations.
        public static bool savePlanEx(
            IDataServiceDIntel objDataService,
            AdnlHookloadPlan objPlanData,
            ref string LastError,
            bool importByRange,
            double fromDepth,
            double toDepth)
        {
            if (objDataService == null)
            {
                LastError = "DataService is not initialized.";
                return false;
            }

            if (objPlanData == null)
            {
                LastError = "Plan data object is null.";
                return false;
            }

            bool startedTransaction = false;

            try
            {
                string wellIdEsc = (objPlanData.WellID ?? "").Replace("'", "''");
                string wellboreIdEsc = (objPlanData.WellboreID ?? "").Replace("'", "''");
                string logIdEsc = (objPlanData.LogID ?? "").Replace("'", "''");
                string planNameEsc = (objPlanData.Name ?? "").Trim().Replace("'", "''");
                string runNoTrimEsc = (objPlanData.RunNo ?? "").Trim().Replace("'", "''");
                string planTypeEsc = (objPlanData.PlanType ?? "").Replace("'", "''");
                string userNameEsc = (objPlanData.Name != null && !string.IsNullOrEmpty(objDataService.UserName)
                    ? objDataService.UserName
                    : Environment.UserName).Replace("'", "''");
                string nowStr = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);

                // --- [ORIGINAL VB LOGIC: Check if any plan with selected run no. is present] ---
                // If objPlanData.RunNo.Trim <> "" Then
                //     If objDataService.IsRecordExist("SELECT PLAN_ID FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND LTRIM(RTRIM(PLAN_NAME))='" + objPlanData.Name + "' AND RUN_NO='" + objPlanData.RunNo.Trim + "'") Then
                //         objPlanData.PlanID = objDataService.getValueFromDatabase("SELECT PLAN_ID FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND LTRIM(RTRIM(PLAN_NAME))='" + objPlanData.Name + "' AND RUN_NO='" + objPlanData.RunNo.Trim + "'")
                //     End If
                // End If
                if (!string.IsNullOrWhiteSpace(objPlanData.RunNo))
                {
                    string checkRunNoSql = $"SELECT PLAN_ID FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND TRIM(PLAN_NAME)='{planNameEsc}' AND RUN_NO='{runNoTrimEsc}'";
                    if (objDataService.IsRecordExist(checkRunNoSql))
                    {
                        string existingPlanId = Convert.ToString(objDataService.GetValueFromDatabase(checkRunNoSql)) ?? "";
                        if (!string.IsNullOrEmpty(existingPlanId))
                        {
                            objPlanData.PlanID = existingPlanId;
                        }
                    }
                }

                string planIdEsc = (objPlanData.PlanID ?? "").Replace("'", "''");

                // Start SQLite transaction to ensure atomicity and high performance across bulk operations
                if (!objDataService.IsInTransaction)
                {
                    objDataService.BeginTransaction();
                    startedTransaction = true;
                }

                // Ensure SQLite tables exist before executing plan operations
                CreateTables(objDataService);

                // --- [ORIGINAL VB LOGIC: Insert or Update VMX_ADNL_HKLD_PLAN header] ---
                // If Not objDataService.IsRecordExist("SELECT PLAN_ID FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "'") Then
                //     INSERT INTO VMX_ADNL_HKLD_PLAN (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_NAME,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE,PLAN_TYPE,RUN_NO,TYPE) VALUES(...)
                // Else
                //     UPDATE VMX_ADNL_HKLD_PLAN SET PLAN_TYPE=..., PLAN_NAME=..., TYPE=..., MODIFIED_BY=..., MODIFIED_DATE=... WHERE ...
                // End If
                string checkHeaderSql = $"SELECT PLAN_ID FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}'";
                if (!objDataService.IsRecordExist(checkHeaderSql))
                {
                    string insertHeaderSql = "INSERT INTO VMX_ADNL_HKLD_PLAN (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_NAME,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE,PLAN_TYPE,RUN_NO,TYPE) VALUES("
                        + "'" + wellIdEsc + "',"
                        + "'" + wellboreIdEsc + "',"
                        + "'" + logIdEsc + "',"
                        + "'" + planIdEsc + "',"
                        + "'" + planNameEsc + "',"
                        + "'" + userNameEsc + "',"
                        + "'" + nowStr + "',"
                        + "'" + userNameEsc + "',"
                        + "'" + nowStr + "',"
                        + "'" + planTypeEsc + "',"
                        + "'" + runNoTrimEsc + "',"
                        + objPlanData.Type.ToString() + ")";

                    objDataService.ExecuteNonQuery(insertHeaderSql);
                }
                else
                {
                    string updateHeaderSql = "UPDATE VMX_ADNL_HKLD_PLAN SET "
                        + "PLAN_TYPE='" + planTypeEsc + "', "
                        + "PLAN_NAME='" + planNameEsc + "', "
                        + "TYPE=" + objPlanData.Type.ToString() + ", "
                        + "MODIFIED_BY='" + userNameEsc + "', "
                        + "MODIFIED_DATE='" + nowStr + "' "
                        + "WHERE WELL_ID='" + wellIdEsc + "' "
                        + "AND WELLBORE_ID='" + wellboreIdEsc + "' "
                        + "AND LOG_ID='" + logIdEsc + "' "
                        + "AND PLAN_ID='" + planIdEsc + "'";

                    objDataService.ExecuteNonQuery(updateHeaderSql);
                }

                // --- [ORIGINAL VB LOGIC: Delete existing data] ---
                // If importByRange Then
                //     objDataService.executeNonQuery("DELETE FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellBORE_ID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "' AND DEPTH>=" + fromDepth.ToString + " AND DEPTH<=" + toDepth.ToString + " ")
                // Else
                //     objDataService.executeNonQuery("DELETE FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "'")
                // End If
                if (importByRange)
                {
                    string fromDepthStr = fromDepth.ToString(CultureInfo.InvariantCulture);
                    string toDepthStr = toDepth.ToString(CultureInfo.InvariantCulture);
                    string deleteRangeSql = $"DELETE FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}' AND DEPTH>={fromDepthStr} AND DEPTH<={toDepthStr};";
                    objDataService.ExecuteNonQuery(deleteRangeSql);
                }
                else
                {
                    string deleteAllSql = $"DELETE FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}';";
                    objDataService.ExecuteNonQuery(deleteAllSql);
                }

                #region 1. Pickup Data
                // --- [ORIGINAL VB LOGIC: Pickup curve inserts] ---
                // Index = 0
                // If importByRange Then
                //     Index = Val(objDataService.getValueFromDatabase("SELECT MAX(SR_NO) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "' AND PLAN_TYPE='" + AdnlHookloadPlan.cnPickup + "'"))
                //     Index += 1
                // End If
                // For Each objItem As HookloadPlanData In objPlanData.pickup.Values ...
                int pickupIndex = 0;
                if (importByRange)
                {
                    string maxPickupSql = $"SELECT COALESCE(MAX(SR_NO), 0) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}' AND PLAN_TYPE='{AdnlHookloadPlan.cnPickup}'";
                    pickupIndex = DataService.checkNull(objDataService.GetValueFromDatabase(maxPickupSql), 0) + 1;
                }

                if (objPlanData.pickup != null)
                {
                    foreach (HookloadPlanData objItem in objPlanData.pickup.Values)
                    {
                        if (objItem == null) continue;

                        bool doContinue = true;
                        if (importByRange)
                        {
                            doContinue = (objItem.Depth >= fromDepth && objItem.Depth <= toDepth);
                        }

                        if (doContinue)
                        {
                            string strSQL = "INSERT INTO VMX_ADNL_HKLD_PLAN_DATA (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_TYPE,SR_NO,DEPTH,WEIGHT,MAX_COMPRESS,MAX_TENSION,MIN_COMPRESS,MIN_TENSION,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES("
                                + "'" + wellIdEsc + "',"
                                + "'" + wellboreIdEsc + "',"
                                + "'" + logIdEsc + "',"
                                + "'" + planIdEsc + "',"
                                + "'" + HookloadPlan.cnPickup + "',"
                                + pickupIndex.ToString() + ","
                                + objItem.Depth.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.Weight.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxTension.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinTension.ToString(CultureInfo.InvariantCulture) + ","
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "',"
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "')";

                            objDataService.ExecuteNonQuery(strSQL);
                            pickupIndex++;
                        }
                    }
                }
                #endregion

                #region 2. SlackOff Data
                // --- [ORIGINAL VB LOGIC: SlackOff curve inserts] ---
                // Index = 0
                // If importByRange Then
                //     Index = Val(objDataService.getValueFromDatabase("SELECT MAX(SR_NO) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "' AND PLAN_TYPE='" + AdnlHookloadPlan.cnSlackOff + "'"))
                //     Index += 1
                // End If
                // For Each objItem As HookloadPlanData In objPlanData.slackoff.Values ...
                int slackoffIndex = 0;
                if (importByRange)
                {
                    string maxSlackoffSql = $"SELECT COALESCE(MAX(SR_NO), 0) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}' AND PLAN_TYPE='{AdnlHookloadPlan.cnSlackOff}'";
                    slackoffIndex = DataService.checkNull(objDataService.GetValueFromDatabase(maxSlackoffSql), 0) + 1;
                }

                if (objPlanData.slackoff != null)
                {
                    foreach (HookloadPlanData objItem in objPlanData.slackoff.Values)
                    {
                        if (objItem == null) continue;

                        bool doContinue = true;
                        if (importByRange)
                        {
                            doContinue = (objItem.Depth >= fromDepth && objItem.Depth <= toDepth);
                        }

                        if (doContinue)
                        {
                            string strSQL = "INSERT INTO VMX_ADNL_HKLD_PLAN_DATA (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_TYPE,SR_NO,DEPTH,WEIGHT,MAX_COMPRESS,MAX_TENSION,MIN_COMPRESS,MIN_TENSION,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES("
                                + "'" + wellIdEsc + "',"
                                + "'" + wellboreIdEsc + "',"
                                + "'" + logIdEsc + "',"
                                + "'" + planIdEsc + "',"
                                + "'" + HookloadPlan.cnSlackOff + "',"
                                + slackoffIndex.ToString() + ","
                                + objItem.Depth.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.Weight.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxTension.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinTension.ToString(CultureInfo.InvariantCulture) + ","
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "',"
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "')";

                            objDataService.ExecuteNonQuery(strSQL);
                            slackoffIndex++;
                        }
                    }
                }
                #endregion

                #region 3. Rotate Data
                // --- [ORIGINAL VB LOGIC: Rotate curve inserts] ---
                // Index = 0
                // If importByRange Then
                //     Index = Val(objDataService.getValueFromDatabase("SELECT MAX(SR_NO) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "' AND PLAN_TYPE='" + AdnlHookloadPlan.cnRotate + "'"))
                //     Index += 1
                // End If
                // For Each objItem As HookloadPlanData In objPlanData.rotate.Values ...
                int rotateIndex = 0;
                if (importByRange)
                {
                    string maxRotateSql = $"SELECT COALESCE(MAX(SR_NO), 0) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}' AND PLAN_TYPE='{AdnlHookloadPlan.cnRotate}'";
                    rotateIndex = DataService.checkNull(objDataService.GetValueFromDatabase(maxRotateSql), 0) + 1;
                }

                if (objPlanData.rotate != null)
                {
                    foreach (HookloadPlanData objItem in objPlanData.rotate.Values)
                    {
                        if (objItem == null) continue;

                        bool doContinue = true;
                        if (importByRange)
                        {
                            doContinue = (objItem.Depth >= fromDepth && objItem.Depth <= toDepth);
                        }

                        if (doContinue)
                        {
                            string strSQL = "INSERT INTO VMX_ADNL_HKLD_PLAN_DATA (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_TYPE,SR_NO,DEPTH,WEIGHT,MAX_COMPRESS,MAX_TENSION,MIN_COMPRESS,MIN_TENSION,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES("
                                + "'" + wellIdEsc + "',"
                                + "'" + wellboreIdEsc + "',"
                                + "'" + logIdEsc + "',"
                                + "'" + planIdEsc + "',"
                                + "'" + HookloadPlan.cnRotate + "',"
                                + rotateIndex.ToString() + ","
                                + objItem.Depth.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.Weight.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxTension.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinTension.ToString(CultureInfo.InvariantCulture) + ","
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "',"
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "')";

                            objDataService.ExecuteNonQuery(strSQL);
                            rotateIndex++;
                        }
                    }
                }
                #endregion

                #region 4. Torque Data
                // --- [ORIGINAL VB LOGIC: Torque curve inserts] ---
                // Index = 0
                // If importByRange Then
                //     Index = Val(objDataService.getValueFromDatabase("SELECT MAX(SR_NO) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "' AND PLAN_TYPE='" + AdnlHookloadPlan.cnTorque + "'"))
                //     Index += 1
                // End If
                // For Each objItem As HookloadPlanData In objPlanData.torque.Values ...
                int torqueIndex = 0;
                if (importByRange)
                {
                    string maxTorqueSql = $"SELECT COALESCE(MAX(SR_NO), 0) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}' AND PLAN_TYPE='{AdnlHookloadPlan.cnTorque}'";
                    torqueIndex = DataService.checkNull(objDataService.GetValueFromDatabase(maxTorqueSql), 0) + 1;
                }

                if (objPlanData.torque != null)
                {
                    foreach (HookloadPlanData objItem in objPlanData.torque.Values)
                    {
                        if (objItem == null) continue;

                        bool doContinue = true;
                        if (importByRange)
                        {
                            doContinue = (objItem.Depth >= fromDepth && objItem.Depth <= toDepth);
                        }

                        if (doContinue)
                        {
                            string strSQL = "INSERT INTO VMX_ADNL_HKLD_PLAN_DATA (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_TYPE,SR_NO,DEPTH,WEIGHT,MAX_COMPRESS,MAX_TENSION,MIN_COMPRESS,MIN_TENSION,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES("
                                + "'" + wellIdEsc + "',"
                                + "'" + wellboreIdEsc + "',"
                                + "'" + logIdEsc + "',"
                                + "'" + planIdEsc + "',"
                                + "'" + HookloadPlan.cnTorque + "',"
                                + torqueIndex.ToString() + ","
                                + objItem.Depth.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.Weight.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxTension.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinTension.ToString(CultureInfo.InvariantCulture) + ","
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "',"
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "')";

                            objDataService.ExecuteNonQuery(strSQL);
                            torqueIndex++;
                        }
                    }
                }
                #endregion

                #region 5. OnTorque Data
                // --- [ORIGINAL VB LOGIC: OnTorque curve inserts] ---
                // Index = 0
                // If importByRange Then
                //     Index = Val(objDataService.getValueFromDatabase("SELECT MAX(SR_NO) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "' AND PLAN_TYPE='" + AdnlHookloadPlan.cnOnTorque + "'"))
                //     Index += 1
                // End If
                // For Each objItem As HookloadPlanData In objPlanData.onTorque.Values ...
                int onTorqueIndex = 0;
                if (importByRange)
                {
                    string maxOnTorqueSql = $"SELECT COALESCE(MAX(SR_NO), 0) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}' AND PLAN_TYPE='{AdnlHookloadPlan.cnOnTorque}'";
                    onTorqueIndex = DataService.checkNull(objDataService.GetValueFromDatabase(maxOnTorqueSql), 0) + 1;
                }

                if (objPlanData.onTorque != null)
                {
                    foreach (HookloadPlanData objItem in objPlanData.onTorque.Values)
                    {
                        if (objItem == null) continue;

                        bool doContinue = true;
                        if (importByRange)
                        {
                            doContinue = (objItem.Depth >= fromDepth && objItem.Depth <= toDepth);
                        }

                        if (doContinue)
                        {
                            string strSQL = "INSERT INTO VMX_ADNL_HKLD_PLAN_DATA (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_TYPE,SR_NO,DEPTH,WEIGHT,MAX_COMPRESS,MAX_TENSION,MIN_COMPRESS,MIN_TENSION,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES("
                                + "'" + wellIdEsc + "',"
                                + "'" + wellboreIdEsc + "',"
                                + "'" + logIdEsc + "',"
                                + "'" + planIdEsc + "',"
                                + "'" + HookloadPlan.cnOnTorque + "',"
                                + onTorqueIndex.ToString() + ","
                                + objItem.Depth.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.Weight.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxTension.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinTension.ToString(CultureInfo.InvariantCulture) + ","
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "',"
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "')";

                            objDataService.ExecuteNonQuery(strSQL);
                            onTorqueIndex++;
                        }
                    }
                }
                #endregion

                #region 6. MkTorque Data
                // --- [ORIGINAL VB LOGIC: MkTorque curve inserts] ---
                // Index = 0
                // If importByRange Then
                //     Index = Val(objDataService.getValueFromDatabase("SELECT MAX(SR_NO) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "' AND PLAN_TYPE='" + AdnlHookloadPlan.cnMkTorque + "'"))
                //     Index += 1
                // End If
                // For Each objItem As HookloadPlanData In objPlanData.mkTorque.Values ...
                int mkTorqueIndex = 0;
                if (importByRange)
                {
                    string maxMkTorqueSql = $"SELECT COALESCE(MAX(SR_NO), 0) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}' AND PLAN_TYPE='{AdnlHookloadPlan.cnMkTorque}'";
                    mkTorqueIndex = DataService.checkNull(objDataService.GetValueFromDatabase(maxMkTorqueSql), 0) + 1;
                }

                if (objPlanData.mkTorque != null)
                {
                    foreach (HookloadPlanData objItem in objPlanData.mkTorque.Values)
                    {
                        if (objItem == null) continue;

                        bool doContinue = true;
                        if (importByRange)
                        {
                            doContinue = (objItem.Depth >= fromDepth && objItem.Depth <= toDepth);
                        }

                        if (doContinue)
                        {
                            string strSQL = "INSERT INTO VMX_ADNL_HKLD_PLAN_DATA (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_TYPE,SR_NO,DEPTH,WEIGHT,MAX_COMPRESS,MAX_TENSION,MIN_COMPRESS,MIN_TENSION,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES("
                                + "'" + wellIdEsc + "',"
                                + "'" + wellboreIdEsc + "',"
                                + "'" + logIdEsc + "',"
                                + "'" + planIdEsc + "',"
                                + "'" + HookloadPlan.cnMkTorque + "',"
                                + mkTorqueIndex.ToString() + ","
                                + objItem.Depth.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.Weight.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxTension.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinTension.ToString(CultureInfo.InvariantCulture) + ","
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "',"
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "')";

                            objDataService.ExecuteNonQuery(strSQL);
                            mkTorqueIndex++;
                        }
                    }
                }
                #endregion

                #region 7. TorqueLimit Data
                // --- [ORIGINAL VB LOGIC: TorqueLimit curve inserts] ---
                // Index = 0
                // If importByRange Then
                //     Index = Val(objDataService.getValueFromDatabase("SELECT MAX(SR_NO) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "' AND PLAN_TYPE='" + AdnlHookloadPlan.cnTorqueLimit + "'"))
                //     Index += 1
                // End If
                // For Each objItem As HookloadPlanData In objPlanData.tqLimit.Values ...
                int tqLimitIndex = 0;
                if (importByRange)
                {
                    string maxTqLimitSql = $"SELECT COALESCE(MAX(SR_NO), 0) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}' AND PLAN_TYPE='{AdnlHookloadPlan.cnTorqueLimit}'";
                    tqLimitIndex = DataService.checkNull(objDataService.GetValueFromDatabase(maxTqLimitSql), 0) + 1;
                }

                if (objPlanData.tqLimit != null)
                {
                    foreach (HookloadPlanData objItem in objPlanData.tqLimit.Values)
                    {
                        if (objItem == null) continue;

                        bool doContinue = true;
                        if (importByRange)
                        {
                            doContinue = (objItem.Depth >= fromDepth && objItem.Depth <= toDepth);
                        }

                        if (doContinue)
                        {
                            string strSQL = "INSERT INTO VMX_ADNL_HKLD_PLAN_DATA (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_TYPE,SR_NO,DEPTH,WEIGHT,MAX_COMPRESS,MAX_TENSION,MIN_COMPRESS,MIN_TENSION,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES("
                                + "'" + wellIdEsc + "',"
                                + "'" + wellboreIdEsc + "',"
                                + "'" + logIdEsc + "',"
                                + "'" + planIdEsc + "',"
                                + "'" + HookloadPlan.cnTorqueLimit + "',"
                                + tqLimitIndex.ToString() + ","
                                + objItem.Depth.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.Weight.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxTension.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinTension.ToString(CultureInfo.InvariantCulture) + ","
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "',"
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "')";

                            objDataService.ExecuteNonQuery(strSQL);
                            tqLimitIndex++;
                        }
                    }
                }
                #endregion

                #region 8. SinRot Data
                // --- [ORIGINAL VB LOGIC: SinRot curve inserts] ---
                // Index = 0
                // If importByRange Then
                //     Index = Val(objDataService.getValueFromDatabase("SELECT MAX(SR_NO) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='" + objPlanData.WellID + "' AND WELLBORE_ID='" + objPlanData.WellboreID + "' AND LOG_ID='" + objPlanData.LogID + "' AND PLAN_ID='" + objPlanData.PlanID + "' AND PLAN_TYPE='" + AdnlHookloadPlan.cnSinRot + "'"))
                //     Index += 1
                // End If
                // For Each objItem As HookloadPlanData In objPlanData.sinRot.Values
                //     Dim doContinue conscience As Boolean = True   ' <--- Note: Typo 'conscience' in original VB snippet corrected to 'bool doContinue = true;'
                int sinRotIndex = 0;
                if (importByRange)
                {
                    string maxSinRotSql = $"SELECT COALESCE(MAX(SR_NO), 0) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='{wellIdEsc}' AND WELLBORE_ID='{wellboreIdEsc}' AND LOG_ID='{logIdEsc}' AND PLAN_ID='{planIdEsc}' AND PLAN_TYPE='{AdnlHookloadPlan.cnSinRot}'";
                    sinRotIndex = DataService.checkNull(objDataService.GetValueFromDatabase(maxSinRotSql), 0) + 1;
                }

                if (objPlanData.sinRot != null)
                {
                    foreach (HookloadPlanData objItem in objPlanData.sinRot.Values)
                    {
                        if (objItem == null) continue;

                        bool doContinue = true;
                        if (importByRange)
                        {
                            doContinue = (objItem.Depth >= fromDepth && objItem.Depth <= toDepth);
                        }

                        if (doContinue)
                        {
                            string strSQL = "INSERT INTO VMX_ADNL_HKLD_PLAN_DATA (WELL_ID,WELLBORE_ID,LOG_ID,PLAN_ID,PLAN_TYPE,SR_NO,DEPTH,WEIGHT,MAX_COMPRESS,MAX_TENSION,MIN_COMPRESS,MIN_TENSION,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES("
                                + "'" + wellIdEsc + "',"
                                + "'" + wellboreIdEsc + "',"
                                + "'" + logIdEsc + "',"
                                + "'" + planIdEsc + "',"
                                + "'" + HookloadPlan.cnSinRot + "',"
                                + sinRotIndex.ToString() + ","
                                + objItem.Depth.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.Weight.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MaxTension.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinCompress.ToString(CultureInfo.InvariantCulture) + ","
                                + objItem.MinTension.ToString(CultureInfo.InvariantCulture) + ","
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "',"
                                + "'" + userNameEsc + "',"
                                + "'" + nowStr + "')";

                            objDataService.ExecuteNonQuery(strSQL);
                            sinRotIndex++;
                        }
                    }
                }
                #endregion

                // Commit SQLite transaction if started locally
                if (startedTransaction)
                {
                    objDataService.Commit();
                    startedTransaction = false;
                }

                return true;
            }
            catch (Exception ex)
            {
                if (startedTransaction)
                {
                    try
                    {
                        objDataService.RollBack();
                    }
                    catch
                    {
                        // Ignore rollback exceptions
                    }
                }

                LastError = ex.Message + (ex.StackTrace ?? "");
                return false;
            }
        }
        #endregion
    }
}
