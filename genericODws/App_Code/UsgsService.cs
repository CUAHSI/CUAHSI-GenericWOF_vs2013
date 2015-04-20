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
    using System.IO;
    using System.Xml;
    using System.Text;

    public class UsgsService
    {
        static XNamespace ns = "http://www.cuahsi.org/waterML/1.1/";

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

            try
            {
                if (endpoint.Contains("/dv/"))
                {
                   string statCd = null;
                   //Select from [USGSDataType] table. example: statCd = "00003" for "DataType=MEAN"
                   statCd = UsgsDataType.GetStatCd(paramValidator.statName);
                   UsgsValues usgsDV = new UsgsValues(
                       endpoint,
                       paramValidator.siteCd, paramValidator.varCd, statCd,
                       paramValidator.startDateField, paramValidator.endDateField);
                    responseNwis = usgsDV.GetValues();

                }
                else if (endpoint.Contains("/iv/"))
                {
                    //request USGS one year at one time
                    responseNwis = splitRequest(endpoint,
                        paramValidator.siteCd, paramValidator.varCd,
                        paramValidator.startDateField, paramValidator.endDateField);

                }
            }
            catch (Exception We)
            {
                log.Warn(We.Message);
            }

            return responseNwis;
        }

        public string splitRequest(string endpoint, string siteCode, string paramCode, string startDT, string endDT)
        {
            //split into each year
            int startYR = int.Parse(startDT.Substring(0, 4));
            int endYR = int.Parse(endDT.Substring(0, 4));
            int nyear = endYR - startYR;
            string startDT2, endDT2;

            if (nyear < 0)
            {
                //Console.WriteLine("start year cannot be after end year");
                return null;
            }

            XDocument xdoc, xdocSav = null;

            if (nyear == 0)
            {
                xdocSav = getXML(endpoint, siteCode, paramCode, startDT, endDT);
            }
            else
            {
                int First = 1;
                for (int i = 0; i <= nyear; i++)
                {
                    //first year
                    if (i == 0)
                    {
                        startDT2 = startDT.Substring(0, 10);
                        endDT2 = startDT.Substring(0, 4) + "-12-31";
                    }
                    else if (i < nyear)
                    {
                        int currYR = startYR + i;
                        startDT2 = currYR.ToString() + "-01-01";
                        endDT2 = currYR.ToString() + "-12-31";
                    }
                    // last year
                    else
                    {
                        startDT2 = endDT.Substring(0, 4) + "-01-01";
                        endDT2 = endDT.Substring(0, 10);
                    }

                    xdoc = getXML(endpoint, siteCode, paramCode, startDT2, endDT2);

                    if (xdoc.Root.Element(ns + "timeSeries") == null) continue;
                 
                    if (First == 1)
                    {
                        xdocSav = xdoc;
                        First = 0;
                    }
                    else
                    {
                        //add "value" Element
                        xdocSav.Root.Element(ns + "timeSeries").Element(ns + "values").Elements(ns + "value").LastOrDefault().AddAfterSelf
                            (from s in xdoc.Root.Element(ns + "timeSeries").Element(ns + "values").Elements(ns + "value") select s);

                        //add "qualifier" Element
                        var qualIDs = (from s in xdocSav.Root.Element(ns + "timeSeries").Element(ns + "values").Elements(ns + "qualifier")
                                       select s.Attribute("qualifierID").Value).ToList();
                        var qual = from s in xdoc.Root.Element(ns + "timeSeries").Element(ns + "values").Elements(ns + "qualifier")
                                   select s;
                        foreach (var ele in qual)
                        {
                            if (!qualIDs.Contains(ele.Attribute("qualifierID").Value))
                            {
                                xdocSav.Root.Element(ns + "timeSeries").Element(ns + "values").Elements(ns + "qualifier").LastOrDefault().AddAfterSelf(ele);
                            }
                        }

                        //add "method" Element
                        var methIDs = (from s in xdocSav.Root.Element(ns + "timeSeries").Element(ns + "values").Elements(ns + "method")
                                       select s.Attribute("methodID").Value).ToList();
                        var meth = from s in xdoc.Root.Element(ns + "timeSeries").Element(ns + "values").Elements(ns + "method")
                                   select s;
                        foreach (var ele in meth)
                        {
                            if (!methIDs.Contains(ele.Attribute("methodID").Value))
                            {
                                xdocSav.Root.Element(ns + "timeSeries").Element(ns + "values").Elements(ns + "method").LastOrDefault().AddAfterSelf(ele);
                            }
                        }
                    }
                }
            } //end if-else
                StringBuilder sb1 = new StringBuilder();
                using (StringWriter sr1 = new StringWriter(sb1))
                {
                    xdocSav.Save(sr1, SaveOptions.None);
                } 
            
                return sb1.ToString();

        } //end of function

        static XDocument getXML(string endpoint, string siteCode, string paramCode, string startDT, string endDT)
        {

            string url = String.Format("{0}?sites={1}&parameterCd={2}&startDT={3}&endDT={4}",
                         endpoint, siteCode, paramCode, startDT, endDT);
            XDocument xdoc = XDocument.Load(url);
            return xdoc;
        }
    }
}