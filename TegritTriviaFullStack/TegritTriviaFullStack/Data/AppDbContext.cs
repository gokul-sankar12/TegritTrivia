using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TegritTrivia.Models;
using TegritTriviaFullStack.Models;

namespace TegritTriviaFullStack.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
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
        public DbSet<UserQuiz> UserQuiz { get; set; }
        public DbSet<Wordle> Wordle { get; set; }
        public DbSet<UserWordle> UserWordle { get; set; }
        public DbSet<ValidWords> ValidWords { get; set; }
    }
}
