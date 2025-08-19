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

        var TriviaResponseId = (int)getQuizCmdResponse;

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

        var UserId = (string)getUserCmdResponse;

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
        }

        var getUserQuizCmd1 = new SqlCommand(@"SELECT TOP 1 Id
                                              FROM UserQuiz
                                              WHERE UserId = @UserId1 AND CAST(QuizDate AS DATE) = @Date1"
                                              , connection);
        getUserQuizCmd1.Parameters.AddWithValue("@UserId1", UserId);
        getUserQuizCmd1.Parameters.AddWithValue("@Date1", quizDate);

        var getUserQuizCmdResponse1 = await getUserQuizCmd.ExecuteScalarAsync();

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

    public class UpdateUserStatisticsRequest
    {
        public string Username { get; set; }
        public string Date { get; set; }
        public int CorrectAnswers { get; set; }
    }

    public class UserStatistics
    {
        public long QuizzesSubmitted { get; set; }
        public double AverageScore { get; set; }
        public long NumPerfectScores { get; set; }
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

        if (quizDate.ToString("yyyy-MM-dd") != DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Bad Request. Can't update answers on a previous days quiz");
            await badRequest.WriteStringAsync("Bad Request. Can't update answers on a previous days quiz");
            return badRequest;
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

        var UserId = (string)getUserCmdResponse;

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
            _logger.LogInformation(content);
        }

        var UserQuizId = (int)getUserQuizCmdResponse;

        // Update UserOptions for the UserQuiz
        var updateUserOptionsCmd = new SqlCommand(@"UPDATE UserQuiz
                                                    SET UserOptions = @UserOptions,
                                                        IsSubmitted = @IsSubmitted
                                                    WHERE Id = @UserQuizId"
                                                    , connection);
        var userOptionsJson = JsonSerializer.Serialize(requestBody.UserOptions);
        _logger.LogInformation(userOptionsJson);
        _logger.LogInformation(UserQuizId.ToString());
        updateUserOptionsCmd.Parameters.AddWithValue("@UserOptions", userOptionsJson);
        updateUserOptionsCmd.Parameters.AddWithValue("@UserQuizId", UserQuizId);
        updateUserOptionsCmd.Parameters.AddWithValue("@IsSubmitted", false);

        var rowsAffected = await updateUserOptionsCmd.ExecuteNonQueryAsync();
        _logger.LogInformation($"Rows affected: {rowsAffected}");


        // var updateUserOptionsCmdResponse = await updateUserOptionsCmd.ExecuteScalarAsync();

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

    [Function("SubmitUserAnswers")]
    public async Task<HttpResponseData> SubmitUserAnswers([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "username/quiz/useroptions/submit")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request");

        var requestBody = await req.ReadFromJsonAsync<UpdateUserAnswersRequest>();

        if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.Username) || string.IsNullOrEmpty(requestBody.Username))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Missing or invalid request body.");
            await badResponse.WriteStringAsync("Missing or invalid request body.");
            return badResponse;
        }

        // Here we would check if the date matched in is today. Otherwise they can't submit a quiz
        if (!DateOnly.TryParse(requestBody.Date, out var quizDate))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Invalid 'date' format. Use YYYY-MM-DD.");
            await badResponse.WriteStringAsync("Invalid 'date' format. Use YYYY-MM-DD");
            return badResponse;
        }

        if (quizDate.ToString("yyyy-MM-dd") != DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Bad Request. Can't submit a previous days quiz");
            await badRequest.WriteStringAsync("Bad Request. Can't submit a previous days quiz");
            return badRequest;
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

        // Grab the UserQuiz corresponding to the UserId and QuizDate
        var getUserQuizCmd = new SqlCommand(@"SELECT TOP 1 Id
                                              FROM UserQuiz
                                              WHERE UserId = @UserId AND CAST(QuizDate AS DATE) = @Date"
                                              , connection);
        getUserQuizCmd.Parameters.AddWithValue("@UserId", UserId);
        getUserQuizCmd.Parameters.AddWithValue("@Date", quizDate);

        var getUserQuizCmdResponse = await getUserQuizCmd.ExecuteScalarAsync();

        if (getUserQuizCmdResponse == null)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Creating new UserQuiz with the given UserId and QuizDate");
            var httpClient = new HttpClient();
            var responseFromGet = await httpClient.GetAsync($"http://localhost:7279/api/username/quiz?username={requestBody.Username}&quiz={requestBody.Date}");

            var content = await responseFromGet.Content.ReadAsStringAsync();
            _logger.LogInformation(content);
        }

        var UserQuizId = (int) getUserQuizCmdResponse;

        // Update UserOptions and mark the UserQuiz as Submitted
        var updateUserOptionsCmd = new SqlCommand(@"UPDATE UserQuiz
                                                    SET UserOptions = @UserOptions,
                                                        IsSubmitted = @IsSubmitted
                                                    WHERE Id = @UserQuizId"
                                                    , connection);
        var userOptionsJson = JsonSerializer.Serialize(requestBody.UserOptions);
        updateUserOptionsCmd.Parameters.AddWithValue("@UserOptions", userOptionsJson);
        updateUserOptionsCmd.Parameters.AddWithValue("@UserQuizId", UserQuizId);
        updateUserOptionsCmd.Parameters.AddWithValue("@IsSubmitted", true);
        
        _logger.LogInformation("Successfully Updated and Submitted UserQuiz for the given username and quizdate");

        var response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }

    [Function("GetUserStatistics")]
    public async Task<HttpResponseData> GetUserStatistics([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "username/userstatistics")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var username = query["username"];

        if (string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(username))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Missing or invalid request body.");
            return badResponse;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Get UserId from ApplicationUser
        _logger.LogInformation("Querying AspNetUsers using username parameter to get UserId");

        var getUserCmd = new SqlCommand(@"SELECT TOP 1 Id, QuizzesSubmitted, AverageScore, NumPerfectScores
                                          FROM AspNetUsers
                                          WHERE UserName = @UserName"
                                          , connection);
        getUserCmd.Parameters.AddWithValue("@UserName", username);

        UserStatistics? userstatistics = null;

        using (var reader = await getUserCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var result = new UserStatistics
                {
                    QuizzesSubmitted = reader["QuizzesSubmitted"] != DBNull.Value
                        ? Convert.ToInt32(reader["QuizzesSubmitted"])
                        : 0,
                    AverageScore = reader["AverageScore"] != DBNull.Value
                        ? Convert.ToInt32(reader["AverageScore"])
                        : 0,
                    NumPerfectScores = reader["NumPerfectScores"] != DBNull.Value
                        ? Convert.ToInt32(reader["NumPerfectScores"])
                        : 0,
                };

                userstatistics = result;
            }
        }

        if (userstatistics == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            _logger.LogInformation("No UserStatistics were found for the given user, or the UserId given is invalid.");
            await notFound.WriteStringAsync("No UserStatistics were found for the given user, or the UserId given is invalid.");
            return notFound;
        }

        _logger.LogInformation("Successfully gathered the UserStatistics data for the given user");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(userstatistics);
        return response;
    }

    [Function("UpdateUserStatistics")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "username/quiz/updateStatistics")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var requestBody = await req.ReadFromJsonAsync<UpdateUserStatisticsRequest>();

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

        if (quizDate.ToString("yyyy-MM-dd") != DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Bad Request. Can't update answers on a previous days quiz");
            await badRequest.WriteStringAsync("Bad Request. Can't update answers on a previous days quiz");
            return badRequest;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Get UserId from ApplicationUser
        _logger.LogInformation("Querying AspNetUsers using username parameter to get UserId");

        var getUserCmd = new SqlCommand(@"SELECT TOP 1 Id, QuizzesSubmitted, AverageScore, NumPerfectScores
                                          FROM AspNetUsers
                                          WHERE UserName = @UserName"
                                          , connection);
        getUserCmd.Parameters.AddWithValue("@UserName", requestBody.Username);

        string? UserId = null;
        int QuizzesSubmitted = 0;
        int AverageScore = 0;
        int NumPerfectScores = 0;

        using (var reader = await getUserCmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                UserId = reader["Id"].ToString();
                QuizzesSubmitted = reader["QuizzesSubmitted"] != DBNull.Value
                    ? Convert.ToInt32(reader["QuizzesSubmitted"])
                    : 0;
                AverageScore = reader["AverageScore"] != DBNull.Value
                    ? Convert.ToInt32(reader["AverageScore"])
                    : 0;
                NumPerfectScores = reader["NumPerfectScores"] != DBNull.Value
                    ? Convert.ToInt32(reader["NumPerfectScores"])
                    : 0;
            }
        }

        int NewAverageScore = (AverageScore * (QuizzesSubmitted - 1) + requestBody.CorrectAnswers) / QuizzesSubmitted;
        
        var updateUserStatisticsCmd = new SqlCommand(@"UPDATE AspNetUsers
                                                       SET QuizzesSubmitted = @QuizzesSubmitted
                                                           AverageScore = @AverageScore
                                                           NumPerfectScores = @NumPerfectScores"
                                                       , connection);
        updateUserStatisticsCmd.Parameters.AddWithValue("@QuizzesSubmitted", QuizzesSubmitted++);
        updateUserStatisticsCmd.Parameters.AddWithValue("@AverageScore", NewAverageScore);
        if (requestBody.CorrectAnswers == 10)
        {
            updateUserStatisticsCmd.Parameters.AddWithValue("@NumPerfectScores", NumPerfectScores++);
        }
        else
        {
            updateUserStatisticsCmd.Parameters.AddWithValue("@NumPerfectScores", NumPerfectScores);
        }

        _logger.LogInformation("Successfully Updated UserStatistics following Quiz Submission");

        var response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }
}