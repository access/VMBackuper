using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VMBackuper.Models;

namespace VMBackuperBeckEnd.Models
{
    public class AppDbContext : IdentityDbContext<UserAccount>
    {
        public DbSet<HyperVisorItem> HyperVisors { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            Database.EnsureCreated();
            Database.Migrate();
        }
    }
}
