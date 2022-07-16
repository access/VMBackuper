using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VMBackuper.Models.DTOs
{
    public class RecoveryImageDTO
    {
        public HyperVisorItem HyperVisor { get; set; }
        public Folder Folder { get; set; }
    }
}
