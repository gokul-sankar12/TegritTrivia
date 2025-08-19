using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TegritTrivia.Models;

namespace CreateNewQuiz;

public class CreateNewQuiz
{
    private readonly ILogger _logger;
    private readonly string? _connectionString;

    public CreateNewQuiz(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CreateNewQuiz>();
        _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    }

    // Function will run at 9:30 AM every weekday 0 30 9 * * 1-5
    // 0 * * * * * to run the Function every minute
    [Function("CreateNewQuiz")]
    public async Task Run([TimerTrigger("0 30 9 * * 1-5")] TimerInfo myTimer)
    {
        _logger.LogInformation($"Function triggered at: {DateTime.Now}");

        var today = DateTime.UtcNow.Date;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var checkCmd = new SqlCommand("SELECT COUNT(*) FROM QuizForm WHERE CAST(FormDate AS DATE) = @Today", connection);
        checkCmd.Parameters.AddWithValue("@Today", today);

        var count = (int) await checkCmd.ExecuteScalarAsync();

        if (count == 0)
        {
            // Call Trivia API to get TriviaResponse
            _logger.LogInformation("Attempting to call Quiz Api");
            var client = new HttpClient();
            var response = client.GetFromJsonAsync<TriviaResponse>("https://opentdb.com/api.php?amount=10");

            if (response == null || response.Result == null || response.Result.Results.IsNullOrEmpty())
            {
                _logger.LogWarning("No trivia data returned.");
                return;
            }

            // Insert Trivia Response
            _logger.LogInformation("Creating row in TriviaResponse");
            var insertTriviaResponseCmd = new SqlCommand(@"INSERT INTO TriviaResponse (ResponseCode, QuizDate) 
                                                           OUTPUT INSERTED.Id 
                                                           VALUES (@ResponseCode, @QuizDate)"
                                                           , connection);
            insertTriviaResponseCmd.Parameters.AddWithValue("@ResponseCode", response.Result.ResponseCode);
            insertTriviaResponseCmd.Parameters.AddWithValue("@QuizDate", today);

            var TriviaResponseId = (int) await insertTriviaResponseCmd.ExecuteScalarAsync();

            // Insert each Trivia Result
            _logger.LogInformation("Creating rows in TriviaResults");
            foreach (var result in response.Result.Results)
            {
                var incorrectAnswersJson = JsonSerializer.Serialize(result.Incorrect_Answers ?? new List<string>());
                var options = result.Incorrect_Answers?.ToList() ?? new List<string>();
                options.Add(result.Correct_Answer ?? "");
                ListExtensions.Shuffle(options);
                var optionsJson = JsonSerializer.Serialize(options);

                var insertResultCmd = new SqlCommand(@"
                    INSERT INTO TriviaResults (Type, Difficulty, Category, Question, Correct_Answer, SelectedOption, IncorrectAnswersJson, OptionsJson, TriviaResponseId)
                    VALUES (@Type, @Difficulty, @Category, @Question, @Correct_Answer, @SelectedOption, @IncorrectAnswersJson, @OptionsJson, @TriviaResponseId)"
                    , connection);

                insertResultCmd.Parameters.AddWithValue("@Type", result.Type ?? (object) DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@Difficulty", result.Difficulty ?? (object) DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@Category", result.Category ?? (object) DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@Question", result.Question ?? (object) DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@Correct_Answer", result.Correct_Answer ?? (object) DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@SelectedOption", result.SelectedOption ?? (object) DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@IncorrectAnswersJson", incorrectAnswersJson);
                insertResultCmd.Parameters.AddWithValue("@OptionsJson", optionsJson);
                insertResultCmd.Parameters.AddWithValue("@TriviaResponseId", TriviaResponseId);

                await insertResultCmd.ExecuteNonQueryAsync();
            }

            // Insert Quiz Form
            _logger.LogInformation("Creating row in QuizForm");
            var insertQuizFormCmd = new SqlCommand(@"INSERT INTO QuizForm (FormDate, TriviaResponseId)
                                                     VALUES (@FormDate, @TriviaResponseId)"
                                                     , connection);
            insertQuizFormCmd.Parameters.AddWithValue("@FormDate", today);
            insertQuizFormCmd.Parameters.AddWithValue("@TriviaResponseId", TriviaResponseId);

            await insertQuizFormCmd.ExecuteNonQueryAsync();

            _logger.LogInformation("New Quiz Created and Added To Azure SQL Database");
        }
        else
        {
            _logger.LogInformation("Row for today already exists.");
        }

        await connection.CloseAsync();
    }

    [Function("CreateNewQuiz_HttpTrigger")]
    public async Task<HttpResponseData> CreateNewQuiz_HttpTrigger([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "createNewQuiz")] HttpRequestData req)
    {
        _logger.LogInformation("C# Http trigger function processed a request");

        var today = DateTime.UtcNow.Date;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var checkCmd = new SqlCommand("SELECT COUNT(*) FROM QuizForm WHERE CAST(FormDate AS DATE) = @Today", connection);
        checkCmd.Parameters.AddWithValue("@Today", today);

        var count = (int)await checkCmd.ExecuteScalarAsync();

        if (count == 0)
        {
            // Call Trivia API to get TriviaResponse
            _logger.LogInformation("Attempting to call Quiz Api");
            var client = new HttpClient();
            var TriviaApiResponse = client.GetFromJsonAsync<TriviaResponse>("https://opentdb.com/api.php?amount=10");

            if (TriviaApiResponse == null || TriviaApiResponse.Result == null || TriviaApiResponse.Result.Results.IsNullOrEmpty())
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                _logger.LogWarning("No trivia data returned when querying the api");
                await notFound.WriteStringAsync("No trivia data returned when querying the api");
                return notFound;
            }

            // Insert Trivia Response
            _logger.LogInformation("Creating row in TriviaResponse");
            var insertTriviaResponseCmd = new SqlCommand(@"INSERT INTO TriviaResponse (ResponseCode, QuizDate) 
                                                           OUTPUT INSERTED.Id 
                                                           VALUES (@ResponseCode, @QuizDate)"
                                                           , connection);
            insertTriviaResponseCmd.Parameters.AddWithValue("@ResponseCode", TriviaApiResponse.Result.ResponseCode);
            insertTriviaResponseCmd.Parameters.AddWithValue("@QuizDate", today);

            var TriviaResponseId = (int)await insertTriviaResponseCmd.ExecuteScalarAsync();

            // Insert each Trivia Result
            _logger.LogInformation("Creating rows in TriviaResults");
            foreach (var result in  TriviaApiResponse.Result.Results)
            {
                var incorrectAnswersJson = JsonSerializer.Serialize(result.Incorrect_Answers ?? new List<string>());
                var options = result.Incorrect_Answers?.ToList() ?? new List<string>();
                options.Add(result.Correct_Answer ?? "");
                ListExtensions.Shuffle(options);
                var optionsJson = JsonSerializer.Serialize(options);

                var insertResultCmd = new SqlCommand(@"
                    INSERT INTO TriviaResults (Type, Difficulty, Category, Question, Correct_Answer, SelectedOption, IncorrectAnswersJson, OptionsJson, TriviaResponseId)
                    VALUES (@Type, @Difficulty, @Category, @Question, @Correct_Answer, @SelectedOption, @IncorrectAnswersJson, @OptionsJson, @TriviaResponseId)"
                    , connection);

                insertResultCmd.Parameters.AddWithValue("@Type", result.Type ?? (object)DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@Difficulty", result.Difficulty ?? (object)DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@Category", result.Category ?? (object)DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@Question", result.Question ?? (object)DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@Correct_Answer", result.Correct_Answer ?? (object)DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@SelectedOption", result.SelectedOption ?? (object)DBNull.Value);
                insertResultCmd.Parameters.AddWithValue("@IncorrectAnswersJson", incorrectAnswersJson);
                insertResultCmd.Parameters.AddWithValue("@OptionsJson", optionsJson);
                insertResultCmd.Parameters.AddWithValue("@TriviaResponseId", TriviaResponseId);

                await insertResultCmd.ExecuteNonQueryAsync();
            }

            // Insert Quiz Form
            _logger.LogInformation("Creating row in QuizForm");
            var insertQuizFormCmd = new SqlCommand(@"INSERT INTO QuizForm (FormDate, TriviaResponseId)
                                                     VALUES (@FormDate, @TriviaResponseId)"
                                                     , connection);
            insertQuizFormCmd.Parameters.AddWithValue("@FormDate", today);
            insertQuizFormCmd.Parameters.AddWithValue("@TriviaResponseId", TriviaResponseId);

            await insertQuizFormCmd.ExecuteNonQueryAsync();

            _logger.LogInformation("New Quiz Created and Added To Azure SQL Database");
        }
        else
        {
            _logger.LogInformation("Row for today already exists.");
        }

        await connection.CloseAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Quiz created successfully");
        return response;
    }
}