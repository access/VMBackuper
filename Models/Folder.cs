using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VMBackuper.Models
{
    public class Folder
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public List<Folder> Directories { get; set; } = new List<Folder>();
        public bool IsRecoveryItem { get; set; } = false;
    }
}
