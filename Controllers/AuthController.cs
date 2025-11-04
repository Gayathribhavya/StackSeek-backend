using Microsoft.AspNetCore.Mvc;
using FirebaseAdmin.Auth;
using System.Threading.Tasks;
using ErrorAnalysisBackend.Models; // <- Where your DTO will go
using Google.Cloud.Firestore;

namespace ErrorAnalysisBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly FirestoreDb _firestore;

        public AuthController(FirestoreDb firestore)
        {
            _firestore = firestore;
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok("Authenticated ping successful!");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest model)
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
                return BadRequest("Email and password are required.");

            try
            {
                // Create user in Firebase
                var userArgs = new UserRecordArgs
                {
                    Email = model.Email,
                    EmailVerified = false,
                    Password = model.Password,
                    DisplayName = model.Name,
                    Disabled = false
                };

                var userRecord = await FirebaseAuth.DefaultInstance.CreateUserAsync(userArgs);

                // Send verification email
                var link = await FirebaseAuth.DefaultInstance.GenerateEmailVerificationLinkAsync(model.Email);

                // Log or send link via SMTP service like SendGrid
                Console.WriteLine($"Verification link: {link}");

                return Ok(new { message = "User registered. Please check your email to verify before login." });
            }
            catch (FirebaseAuthException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
