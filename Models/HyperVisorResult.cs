using System.Collections.Generic;

namespace VMBackuper.Models
{
    public class HyperVisorResult
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; }
    }
}
