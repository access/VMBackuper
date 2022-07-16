using System;

namespace VMBackuperBeckEnd.Configuration
{
    public static class AppConfig
    {
        public static DateTime RefreshTokenLifeTime => DateTime.UtcNow.AddDays(1);
        public static DateTime AccessTokenLifeTime => DateTime.UtcNow.AddMinutes(20);
    }
}
