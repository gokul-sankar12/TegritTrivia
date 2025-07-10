using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace TegritTrivia.Models
{
    public class TriviaResponse
    {
        [Key]
        public int Id { get; set; }
        
        public int ResponseCode { get; set; }

        public DateOnly QuizDate { get; set; }

        public ICollection<TriviaResults>? Results { get; set; } = new List<TriviaResults>();
    }

    public class TriviaResults
    {
        public TriviaResults() { }
        
        [JsonConstructor]
        public TriviaResults(string? type, string? difficulty, string? category, string? question, string? correct_Answer, List<string>? incorrect_Answers)
        {
            Type = type;
            Difficulty = difficulty;
            Category = category;
            Question = question;
            Correct_Answer = correct_Answer;
            Incorrect_Answers = incorrect_Answers;
            
            // Shuffle the possible multiple choice options
            Options = incorrect_Answers.ToList();
            Options.Add(correct_Answer);
            ListExtensions.Shuffle(Options);
        }

        [Key]
        public int Id { get; set; }

        public int TriviaResponseId { get; set; }
        
        [ForeignKey(nameof(TriviaResponseId))]
        public TriviaResponse? TriviaResponse { get; set; }

        public string? Type { get; set; }

        public string? Difficulty { get; set; }

        public string? Category { get; set; }

        public string? Question { get; set; }

        public string? Correct_Answer { get; set; }

        public List<string>? Incorrect_Answers { get; set; }

        public List<string>? Options { get; set; }

        public string? IncorrectAnswersJson
        {
            get => JsonSerializer.Serialize(Incorrect_Answers);
            set => Incorrect_Answers = string.IsNullOrEmpty(value) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(value);
        }
        public string? OptionsJson
        {
            get => JsonSerializer.Serialize(Options);
            set => Options = string.IsNullOrEmpty(value) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(value);
        }

        public string? SelectedOption { get; set; }
    }

    public static class ListExtensions
    {
        private static Random rng = new Random();

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
