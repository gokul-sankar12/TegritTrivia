using Microsoft.EntityFrameworkCore;
using TegritTrivia.Models;

namespace TegritTriviaFullStack.Data
{
    public class AppDbContext : DbContext
    {
        public IConfiguration _config { get; set; }

        public AppDbContext(IConfiguration config)
        {
            _config = config;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_config.GetConnectionString("DefaultConnection"));
        }

        public DbSet<QuizForm> QuizForm { get; set; }
        public DbSet<TriviaResponse> TriviaResponse { get; set; }
        public DbSet<TriviaResults> TriviaResults { get; set; }
    }
}
