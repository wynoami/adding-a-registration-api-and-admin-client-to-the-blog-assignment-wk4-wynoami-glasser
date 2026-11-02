namespace AtlasBlog.Models.Requests;

// These classes describe the JSON the console app sends to the API.

public class ApiKeyRequest
{
    public string? ApiKey { get; set; }
}

// Admin and moderator registration: key + email + password.
public class RegisterRequest : ApiKeyRequest
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}

// Author registration: everything above, plus the blog details.
public class RegisterAuthorRequest : RegisterRequest
{
    public string? BlogTitle { get; set; }
    public string? BlogSubtitle { get; set; }
    public string? BlogDescription { get; set; } 
}