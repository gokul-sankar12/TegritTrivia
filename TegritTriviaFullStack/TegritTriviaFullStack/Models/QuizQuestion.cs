namespace TegritTrivia.Models
{
    public class QuizQuestion
    {
        public string Question { get; set; }

        public List<string> Options { get; set; }

        public int CorrectAnswer { get; set; }
    }
}
