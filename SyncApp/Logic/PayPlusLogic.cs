﻿using Log4NetLibrary;
using Newtonsoft.Json;
using RestSharp;
using SyncApp.Helpers;
using SyncApp.Models;
using SyncApp.Models.EF;
using SyncApp.Models.PayPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Logic
{
    public class PayPlusLogic
    {
        private readonly ShopifyAppContext _context;
        private static readonly log4net.ILog _log = Logger.GetLogger();

        public PayPlusLogic(ShopifyAppContext context)
        {
            _context = context;
        }

        private Configrations _config
        {
            get
            {
                return _context.Configrations.First();
            }
        }

        public PayPlusIPNResponse GetPaymentInfo(string transaction_uid)
        {
            string baseUrl = _config.PayPlusUrl?.Trim();
            string path = PayPlusPathConst.GET_PAYMENT_PAGE.Trim();
            string url = baseUrl + path;

            var payPlusIPNRequest = new PayPlusIPNRequest
            {
                transaction_uid = transaction_uid
            };

            string authorizationValue = "{\"api_key\":\"" + _config.PayPlusApiKey + "\", \"secret_key\":\"" + _config.PayPlusSecretKey + "\"}";
            Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "authorization", authorizationValue }
                };

            string body = JsonConvert.SerializeObject(payPlusIPNRequest);
            var payPlusIPNResponse = new RESTHelper().SendPostRequest<PayPlusIPNResponse>(
                url, body, headers);

            Logger.LogObjectInfo(payPlusIPNResponse, payPlusIPNRequest, "GetPaymentInfo", url);

            if (payPlusIPNResponse != null && payPlusIPNResponse.results.status != "error")
                return payPlusIPNResponse;

            throw new Exception("Error retrieving IPN from payplus for transaction_uid: " + transaction_uid);
        }

        public int GetClearingCompanyCodeById(int clearing_id)
        {
            string baseUrl = _config.PayPlusUrl?.Trim();
            string path = PayPlusPathConst.GET_CLEARING_COMPANIES.Trim();
            string url = baseUrl + path;

            string authorizationValue = "{\"api_key\":\"" + _config.PayPlusApiKey + "\", \"secret_key\":\"" + _config.PayPlusSecretKey + "\"}";
            Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "authorization", authorizationValue }
                };

            var clearingCompanies = new RESTHelper().SendGetRequest<ClearingCompaniesResult>(
                url, headers);

            Logger.LogObjectInfo(clearingCompanies, clearing_id, "GetClearingCompanyCodeById", url);

            if (clearingCompanies != null)
                return clearingCompanies.clearing.FirstOrDefault(c => c.id == clearing_id).code;

            throw new Exception("Error retrieving clearing companies from payplus");
        }
    }
}