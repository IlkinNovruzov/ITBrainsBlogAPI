namespace ITBrainsBlogAPI.Models
{
    public class Blog
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int LikeCount { get; set; } 
        public int ViewCount { get; set; } 
        public int ReviewCount { get; set; }
        public List<Image> Images { get; set; }
        public List<Review> Reviews { get; set; }
    }
}
