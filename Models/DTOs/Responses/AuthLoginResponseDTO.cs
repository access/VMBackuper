using VMBackuperBeckEnd.Configuration;
using VMBackuperBeckEnd.Models;

namespace VMBackuper.Models.DTOs.Responses
{
    public class AuthLoginResponseDTO : AuthResult
    {
        public UserAccount UserAccount { get; set; }
    }
}
