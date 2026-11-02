using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace BlogConsole;

public class Program
{
    // One HttpClient for the whole app.
    private static HttpClient _http = null!;
    private static string _apiKey = string.Empty;

    public static async Task Main()
    {
        // 1. Load settings from this app's own appsettings.json
        IConfiguration config;
        try
        {
            config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
        }
        catch (Exception ex)
        {
            WriteColor($"Could not load appsettings.json: {ex.Message}", ConsoleColor.Red);
            return;
        }

        string? apiKey = config["ApiKey"];
        string? baseUrl = config["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(baseUrl))
        {
            WriteColor("appsettings.json needs both \"ApiKey\" and \"ApiBaseUrl\".", ConsoleColor.Red);
            return;
        }
        _apiKey = apiKey;

        // 2. Set up the HttpClient. 
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // 3. The menu loop.
        while(true)
        {
            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }
            WriteColor("=== Atlas Blog: Account Manager ===", ConsoleColor.Cyan);
            Console.WriteLine($"API: {_http.BaseAddress}");
            Console.WriteLine();
            Console.WriteLine("1) Register an admin");
            Console.WriteLine("2) Regiser an author (with blog)");
            Console.WriteLine("3) Register a moderator");
            Console.WriteLine("4) Exit");
            Console.WriteLine();
            Console.WriteLine("Choose an option: ");

            string? choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    await RegisterAdminAsync();
                    break;
                case "2":
                    await RegisterAuthorAsync();
                    break;
                case "3":
                    await RegisterModeratorAsync();
                    break;
                case "4":
                case null: // input stream closed
                    Console.WriteLine("Goodbye!");
                    _http.Dispose();
                    return;
                default:
                    WriteColor("Please enter 1, 2, 3, or 4.", ConsoleColor.Yellow);
                    break;
            }

            Console.WriteLine();
            Console.Write("Press Enter to return to the menu...");
            Console.ReadLine();
        }
    }

    // ----------- Menu actions -----------

    private static async Task RegisterAdminAsync()
    {
        WriteColor("-- New Admin --", ConsoleColor.Cyan);
        string email = ReadRequired("Email: ");
        string password = ReadPassword("Password: ");

        // Anonymous objects are turned into JSON
        await SendRegistrationAsync("api/auth/registeradmin", 
            new { apiKey = _apiKey, email, password });
    }

    private static async Task RegisterAuthorAsync()
    {
        WriteColor("-- New Author --", ConsoleColor.Cyan);
        string email = ReadRequired("Email: ");
        string password = ReadPassword("Password: ");
        string blogTitle = ReadRequired("Blog title: ");
        string? blogSubtitle = ReadOptional("Blog subtitle (optional): ");
        string? blogDescription = ReadOptional ("Blog description (optional): ");

        await SendRegistrationAsync("api/auth/registerauthor",
            new { apiKey = _apiKey, email, password, blogTitle, blogSubtitle, blogDescription });
    }

    private static async Task RegisterModeratorAsync()
    {
        WriteColor("-- New Moderator --", ConsoleColor.Cyan);
        string email = ReadRequired("Email: ");
        string password = ReadPassword("Password: ");

        await SendRegistrationAsync("api/auth/registermoderator",
            new { apiKey = _apiKey, email, password });
    }

    // ----------- Talking to the API ----------
    private static async Task SendRegistrationAsync(string endpoint, object payload)
    {
        Console.WriteLine();
        Console.WriteLine("Sending to the API...");

        try
        {
            using HttpResponseMessage response = await _http.PostAsJsonAsync(endpoint, payload);
            string message = await ReadMessageAsync(response);

            if (response.IsSuccessStatusCode)
            {
                WriteColor($"Success! {message}", ConsoleColor.Green);
                return;
            }

            // Not successful
            string reason = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "The API rejected the API key. Check that ApiKey matches in both appsettings.json files.",
                HttpStatusCode.BadRequest => "The API couldn't accept that information.",
                HttpStatusCode.Conflict => "That account already exists.",
                HttpStatusCode.NotFound => "The API address wasn't found. Check ApiBaseUrl and the endpoint name.",
                >= HttpStatusCode.InternalServerError => "The server ran into a problem.",
                _=> "The request didn't succeed."
            };

            WriteColor($"Error ({(int)response.StatusCode}): {reason}", ConsoleColor.Red);
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine(message);
            }
        }
        catch (HttpRequestException)
        {
            WriteColor("The API took too long to respond. Try again in a moment.", ConsoleColor.Red);
        }
    }

    // Pulls the "message" (and any "errors" list) out of the API's JSON reply.
    // If the reply isn't the JSON expected, falls back to the raw text.
    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            var lines = new List<string>();

            if (root.TryGetProperty("message", out JsonElement message))
            {
                lines.Add(message.GetString() ?? string.Empty);
            }
            else if (root.TryGetProperty("title", out JsonElement title))
            {
                // ASP.NET's built-in error format ("ProblemDetails") uses "title".
                lines.Add(title.GetString() ?? string.Empty);
            }

            if (root.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement error in errors.EnumerateArray())
                {
                    lines.Add($"  - {error.GetString()}");
                }
            }

            return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : body;
        }
        catch (JsonException)
        {
            return body;
        }
    }

    // ---------- Input helpers ----------

    // Keeps asking until the user types something.
    private static string ReadRequired(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string? input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(input))
            {
                return input;
            }
            WriteColor("This field is required.", ConsoleColor.Yellow);
        }
    }

    // Blank is allowed. Returns null so the API stores nothing.
    private static string? ReadOptional(string prompt)
    {
        Console.Write(prompt);
        string? input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? null : input;
    }

    // Shows * instead of the password as it's typed
    private static string ReadPassword(string prompt)
    {
        Console.WriteLine("(8+ characters with an uppercase letter, a lowercase letter, a number, and a symbol)");

        // If input is piped in, ReadKey won't work, so read normally.
        if (Console.IsInputRedirected)
        {
            return ReadRequired(prompt);
        }

        while (true)
        {
            Console.Write(prompt);
            var password = new System.Text.StringBuilder();

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password.Length--;
                        Console.Write("\b \b");
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                    Console.Write('*');
                }
            }

            if (password.Length >0)
            {
                return password.ToString();
            }
            WriteColor("This field is required.", ConsoleColor.Yellow);
        }
    }

    private static void WriteColor(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}