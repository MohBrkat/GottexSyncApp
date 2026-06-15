using System.Collections.Generic;

namespace SyncAppEntities.ViewModel
{
    public class FileModel
    {
        public FileContent DetailedFile { get; set; }
        public FileContent SummarizedFile { get; set; }
        public List<FileContent> ShippingFiles { get; set; }
        public FileContent InvalidProducts { get; set; }
    }

    public class FileContent
    {
        public string FileName { get; set; }
        public byte[] FileData { get; set; }
        public string FileContentType { get; set; }
    }
}
