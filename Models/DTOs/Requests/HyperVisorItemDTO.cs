namespace VMBackuper.Models
{
    public class HyperVisorItemAddDTO
    {
        public string Name { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public override string ToString() => $"Name: {Name} Hostname: {HostName} Port: {Port} UserName: {UserName} Password: {Password}";
    }
}
