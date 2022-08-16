using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SyncAppJob
{
    public class RestSharpClient
    {
        private string _url;
        public RestSharpClient(string url)
        {
            _url = url;
        }

        public T Get<T>(string methodName, string urlParameters) where T : new()
        {
            T result;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var fullUrl = _url + methodName + urlParameters;

            var client = new RestClient(fullUrl);
            var request = new RestRequest();

            //Add Headers
            request.AddHeader("cache-control", "no-cache");
            request.Timeout = 3 * 60 * 1000;

            var response = client.Get(request);

            if (response.ResponseStatus == ResponseStatus.Completed)
            {
                result = JsonConvert.DeserializeObject<T>(response.Content);
            }
            else
            {
                throw new Exception($"Failed to proceed the request. Error: {response.ErrorMessage}, Code: {response.StatusCode}");
            }

            return result;
        }
    }
}
