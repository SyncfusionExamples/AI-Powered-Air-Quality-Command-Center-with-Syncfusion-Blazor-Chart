using AirQualityChartTracker.Components.Pages;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

public class AIAirQualityService
{
    #region Properties

    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _deployment;
    private readonly ILogger<AIAirQualityService> _logger;
    #endregion

    #region Constructor
    public AIAirQualityService(HttpClient httpClient, IConfiguration configuration, ILogger<AIAirQualityService> logger)
    {
        _httpClient = httpClient;
        _endpoint = configuration["AzureOpenAI:Endpoint"];
        _apiKey = configuration["AzureOpenAI:ApiKey"];
        _deployment = configuration["AzureOpenAI:DeploymentId"];
        _logger = logger;
    }

    #endregion

    #region Methods

    public async Task<string> GetResponseFromOpenAI(string prompt)
    {
        var requestBody = new
        {
            messages = new[]
        {
               new { role = "user", content = prompt }
            },
            max_tokens = 2000
        };
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/openai/deployments/{_deployment}/chat/completions?api-version=2025-01-01-preview")
        {
            Content = JsonContent.Create(requestBody)
        };

        request.Headers.Add("api-key", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            string? responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        else
        {
            string? errorContent = await response.Content.ReadAsStringAsync();
            return $"Error: {response.StatusCode} - {errorContent}";
        }
    }

    internal async Task<List<AirQualityInfo>> PredictAirQualityTrends(string location)
    {
        try
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            string startDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");

            var userMessage = $"You are an AI model specialized in air pollution forecasting and environmental analysis. " +
            $"Your task is to generate a realistic dataset for the past 30 days ({startDate} to {today}) " +
            $"for the specified location, {location}. The data should include daily air quality trends. " +
            "Use the following structure for each entry: " +
            "[ { \"Date\": \"YYYY-MM-DD\", \"PollutionIndex\": number, " +
            "\"AirQualityStatus\": \"Good | Satisfactory | Moderate | Poor | Very Poor | Severe\", " +
            "\"Latitude\": number, \"Longitude\": number, \"AIPredictionAccuracy\": number } ]. " +
            "Base the predictions on historical data trends, ensuring the dataset includes fluctuations and avoids a uniform increase or decrease." +
            $"The provided latitude and longitude values must represent the {location}'s geographical coordinates." +
            "Output ONLY valid JSON without any additional explanations.";

            return await HandleResponseFromAI(userMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get air quality trends");
            return GetFallbackData("wwwroot/Resources/current_data.json");
        }
    }

    internal async Task<List<AirQualityInfo>> PredictNextMonthForecast(List<AirQualityInfo> historicalData)
    {
        try
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            string futureDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");

            var userMessage = "You are an AI model specialized in air pollution forecasting. " +
            "Based on the provided historical data, generate an accurate prediction " +
            $"for air quality trends over the next 30 days ({today} to {futureDate}). " +
            "Using the following historical dataset, predict the Pollution Index for the next 30 days:\n\n" +
            $"{System.Text.Json.JsonSerializer.Serialize(historicalData)}\n\n" +
            "Use the following structure for each entry: " +
            "[ { \"Date\": \"YYYY-MM-DD\", \"PollutionIndex\": number, " +
            "\"AirQualityStatus\": \"Good | Satisfactory | Moderate | Poor | Very Poor | Severe\", " +
            "\"Latitude\": number, \"Longitude\": number, \"AIPredictionAccuracy\": number } ]. " +
            "Output ONLY valid JSON without any additional explanations.";

            return await HandleResponseFromAI(userMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Forecast generation failed");
            return GetFallbackData("wwwroot/Resources/prediction_data.json");
        }
    }

    private async Task<List<AirQualityInfo>> HandleResponseFromAI(string prompt)
    {
        string response = await GetResponseFromOpenAI(prompt);
        string extractedJson = JsonExtractor.ExtractJson(response);
        if (!string.IsNullOrEmpty(extractedJson))
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true
            };

            return System.Text.Json.JsonSerializer.Deserialize<List<AirQualityInfo>>(extractedJson, options)
                ?? new List<AirQualityInfo>();
        }
        else
        {
            return new List<AirQualityInfo>();
        }
    }

    private List<AirQualityInfo> GetFallbackData(string filePath)
    {
        return JsonConvert.DeserializeObject<List<AirQualityInfo>>(File.ReadAllText(filePath)) ?? new List<AirQualityInfo>();
    }

    #endregion
}

public class JsonExtractor
{
    public static string ExtractJson(string response)
    {
        try
        {
            response = response.TrimStart(new[] { '`', 'j', 's', 'o', 'n', '\n' });
            response = response.TrimStart(new[] { '`', 'j', 's', 'o', 'n', '\n' });
            response = response.TrimEnd(new[] { '`', '\n' });
            Match match = Regex.Match(response, @"\[.*?\]", RegexOptions.Singleline);

            if (match.Success)
            {
                string json = match.Groups[0].Value.Trim();
                json = Regex.Replace(json, @",(\s*})", "$1");

                Debug.WriteLine("Successfully extracted JSON");
                return json;
            }
            else
            {
                Debug.WriteLine("No JSON found in the response.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"JSON extraction failed: {ex.Message}");
        }
        return string.Empty;
    }
}