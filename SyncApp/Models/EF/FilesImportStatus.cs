using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncApp.Models.EF
{
    public partial class FilesImportStatus
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsImportSuccess { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
    }
}
