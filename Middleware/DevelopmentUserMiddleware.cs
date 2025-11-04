using System.Collections.Generic; // Added for List
using System.Linq;             // Added for FirstOrDefault
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting; // Required for IHostEnvironment

namespace ErrorAnalysisBackend.Middleware
{
    public class DevelopmentUserMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostEnvironment _environment;

        public DevelopmentUserMiddleware(RequestDelegate next, IHostEnvironment environment)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // --- DEVELOPMENT-ONLY BYPASS ---
            // Check if running locally AND if the special header exists
            if (_environment.IsDevelopment() && context.Request.Headers.TryGetValue("X-Test-User-ID", out var userIdValue))
            {
                var userId = userIdValue.FirstOrDefault(); // Get the first value from the header
                if (!string.IsNullOrEmpty(userId))
                {
                    // Create a fake identity for this user
                    var claims = new List<Claim>
                    {
                        // ✨ --- CORRECTED CLAIM TYPE --- ✨
                        // Use ClaimTypes.NameIdentifier to match what controllers expect
                        new Claim(ClaimTypes.NameIdentifier, userId),
                        // ✨ --- END OF CORRECTION --- ✨

                        // You can optionally add other claims if needed
                        // new Claim(ClaimTypes.Email, "test.user@example.com"),
                        // new Claim("name", "Test User") // Example name claim
                    };

                    // Create the identity and principal
                    var identity = new ClaimsIdentity(claims, "TestAuth"); // Use a specific scheme name
                    context.User = new ClaimsPrincipal(identity);

                    Console.WriteLine($"---> DEVELOPMENT MODE: Bypassed JWT Auth. Using Test User ID: {userId}");
                }
            }
            // --- END OF BYPASS ---

            // Call the next middleware (e.g., UseAuthentication, UseAuthorization, EndpointRouting)
            await _next(context);
        }
    }
}