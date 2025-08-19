using Microsoft.AspNetCore.Identity;

namespace TegritTriviaFullStack.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public long QuizzesSubmitted { get; set; }

        public double AverageScore { get; set; }

        public long NumPerfectScores { get; set; }
    }
}
