using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncAppEntities.ViewModel
{
    public class FileModel
    {
        public FileContent DetailedFile { get; set; }
        public FileContent SummarizedFile { get; set; }
        public FileContent InvalidProducts { get; set; }
    }

    public class FileContent
    {
        public string FileName { get; set; }
        public byte[] FileData { get; set; }
        public string FileContentType { get; set; }
    }
}
