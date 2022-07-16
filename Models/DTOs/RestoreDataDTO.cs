using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VMBackuper.Models.DTOs
{
    public class RestoreDataDTO
    {
        public HyperVisorItem HyperVisor { get; set; }
        public RestoreConfig RestoreConfig { get; set; }
    }

    public class RestoreConfig
    {
        public int DiskFormat { get; set; }
        public string ImagePath { get; set; }
        public string RestorePath { get; set; }
        public string NameOfRestoreVM { get; set; }
        public override string ToString() => $"Format: {DiskFormat} RestorePath: {RestorePath} ImagePath:{ImagePath} Name: {NameOfRestoreVM}";
    }
}
