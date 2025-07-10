using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;
using TegritTrivia.Models;

namespace TegritTriviaFullStack.Data
{
    public class QuizForm
    {
        [Key]
        public int Id { get; set; }

        public DateOnly FormDate { get; set; }

        [ForeignKey(nameof(TriviaResponse))]
        public int TriviaResponseId { get; set; }
    }
}