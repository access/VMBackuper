using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace VMBackuper.Models
{
    public class HyperVisorItem
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string HostName { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        [JsonIgnore]
        public string PrivateKeyFileName { get; set; }
        [JsonIgnore]
        public string PublicKeyFileName { get; set; }
        [JsonIgnore]
        public string PrivateKeyPassPhrase { get; set; }
        [JsonIgnore]
        public string GhettoBackupConfigFileName { get; set; } = "VMBackupperBackup.conf";
        public DateTime DateAdded { get; set; }
        public override string ToString() => $"Name: {Name} Hostname: {HostName} Port: {Port} UserName: {UserName} PrivateKeyFileName: {PrivateKeyFileName} DateAdded: {DateAdded}";
    }
}
