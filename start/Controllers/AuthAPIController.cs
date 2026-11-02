using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AtlasBlog.Data;
using AtlasBlog.Models;
using AtlasBlog.Models.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AtlasBlog.Controllers;

// [ApiController] = this controller returns data (JSON), not web pages.
// [Route("api/auth")] = every endpoint below starts with /api/auth.
[ApiController]
[Route("api/auth")]
public class AuthAPIController : ControllerBase
{
    private readonly string _apiKey;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthAPIController> _logger;


    // Dependency injection: ASP.NET hands us these when it creates the controller.
    public AuthAPIController(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<AuthAPIController> logger)
    {
        _apiKey = configuration["ApiKey"]
            ?? throw new InvalidOperationException("ApiKey is missing from appsettings.json.");
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    // POST /api/auth/registeradmin.
    [HttpPost("registeradmin")]
    public Task<IActionResult> RegisterAdmin([FromBody] RegisterRequest request)
        => RegisterAccountAsync(request, "admin");

    // POST /api/auth/registerauthor
    [HttpPost("registerauthor")]
    public Task<IActionResult> RegisterAuthor([FromBody] RegisterAuthorRequest request)
        => RegisterAccountAsync(request, "author");

    // POST api/auth/registermoderator
    [HttpPost("registermoderator")]
    public Task<IActionResult> RegisterModerator([FromBody] RegisterRequest request)
        => RegisterAccountAsync(request, "moderator");

    // All three endpoints follow the same steps, so they share this method.
    // The only extra work for authors is creating their blog.
    private async Task<IActionResult> RegisterAccountAsync(RegisterRequest request, string claimType)
    {
        // Step 1: the bouncer. No valid key, no entry. 401 Unauthorized.
        if (!IsValidApiKey(request.ApiKey))
        {
            return Unauthorized(new { message = "Invalid or missing API key." });
        }

        // Step 2: check the fields. Missing or bad input = 400 Bad Request.
        string? validationError = ValidateFields(request);
        if (validationError != null)
        {
            return BadRequest(new { message = validationError });
        }

        string email = request.Email!.Trim();

        try
        {
            // Step 3: is this email already taken? 409 Conflict.
            if (await _userManager.FindByEmailAsync(email) != null)
            {
                return Conflict(new { message = $"An account with {email} already exists." });
            }

            // Step 4: start a transaction so the user, blog, and claim are saved
            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Step 5: create the user.
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            IdentityResult createResult = await _userManager.CreateAsync(user, request.Password!);
            if (!createResult.Succeeded)
            {
                return BadRequest(new
                {
                    message = "The account could not be created.",
                    errors = createResult.Errors.Select(e => e.Description)
                });
            }

            // Step 6 (authors only): create the blog linked to the new user's Id.
            if (request is RegisterAuthorRequest author)
            {
                DateTime now = DateTime.UtcNow;
                var blog = new Blog
                {
                    Title = author.BlogTitle!.Trim(),
                    Subtitle = CleanOptional(author.BlogSubtitle),
                    Description = CleanOptional(author.BlogDescription),
                    CreatedAt = now,
                    LastModifiedAt = now,
                    AuthorId = user.Id
                };

                _context.Blogs.Add(blog);
                await _context.SaveChangesAsync();
            }

            // Step 7: add the claim
            IdentityResult claimResult = await _userManager.AddClaimAsync(user, new Claim(claimType, "true"));
            if (!claimResult.Succeeded)
            {
                // Not the caller's fault, so throw and let the catch below return a 500.
                throw new InvalidOperationException(
                    "Could not add claim: " + string.Join(" ", claimResult.Errors.Select(e => e.Description)));
            }

            // Step 8: everything worked, so save it all for real.
            await transaction.CommitAsync();

            // 201 Created = "your request made something new."
            return StatusCode(StatusCodes.Status201Created, new
            {
                message = $"{claimType} account created for {email}.",
                userId = user.Id,
                email
            });
        }
        catch (Exception ex)
        {
            // Something unexpected. Log the details and send the caller a safe message: 500.
            _logger.LogError(ex, "Failed to register {ClaimType} account for {Email}", claimType, email);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Something went wrong on the server while creating the account. Nothing was saved."
            });
        }
    }

    // Compares the keys in a way that takes the same amount of time whether
    // they match early or late, so an attacker can't guess the key by timing responses.
    private bool IsValidApiKey(string? provideKey)
    {
        if (string.IsNullOrEmpty(provideKey))
        {
            return false;
        }

        byte[] provided = Encoding.UTF8.GetBytes(provideKey);
        byte[] expected = Encoding.UTF8.GetBytes(_apiKey);
        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    // Returns an error message, or null if everything looks fine.
    private static string? ValidateFields(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return "Email and password are both required.";
        }

        if (!new EmailAddressAttribute().IsValid(request.Email.Trim()))
        {
            return $"'{request.Email}' is not a valid email address.";
        }

        if (request is RegisterAuthorRequest author)
        {
            if (string.IsNullOrWhiteSpace(author.BlogTitle))
            {
                return "Authors need a blog title.";
            }
            if (author.BlogTitle.Trim().Length > 200)
            {
                return "Blog title must be 200 characters or fewer.";
            }
            if (author.BlogSubtitle?.Trim().Length > 500)
            {
                return "Blog subtitle must be 500 characters or fewer.";
            }
            if (author.BlogDescription?.Trim().Length > 2000)
            {
                return "Blog description must be 2000 characters or fewer.";
            }
        }

        return null;
    }

    // Subtitle and description are optional, so store blanks as null.
    private static string? CleanOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
