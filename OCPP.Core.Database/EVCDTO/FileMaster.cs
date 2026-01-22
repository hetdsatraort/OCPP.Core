using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class FileMaster
    {
        public string RecId { get; set; }
        public string UserId { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string FileURL { get; set; }
        public byte[] FileContent { get; set; }
        public long FileSize { get; set; }
        public string Remarks { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
