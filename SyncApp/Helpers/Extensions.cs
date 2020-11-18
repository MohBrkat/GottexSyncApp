using Microsoft.Extensions.Configuration;
using ShopifySharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ShopifyApp2
{
    public static class Extensions
    {
        public static string AddDoubleQuotes(this string value)
        {
            return string.Format('"' + @"{0}" + '"',value);
        }
        public static bool IsNotNullOrEmpty(this string s)
        {
            return  !(string.IsNullOrEmpty(s) && string.IsNullOrWhiteSpace(s));
        }
        public static string InsertLeadingSpaces(this string s, int target)
        {
            while (s.Length < target)
            {
                s = s.Insert(0  , " ");
            }
            return s;
        }
        public static string InsertLeadingZeros(this string s, int target)
        {
            while (s.Length < target)
            {
               s =  s.Insert(0, "0");
            }
            return s;
        }

        public static string GetNumberWithDecimalPlaces(this decimal input, int digits)
        {
            return string.Format("{0:f@digit}".Replace("@digit", digits.ToString()), input);
        }
        public static string GetNumberWithDecimalPlaces(this decimal? input, int digits)
        {
            return string.Format("{0:f@digit}".Replace("@digit", digits.ToString()), input.GetValueOrDefault());
        }

        public static string ToOneString(this List<string> ls)
        {
            return string.Join(",", ls.ToArray());
        }

        public static decimal ValueWithoutTax(this decimal? value, decimal taxPercentage = 17)
        {
            return value.GetValueOrDefault() / ((taxPercentage / 100) + 1);
        }

        public static decimal ValueWithoutTax(this decimal value, decimal taxPercentage = 17)
        {
            return value / ((taxPercentage / 100) + 1);
        }

    }
    public static class ConfigurationManager
    {
        public static IConfigurationRoot Configuration;

        public static string GetConfig(string parent,string child)
        {
            var builder = new ConfigurationBuilder()
                   
                   .AddJsonFile("appsettings.json");


            Configuration = builder.Build();
           return Configuration[parent+":"+child];


        }

    }

}
