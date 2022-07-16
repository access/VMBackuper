using VMBackuperBeckEnd.Configuration;

namespace VMBackuperBeckEnd.Models.DTOs.Responses
{
    public class AuthRegistrationResponseDTO : AuthResult
    {
        public UserAccount UserAccount { get; set; }
    }
}