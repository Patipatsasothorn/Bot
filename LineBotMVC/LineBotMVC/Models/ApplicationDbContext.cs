using Microsoft.EntityFrameworkCore;

namespace LineBotMVC.Models
{

    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<BotCommand> BotCommands { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<LineBot> LineBots { get; set; }

    }

}
