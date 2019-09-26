using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ShopifySharp;

namespace ShopifyApp2
{
    public class OrderEntity
    {
        /// <summary>
        /// 
        /// </summary>
        public string ShopifyId { set; get; }

        /// <summary>
        /// 
        /// </summary>

        public string OrderNumber { set; get; }
        /// <summary>
        /// The sum of all line item prices, discounts, shipping, taxes, and tips in the shop currency. Must be positive.
        /// </summary>
        public string TotalPrice { set; get; }
        /// <summary>
        /// The sum of all the taxes applied to the order in th shop currency. Must be positive).
        /// </summary>
        public string TotalTax { set; get; }
        public DateTime CreationDate { set; get; }
        public string PaymentGateway { set; get; }

        public string Customer { set; get; }

        public string DiscountApplications { set; get; }

        public string Email { set; get; }

        public string FinancialStatus { set; get; }
        public string Fulfillments { set; get; }

        public string FulfillmentStatus { set; get; }

        public string LineItems { set; get; }

        public string Transactions { set; get; }

         public DateTime ClosedAt { set; get; } 
        /// <summary>
        /// Json object
        /// </summary>
        public string BillingAddress { set; get; }
        public string CustomerPhone { set; get; }
        public string HFDStatus { set; get; }
        //public string CustomerId { set; get; }
        public string ProcessingMethod { set; get; }

        /// <summary>
        /// The total discounts applied to the price of the order in the shop currency.
        /// </summary>
        public string TotalDiscounts { set; get; }
        /// <summary>
        /// The price of the order in the shop currency after discounts but before shipping, taxes, and tips.
        /// </summary>
        public string SubTotalPrice { set; get; }


    }
}
