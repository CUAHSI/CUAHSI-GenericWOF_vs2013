using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using tableSpace = WaterOneFlow.odm.v1_1.DataTypeDataSetTableAdapters;

/// <summary>
/// Summary description for UsgsDataType
/// </summary>
namespace USGSTranducer
{

    using System.Data;
    using WaterOneFlow.odm.v1_1;
    using WaterOneFlow.odws.v1_1;
    using WaterOneFlowImpl;
    using USGSDataTypeTableAdapter = tableSpace.USGSDataTypeTableAdapter;

    public class UsgsDataType
    {

        public static string GetStatCd(string statName)
        {
            string query = "stat_NM = '" + statName + "'";
            DataRow[] rows = null;
            string statCd = null;

            DataTypeDataSet ds = new DataTypeDataSet();
            USGSDataTypeTableAdapter dtTableAdapter = new USGSDataTypeTableAdapter();

            //Yaping Notes
            //Without commenting out this line, there is compile error. not sure why?
            //dtTableAdapter.Connection.ConnectionString = WaterOneFlow.odws.v1_1.Config.ODDB();
            //dtTableAdapter.Connection.ConnectionString = Config.ODDB();

            try
            {
                dtTableAdapter.Fill(ds.USGSDataType);
            }
            catch (Exception e)
            {
                throw new WaterOneFlowServerException("Database error", e);
            }

            try
            {
                rows = ds.USGSDataType.Select(query);
            }
            catch (Exception e)
            {
                throw new WaterOneFlowServerException("Database error", e);
            }

            if (rows.Length != 1)
            {
                throw new Exception("Error: more than one or no rows  found for "+statName);
            } else {
                statCd = rows[0]["stat_CD"].ToString();
            } 

            return statCd;
        }

    }

}