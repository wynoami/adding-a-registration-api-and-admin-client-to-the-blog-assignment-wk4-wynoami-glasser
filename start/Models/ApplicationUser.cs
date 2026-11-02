using Microsoft.AspNetCore.Identity;

namespace AtlasBlog.Models;

public class ApplicationUser : IdentityUser
{
    public virtual Blog? Blog { get; set; }
}
