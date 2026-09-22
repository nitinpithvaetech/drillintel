using DrillIntel.Data.Objects.DataObjects.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrillIntel.Data.Objects.DataObjects.Services
{
    public class TimeLogIndexService
    {
        public static Dictionary<string, TimeLogIndex> getList(IDataServiceDIntel objDataService)
        {
            try
            {
                var list = new Dictionary<string, TimeLogIndex>();

                DataTable objData = objDataService.GetTable("SELECT * FROM VMX_TIMELOG_INDEXES ORDER BY PREFIX");

                foreach (DataRow objRow in objData.Rows)
                {
                    var objIndex = new TimeLogIndex
                    {
                        ID = DataService.checkNull(objRow["ID"], ""),
                        Prefix = DataService.checkNull(objRow["PREFIX"], ""),
                        Channels = DataService.checkNull(objRow["CHANNELS"], ""),
                        Active = Convert.ToInt32(DataService.checkNull(objRow["ACTIVE"], 0)) == 1
                    };

                    list[objIndex.ID] = objIndex;
                }

                return list;
            }
            catch (Exception)
            {
                return new Dictionary<string, TimeLogIndex>();
            }
        }

        public static Dictionary<string, TimeLogIndex> getActiveList(IDataServiceDIntel objDataService)
        {
            try
            {
                var list = new Dictionary<string, TimeLogIndex>();

                DataTable objData = objDataService.GetTable("SELECT * FROM VMX_TIMELOG_INDEXES WHERE ACTIVE=1 ORDER BY PREFIX");

                foreach (DataRow objRow in objData.Rows)
                {
                    var objIndex = new TimeLogIndex
                    {
                        ID = DataService.checkNull(objRow["ID"], ""),
                        Prefix = DataService.checkNull(objRow["PREFIX"], ""),
                        Channels = DataService.checkNull(objRow["CHANNELS"], ""),
                        Active = Convert.ToInt32(DataService.checkNull(objRow["ACTIVE"], 0)) == 1
                    };

                    list[objIndex.ID] = objIndex;
                }

                return list;
            }
            catch (Exception)
            {
                return new Dictionary<string, TimeLogIndex>();
            }
        }

        public static bool addIndex(IDataServiceDIntel objDataService, TimeLogIndex objIndex)
        {
            try
            {
                string strSQL = "INSERT INTO VMX_TIMELOG_INDEXES " +
                    "(ID,PREFIX,CHANNELS,ACTIVE,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES " +
                    "(@ID,@Prefix,@Channels,@Active,@CreatedBy,@CreatedDate,@ModifiedBy,@ModifiedDate)";

                string now = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                var parameters = new Dictionary<string, object>
            {
                { "@ID", objIndex.ID },
                { "@Prefix", objIndex.Prefix },
                { "@Channels", objIndex.Channels },
                { "@Active", objIndex.Active ? 1 : 0 },
                { "@CreatedBy", objDataService.UserName },
                { "@CreatedDate", now },
                { "@ModifiedBy", objDataService.UserName },
                { "@ModifiedDate", now }
            };

                return objDataService.ExecuteNonQuery(strSQL, parameters);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool updateIndex(IDataServiceDIntel objDataService, TimeLogIndex objIndex)
        {
            try
            {
                string strSQL = "UPDATE VMX_TIMELOG_INDEXES SET " +
                    "PREFIX=@Prefix, CHANNELS=@Channels, ACTIVE=@Active, " +
                    "MODIFIED_BY=@ModifiedBy, MODIFIED_DATE=@ModifiedDate " +
                    "WHERE ID=@ID";

                var parameters = new Dictionary<string, object>
            {
                { "@Prefix", objIndex.Prefix },
                { "@Channels", objIndex.Channels },
                { "@Active", objIndex.Active ? 1 : 0 },
                { "@ModifiedBy", objDataService.UserName },
                { "@ModifiedDate", DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") },
                { "@ID", objIndex.ID }
            };

                return objDataService.ExecuteNonQuery(strSQL, parameters);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool removeIndex(IDataServiceDIntel objDataService, string paramID)
        {
            try
            {
                string strSQL = "DELETE FROM VMX_TIMELOG_INDEXES WHERE ID=@ID";

                var parameters = new Dictionary<string, object>
            {
                { "@ID", paramID }
            };

                objDataService.ExecuteNonQuery(strSQL, parameters);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static TimeLogIndex loadIndex(IDataServiceDIntel objDataService, string paramID)
        {
            try
            {
                string strSQL = "SELECT * FROM VMX_TIMELOG_INDEXES WHERE ID=@ID";

                var parameters = new Dictionary<string, object>
            {
                { "@ID", paramID }
            };

                DataTable objData = objDataService.GetTable(strSQL, parameters);

                if (objData.Rows.Count > 0)
                {
                    DataRow objRow = objData.Rows[0];

                    return new TimeLogIndex
                    {
                        ID = paramID,
                        Prefix = DataService.checkNull(objRow["PREFIX"], ""),
                        Channels = DataService.checkNull(objRow["CHANNELS"], ""),
                        Active = Convert.ToInt32(DataService.checkNull(objRow["ACTIVE"], 0)) == 1
                    };
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

    }//class
}//namespace
