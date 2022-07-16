using System.Collections.Generic;

namespace VMBackuper.Models.DTOs.Responses
{
    public class AuthLogoutResponseDTO
    {
        public bool Success { get; set; }
        public List<string> Errors { get; set; }
    }
}
