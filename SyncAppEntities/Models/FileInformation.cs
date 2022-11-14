using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace SyncAppEntities.Models
{
    public class FileInformation
    {
        public string fileName { get; set; }
        public bool isValid { get; set; }
        public int lsErrorCount { get; set; }
        public int lsSuccessCount { get; set; }
        public List<string> LsOfErrors { set; get; } = new List<string>();
        public List<string> LsOfSucess { set; get; } = new List<string>();
        public List<string> fileRows { set; get; }
        public IFormFile File { set; get; }

    }
}
