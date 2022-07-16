using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VMBackuper.Models.DTOs
{
    public class SaveGhettoConfigDTO
    {
        public HyperVisorItem HyperVisor { get; set; }
        public string Config { get; set; }
    }
}
