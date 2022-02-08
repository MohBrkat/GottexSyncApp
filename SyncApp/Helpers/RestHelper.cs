using Log4NetLibrary;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Helpers
{
    public class RESTHelper : RestClient
    {
        private static readonly log4net.ILog _errorLogger = Logger.GetLogger();

        public RESTHelper() : base()
        {
        }

        public T SendGetRequest<T>(string resource, Dictionary<string, string> headers) where T : new()
        {
            var request = new RestRequest(resource, Method.GET);

            request.AddHeader("Content-Type", "application/json; charset=utf-8");
            foreach (var pair in headers)
            {
                request.AddHeader(pair.Key, pair.Value);
            }

            var response = Execute<T>(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return JsonConvert.DeserializeObject<T>(response.Content);
            }
            else
            {
                LogError(request, response);
                return default;
            }
        }

        public T SendPostRequest<T>(string resource,string body, Dictionary<string, string> headers) where T : new()
        {
            var request = new RestRequest(resource, Method.POST);

            request.AddHeader("Content-Type", "application/json; charset=utf-8");
            foreach (var pair in headers)
            {
                request.AddHeader(pair.Key, pair.Value);
            }

            request.AddParameter("application/json", body, ParameterType.RequestBody);

            var response = Execute<T>(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return JsonConvert.DeserializeObject<T>(response.Content);
            }
            else
            {
                LogError(request, response);
                return default;
            }
        }

        public override IRestResponse Execute(IRestRequest request)
        {
            var response = base.Execute(request);
            TimeoutCheck(request, response);
            return response;
        }

        public override IRestResponse<T> Execute<T>(IRestRequest request)
        {
            var response = base.Execute<T>(request);
            TimeoutCheck(request, response);
            return response;
        }

        private void TimeoutCheck(IRestRequest request, IRestResponse response)
        {
            if (response.StatusCode == 0)
            {
                LogError(request, response);
            }
        }

        private void LogError(IRestRequest request, IRestResponse response)
        {
            //Get the values of the parameters passed to the API
            string parameters = string.Join(", ", request.Parameters.Select(x => x.Name.ToString() + "=" + ((x.Value == null) ? "NULL" : x.Value)).ToArray());

            //Set up the information message with the URL, the status code, and the parameters.
            string info = "Request to " + request.Resource + " failed with status code " + response.StatusCode + ", parameters: "
            + parameters + ", and content: " + response.Content;

            //Acquire the actual exception
            Exception ex;
            if (response != null && response.ErrorException != null)
            {
                ex = response.ErrorException;
                _errorLogger.Info(info);
                info = string.Empty;
            }
            else
            {
                ex = new Exception(info);
                info = string.Empty;
            }

            //Log the exception and info message
            _errorLogger.Info(ex);
            throw ex;
        }
    }

}
