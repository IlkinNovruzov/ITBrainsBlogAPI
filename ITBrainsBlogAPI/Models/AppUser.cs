using ITBrainsBlogAPI.Models;
using Microsoft.AspNetCore.Identity;

namespace ITBrainsBlogAPI.Models
{
    public class AppUser : IdentityUser<int>
    {
        public string Name { get; set; }
        public string Surname { get; set; }
        public string ImageUrl { get; set; }
        public List<Review> Reviews { get; set; }
        public List<Blog> Blogs { get; set; }
    }
}