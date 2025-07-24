using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using TegritTrivia.Models;
namespace GetQuizByDate;

public class GetQuizByDate
{
    private readonly ILogger<GetQuizByDate> _logger;

    public GetQuizByDate(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GetQuizByDate>();
    }

    [Function("GetQuizByDate")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "quiz")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var dateParam = query["date"];

        if (!DateOnly.TryParse(dateParam, out var quizDate))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Invalid or missing 'date' parameter. Use format YYYY-MM-DD.");
            await badResponse.WriteStringAsync("Invalid or missing 'date' parameter. Use format YYYY-MM-DD.");
            return badResponse;
        }

        var _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        _logger.LogInformation(_connectionString);

        using var connection = new SqlConnection(_connectionString);
        _logger.LogInformation("Come this far");
        await connection.OpenAsync();
        _logger.LogInformation("Able to open a stable connection");

        // Get TriviaResponseId from QuizForm
        _logger.LogInformation("Querying QuizForm using date parameter to get TriviaResponseId");
        var getQuizCmd = new SqlCommand(@"SELECT TOP 1 TriviaResponseId
                                          FROM QuizForm
                                          WHERE CAST(FormDate AS DATE) = @Date"
                                          , connection);
        getQuizCmd.Parameters.AddWithValue("@Date", quizDate);

        var getQuizCmdResponse = await getQuizCmd.ExecuteScalarAsync();

        if (getQuizCmdResponse == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            _logger.LogInformation("No quiz found for the given date.");
            await notFound.WriteStringAsync("No quiz found for the given date.");
            return notFound;
        }

        var TriviaResponseId = (int) getQuizCmdResponse;

        // Get TriviaResponse using TriviaResponseId and create TriviaResponse instance
        _logger.LogInformation("Querying TriviaResponse using TriviaResponseId");
        var getTriviaResponseCmd = new SqlCommand(@"SELECT Id, ResponseCode, QuizDate
                                                    FROM TriviaResponse
                                                    WHERE Id = @TriviaResponseId"
                                                    , connection);
        getTriviaResponseCmd.Parameters.AddWithValue("@TriviaResponseId", TriviaResponseId);

        TriviaResponse TriviaResponse = null;
        using (SqlDataReader reader = await getTriviaResponseCmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                TriviaResponse = new TriviaResponse()
                {
                    Id = reader.GetInt32(0),
                    ResponseCode = reader.GetInt32(1),
                    QuizDate = DateOnly.FromDateTime(reader.GetDateTime(2)),
                    Results = new List<TriviaResults>()
                };
            }
        }

        if (TriviaResponse == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            _logger.LogInformation("No TriviaResponse found for the given TriviaResponseId");
            await notFound.WriteStringAsync("No TriviaResponse found for the given TriviaResponseId");
            return notFound;
        }

        // Get TriviaResults using TriviaResponseId and create TriviaResults instance
        _logger.LogInformation("Querying TriviaResults using TriviaResponseId");
        var getTriviaResultsCmd = new SqlCommand(@"SELECT Id, Type, Difficulty, Category, Question, Correct_Answer, SelectedOption, IncorrectAnswersJson, OptionsJson
                                                   FROM TriviaResults
                                                   WHERE TriviaResponseId = @TriviaResponseId"
                                                   , connection);
        getTriviaResultsCmd.Parameters.AddWithValue("@TriviaResponseId", TriviaResponseId);

        using (var reader = await getTriviaResultsCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var result = new TriviaResults
                {
                    Id = reader.GetInt32(0),
                    TriviaResponseId = TriviaResponseId,
                    Type = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Difficulty = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Question = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Correct_Answer = reader.IsDBNull(5) ? null : reader.GetString(5),
                    SelectedOption = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Incorrect_Answers = JsonSerializer.Deserialize<List<string>>(reader.GetString(7)),
                    Options = JsonSerializer.Deserialize<List<string>>(reader.GetString(8))
                };

                TriviaResponse.Results.Add(result);
            }
        }

        if (TriviaResponse.Results.Count() < 0)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            _logger.LogInformation("No TriviaResults found for the given TriviaResponseId");
            await notFound.WriteStringAsync("No TriviaResults found for the given TrivaResponseId");
            return notFound;
        }

        _logger.LogInformation("Successfully created a TriviaResponse using the given date parameter");
        
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(TriviaResponse);
        return response;
    }
}