using System.ComponentModel.DataAnnotations;

namespace VMBackuper.Models.DTOs.Requests
{
    public class AuthChangePasswordDTO
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string OldPassword { get; set; }
        [Required]
        public string NewPassword { get; set; }
        public override string ToString() => $"Username: {Username} OldPassword: {OldPassword} NewPassword: {NewPassword}";
    }
}
