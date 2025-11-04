using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
// using Microsoft.Extensions.Configuration; // Keep if needed for other config
using System.Security.Claims;          // Correct way to get User ID
using ErrorAnalysisBackend.Services;     // Your services
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;
using ErrorAnalysisBackend.Models;       // Your models
// using FirebaseAdmin.Auth; // Only needed if you directly interact with Auth users here

namespace ErrorAnalysisBackend.Controllers
{
    [Authorize] // 🔐 Secure the whole controller
    [ApiController]
    [Route("api/repository")]
    public class RepositoryController : ControllerBase
    {
        private readonly FirestoreDb _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly UserAnalysisService _userAnalysisService;

        public RepositoryController(
            FirestoreDb db,
            IHttpClientFactory httpClientFactory,
            UserAnalysisService userAnalysisService)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _userAnalysisService = userAnalysisService;
        }

        public class ConnectRepoRequest
        {
            public string RepoUrl { get; set; } = string.Empty;
            public bool IsPrivate { get; set; }
        }

        [HttpPost("connect")]
        public async Task<IActionResult> ConnectRepo([FromBody] ConnectRepoRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.RepoUrl))
                return BadRequest(new { message = "Request body is missing or invalid (RepoUrl required)." });

            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "User ID not found in token." });

            if (!Uri.IsWellFormedUriString(model.RepoUrl, UriKind.Absolute))
                return BadRequest(new { message = "Invalid repository URL format." });

            // Check if repo already exists
            var existingQuery = _db.Collection("repositories")
                                   .WhereEqualTo("userId", userId)
                                   .WhereEqualTo("url", model.RepoUrl);
            var existingSnapshot = await existingQuery.Limit(1).GetSnapshotAsync();

            // ✨ --- THIS IS LINE 69 (approx) - CORRECTED CHECK --- ✨
            if (existingSnapshot.Count > 0) // Use Count instead of IsEmpty
            // ✨ --- END OF CORRECTION --- ✨
            {
                return Ok(new { message = "Repository already connected." });
            }

            // --- USE SERVICE TO CHECK AND INCREMENT REPO LIMIT ---
            var (success, limitMessage, newCount) = await _userAnalysisService.CheckAndIncrementRepoCount(userId);
            if (!success)
            {
                return StatusCode(429, new { message = limitMessage });
            }
            // --- END OF LIMIT CHECK AND INCREMENT ---

            Uri repoUri = new Uri(model.RepoUrl);
            string host = repoUri.Host.ToLowerInvariant();
            string providerName = null;
            string apiValidationUrl = null;
            string tokenFieldName = null;
            string authScheme = "Bearer";

            // Determine provider and API details
             if (host.Contains("github.com")) {
                 providerName = "github";
                 tokenFieldName = "GitHubToken";
                 authScheme = "token";
                 apiValidationUrl = model.RepoUrl.Replace("https://github.com/", "https://api.github.com/repos/");
             } else if (host.Contains("bitbucket.org")) {
                 providerName = "bitbucket";
                 tokenFieldName = "BitbucketToken";
                 apiValidationUrl = model.RepoUrl.Replace("https://bitbucket.org/", "https://api.bitbucket.org/2.0/repositories/");
             } else if (host.Contains("gitlab.com")) {
                 providerName = "gitlab";
                 tokenFieldName = "GitLabToken";
                 var projectPath = repoUri.AbsolutePath.Trim('/');
                 if (projectPath.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) projectPath = projectPath[..^4];
                 var encodedPath = Uri.EscapeDataString(projectPath);
                 apiValidationUrl = $"https://gitlab.com/api/v4/projects/{encodedPath}";
             } else if (host.Contains("dev.azure.com") || host.Contains("visualstudio.com")) {
                 providerName = "azure_devops";
                 tokenFieldName = "AzureDevOpsToken";
                 apiValidationUrl = null; // Skip direct API validation for PAT
             } else {
                 await _userAnalysisService.DecrementRepoCountAsync(userId); // Rollback increment
                 return BadRequest(new { message = "Unsupported repository host." });
             }

            // --- Validate token and repository access ---
            string accessToken = null;
            try
            {
                var tokenRef = _db.Collection("users").Document(userId).Collection("privateData").Document("oauthTokens");
                var tokenDoc = await tokenRef.GetSnapshotAsync();

                if (!tokenDoc.Exists || !tokenDoc.TryGetValue(tokenFieldName, out accessToken) || string.IsNullOrEmpty(accessToken))
                {
                    await _userAnalysisService.DecrementRepoCountAsync(userId); // Rollback increment
                    return Unauthorized(new { message = $"Access token for {providerName} not found or not connected." });
                }

                // If we have an API URL, try to validate access
                if (!string.IsNullOrEmpty(apiValidationUrl))
                {
                    using var httpClient = _httpClientFactory.CreateClient();
                    var request = new HttpRequestMessage(HttpMethod.Get, apiValidationUrl);
                    request.Headers.Authorization = new AuthenticationHeaderValue(authScheme, accessToken);
                    request.Headers.UserAgent.ParseAdd("StackSeekApp/1.0");

                    var response = await httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        await _userAnalysisService.DecrementRepoCountAsync(userId); // Rollback increment
                        var errorContent = await response.Content.ReadAsStringAsync();
                        return BadRequest(new { message = $"Failed to verify access to the {providerName} repository.", status = response.StatusCode, error = errorContent });
                    }
                }
                 else if (providerName == "azure_devops") // Specific checks for Azure PAT
                {
                     if (accessToken.Length < 40) { // Basic length check
                         await _userAnalysisService.DecrementRepoCountAsync(userId);
                         return BadRequest(new { message = "Azure DevOps token appears invalid (too short)." });
                     }
                      Console.WriteLine($"[Azure DevOps] Token format validation passed for user {userId}");
                }
            }
            catch (Exception ex)
            {
                await _userAnalysisService.DecrementRepoCountAsync(userId); // Rollback increment on any error
                Console.WriteLine($"ERROR verifying repository access for user {userId}, repo {model.RepoUrl}: {ex}");
                return StatusCode(500, new { message = "Failed to verify repository access due to an internal error.", error = ex.Message });
            }

            // --- Save the repository ---
            try
            {
                var docRef = _db.Collection("repositories").Document(); // Auto-generate ID
                var newRepoData = new
                {
                    userId = userId,
                    url = model.RepoUrl,
                    isPrivate = model.IsPrivate,
                    provider = providerName,
                    createdAt = Timestamp.GetCurrentTimestamp()
                };
                await docRef.SetAsync(newRepoData);

                return Ok(new { message = "Repository connected and saved successfully.", repositoryId = docRef.Id });
            }
            catch (Exception ex)
            {
                // If saving fails AFTER incrementing, roll back the increment.
                await _userAnalysisService.DecrementRepoCountAsync(userId); // Rollback increment
                Console.WriteLine($"ERROR saving repository for user {userId}, repo {model.RepoUrl}: {ex}");
                return StatusCode(500, new { message = "Repository validated but failed to save.", error = ex.Message });
            }
        }

        // -----------------------------
        // USER'S SAVED REPOS
        // -----------------------------
        [HttpGet("user")] // Route: api/repository/user
        public async Task<IActionResult> GetUserRepositories()
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "User ID not found in token." });

            try
            {
                var reposQuery = _db.Collection("repositories").WhereEqualTo("userId", userId);
                var reposSnapshot = await reposQuery.GetSnapshotAsync();

                var results = reposSnapshot.Documents.Select(doc =>
                {
                    doc.TryGetValue("url", out string url);
                    doc.TryGetValue("isPrivate", out bool isPrivate);
                    doc.TryGetValue("provider", out string provider);
                    return new {
                        id = doc.Id,
                        url = url ?? string.Empty,
                        isPrivate = isPrivate,
                        provider = provider
                    };
                }).ToList();

                return Ok(results);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR getting user repositories for {userId}: {ex}");
                return StatusCode(500, new { message = "Failed to retrieve repositories." });
            }
        }

        // -----------------------------
        // DELETE A SINGLE CONNECTED REPOSITORY
        // -----------------------------
        [HttpDelete("{id}")] // Route: api/repository/{id}
        public async Task<IActionResult> DeleteRepository(string id)
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "User ID not found in token." });

            if (string.IsNullOrEmpty(id)) return BadRequest(new { message = "Repository ID is required." });

            var repoRef = _db.Collection("repositories").Document(id);

            try
            {
                var snapshot = await repoRef.GetSnapshotAsync();

                if (!snapshot.Exists)
                    return NotFound(new { message = "Repository not found." });

                snapshot.TryGetValue("userId", out string repoOwnerId);
                if (repoOwnerId != userId)
                {
                    return Forbid(); // 403 Forbidden
                }

                await repoRef.DeleteAsync();

                await _userAnalysisService.DecrementRepoCountAsync(userId);

                Console.WriteLine($"[Repository] Deleted repo {id} for user {userId}");
                return Ok(new { message = "Repository deleted successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR deleting repository {id} for user {userId}: {ex}");
                return StatusCode(500, new { message = "Failed to delete repository." });
            }
        }
    }
}