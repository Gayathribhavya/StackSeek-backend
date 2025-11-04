using System.Security.Claims;
using FirebaseAdmin.Auth;

namespace ErrorAnalysisBackend.Middleware
{
    public class FirebaseAuthMiddleware
    {
        private readonly RequestDelegate _next;

        public FirebaseAuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

            if (string.IsNullOrWhiteSpace(token))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Missing Authorization token");
                return;
            }

            try
            {
                var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);
                var claims = new List<Claim>
                {
                    new Claim("user_id", decodedToken.Uid),
                    new Claim(ClaimTypes.NameIdentifier, decodedToken.Uid),
                    // Add custom claims if needed
                };

                var identity = new ClaimsIdentity(claims, "Firebase");
                var principal = new ClaimsPrincipal(identity);
                context.User = principal;
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync($"Token verification failed: {ex.Message}");
                return;
            }

            await _next(context);
            
        }
    }
}
