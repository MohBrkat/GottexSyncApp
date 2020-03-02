using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models
{
    public class FileInformation
    {
        public string fileName { get; set; }
        public bool isValid { get; set; }
        public int lsErrorCount { get; set; }
        public int lsSuccessCount { get; set; }
        public List<string> LsOfErrors { set; get; }
        public List<string> LsOfSucess { set; get; }
        public List<string> fileRows { set; get; }
        public IFormFile File { set; get; }

    }
}
