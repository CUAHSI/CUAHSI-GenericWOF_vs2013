
using System;
using System.Configuration;
using System.Text;
using log4net;
using WaterOneFlowImpl;
using System.Web;
using System.Web.Services;
using System.Xml.Serialization;
using System.Linq;
using System.Linq.Expressions;
using System.IO;
using System.Xml;
using System.Web.Services.Protocols;
using WaterOneFlow.Schema.v1_1;
using WaterOneFlow;
using Microsoft.Web.Services3;
using Microsoft.Web.Services3.Addressing;
using Microsoft.Web.Services3.Messaging;
using WaterOneFlowImpl.geom;

using USGSTranducer;

namespace WaterOneFlow.odws
{
    namespace v1_1
    {
        using ConstantsNamespace = WaterOneFlowImpl.v1_1.Constants;

        using IService = WaterOneFlow.v1_1.IService;
        using System.Xml.Linq;
        using WaterOneFlowImpl.v1_1;

        public class Config
        {
            public static string ODDB()
            {
                if (ConfigurationManager.ConnectionStrings["ODDB"] != null)
                {
                    return ConfigurationManager.ConnectionStrings["ODDB"].ConnectionString;
                }
                else
                {
                    return null;
                }
            }
        }

        [WebService(Name = WsDescriptions.WsDefaultName,
   Namespace = ConstantsNamespace.WS_NAMSPACE,
    Description = WsDescriptions.SvcDevelopementalWarning + WsDescriptions.WsDefaultDescription)]
        [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
        [SoapActor("*")]
        public class Service : WebService, IService
        {

            protected ODService ODws;

            //Yaping
            protected UsgsService USGSws;
            private Boolean useUSGSForValues;

            private Boolean useODForValues;
            private Boolean requireAuthToken;

            private static readonly ILog log = LogManager.GetLogger(typeof(Service));
            private static readonly ILog queryLog = LogManager.GetLogger("QueryLog");

            public struct Series
            {
                public DateTime dateTime;
                public float value;
                public string qclCode;
                public string sourceOrg;
            }

            public Service()
            {
                //log4net.Util.LogLog.InternalDebugging = true; 

                ODws = new ODService(this.Context);//INFO we can extend this for other service types

                //Yaping
                USGSws = new UsgsService();

                try
                {
                    useODForValues = Boolean.Parse(ConfigurationManager.AppSettings["UseODForValues"]);

                    //Yaping
                    useUSGSForValues = Boolean.Parse(ConfigurationManager.AppSettings["UseUSGSForValues"]);
                }
                catch (Exception e)
                {
                    String error = "Missing or invalid value for UseODForValues. Must be true or false";
                    log.Fatal(error);

                    throw new SoapException("Invalid Server Configuration. " + error,
                                         new XmlQualifiedName(SoapExceptionGenerator.ServerError));
                }

                try
                {
                    requireAuthToken = Boolean.Parse(ConfigurationManager.AppSettings["requireAuthToken"]);
                }
                catch (Exception e)
                {
                    String error = "Missing or invalid value for requireAuthToken. Must be true or false";
                    log.Fatal(error);
                    throw new SoapException(error,
                                            new XmlQualifiedName(SoapExceptionGenerator.ServerError));

                }

            }


            #region IService Members

            public string GetSites(string[] SiteNumbers, String authToken)
            {
                SiteInfoResponseType aSite = GetSitesObject(SiteNumbers, authToken);
                string xml = WSUtils.ConvertToXml(aSite, typeof(SiteInfoResponseType));
                return xml;

            }

            public virtual string GetSiteInfo(string SiteNumber, String authToken)
            {
                SiteInfoResponseType aSite = GetSiteInfoObject(SiteNumber, authToken);
                string xml = WSUtils.ConvertToXml(aSite, typeof(SiteInfoResponseType));
                return xml;
            }

            public string GetVariableInfo(string variable, String authToken)
            {
                VariablesResponseType aVType = GetVariableInfoObject(variable, authToken);
                string xml = WSUtils.ConvertToXml(aVType, typeof(VariablesResponseType));
                return xml;
            }

            public SiteInfoResponseType GetSitesObject(string[] site, String authToken)
            {
                GlobalClass.WaterAuth.SitesServiceAllowed(Context, authToken);

                try
                {
                    return ODws.GetSites(site);
                }
                catch (Exception we)
                {
                    log.Warn(we.Message);
                    throw SoapExceptionGenerator.WOFExceptionToSoapException(we);

                }
            }

            public virtual SiteInfoResponseType GetSiteInfoObject(string site, String authToken)
            {
                GlobalClass.WaterAuth.SiteInfoServiceAllowed(Context, authToken);

                try
                {
                    return ODws.GetSiteInfo(site);
                }
                catch (Exception we)
                {
                    log.Warn(we.Message);
                    throw SoapExceptionGenerator.WOFExceptionToSoapException(we);

                }


            }

            public VariablesResponseType GetVariableInfoObject(string variable, String authToken)
            {
                GlobalClass.WaterAuth.VariableInfoServiceAllowed(Context, authToken);

                try
                {
                    return ODws.GetVariableInfo(variable);
                }
                catch (Exception we)
                {
                    log.Warn(we.Message);
                    throw SoapExceptionGenerator.WOFExceptionToSoapException(we);

                }

            }

            public virtual string GetValues(string location, string variable, string startDate, string endDate, String authToken)
            {
                TimeSeriesResponseType aSite = GetValuesObject(location, variable, startDate, endDate, null);
                return WSUtils.ConvertToXml(aSite, typeof(TimeSeriesResponseType));
            }

            public virtual TimeSeriesResponseType GetValuesObject(string location, string variable, string startDate, string endDate, String authToken)
            {
                GlobalClass.WaterAuth.DataValuesServiceAllowed(Context, authToken);

                try
                {
                    if (useUSGSForValues)
                    {
                        string responseNwisXml = null;
                        TimeSeriesResponseType response;
                        string network = System.Configuration.ConfigurationManager.AppSettings["network"];
                        if (network.ToLower().Contains("ngwmn")) {
                            char separator = ':';
                            string[] partsLocation = location.Split(separator);
                            string siteCd = null;
                            if (location.Contains(separator.ToString())) siteCd = partsLocation[1];
                            else siteCd = partsLocation[0];

                            responseNwisXml = USGSws.ngwmn_GetValues(siteCd);
                            response = TimeSeriesResponseBuilder_ngwmn(responseNwisXml, location);
                        }
                        else {
                            responseNwisXml = USGSws.GetValues(location, variable, startDate, endDate);
                            response = TimeSeriesResponseBuilder_nwis(responseNwisXml, network);
                        }

                        //string responseNwisXml = HttpUtility.HtmlDecode(USGSws.GetValues(location, variable, startDate, endDate));
                        //System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                        //string responseNwisXml = USGSws.GetValues(location, variable, startDate, endDate);
                        //sw.Stop();
                        //long time = sw.ElapsedTicks;

                        var queryInfo = new QueryInfoType()
                        {
                            //queryURL = o.Element(ns1 + "queryURL").Value,
                            criteria = new QueryInfoTypeCriteria()
                            {
                                locationParam = location,
                                variableParam = variable,
                                timeParam = new QueryInfoTypeCriteriaTimeParam()
                                {
                                    beginDateTime = startDate,
                                    endDateTime = endDate
                                },
                                parameter = new QueryInfoTypeCriteriaParameter[]
                                {
                                    new QueryInfoTypeCriteriaParameter() {
                                            name = "site",
                                            value = location
                                        },
                                    new QueryInfoTypeCriteriaParameter() {
                                            name = "variable",
                                            value = variable
                                        },
                                    new QueryInfoTypeCriteriaParameter() {
                                            name = "beginDate",
                                            value = startDate
                                        },
                                    new QueryInfoTypeCriteriaParameter() {
                                            name = "endDate",
                                            value = endDate
                                    }
                                }
                            }
                        };

                        response.queryInfo = queryInfo;

                        return response;

                    }
                    else
                    {
                        return ODws.GetValues(location, variable, startDate, endDate);
                    }
                }
                catch (Exception we)
                {
                    log.Warn(we.Message);
                    throw SoapExceptionGenerator.WOFExceptionToSoapException(we);

                }

            }


//<?xml version = "1.0" encoding="utf-8"?>
//<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
//  <soap:Body>
//    <GetValues xmlns = "http://www.cuahsi.org/his/1.1/ws/" >
//      < location > ngwmn:VW_GWDP_GEOSERVER.USGS.403836085374401</location>
//      <variable></variable>
//      <startDate></startDate>
//      <endDate></endDate>
//      <authToken></authToken>
//    </GetValues>
//  </soap:Body>
//</soap:Envelope>
            public TimeSeriesResponseType TimeSeriesResponseBuilder_ngwmn(string responseNwisXml, string siteCd)
            {
                TimeSeriesResponseType response = CuahsiBuilder.CreateTimeSeriesObjectSingleValue(1);

                XDocument xDocument;
                using (TextReader xmlReader = new StringReader(responseNwisXml))
                {
                    xDocument = XDocument.Load(xmlReader);
                }

                //XPathSelectElement | XPathEvaluate
                //"VariableCode" xpath = "/GetObservationResponse/observationData/OM_Observation/result/MeasurementTimeseries/defaultPointMetadata/DefaultTVPMeasurementMetadata/uom/@title"   
                string variableCode = xDocument.Descendants("uom").FirstOrDefault().Attribute("title").Value;
                string vocab = "default";
                string variableName = "Well Depth";
                string valueType = "unknown";
                string generalCategory = "Hydrology";
                string sampleMedium = "groundwater";
                string sourceID = "0";
                string QCLID = "0";


                //"DataType" xpath = "/GetObservationResponse/observationData/OM_Observation/result/MeasurementTimeseries/defaultPointMetadata/DefaultTVPMeasurementMetadata/interpolationType/@title"
                var datatype = xDocument.Descendants("interpolationType").FirstOrDefault().Attribute("title").Value;

                //"VariableUnitsName" xpath = "/GetObservationResponse/observationData/OM_Observation/result/MeasurementTimeseries/defaultPointMetadata/DefaultTVPMeasurementMetadata/uom/@code" 
                var variableUnitsName = xDocument.Descendants("uom").FirstOrDefault().Attribute("code").Value;

                var MeasurementTVP = xDocument.Descendants("MeasurementTVP");

                //under a loop for a specified SiteCode
                //Series[] series = null;
                //series = (from o in MeasurementTVP
                //          select new Series()
                //          {
                //              dateTime = DateTime.Parse(o.Element("time").Value),
                //              value = float.Parse(o.Element("value").Value),
                //              sourceOrg = o.Element("metadata").Element("TVPMeasurementMetadata").Element("source").Attribute("title").Value,
                //              qclCode = o.Element("metadata").Element("TVPMeasurementMetadata").Element("comment").Value,
                //          }).ToArray();

                ValueSingleVariable[] seriesValue;
                QualifierType[] seriesQualifier;
                MethodType[] seriesMethod;
                try
                {
                    seriesValue = (from o in MeasurementTVP
                                       select new ValueSingleVariable()
                                       {
                                           dateTime = DateTime.Parse(o.Element("time").Value),
                                           Value = Decimal.Parse(o.Element("value").Value),
                                           qualifiers = o.Element("metadata").Element("TVPMeasurementMetadata").Element("comment").Value,
                                       }).ToArray();

                    seriesQualifier = (from o in MeasurementTVP
                                select new QualifierType()
                                {
                                    qualifierCode = o.Element("metadata").Element("TVPMeasurementMetadata").Element("comment").Value,
                                    qualifierDescription = "unknown",
                                    qualifierIDSpecified = true,
                                    qualifierID = 0 // int.Parse(t.Attribute("qualifierID").Value)
                                }).ToArray();

                    seriesMethod = (from o in MeasurementTVP
                                       select new MethodType()
                                       {
                                           methodDescription = "unknown",
                                           methodIDSpecified = false,
                                           methodID = 0
                                       }).ToArray();
                }
                catch (Exception e)
                {
                    seriesValue = null;
                    seriesQualifier = null;
                    seriesMethod = null;
                }


                string bDT = xDocument.Element("GetObservationResponse").Element("extension").Element("temporalExtent").Element("TimePeriod").Element("beginPosition").Value;
                string eDT = xDocument.Element("GetObservationResponse").Element("extension").Element("temporalExtent").Element("TimePeriod").Element("endPosition").Value;

                TsValuesSingleVariableType[] values = null;

                //If nothing returns in <timeSeries> node
                var tsResp = xDocument.Element("GetObservationResponse").Element("observationData");

                if (tsResp == null)
                {
                    response.timeSeries[0].sourceInfo = null;
                    response.timeSeries[0].variable = null;
                    response.timeSeries[0].values = null;
                }
                else
                {
                    //GetObservationResponse/observationData/OM_Observation/metadata/ObservationMetadata/contact/@href
                    string sourcehref = xDocument.Element("GetObservationResponse").Element("observationData").Element("OM_Observation")
                        .Element("metadata").Element("ObservationMetadata").Element("contact").Attribute("href").Value;

                    var sourceInfo = new SiteInfoType()
                                      {
                                          //siteName = ,

                                          siteCode = new SiteInfoTypeSiteCode[] {
                                                            new SiteInfoTypeSiteCode(){
                                                            network = System.Configuration.ConfigurationManager.AppSettings["network"],
                                                            //agencyCode = t.Attribute("agencyCode").Value,
                                                            Value = siteCd
                                                            }
                                          }

                                          //note = new NoteType[] {
                                          //        new NoteType()
                                          //        {
                                          //            type = "note",
                                          //            title = "sourcehref",
                                          //            Value = sourcehref
                                          //        }
                                          //      }

                                          //timeZoneInfo = ,

                                          //geoLocation = ,

                                          //siteType = 
                    };

                    var varInfo = new VariableInfoType()
                                   {
                                       //USGS data service mix these two concepts: 'variableName', and 'variableDescription'
                                       variableDescription = variableName,
                                       variableName = variableName,

                                       generalCategory = generalCategory,
                                       sampleMedium = sampleMedium,
                                       //oid = o.Attribute(ns1 + "oid").Value,
                                       valueType = valueType,

                                       dataType = datatype,

                                       variableCode = new VariableInfoTypeVariableCode[] {
                                                               new VariableInfoTypeVariableCode(){
                                                                   network = System.Configuration.ConfigurationManager.AppSettings["network"],
                                                                   //variableID = int.Parse(t.Attribute("variableID").Value),
                                                                   vocabulary = vocab,
                                                                   Value = variableCode
                                                                   //@default = bool.Parse(t.Attribute("default").Value)
                                                               }},

                                       unit = new UnitsType
                                               {
                                                   unitCode = variableUnitsName,
                                                   unitAbbreviation = "ft"
                                               },

                                       noDataValueSpecified = true,
                                       noDataValue = double.Parse("-999999.0")
                                   };

                        values = new TsValuesSingleVariableType[] {
                                           new TsValuesSingleVariableType()
                                          {
                                              value = seriesValue,
                                              qualifier =  seriesQualifier,
                                              method = seriesMethod
                                          }};

                    response.timeSeries[0].sourceInfo = sourceInfo;
                    response.timeSeries[0].variable = varInfo;
                    response.timeSeries[0].values = values;
                    //response.timeSeries[0].name = tsResp.Attribute("name").Value;
                }

                return response;
            }


            public TimeSeriesResponseType TimeSeriesResponseBuilder_nwis(string responseNwisXml, string network)
            {
                TimeSeriesResponseType response = CuahsiBuilder.CreateTimeSeriesObjectSingleValue(1);
                XNamespace ns1;
                if (network.ToLower().Contains("nwisgw"))
                    ns1 = "http://www.cuahsi.org/waterML/1.2/";
                else
                    ns1 = "http://www.cuahsi.org/waterML/1.1/";

                XDocument xDocument;
                using (TextReader xmlReader = new StringReader(responseNwisXml))
                {
                    xDocument = XDocument.Load(xmlReader);
                }

                XElement root = xDocument.Root;
                var tsResp = root.Element(ns1 + "timeSeries");

                string bDT, eDT;

                bDT = (from o in root.Descendants(ns1 + "beginDateTime")
                       select o.Value).FirstOrDefault().ToString();
                eDT = (from o in root.Descendants(ns1 + "endDateTime")
                       select o.Value).FirstOrDefault().ToString();

                //var queryInfo = (from o in root.Descendants(ns1 + "queryInfo")
                //                 select new QueryInfoType()
                //                 {
                //                     queryURL = o.Element(ns1 + "queryURL").Value,
                //                     criteria = (from t in o.Elements(ns1 + "criteria")
                //                                 select new QueryInfoTypeCriteria()
                //                                 {
                //                                     locationParam = t.Element(ns1 + "locationParam").Value,
                //                                     variableParam = t.Element(ns1 + "variableParam").Value,
                //                                     timeParam = (from p in t.Elements(ns1 + "timeParam")
                //                                                  select new QueryInfoTypeCriteriaTimeParam()
                //                                                  {
                //                                                      //in DV
                //                                                      //beginDateTime = p.Element(ns1 + "beginDateTime").Value,
                //                                                      //endDateTime = p.Element(ns1 + "endDateTime").Value
                //                                                      //in UV
                //                                                      beginDateTime = bDT.Substring(0, 23),
                //                                                      endDateTime = eDT.Substring(0, 23)
                //                                                  }).FirstOrDefault(),
                //                                     parameter = new QueryInfoTypeCriteriaParameter[] {
                //                                                        new QueryInfoTypeCriteriaParameter() {
                //                                                                name = "site",
                //                                                                value = t.Element(ns1 + "locationParam").Value
                //                                                            },
                //                                                        new QueryInfoTypeCriteriaParameter() {
                //                                                                name = "variable",
                //                                                                value = t.Element(ns1 + "variableParam").Value
                //                                                            },
                //                                                        new QueryInfoTypeCriteriaParameter() {
                //                                                                name = "beginDate",
                //                                                                value = bDT.Substring(0, 23)
                //                                                                //value = (from p in t.Elements(ns1 + "timeParam")
                //                                                                //    select p.Element(ns1+"beginDateTime").Value).Single()
                //                                                            },
                //                                                        new QueryInfoTypeCriteriaParameter() {
                //                                                                name = "endDate",
                //                                                                value = eDT.Substring(0, 23)
                //                                                                //value = (from p in t.Elements(ns1 + "timeParam")
                //                                                                //    select p.Element(ns1+"endDateTime").Value).Single()
                //                                                            }

                //                                                        }
                //                                 }).FirstOrDefault(),

                //                 }).FirstOrDefault();

                //response.queryInfo = queryInfo;

                TsValuesSingleVariableType[] values = null;

                //If nothing returns in <timeSeries> node
                if (tsResp == null)
                {
                    response.timeSeries[0].sourceInfo = null;
                    response.timeSeries[0].variable = null;
                    response.timeSeries[0].values = null;
                }
                else
                {
                    var sourceInfo = (from o in tsResp.Elements(ns1 + "sourceInfo")
                                      select new SiteInfoType()
                                      {
                                          siteName = o.Element(ns1 + "siteName").Value,

                                          siteCode = (from t in o.Descendants(ns1 + "siteCode")
                                                      select new SiteInfoTypeSiteCode[] {
                                                            new SiteInfoTypeSiteCode(){
                                                            //Original value: t.Attribute("network").Value = "NWIS"
                                                            network = System.Configuration.ConfigurationManager.AppSettings["network"],
                                                            agencyCode = t.Attribute("agencyCode").Value,
                                                            Value = t.Value
                                                            }}
                                                      ).FirstOrDefault(),

                                          timeZoneInfo = (from t in o.Descendants(ns1 + "timeZoneInfo")
                                                          select new SiteInfoTypeTimeZoneInfo()
                                                          {
                                                              siteUsesDaylightSavingsTime = bool.Parse(t.Attribute("siteUsesDaylightSavingsTime").Value),
                                                              defaultTimeZone = new SiteInfoTypeTimeZoneInfoDefaultTimeZone()
                                                              {
                                                                  zoneOffset = t.Element(ns1 + "defaultTimeZone").Attribute("zoneOffset").Value,
                                                                  zoneAbbreviation = t.Element(ns1 + "defaultTimeZone").Attribute("zoneAbbreviation").Value
                                                              },
                                                              daylightSavingsTimeZone = new SiteInfoTypeTimeZoneInfoDaylightSavingsTimeZone()
                                                              {
                                                                  zoneOffset = t.Element(ns1 + "daylightSavingsTimeZone").Attribute("zoneOffset").Value,
                                                                  zoneAbbreviation = t.Element(ns1 + "daylightSavingsTimeZone").Attribute("zoneAbbreviation").Value
                                                              }
                                                          }).FirstOrDefault(),

                                          geoLocation = (from t in o.Descendants(ns1 + "geoLocation")
                                                         select new SiteInfoTypeGeoLocation()
                                                         {
                                                             geogLocation = (from s in t.Descendants(ns1 + "geogLocation")
                                                                                 //default value => t.Attribute("xsi:type").Value = "LatLonPointType"
                                                                                 //where( t.Name.ToString().Contains("LatLonPointType"))
                                                                             select new LatLonPointType()
                                                                             {
                                                                                 latitude = double.Parse(s.Element(ns1 + "latitude").Value),
                                                                                 longitude = double.Parse(s.Element(ns1 + "longitude").Value)
                                                                             }).FirstOrDefault()
                                                         }).FirstOrDefault(),

                                          //<note title="siteTypeCd">ST</note>
                                          //<note title="hucCd">01070004</note>
                                          //<note title="stateCd">25</note>
                                          //<note title="countyCd">25027</note>
                                          //NoteType {typeField; hrefField;titleField;showField;valueField;
                                          note = (from t in o.Descendants(ns1 + "siteProperty")
                                                  let tit = t.Attribute("name").Value
                                                  select new NoteType()
                                                  {
                                                      type = "note",
                                                      title = tit,
                                                      Value = t.Value
                                                  }).ToArray(),

                                          siteType = (from t in o.Descendants(ns1 + "siteProperty")
                                                      where t.Attribute("name").Value.Equals("siteTypeCd")
                                                      select new string[]
                                                      {
                                                                  t.Value
                                                      }.ToArray()).FirstOrDefault()

                                      }).FirstOrDefault();

                    var varInfo = (from o in tsResp.Elements(ns1 + "variable")
                                   select new VariableInfoType()
                                   {
                                       //USGS data service mix these two concepts: 'variableName', and 'variableDescription'
                                       variableDescription = o.Element(ns1 + "variableName").Value,
                                       variableName = o.Element(ns1 + "variableDescription").Value,

                                       generalCategory = System.Configuration.ConfigurationManager.AppSettings["generalCategory"],
                                       sampleMedium = System.Configuration.ConfigurationManager.AppSettings["sampleMedium"],
                                       oid = o.Attribute(ns1 + "oid").Value,
                                       valueType = o.Element(ns1 + "valueType").Value,

                                       dataType = network.ToLower().Contains("nwisdv") ?
                                            o.Descendants(ns1 + "option").FirstOrDefault().Value :
                                            System.Configuration.ConfigurationManager.AppSettings["dataType"],

                                       variableCode = (from t in o.Descendants(ns1 + "variableCode")
                                                       select new VariableInfoTypeVariableCode[] {
                                                               new VariableInfoTypeVariableCode(){
                                                                   network = System.Configuration.ConfigurationManager.AppSettings["network"],
                                                                   variableID = int.Parse(t.Attribute("variableID").Value),
                                                                   vocabulary = System.Configuration.ConfigurationManager.AppSettings["network"],
                                                                   Value = t.Value,
                                                                   @default = bool.Parse(t.Attribute("default").Value)
                                                               }}).FirstOrDefault(),
                                       //variableProperty,
                                       unit = (from t in o.Descendants(ns1 + "unit")
                                               select new UnitsType
                                               {
                                                   unitCode = t.Value,

                                                   //Harvester requires sreies.variable.unit != null
                                                   //unitIDSpecified = true,
                                                   //unitID = int.Parse(t.Value),

                                                   //not exposed in USGS service, but required in HydroDesktop
                                                   //since HydroDesktop only select node with r.GetAttribute("unitsAbbreviation");
                                                   //    see more in HydroDesktop -> WATERML11Parser.cs
                                                   //
                                                   //Correspondingly, I added  in cuahsiTimeSeries_v1_1.cs: 
                                                   //-----------------------------------------------------------
                                                   //[XmlElementAttribute("unitsAbbreviation")]  //yaping added
                                                   //        public string unitAbbreviation
                                                   //-----------------------------------------------------------
                                                   unitAbbreviation = t.Value
                                               }).FirstOrDefault(),

                                       options = (from t in o.Descendants(ns1 + "options")
                                                  select new option[] {
                                                         new option() {
                                                          name = t.Element(ns1+"option").Attribute("name").Value,
                                                          optionCode = t.Element(ns1+"option").Attribute("optionCode").Value,
                                                          Value = t.Element(ns1+"option").Value
                                                      }}.ToArray()).FirstOrDefault(),

                                       noDataValueSpecified = true,
                                       noDataValue = o.Element(ns1 + "noDataValue").IsEmpty ? double.Parse("-999999.0") : double.Parse(o.Element(ns1 + "noDataValue").Value)
                                   }).FirstOrDefault();

                    if (network.ToLower().Contains("nwisgw"))
                    {
                        values = (from o in tsResp.Elements(ns1 + "values")
                                  select new TsValuesSingleVariableType[] {
                                           new TsValuesSingleVariableType()
                                          {
                                              //ValueSingleVariable[] value
                                              value = (from t in o.Elements(ns1+"value")
                                                       let dt = t.Attribute("dateTime").Value
                                                       select new ValueSingleVariable() {
                                                                //qualifiers = t.Attribute("qualifiers").Value,
                                                                dateTime = new DateTime(
                                                                int.Parse(dt.Substring(0, 4)), int.Parse(dt.Substring(5, 2)), int.Parse(dt.Substring(8,2)), 
                                                                //UV fixed from (00:00:00)
                                                                int.Parse(dt.Substring(11, 2)), int.Parse(dt.Substring(14, 2)), int.Parse(dt.Substring(17, 2))),
                                                                Value = Decimal.Parse(t.Value)
                                                       }).ToArray(),

                                                method = (from t in o.Descendants(ns1 + "method")
                                                          select new MethodType()
                                                          {
                                                            //methodDescription = t.Element(ns1 + "methodDescription").IsEmpty? null: t.Element(ns1+"methodDescription").Value,
                                                            //methodIDSpecified = true,
                                                            methodID = int.Parse(t.Attribute("methodID").Value)
                                                            //methodCode = t.Attribute("methodID").Value
                                                          }).ToArray()

                                          }}).FirstOrDefault();
                    }
                    else
                    {
                        values = (from o in tsResp.Elements(ns1 + "values")
                                  select new TsValuesSingleVariableType[] {
                                           new TsValuesSingleVariableType()
                                          {
                                              //ValueSingleVariable[] value
                                              value = (from t in o.Elements(ns1+"value")
                                                       let dt = t.Attribute("dateTime").Value
                                                       select new ValueSingleVariable() {
                                                                qualifiers = t.Attribute("qualifiers").Value,
                                                                dateTime = new DateTime(
                                                                int.Parse(dt.Substring(0, 4)), int.Parse(dt.Substring(5, 2)), int.Parse(dt.Substring(8,2)), 
                                                                //UV fixed from (00:00:00)
                                                                int.Parse(dt.Substring(11, 2)), int.Parse(dt.Substring(14, 2)), int.Parse(dt.Substring(17, 2))),
                                                                Value = Decimal.Parse(t.Value)
                                                       }).ToArray(),

                                              qualifier =  (from t in o.Elements(ns1 + "qualifier")
                                                            select new QualifierType()
                                                            {
                                                                qualifierCode = t.Element(ns1 + "qualifierCode").IsEmpty? "-9999": t.Element(ns1+"qualifierCode").Value,
                                                                qualifierDescription = t.Element(ns1 + "qualifierDescription").IsEmpty? "Unknown": t.Element(ns1+"qualifierDescription").Value,
                                                                qualifierIDSpecified = true,
                                                                qualifierID = int.Parse(t.Attribute("qualifierID").Value)
                                                                //network = t.Attribute("network").Value,
                                                                //vocabulary = t.Attribute("vocabulary").Value
                                                            }).ToArray(),

                                                method = (from t in o.Descendants(ns1 + "method")
                                                          select new MethodType()
                                                          {
                                                            methodDescription = t.Element(ns1 + "methodDescription").IsEmpty? null: t.Element(ns1+"methodDescription").Value,
                                                            methodIDSpecified = true,
                                                            methodID = int.Parse(t.Attribute("methodID").Value),
                                                            methodCode = t.Attribute("methodID").Value
                                                          }).ToArray()

                                          }}).FirstOrDefault();
                    }


                    response.timeSeries[0].sourceInfo = sourceInfo;

                    //There is only one timeseries returned from USGS, even with those sites with dd_num > 1 
                    // This is actually some ambiguity of (different methodIDs, but only one timesereis exposed)
                    response.timeSeries[0].name = tsResp.Attribute("name").Value;

                    response.timeSeries[0].variable = varInfo;
                    response.timeSeries[0].values = values;


                    //Get DataType
                    //Assuming there is only one <option> node 
                    string DT;
                    DT = varInfo.dataType;
                    //tsResp.Elements(ns1 + "option").FirstOrDefault().IsEmpty? "Instantaneous": tsResp.Elements(ns1 + "option").FirstOrDefault().Value;
                    if (DT != String.Empty)
                        response.timeSeries[0].variable.variableCode[0].Value = response.timeSeries[0].variable.variableCode[0].Value + "/DataType=" + DT;

                } // else

                return response;
            }


            public SiteInfoResponseType GetSiteInfoMultpleObject(string[] site, string authToken)
            {
                GlobalClass.WaterAuth.SiteInfoServiceAllowed(Context, authToken);

                try
                {
                    return ODws.GetSiteInfo(site, true);
                }
                catch (Exception we)
                {
                    log.Warn(we.Message);
                    throw SoapExceptionGenerator.WOFExceptionToSoapException(we);

                }
            }

            public SiteInfoResponseType GetSitesByBoxObject(float west, float south, float east, float north, bool IncludeSeries, string authToken)
            {
                GlobalClass.WaterAuth.SiteInfoServiceAllowed(Context, authToken);
                return ODws.GetSitesInBox(west, south, east, north, IncludeSeries);
            }

            public TimeSeriesResponseType GetValuesForASiteObject(string site, string StartDate, string EndDate, string authToken)
            {
                GlobalClass.WaterAuth.DataValuesServiceAllowed(Context, authToken);
                return ODws.GetValuesForASite(site, StartDate, EndDate);
            }

            public TimeSeriesResponseType GetValuesByBoxObject(string variable, string StartDate, string EndDate, float west, float south, float east, float north, string authToken)
            {
                throw new NotImplementedException();
            }

            public string GetVariables(String authToken)
            {
                VariablesResponseType aVType = GetVariableInfoObject(null, authToken);
                string xml = WSUtils.ConvertToXml(aVType, typeof(VariablesResponseType));
                return xml;
            }

            public VariablesResponseType GetVariablesObject(String authToken)
            {

                return GetVariableInfoObject(null, authToken);

            }

            //ms
            public string GetSources(String authToken)
            {
                string sourcesXML = string.Empty;

                return sourcesXML;
            }

            public string GetSourceObject(string[] source, String authToken)
            {
                GlobalClass.WaterAuth.SitesServiceAllowed(Context, authToken);

                try
                {
                    return "";//ODws.GetSites(source);
                }
                catch (Exception we)
                {
                    log.Warn(we.Message);
                    throw SoapExceptionGenerator.WOFExceptionToSoapException(we);

                }
            }

            #endregion


        }
    }
}