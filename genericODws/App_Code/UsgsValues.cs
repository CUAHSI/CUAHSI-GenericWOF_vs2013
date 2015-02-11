using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// Summary description for GetUsgsValues
/// </summary>
namespace USGSTranducer
{

    public class UsgsValues
    {
        protected string endpoint;
        protected string siteCd, varCd, statCd, startDate, endDate;

        public UsgsValues(string _endpoint, string _siteCd, string _varCd, string _statCd, string _startDate, string _endDate)
        {
            endpoint = _endpoint;
            siteCd = _siteCd;
            varCd = _varCd;
            statCd = _statCd;
            startDate = _startDate;
            endDate = _endDate;
        }

        public String GetValues()
        {
            String responseNwis = null;
            RestClient client = new RestClient();
            client.EndPoint = endpoint;
            //default   client.Method = HttpVerb.POST;

            try
            {
                responseNwis = client.MakeRequest(
                    "?site=" + siteCd +
                    "&parameterCd=" + varCd +
                    "&statCd=" + statCd +
                    "&startDT=" + startDate.Substring(0,10) +
                    "&endDT=" + endDate.Substring(0, 10));
            }
            catch (Exception e)
            {
                throw new Exception("Error: failed to access " + endpoint);
            }

            return responseNwis;
        }
    }
}