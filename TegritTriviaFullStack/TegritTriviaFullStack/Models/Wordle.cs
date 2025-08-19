using System.ComponentModel.DataAnnotations;

namespace TegritTriviaFullStack.Models
{
    public class Wordle
    {
        [Key]
        public int Id { get; set; }

        public string Word { get; set; }

        public DateOnly Date { get; set; }
    }
}
