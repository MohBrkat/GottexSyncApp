using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models.PayPlus
{
    public class ClearingCompaniesResult
    {
        public List<ClearingCompany> clearing { get; set; }
    }

    public class ClearingCompany
    {
        public int id { get; set; }
        public int code { get; set; }
        public string name { get; set; }
    }
}
