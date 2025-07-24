using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TegritTrivia.Models;
using TegritTriviaFullStack.Data;

namespace TegritTriviaFullStack.Models
{
    public class UserQuiz
    {
        [Key]
        public int Id { get; set; }

        public DateOnly QuizDate { get; set; }

        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser ApplicationUser { get; set; }

        public int TriviaResponseId { get; set; }

        [ForeignKey(nameof(TriviaResponseId))]
        public TriviaResponse? TriviaResponse { get; set; }

        public bool IsSubmitted { get; set; }

        public DateTime StartedAt { get; set; }

        // public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();

        public string UserOptions { get; set; } = string.Empty;
    }
}
