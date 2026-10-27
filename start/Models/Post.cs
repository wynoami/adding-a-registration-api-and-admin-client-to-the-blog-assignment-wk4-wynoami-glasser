using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AtlasBlog.Models;

public class Post
{
    [Key]
    public int PostId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }

    public int BlogId { get; set; }

    [ForeignKey("BlogId")]
    public virtual Blog Blog { get; set; } = null!;

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
