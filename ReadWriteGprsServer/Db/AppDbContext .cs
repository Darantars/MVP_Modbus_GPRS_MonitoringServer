using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Read_Write_GPRS_Server.Db
{
    public class ApplicationDbContext : IdentityDbContext
    {
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Дополнительные настройки модели
        }
    }
}

