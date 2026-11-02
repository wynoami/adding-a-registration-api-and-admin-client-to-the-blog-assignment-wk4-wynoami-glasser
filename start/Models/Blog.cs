using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AtlasBlog.Models;

public class Blog
{
    [Key]
    public int BlogId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Subtitle { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;
    [ForeignKey("AuthorId")]
    public virtual ApplicationUser Author { get; set; } = null!;

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}
