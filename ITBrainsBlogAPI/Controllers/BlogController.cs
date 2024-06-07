using ITBrainsBlogAPI.Models;
using ITBrainsBlogAPI.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITBrainsBlogAPI.Services;
using Microsoft.AspNetCore.Identity;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Reflection.Metadata;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ITBrainsBlogAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BlogController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WeatherForecastController> _logger;
        public AzureBlobService _service;

        public BlogController(AppDbContext context, UserManager<AppUser> userManager, IConfiguration configuration, ILogger<WeatherForecastController> logger, AzureBlobService service)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<Blog>>> GetBlogs()
        {

            var blogs = await _context.Blogs
                .Include(b => b.Images)
                .Include(b => b.Reviews)
                .Include(b => b.AppUser)
                .ToListAsync();
            return Ok(blogs);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Blog>> GetBlog(int id)
        {
            var blog = await _context.Blogs
                .Include(b => b.Images)
                .Include(b => b.Reviews)
                .SingleOrDefaultAsync(b => b.Id == id);

            if (blog == null)
            {
                return NotFound();
            }

            return blog;
        }
        [HttpPost("create")]
        public async Task<ActionResult<Blog>> AddBlog([FromForm] BlogDTO model, [FromHeader(Name = "Authorization")] string token)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Token var mı ve geçerli mi kontrolü yap
            if (string.IsNullOrEmpty(token) || !token.StartsWith("Bearer "))
            {
                return Unauthorized("Invalid token.");
            }

            var tokenValue = token.Substring("Bearer ".Length).Trim();

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(tokenValue, validationParameters, out validatedToken);

                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized("Invalid user.");
                }

                var user = await _userManager.FindByEmailAsync(userIdClaim.Value);

                if (user == null)
                {
                    return Unauthorized("User not found.");
                }

                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    var blog = new Blog
                    {
                        AppUserId = user.Id,
                        Title = model.Title,
                        Body = model.Body,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };

                    _context.Blogs.Add(blog);
                    await _context.SaveChangesAsync();

                    foreach (var item in model.ImgFiles ?? Enumerable.Empty<IFormFile>())
                    {
                        if (!FileExtensions.IsImage(item))
                        {
                            return BadRequest("This file type is not accepted.");
                        }

                        var imgUrl = await _service.UploadFile(item);
                        var imageUrl = $"https://itbblogstorage.blob.core.windows.net/itbcontainer/{imgUrl}";

                        var img = new Image
                        {
                            BlogId = blog.Id,
                            ImageUrl = imageUrl
                        };

                        _context.Images.Add(img);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok("Blog Added.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the blog.");
                return StatusCode(500, "Internal server error");
            }
        }


        [HttpPut("edit/{blogId}")]
        public async Task<ActionResult<Blog>> EditBlog(int blogId, [FromForm] BlogDTO model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || !User.Identity.IsAuthenticated)
            {
                return Unauthorized("Please login to edit a blog.");
            }

            var existingBlog = await _context.Blogs.FindAsync(blogId);
            if (existingBlog == null)
            {
                return NotFound("Blog not found.");
            }

            if (existingBlog.AppUserId != user.Id)
            {
                return Unauthorized("You do not have permission to edit this blog.");
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    existingBlog.Title = model.Title;
                    existingBlog.Body = model.Body;
                    existingBlog.UpdatedAt = DateTime.UtcNow;

                    _context.Entry(existingBlog).State = EntityState.Modified;
                    await _context.SaveChangesAsync();

                    // Delete existing images associated with the blog
                    var existingImages = _context.Images.Where(i => i.BlogId == existingBlog.Id);
                    _context.Images.RemoveRange(existingImages);

                    // Add new images
                    foreach (var item in model.ImgFiles ?? Enumerable.Empty<IFormFile>())
                    {
                        if (!FileExtensions.IsImage(item))
                        {
                            return BadRequest("This file type is not accepted.");
                        }

                        var imgUrl = await _service.UploadFile(item);
                        var imageUrl = $"https://itbblogstorage.blob.core.windows.net/blogcontainer/{imgUrl}";

                        var img = new Image
                        {
                            BlogId = existingBlog.Id,
                            ImageUrl = imageUrl
                        };

                        _context.Images.Add(img);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok("Blog updated successfully.");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "An error occurred while editing the blog.");
                    return StatusCode(500, "Internal server error");
                }
            }
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBlog(int id)
        {
            var blog = await _context.Blogs.FindAsync(id);
            if (blog == null)
            {
                return NotFound();
            }

            _context.Blogs.Remove(blog);
            await _context.SaveChangesAsync();

            return Ok("Removed");
        }

        private bool BlogExists(int id)
        {
            return _context.Blogs.Any(e => e.Id == id);
        }
    }
}
