using System.Collections.Generic;

namespace VMBackuperBeckEnd.Configuration
{
    public class AuthResult
    {
        public string AccessToken { get; set; }
        public bool Success { get; set; }
        public List<string> Errors { get; set; }
    }
}
