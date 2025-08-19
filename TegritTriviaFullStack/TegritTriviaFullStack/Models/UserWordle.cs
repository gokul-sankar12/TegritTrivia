using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TegritTriviaFullStack.Data;

namespace TegritTriviaFullStack.Models
{
    public class UserWordle
    {
        [Key]
        public int Id { get; set; }

        public DateOnly Date { get; set; }

        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser ApplicationUser { get; set; }

        public string UserSubmissions { get; set; } = string.Empty;

        // This variable doesn't neccessarily indicate whether the user was successful
        public bool WordleCompleted { get; set; }
    }
}
