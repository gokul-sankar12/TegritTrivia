using System.ComponentModel.DataAnnotations;

namespace TegritTriviaFullStack.Models
{
    public class ValidWords
    {
        [Key]
        public int Id { get; set; }

        public string Word { get; set; }
    }
}
