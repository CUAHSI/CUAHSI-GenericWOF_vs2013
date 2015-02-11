using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Web;

using log4net;
using WaterOneFlowImpl;
using WaterOneFlowImpl.v1_1;
using WaterOneFlow.Schema.v1_1;

/// <summary>
/// Usgs service is used for getting DataValues from USGS REST API
/// input parameters:
///  siteCode, variableCode, startDate, endDate
/// </summary>

namespace USGSTranducer
{
    using CuahsiBuilder = WaterOneFlowImpl.v1_1.CuahsiBuilder;
    using System.Xml.Linq;

    public class UsgsService
    {
        // = @"http://waterservices.usgs.gov/nwis/dv/";
        private string endpoint;   
        private static readonly ILog log = LogManager.GetLogger(typeof(UsgsService));

        public UsgsService()
        {
            endpoint = ConfigurationManager.AppSettings["USGSendpoint"];

        }

        public String GetValues(string location, string variable, string startDate, string endDate)
        {
            String responseNwis = null;
            char separator = ':';
            string[] partsLocation = location.Split(separator);
            string[] partsVariable = variable.Split(separator);
            string siteCd = null;
            string varCd = null;

            if (location.Contains(separator.ToString()))
            {
                siteCd = partsLocation[1];
            }
            else
            {
                siteCd = partsLocation[0];
            }

            if (variable.Contains(separator.ToString()))
            {
                varCd = partsVariable[1];
            }
            else
            {
                varCd = partsVariable[0];
            }

            //example data
            //location  LBR:NWISDV|00003
            //varaible  LBR:NWISDV|00003|DataType=MAXIMUM
            if (!siteCd.StartsWith("NWIS", StringComparison.InvariantCultureIgnoreCase))
                return null;

            string statCd = null;

            UsgsParamValidator paramValidator = new UsgsParamValidator(location, variable, startDate, endDate);

            //Yaping - comment out temporally so that it accept YYYY-MM-DDTHH:MM format, like, 2015-01-26T00:00
            //  need to fix this later
            //try
            //{
            //    paramValidator.Validate();
            //}
            //catch (ArgumentException e) 
            //{
            //    log.Warn(e.Message);
            //}

            //Select from [USGSDataType] table. example: statCd = "00003" for "DataType=MEAN"
            statCd = UsgsDataType.GetStatCd(paramValidator.statName);
            UsgsValues usgsDV = new UsgsValues(
                endpoint,
                paramValidator.siteCd, paramValidator.varCd, statCd,
                paramValidator.startDateField, paramValidator.endDateField);

            try
            {
                responseNwis = usgsDV.GetValues();
            }
            catch (Exception We)
            {
                log.Warn(We.Message);
            }

            return responseNwis;

            //Yaping - comment out so that the right xml is returned for deserialization in GetValuesObject() call
            //return WaterOneFlowImpl.v1_1.WSUtils.ConvertToXml(responseNwis, typeof(String));
        }


    }

}