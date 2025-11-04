using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ErrorAnalysisBackend.Services;
using System.Security.Claims;
using ErrorAnalysisBackend.Models; // Ensure PlanUpdateRequest is defined here
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace ErrorAnalysisBackend.Controllers
{
    [Authorize] // 🔐 Secures all endpoints
    [ApiController]
    [Route("api/[controller]")] // Base route: "api/useranalysis"
    public class UserAnalysisController : ControllerBase
    {
        private readonly UserAnalysisService _analysisService;

        public UserAnalysisController(UserAnalysisService analysisService)
        {
            _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
        }

        /// <summary>
        /// Endpoint to analyze an error message after checking usage limits.
        /// Route: POST api/useranalysis/analyze
        /// </summary>
        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeError([FromBody] AnalysisRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ErrorText))
            {
                return BadRequest(new { message = "Error text required." });
            }

            // Safely get userId
            string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value; // Use nullable string
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User ID not found." });
            }

            var (success, message, newCount) = await _analysisService.CheckAndIncrementUsage(userId);
            if (!success) {
                // Return specific status codes based on the error message
                return message.Contains("limit")
                    ? StatusCode(429, new { message, currentCount = newCount }) // 429 Too Many Requests
                    : message.Contains("not found")
                        ? NotFound(new { message }) // 404 Not Found (for user or plan)
                        : BadRequest(new { message }); // 400 Bad Request for other errors
            }

            try {
                // TODO: Replace with actual AI analysis call
                await Task.Delay(50); // Simulate work
                var aiResponse = $"Analyzed: '{request.ErrorText.Substring(0, Math.Min(30, request.ErrorText.Length))}'...";
                return Ok(new { success = true, message = "Analysis successful", analysisResult = aiResponse, newCount = newCount });
            } catch(Exception ex) {
                 Console.WriteLine($"Error during AI analysis {userId}: {ex.Message}");
                 // Log full exception details if possible
                 return StatusCode(500, new { message = "Analysis processing failed."});
            }
        }

         /// <summary>
         /// TEST Endpoint: Updates the user's plan.
         /// Route: POST api/useranalysis/plan/{targetUserId}
         /// NOTE: Add admin authorization checks for production use!
         /// </summary>
         [HttpPost("plan/{targetUserId}")]
         // Add specific authorization policy for admins later: [Authorize(Policy = "AdminOnly")]
         public async Task<IActionResult> UpdateUserPlan(string targetUserId, [FromBody] PlanUpdateRequest model) // Ensure PlanUpdateRequest is defined
         {
             if (string.IsNullOrEmpty(targetUserId) || string.IsNullOrEmpty(model?.PlanName))
             {
                  return BadRequest(new { message = "User ID and PlanName required." });
             }
             // Optional: Add admin/ownership check here for security

             try {
                 await _analysisService.SetUserPlanAsync(targetUserId, model.PlanName); // Use correct service method
                 return Ok(new { message = $"Plan for user {targetUserId} updated successfully to {model.PlanName}." });
             }
             catch (ArgumentException ex) // Catch specific errors like "Plan not found" or "User not found"
             {
                 // Return 404 if the user/plan wasn't found, 400 otherwise
                 return ex.Message.Contains("not exist") || ex.Message.Contains("not found")
                     ? NotFound(new { message = ex.Message })
                     : BadRequest(new { message = ex.Message });
             }
             catch (Exception ex) // Catch unexpected errors
             {
                  Console.WriteLine($"Error updating plan for user {targetUserId}: {ex.Message}");
                  // Log full exception
                  return StatusCode(500, new { message = "Failed to update user plan due to an internal error."});
             }
         }

         /// <summary>
         /// TEST Endpoint: Gets top users by analysis count.
         /// Route: GET api/useranalysis/top/{count}
         /// NOTE: Add admin authorization checks for production use!
         /// </summary>
         [HttpGet("top/{count}")]
         // Add specific authorization policy for admins later: [Authorize(Policy = "AdminOnly")]
         public async Task<IActionResult> GetTopUsers(int count)
         {
             if (count <= 0 || count > 100) // Validate count range
             {
                 return BadRequest(new { message = "Count must be between 1 and 100." });
             }
             try {
                 List<UserProfile> users = await _analysisService.GetTopUsersAsync(count); // Use correct service method
                 return Ok(users); // Return the list
             } catch(Exception ex) {
                  Console.WriteLine($"Error getting top users: {ex.Message}");
                  // Log full exception
                  return StatusCode(500, new { message = "Failed to retrieve top users due to an internal error."});
             }
         }
    }
}