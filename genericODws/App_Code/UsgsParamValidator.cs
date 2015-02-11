using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

/// <summary>
/// Summary description for UsgsParamValidator
/// </summary>
namespace USGSTranducer
{
    public class UsgsParamValidator
    {
        private string location, variable, startDate, endDate;

        public string siteCd
        {
            get
            {
                Regex regex = new Regex(@"\d+");
                Match match = regex.Match(location);
                return match.Value;
            }
        }

        public string varCd
        {
            get
            {
                Regex regex = new Regex(@"\d+", RegexOptions.IgnoreCase);
                Match match = regex.Match(variable);
                return match.Value;
            }
        }

        public string statName
        {
            get
            {
                Regex regex = new Regex(@"DataType=([\w-]+)", RegexOptions.IgnoreCase);  
                Match match = regex.Match(variable);
                return match.Groups[1].Value;
            }
        }

        public string startDateField
        {
            get
            {
                return startDate;
            }
        }

        public string endDateField
        {
            get
            {
                return endDate;
            }
        }

        
        public UsgsParamValidator(string _location, string _variable, string _startDate, string _endDate)
        {
            location = _location;
            variable = _variable;
            startDate = _startDate;
            endDate = _endDate;
        }

        //Validate input parameters
        public void Validate()
        {
            string locPattern = @"\w*:\w*_([0-9][0-9][0-9][0-9][0-9])";
            string varPattern = @"\w*:\w*_([0-9][0-9][0-9][0-9][0-9])(_(?i)DataType(?-i)=)(\w*)";
            string datePattern = @"\d{4}-\d{2}-\d{2}";

            RegexStringValidator locValidator = new RegexStringValidator(locPattern);
            RegexStringValidator varValidator = new RegexStringValidator(varPattern);
            RegexStringValidator dateValidator = new RegexStringValidator(datePattern);
            try
            {
                locValidator.Validate(locPattern);
                varValidator.Validate(varPattern);
                dateValidator.Validate(startDate);
                dateValidator.Validate(endDate);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException(e.Message);
            }
        }

    }
}
