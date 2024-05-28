﻿namespace ITBrainsBlogAPI.Models
{
    public class Review
    {
        public int Id { get; set; }
        public string Comment { get; set; }
        public DateTime Date { get; set; }
        public int UserId { get; set; }
        public AppUser User { get; set; }
        public int BlogId { get; set; }
        public Blog Blog { get; set; }
        public int? ParentReviewId { get; set; }
        public Review ParentReview { get; set; } 
        public List<Review> Replies { get; set; }
    }
}
