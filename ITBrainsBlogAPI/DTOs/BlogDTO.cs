using System.ComponentModel.DataAnnotations;

namespace ITBrainsBlogAPI.DTOs
{
    public class BlogDTO
    {
        [Required]
        public string Title { get; set; }
        [Required]
        public string Body { get; set; }
        public IFormFile ImgFile { get; set; }
    }
}
