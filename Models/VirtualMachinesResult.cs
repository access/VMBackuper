using System.Collections.Generic;

namespace VMBackuper.Models
{
    public class VirtualMachinesResult
    {
        public List<VirtualMachine> VirtualMachines { get; set; } = new List<VirtualMachine>();
    }
}
