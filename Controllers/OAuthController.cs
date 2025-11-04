using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Google.Cloud.Firestore;
using System.Security.Claims;
using ErrorAnalysisBackend.Models;
using ErrorAnalysisBackend.Services; // Needed for FirestoreService

namespace ErrorAnalysisBackend.Controllers
{
    [Authorize] // 🔐 Secures the entire controller
    [ApiController]
    [Route("api/oauth")]
    public class OAuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly FirestoreDb _db;
        private readonly FirestoreService _firestoreService;

        public OAuthController(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            FirestoreDb db,
            FirestoreService firestoreService)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _db = db;
            _firestoreService = firestoreService;
        }

        // --- Register Endpoint ---
        [HttpPost("register")]
        public async Task<IActionResult> RegisterCallback()
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userEmail = User.FindFirst(ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User ID not found in token." });
            }

            var userRef = _db.Collection("users").Document(userId);
            var snapshot = await userRef.GetSnapshotAsync();

            if (snapshot.Exists)
            {
                return Ok(new { message = "User profile already exists." });
            }

            var newUser = new UserProfile { Email = userEmail ?? string.Empty, PlanId = "free" };
            try
            {
                 await userRef.SetAsync(newUser);
                 Console.WriteLine($"--- Firestore: Successfully created user profile for userId: {userId} ---");
                 return Created($"/api/users/{userId}", new { message = "User profile created successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating user profile for {userId}: {ex.Message}");
                return StatusCode(500, new { message = "Failed to create user profile." });
            }
        }

        // --- GitHub Endpoint ---
        [HttpPost("github")]
        public async Task<IActionResult> ExchangeGitHubCode([FromBody] CodeRequest request)
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "User ID not found." });
            if (string.IsNullOrEmpty(request?.Code)) return BadRequest(new { message = "Missing GitHub code." });

            var clientId = _config["OAuth:GitHub:ClientId"];
            var clientSecret = _config["OAuth:GitHub:ClientSecret"];
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                return StatusCode(500, new { message = "GitHub OAuth credentials not configured." });

            var tokenParams = new Dictionary<string, string> { { "client_id", clientId }, { "client_secret", clientSecret }, { "code", request.Code } };
            using var httpClient = _httpClientFactory.CreateClient();
            var reqMsg = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token") { Content = new FormUrlEncodedContent(tokenParams) };
            reqMsg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try { /* ... GitHub token exchange logic ... */
                var response = await httpClient.SendAsync(reqMsg);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode, new { message = "GitHub token exchange failed.", error = body });

                using var jsonDoc = JsonDocument.Parse(body);
                if (!jsonDoc.RootElement.TryGetProperty("access_token", out var tokenEl) || tokenEl.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(tokenEl.GetString())) {
                    var err = jsonDoc.RootElement.TryGetProperty("error_description", out var d) ? d.GetString() : "Unknown";
                    return Unauthorized(new { message = $"GitHub token exchange failed: {err}" });
                }
                var accessToken = tokenEl.GetString()!;
                await _firestoreService.SaveProviderAuthAsync(userId, "github", accessToken, "unknown", null);
                return Ok(new { message = "GitHub account connected successfully." });
            } catch (Exception ex) {
                 Console.WriteLine($"Error during GitHub OAuth for user {userId}: {ex.Message}");
                return StatusCode(500, new { message = "An unexpected error occurred during GitHub OAuth.", error = ex.Message });
            }
        }

        // --- Azure DevOps Endpoint ---
        [HttpPost("azure")]
        public async Task<IActionResult> ExchangeAzureCode([FromBody] CodeRequest request)
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "User ID not found." });
            if (string.IsNullOrEmpty(request?.Code)) return BadRequest(new { message = "Missing Azure DevOps code." });

            var clientId = _config["OAuth:AzureDevOps:ClientId"];
            var clientSecret = _config["OAuth:AzureDevOps:ClientSecret"];
            var redirectUri = _config["OAuth:AzureDevOps:RedirectUri"];
            if (clientId == "DUMMY_AZURE_CLIENT_ID" || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                return BadRequest(new { message = "Azure DevOps connection not configured." });
            if (string.IsNullOrEmpty(redirectUri)) return StatusCode(500, new { message = "Azure DevOps RedirectUri not configured." });

            var tokenParams = new Dictionary<string, string> { { "client_assertion_type", "urn:ietf:params:oauth:client-assertion-type:jwt-bearer" },
                { "client_assertion", clientSecret }, { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                { "assertion", request.Code }, { "redirect_uri", redirectUri } };
            using var httpClient = _httpClientFactory.CreateClient();
             var reqMsg = new HttpRequestMessage(HttpMethod.Post, "https://app.vssps.visualstudio.com/oauth2/token") { Content = new FormUrlEncodedContent(tokenParams) };
            reqMsg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            try { /* ... Azure token exchange logic ... */
                 var response = await httpClient.SendAsync(reqMsg);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode, new { message = "Azure DevOps token exchange failed.", error = body });

                using var jsonDoc = JsonDocument.Parse(body);
                 if (!jsonDoc.RootElement.TryGetProperty("access_token", out var tokenEl) || tokenEl.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(tokenEl.GetString())) {
                    var err = jsonDoc.RootElement.TryGetProperty("error_description", out var d) ? d.GetString() : "Unknown";
                    return Unauthorized(new { message = $"Azure DevOps token exchange failed: {err}" });
                }
                var accessToken = tokenEl.GetString()!;
                 await _firestoreService.SaveProviderAuthAsync(userId, "azure_devops", accessToken, "unknown", null);
                 return Ok(new { message = "Azure DevOps account connected successfully." });
            } catch (Exception ex) {
                 Console.WriteLine($"Error during Azure OAuth for user {userId}: {ex.Message}");
                return StatusCode(500, new { message = "An unexpected error occurred during Azure OAuth exchange.", error = ex.Message });
            }
        }

        // --- ✨ ADD THIS ENDPOINT FOR MANUAL PAT SAVING --- ✨
        /// <summary>
        /// Saves a manually provided Azure DevOps Personal Access Token (PAT).
        /// Route: POST /api/oauth/azure/save-pat
        /// </summary>
        [HttpPost("azure/save-pat")]
        public async Task<IActionResult> SaveAzureDevOpsPat([FromBody] PatRequest request) // Uses PatRequest model
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "User ID not found." });
            if (string.IsNullOrEmpty(request?.Token)) return BadRequest(new { message = "Access Token (PAT) is required." });

            // Basic validation (PATs are typically long and complex)
            if (request.Token.Length < 40 || request.Token.Contains(" ")) // Example simple checks
            {
                return BadRequest(new { message = "Invalid Personal Access Token format provided." });
            }

            try
            {
                // Optional: Validate the PAT here by making a simple Azure DevOps API call
                // using var httpClient = _httpClientFactory.CreateClient(); ...

                // Use FirestoreService to save the PAT securely
                await _firestoreService.SaveProviderAuthAsync(userId, "azure_devops", request.Token, "unknown_pat_user", null); // Save PAT

                return Ok(new { message = "Azure DevOps Personal Access Token saved successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving Azure DevOps PAT for user {userId}: {ex.Message}");
                // Log full exception details if possible
                return StatusCode(500, new { message = "Failed to save Azure DevOps token due to an internal error." });
            }
        }
        // --- ✨ END OF ADDED ENDPOINT --- ✨

    } // End of OAuthController class
} // End of namespace