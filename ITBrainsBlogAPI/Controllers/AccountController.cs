using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using ITBrainsBlogAPI.Models;
using ITBrainsBlogAPI.DTOs;
using ITBrainsBlogAPI.Services;
using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs.Models;
using System.Net.Mail;

namespace ITBrainsBlogAPI.Controllers
{
    [Route("blog/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IEmailService _emailService;
        public AzureBlobService _service;

        public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, IEmailService emailService, AzureBlobService service)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _service = service;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new AppUser { UserName = model.Email, Email = model.Email, Name = model.Name, Surname = model.Surname, ImageUrl = "https://itbblogstorage.blob.core.windows.net/itbcontainer/0e39b3d3-971a-43c4-bf37-5f517b6bd0c8_defaultimage.png" };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = $"http://localhost:5173/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
                try
                {
                    await _emailService.SendEmailAsync(model.Email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");
                }
                catch (SmtpException ex)
                {
                    return StatusCode(500, $"Error sending confirmation email: {ex.Message}");
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Error sending confirmation email: {ex.Message}");
                }
                await _signInManager.SignInAsync(user, isPersistent: false);
                return Ok("Register is successfully");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return BadRequest(ModelState);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null && !await _userManager.IsEmailConfirmedAsync(user))
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = $"http://localhost:5173/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
                //var confirmationLink = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, token = token }, Request.Scheme);
                try
                {
                    await _emailService.SendEmailAsync(model.Email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");
                }
                catch (Exception ex)
                {
                    // Log the exception
                    return StatusCode(500, "Error sending confirmation email." + ex.Message);
                }

                return BadRequest("Email not confirmed. Confirmation email has been sent.");
            }
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                return Ok("Successfully");
            }

            if (result.IsLockedOut)
            {
                return BadRequest("User account locked out.");
            }

            return BadRequest("Invalid login attempt.");
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(int userId, string token)
        {
            if (userId == 0 || token == null)
            {
                return BadRequest("Invalid email confirmation request.");
            }

            var user = await _userManager.Users.SingleOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return BadRequest("Invalid email confirmation request.");
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                return Ok("Email confirmed successfully.");
            }

            return BadRequest("Error confirming your email.");
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok("Logout");
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                user.UserName,
                user.Email
            });
        }

        [HttpGet]
        public async Task<ActionResult<List<IdentityUser>>> GetUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            return Ok(users);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser([FromRoute] int id)
        {
            var user = await _userManager.Users.SingleOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { Message = "User Not Found" });
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return Ok(new { Message = "User deleted successfully" });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser([FromRoute] int id)
        {
            var user = await _userManager.Users.SingleOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { Message = "User Not Found" });
            }
            return Ok(user);
        }

        [HttpPost("{id}/upload-profile-image")]
        public async Task<IActionResult> UploadProfileImage([FromRoute] int id,IFormFile file)
        {
            var user = await _userManager.Users.SingleOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }
            if (!FileExtensions.IsImage(file))
            {
                return BadRequest("This file type is not accepted.");
            }

            var fileName = await _service.UploadFile(file);
            var profileImageUrl = $"https://itbblogstorage.blob.core.windows.net/itbcontainer/{fileName}";
            user.ImageUrl = profileImageUrl;
            await _userManager.UpdateAsync(user);
            return Ok(new { Message = "Profile image uploaded successfully", ImageUrl = user.ImageUrl });
        }

    }
}
