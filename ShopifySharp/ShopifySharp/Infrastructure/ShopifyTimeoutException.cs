using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;

namespace ShopifySharp.Infrastructure
{
    public class ShopifyTimeoutException : ShopifyException
    {
        public ShopifyTimeoutException(HttpResponseMessage response, HttpStatusCode httpStatusCode, IEnumerable<string> errors, string message, string rawBody, string requestId) 
            : base(response, httpStatusCode, errors, message, rawBody, requestId)
        {
        }
    }
}
