using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace VMBackuperBeckEnd.Models
{
    public class UserAccount : IdentityUser
    {
        public DateTime ActiveStart { get; set; }
        public DateTimeOffset ActiveUntil { get; set; }
        [JsonIgnore]
        public List<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }

    [Owned]
    public class RefreshToken
    {
        [Key]
        public int Id { get; set; }
        public string Token { get; set; }
        public DateTime Expires { get; set; }
        public bool IsExpired => DateTime.UtcNow >= Expires;
        public DateTime Created { get; set; }
        public DateTime? Revoked { get; set; }
        public string ReplacedByToken { get; set; }
        public bool IsActive => Revoked == null && !IsExpired;
    }

}
