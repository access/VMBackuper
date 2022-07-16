using System.ComponentModel.DataAnnotations;

namespace VMBackuperBeckEnd.Models.DTOs.Requests
{
    public class AuthLoginRequestDTO
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
    }
}