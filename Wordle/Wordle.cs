using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Wordle;

public class Wordle
{
    private readonly ILogger _logger;
    private readonly string? _connectionString;

    public Wordle(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<Wordle>();
        _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
    }

    [Function("CreateWordOfTheDay")]
    public async Task Run([TimerTrigger("0 30 9 * * 1-5")] TimerInfo myTimer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);

        var today = DateTime.UtcNow;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next timer schedule at: {nextSchedule}", myTimer.ScheduleStatus.Next);
        }

        var checkDupCmd = new SqlCommand("SELECT COUNT(*) FROM Wordle WHERE CAST(Date AS DATE) = @Today", connection);
        checkDupCmd.Parameters.AddWithValue("@Today", today);

        var count = (int) await checkDupCmd.ExecuteScalarAsync();

        if (count == 0)
        {
            var random = new Random();
            int randomNumber = random.Next(1, 14856); // Upper bound is exclusive

            string? selectedWord = string.Empty;

            var getWordCmd = new SqlCommand(@"SELECT TOP 1 Word
                                              FROM ValidWords
                                              WHERE Id = @Id"
                                              , connection);

            getWordCmd.Parameters.AddWithValue("@Id", randomNumber);

            using (var reader = await getWordCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    selectedWord = reader["Word"].ToString();
                }
            }

            var insertWordCmd = new SqlCommand(@"INSERT INTO Wordle (Word, Date)
                                                 OUTPUT INSERTED.ID
                                                 VALUES (@Word, @Date)"
                                                 , connection);
            insertWordCmd.Parameters.AddWithValue("@Word", selectedWord);
            insertWordCmd.Parameters.AddWithValue("@Date", today);

            var insertedWordId = await insertWordCmd.ExecuteScalarAsync();

            _logger.LogInformation("Word of the day successfully created and added to Azure SQL Database");
        }
        else
        {
            _logger.LogInformation("Word for today already exists");
        }

        await connection.CloseAsync();
    }

    [Function("CreateWordOfTheDay_HttpTrigger")]
    public async Task<HttpResponseData> CreateWordOfTheDay_HttpTrigger([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "createWordOfTheDay")] HttpRequestData req)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {executionTime}", DateTime.Now);

        var today = DateTime.Today;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var checkDupCmd = new SqlCommand("SELECT COUNT(*) FROM Wordle WHERE CAST(Date AS DATE) = @Today", connection);
        checkDupCmd.Parameters.AddWithValue("@Today", today);

        var count = (int)await checkDupCmd.ExecuteScalarAsync();

        if (count == 0)
        {
            var random = new Random();
            int randomNumber = random.Next(1, 14856); // Upper bound is exclusive

            string? selectedWord = string.Empty;

            var getWordCmd = new SqlCommand(@"SELECT TOP 1 Word
                                              FROM ValidWords
                                              WHERE Id = @Id"
                                              , connection);

            getWordCmd.Parameters.AddWithValue("@Id", randomNumber);

            using (var reader = await getWordCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    selectedWord = reader["Word"].ToString();
                }
            }

            var insertWordCmd = new SqlCommand(@"INSERT INTO Wordle (Word, Date)
                                                 OUTPUT INSERTED.ID
                                                 VALUES (@Word, @Date)"
                                                 , connection);
            insertWordCmd.Parameters.AddWithValue("@Word", selectedWord);
            insertWordCmd.Parameters.AddWithValue("@Date", today);

            var insertedWordId = await insertWordCmd.ExecuteScalarAsync();

            _logger.LogInformation("Word of the day successfully created and added to Azure SQL Database");
        }
        else
        {
            _logger.LogInformation("Word for today already exists");
        }

        await connection.CloseAsync();
        var response = req.CreateResponse(HttpStatusCode.OK);
        return response;
    }

    public class UserWordleSubmission
    {
        public string Username { get; set; }
        public string Date { get; set; }
        public List<string> Guesses { get; set; }
    }

    [Function("CreateUserWordle")]
    public async Task<HttpResponseData> CreateUserWordle([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "username/wordle")] HttpRequestData req)
    {
        _logger.LogInformation("C# Http trigger function processed a request");

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

        // Create a blank UserWordle instance
        var createBlankUserWordleCmd = new SqlCommand(@"INSERT INTO UserWrodle (Date, UserId, UserSubmissions)
                                                        OUTPUT INSERTED.UserSubmissions
                                                        VALUES (@Date, @UserId, @UserSubmissions)"
                                                        , connection);
        createBlankUserWordleCmd.Parameters.AddWithValue("@Date", DateTime.Today);
        createBlankUserWordleCmd.Parameters.AddWithValue("UserId", UserId);
        createBlankUserWordleCmd.Parameters.AddWithValue("UserSubmissions", "[]");

        try
        {
            var insertUserQuizCmdResponse = await createBlankUserWordleCmd.ExecuteScalarAsync();

            if (insertUserQuizCmdResponse == null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                _logger.LogInformation("No UserWordle was created for the given username and date.");
                await notFound.WriteStringAsync("No UserWordle was created for the given username and date.");
                return notFound;
            }

            var UserQuizOptions = (string)insertUserQuizCmdResponse;

            _logger.LogInformation("Successfully created a UserWordle using the given date and username parameter");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(UserQuizOptions);
            await connection.CloseAsync();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting UserWordle.");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("An error occurred while creating the UserWordle.");
            await connection.CloseAsync();
            return errorResponse;
        }
    }

    [Function("GetWordOfTheDay")]
    public async Task<HttpResponseData> GetWordOfTheDay([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "date/wordoftheday")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request");

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var dateParam = query["date"];

        if (!DateOnly.TryParse(dateParam, out var quizDate))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            _logger.LogInformation("Invalid or missing 'date' parameter. Use format YYYY-MM-DD.");
            await badResponse.WriteStringAsync("Invalid or missing 'date' parameter. Use format YYYY-MM-DD.");
            return badResponse;
        }

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var getWordOfTheDayCmd = new SqlCommand(@"SELECT TOP 1 Word
                                                  FROM Wordle
                                                  WHERE CAST(Date AS DATE) = @DATE"
                                                  , connection);
        getWordOfTheDayCmd.Parameters.AddWithValue("@Date", dateParam);

        var getWordOfTheDayCmdResponse = await getWordOfTheDayCmd.ExecuteScalarAsync();

        if (getWordOfTheDayCmdResponse == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            _logger.LogInformation("No word found for the given date");
            await notFound.WriteStringAsync("No word found for the given date");
            return notFound;
        }

        var word = (string) getWordOfTheDayCmdResponse;

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(word);
        await connection.CloseAsync();
        return response;
    }

    [Function("GetUserGuesses")]
    public async Task<HttpResponseData> GetUserGuesses([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "username/wordle/get")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request");

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var username = query["username"];
        var dateParam = query["wordle"];

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

        var UserId = (string)getUserCmdResponse;

        // If a UserWordle doesn't exist call CreateUserWordle function otherwise return the corresponding UserSubmissions
        var getUserWordleCmd = new SqlCommand(@"SELECT TOP 1 Id
                                                FROM UserWordle
                                                WHERE UserId = @UserId AND CAST(Date AS DATE) = @Date"
                                                , connection);
        getUserCmd.Parameters.AddWithValue("@UserId", UserId);
        getUserCmd.Parameters.AddWithValue("@Date", quizDate);

        var getUserWordleCmdResponse = await getUserWordleCmd.ExecuteScalarAsync();

        if (getUserWordleCmdResponse == null)
        {
            var httpClient = new HttpClient();
            var responseFromGet = await httpClient.GetAsync($"https://wordle20250818134329.azurewebsites.net/api/username/wordle?username={username}&wordle={dateParam}");

            var content = await responseFromGet.Content.ReadAsStringAsync();
            _logger.LogInformation(content);
        }

        var UserWordleId = (int)getUserWordleCmdResponse;

        var getUserSubmissionsCmd = new SqlCommand(@"SELECT TOP 1 UserSubmissions
                                                     FROM UserWordle
                                                     WHERE Id = @UserWordleId"
                                                     , connection);
        getUserSubmissionsCmd.Parameters.AddWithValue("@UserWordleId", UserWordleId);

        var getUserSubmissionsCmdResponse = await getUserSubmissionsCmd.ExecuteScalarAsync();

        if (getUserSubmissionsCmdResponse == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            _logger.LogInformation("No UserSubmissions found for the given username and date");
            await notFound.WriteStringAsync("No UserSubmissions found for the given username and date");
            return notFound;
        }

        var UserSubmissions = (string) getUserSubmissionsCmdResponse;

        _logger.LogInformation("Successfully returned a UserSubmission using the given username and date");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(UserSubmissions);
        await connection.CloseAsync();
        return response;
    }

    [Function("UpdateWordleGuess")]
    public async Task<HttpResponseData> UpdateWordleGuess([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "wordle/updateGuess")] HttpRequestData req)
    {
        _logger.LogInformation("C# Http trigger function processed a request");

        var requestBody = await req.ReadFromJsonAsync<UserWordleSubmission>();

        if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.Username) || string.IsNullOrWhiteSpace(requestBody.Date))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Missing or invalid request body");
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

        // If a UserWordle doesn't exist call CreateUserWordle function otherwise return the corresponding UserSubmissions
        var getUserWordleCmd = new SqlCommand(@"SELECT TOP 1 Id
                                                FROM UserWordle
                                                WHERE UserId = @UserId AND CAST(Date AS DATE) = @Date"
                                                , connection);
        getUserCmd.Parameters.AddWithValue("@UserId", UserId);
        getUserCmd.Parameters.AddWithValue("@Date", quizDate);

        var getUserWordleCmdResponse = await getUserWordleCmd.ExecuteScalarAsync();

        if (getUserWordleCmdResponse == null)
        {
            var httpClient = new HttpClient();
            var responseFromGet = await httpClient.GetAsync($"https://wordle20250818134329.azurewebsites.net/api/username/wordle?username={requestBody.Username}&wordle={requestBody.Date}");

            var content = await responseFromGet.Content.ReadAsStringAsync();
            _logger.LogInformation(content);
        }

        var UserWordleId = (int) getUserWordleCmdResponse;

        // Update UserSubmissions for the UserWordle
        var updateUserSubmissionsCmd = new SqlCommand(@"UPDATE UserWordle
                                                        SET UserSubmissions = @UserSubmissions,
                                                            WordleCompleted = @WordleCompleted
                                                        WHERE Id = @UserWordleId"
                                                        , connection);
        var userWordleJson = JsonSerializer.Serialize(requestBody.Guesses);
        updateUserSubmissionsCmd.Parameters.AddWithValue("@UserSubmissions", userWordleJson);
        updateUserSubmissionsCmd.Parameters.AddWithValue("@UserWordleId", UserWordleId);
        updateUserSubmissionsCmd.Parameters.AddWithValue("@WordleCompleted", false);

        _logger.LogInformation("Successfully updated UserSubmissions for the given username and date");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await connection.CloseAsync();
        return response;
    }

    [Function("SubmitWordleGuesses")]
    public async Task<HttpResponseData> SubmitWordleGuess([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "wordle/submitGuess")] HttpRequestData req)
    {
        _logger.LogInformation("C# Http trigger function processed a request");

        var requestBody = await req.ReadFromJsonAsync<UserWordleSubmission>();

        if (requestBody == null || string.IsNullOrWhiteSpace(requestBody.Username) || string.IsNullOrWhiteSpace(requestBody.Date))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Missing or invalid request body");
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

        // If a UserWordle doesn't exist call CreateUserWordle function otherwise return the corresponding UserSubmissions
        var getUserWordleCmd = new SqlCommand(@"SELECT TOP 1 Id
                                                FROM UserWordle
                                                WHERE UserId = @UserId AND CAST(Date AS DATE) = @Date"
                                                , connection);
        getUserCmd.Parameters.AddWithValue("@UserId", UserId);
        getUserCmd.Parameters.AddWithValue("@Date", quizDate);

        var getUserWordleCmdResponse = await getUserWordleCmd.ExecuteScalarAsync();
        
        var UserWordleId = (int)getUserWordleCmdResponse;

        // Update UserSubmissions for the UserWordle
        var updateUserSubmissionsCmd = new SqlCommand(@"UPDATE UserWordle
                                                        SET UserSubmissions = @UserSubmissions,
                                                            WordleCompleted = @WordleCompleted
                                                        WHERE Id = @UserWordleId"
                                                        , connection);
        var userWordleJson = JsonSerializer.Serialize(requestBody.Guesses);
        updateUserSubmissionsCmd.Parameters.AddWithValue("@UserSubmissions", userWordleJson);
        updateUserSubmissionsCmd.Parameters.AddWithValue("@UserWordleId", UserWordleId);
        updateUserSubmissionsCmd.Parameters.AddWithValue("@WordleCompleted", true);

        _logger.LogInformation("Successfully updated UserSubmissions for the given username and date");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await connection.CloseAsync();
        return response;
    }


    [Function("InitializeValidWordsTable")]
    public async Task<HttpResponseData> InitializeValidWordsTable([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "validwords/create")] HttpRequestData req)
    {
        _logger.LogInformation("C# Http trigger function processed a request");

        var path = @"C:\Users\gokul.sankar\source\repos\TegritTrivia\TegritTriviaFullStack\TegritTriviaFullStack\valid-wordle-words.txt";
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var lines = await File.ReadAllLinesAsync(path);
        
        for (int i=0; i < lines.Length; i++)
        {
            var initializeValidWordsTableCmd = new SqlCommand(@"INSERT INTO ValidWords (Word)
                                                                OUTPUT INSERTED.Id
                                                                VALUES (@Word)"
                                                                , connection);
            initializeValidWordsTableCmd.Parameters.AddWithValue("@Word", lines[i]);

            var insertedWordID = await initializeValidWordsTableCmd.ExecuteScalarAsync();

            if (insertedWordID == null)
            {
                _logger.LogInformation("Error adding word at index: " + i);
                break;
            }
        }
        
        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        _logger.LogInformation("Successfully initialized ValidWords table in Azure SQL Database");
        await connection.CloseAsync();
        return response;
    }
}