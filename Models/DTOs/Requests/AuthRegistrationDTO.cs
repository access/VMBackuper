using System.ComponentModel.DataAnnotations;

namespace VMBackuperBeckEnd.Models.DTOs.Requests
{
    public class AuthRegistrationDTO
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }

        public override string ToString() => $"Username: {Username} Password: {Password}";
    }
}