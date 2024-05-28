using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ITBrainsBlogAPI.Models
{
    public class Image
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ImageUrl { get; set; }

        [Required]
        public int BlogId { get; set; }
        public Blog Blog { get; set; }

        public bool IsActive { get; set; }

        [NotMapped]
        public IFormFile ImageFile { get; set; }
    }
}
