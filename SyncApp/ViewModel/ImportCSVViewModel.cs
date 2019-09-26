using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShopifyApp2.ViewModel
{
    public class ImportCSVViewModel
    {
        public List<string> LsOfErrors { set; get; }
        public List<string> LsOfSucess { set; get; }

        public int? ErrorCount { set; get; }
        public int? SucessCount { set; get; }

        public bool Validate { set; get; }

        public IFormFile File { set; get; }
    }
}
