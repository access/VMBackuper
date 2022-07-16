namespace VMBackuper.Models
{
    public class VirtualMachine
    {
        public int VmId { get; set; }
        public string Name { get; set; }
        public string File { get; set; }
        public string GuestOS { get; set; }
        public string Version { get; set; }
        public string Annotation { get; set; }
        public string Data { get; set; }
    }
}
