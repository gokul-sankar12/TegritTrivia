using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Data.SqlClient;
using System.Net;
using System.Text.Json;

namespace UserAnswers;

public class UserAnswer
{
    private readonly ILogger<UserAnswer> _logger;
    private readonly string _connectionString;

    public UserAnswer(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<UserAnswer>();
        _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    }

    [Function("CreateUserAnswers")]
    public async Task<HttpResponseData> CreateUserAnswers([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "username/quiz")] HttpRequestData req)
    {
        _logger.LogInformation("C# Http trigger function processed a request.");

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var username = query["username"];
        var dateParam = query["date"];

        if (!DateOnly.TryParse(dateParam, out var quizDate))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Invalid or missing 'date' parameter. Use format YYYY-MM-DD.");
            await badResponse.WriteStringAsync("Invalid or missing 'date' parameter. Use format YYYY-MM-DD.");
            return badResponse;
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(username))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Invalid or missing 'username' paramter. Make sure the username is a string.");
            await badResponse.WriteStringAsync("Invalid or missing 'username' paramter. Make sure the username is a string");
            return badResponse;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

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

        // Get UserId from ApplicationUser
        _logger.LogInformation("Querying AspNetUsers using username parameter to get UserId");
        var getUserCmd = new SqlCommand(@"SELECT TOP 1 Id
                                          FROM AspNetUsers
                                          WHERE UserName = @UserName"
                                          , connection);
        getUserCmd.Parameters.AddWithValue("@UserName", username);

        var getUserCmdResponse = await getUserCmd.ExecuteScalarAsync();

        if (getUserCmdResponse == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            _logger.LogInformation("No user found for the given username");
            await notFound.WriteStringAsync("No user found for the given username");
            return notFound;
        }

        var UserId = (string) getUserCmdResponse;

        // Create a blank UserQuiz instance
        _logger.LogInformation("Creating a UserQuiz using the given username and quiz.");

        var getUserQuizCmd = new SqlCommand(@"SELECT TOP 1 Id
                                              FROM UserQuiz
                                              WHERE UserId = @UserId AND TriviaResponseId = @TriviaResponseId"
                                              , connection);
        getUserQuizCmd.Parameters.AddWithValue("@UserId", UserId);
        getUserQuizCmd.Parameters.AddWithValue("@TriviaResponseId", TriviaResponseId);

        var getUserQuizCmdResponse = await getUserQuizCmd.ExecuteScalarAsync();

        if (getUserQuizCmdResponse != null)
        {
            var conflict = req.CreateResponse(HttpStatusCode.Conflict);
            _logger.LogInformation("A UserQuiz already exists for the given username and quizdate.");
            await conflict.WriteStringAsync("A UserQuiz already exists for the given username and quizdate.");
            return conflict;
        }

        var insertUserQuizCmd = new SqlCommand(@"INSERT INTO UserQuiz (QuizDate, UserId, TriviaResponseId, IsSubmitted, StartedAt, UserOptions)
                                                 OUTPUT INSERTED.UserOptions
                                                 VALUES (@QuizDate, @UserId, @TriviaResponseId, @IsSubmitted, @StartedAt, @UserOptions)"
                                                 , connection);
        insertUserQuizCmd.Parameters.AddWithValue("@QuizDate", quizDate);
        insertUserQuizCmd.Parameters.AddWithValue("@UserId", UserId);
        insertUserQuizCmd.Parameters.AddWithValue("@TriviaResponseId", TriviaResponseId);
        insertUserQuizCmd.Parameters.AddWithValue("@IsSubmitted", false);
        insertUserQuizCmd.Parameters.AddWithValue("@StartedAt", DateTime.UtcNow);
        insertUserQuizCmd.Parameters.AddWithValue("@UserOptions", "[]");

        try
        {
            var insertUserQuizCmdResponse = await insertUserQuizCmd.ExecuteScalarAsync();

            if (insertUserQuizCmdResponse == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                _logger.LogInformation("No UserQuiz was created for the given username and quizdate.");
                await notFound.WriteStringAsync("No UserQuiz was created for the given username and quizdate.");
                return notFound;
            }

            var UserQuizOptions = (string)insertUserQuizCmdResponse;

            _logger.LogInformation("Successfully created a UserAnswer using the given date parameter");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(UserQuizOptions);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting UserQuiz.");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while creating the UserQuiz.");
            return errorResponse;
        }
    }

    [Function("GetUserAnswers")]
    public async Task<HttpResponseData> GetUserAnswers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "username/quiz/get")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var username = query["username"];
        var dateParam = query["date"];

        if (!DateOnly.TryParse(dateParam, out var quizDate))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Invalid or missing 'date' parameter. Use format YYYY-MM-DD.");
            await badResponse.WriteStringAsync("Invalid or missing 'date' parameter. Use format YYYY-MM-DD.");
            return badResponse;
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(username))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Invalid or missing 'username' paramter. Make sure the username is a string.");
            await badResponse.WriteStringAsync("Invalid or missing 'username' paramter. Make sure the username is a string");
            return badResponse;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Get UserId from ApplicationUser
        _logger.LogInformation("Querying AspNetUsers using username parameter to get UserId");
        var getUserCmd = new SqlCommand(@"SELECT TOP 1 Id
                                          FROM AspNetUsers
                                          WHERE UserName = @UserName"
                                          , connection);
        getUserCmd.Parameters.AddWithValue("@UserName", username);

        var getUserCmdResponse = await getUserCmd.ExecuteScalarAsync();

        if (getUserCmdResponse == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            _logger.LogInformation("No user found for the given username");
            await notFound.WriteStringAsync("No user found for the given username");
            return notFound;
        }

        var UserId = (string) getUserCmdResponse;

        // If a UserQuiz doesn't exist call CreateUserAnswers function otherwise return the corresponding UserOptions
        var getUserQuizCmd = new SqlCommand(@"SELECT TOP 1 Id
                                              FROM UserQuiz
                                              WHERE UserId = @UserId AND CAST(QuizDate AS DATE) = @Date"
                                              , connection);
        getUserQuizCmd.Parameters.AddWithValue("@UserId", UserId);
        getUserQuizCmd.Parameters.AddWithValue("@Date", quizDate);

        var getUserQuizCmdResponse = await getUserQuizCmd.ExecuteScalarAsync();

        if (getUserQuizCmdResponse == null)
        {
            _logger.LogInformation("Enterred this if condition.");
            
            var httpClient = new HttpClient();
            var responseFromGet = await httpClient.PostAsync($"http://localhost:7279/api/username/quiz?username={username}&date={dateParam}", new StringContent(""));

            var content = await responseFromGet.Content.ReadAsStringAsync();

            var blankUserAnswersResponse = req.CreateResponse(HttpStatusCode.OK);
            await blankUserAnswersResponse.WriteAsJsonAsync($"{content}");
            return blankUserAnswersResponse;
        }

        var UserQuizId = (int) getUserQuizCmdResponse;

        var getUserOptionsCmd = new SqlCommand(@"SELECT TOP 1 UserOptions
                                                 FROM UserQuiz
                                                 WHERE Id = @UserQuizId"
                                                 , connection);
        getUserOptionsCmd.Parameters.AddWithValue("@UserQuizId", UserQuizId);

        var UserOptionsCmdResponse = await getUserOptionsCmd.ExecuteScalarAsync();

        if (UserOptionsCmdResponse == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            _logger.LogInformation("No UserOptions fonund for the given UserQuizId");
            await notFound.WriteStringAsync("No UserOptions found for the given UserQuizId");
            return notFound;
        }

        var UserOptions = (string) UserOptionsCmdResponse;

        _logger.LogInformation("Successfully returned a UserAnswer using the given username and quizdate.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(UserOptions);
        return response;
    }

    public class UpdateUserAnswersRequest
    {
        public string Username { get; set; }
        public string Date { get; set; }
        public List<string> UserOptions { get; set; }
    }

    [Function("UpdateUserAnswers")]
    public async Task<HttpResponseData> UpdateUserAnswers([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "username/quiz/useroptions")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        
        var requestBody = await req.ReadFromJsonAsync<UpdateUserAnswersRequest>();

        if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.Username) || string.IsNullOrWhiteSpace(requestBody.Date))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Missing or invalid request body.");
            return badResponse;
        }

        if (!DateOnly.TryParse(requestBody.Date, out var quizDate))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Invalid 'date' format. Use YYYY-MM-DD.");
            return badResponse;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Get UserId from ApplicationUser
        _logger.LogInformation("Querying AspNetUsers using username parameter to get UserId");
        var getUserCmd = new SqlCommand(@"SELECT TOP 1 Id
                                          FROM AspNetUsers
                                          WHERE UserName = @UserName"
                                          , connection);
        getUserCmd.Parameters.AddWithValue("@UserName", requestBody.Username);

        var getUserCmdResponse = await getUserCmd.ExecuteScalarAsync();

        if (getUserCmdResponse == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            _logger.LogInformation("No user found for the given username");
            await notFound.WriteStringAsync("No user found for the given username");
            return notFound;
        }

        var UserId = (string) getUserCmdResponse;

        // If a UserQuiz doesn't exist call CreateUserAnswers function otherwise return the corresponding UserOptions
        var getUserQuizCmd = new SqlCommand(@"SELECT TOP 1 Id
                                              FROM UserQuiz
                                              WHERE UserId = @UserId AND CAST(QuizDate AS DATE) = @Date"
                                              , connection);
        getUserQuizCmd.Parameters.AddWithValue("@UserId", UserId);
        getUserQuizCmd.Parameters.AddWithValue("@Date", quizDate);

        var getUserQuizCmdResponse = await getUserQuizCmd.ExecuteScalarAsync();

        if (getUserQuizCmdResponse == null)
        {
            var httpClient = new HttpClient();
            var responseFromGet = await httpClient.GetAsync($"http://localhost:7279/api/username/quiz?username={requestBody.Username}&quiz={requestBody.Date}");

            var content = await responseFromGet.Content.ReadAsStringAsync();

            var blankUserAnswersResponse = req.CreateResponse(HttpStatusCode.OK);
            await blankUserAnswersResponse.WriteAsJsonAsync($"Response from GetUserAnswers: {content}");
            return blankUserAnswersResponse;
        }

        var UserQuizId = (int) getUserQuizCmdResponse;

        var updateUserOptionsCmd = new SqlCommand(@"UPDATE UserQuiz
                                                    SET UserOptions = @UserOptions
                                                    WHERE Id = @UserQuizId"
                                                    , connection);
        var userOptionsJson = JsonSerializer.Serialize(requestBody.UserOptions);
        updateUserOptionsCmd.Parameters.AddWithValue("@UserOptions", userOptionsJson);
        updateUserOptionsCmd.Parameters.AddWithValue("@UserQuizId", UserQuizId);

        var updateUserOptionsCmdResponse = await updateUserOptionsCmd.ExecuteScalarAsync();

        //if (updateUserOptionsCmdResponse == null)
        //{
        //    var notFound = req.CreateResponse(HttpStatusCode.NotFound);
        //    _logger.LogInformation("No user found for the given username");
        //    await notFound.WriteStringAsync("No user found for the given username");
        //    return notFound;
        //}

        _logger.LogInformation("Successfully updated UserOptions for the given username and quizdate.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }
}