using Google.Cloud.Firestore;
using System;

namespace ErrorAnalysisBackend.Models
{
    // C# convention: Class name starts with uppercase
    [FirestoreData]
    public class Plan
    {
        [FirestoreProperty]
        public string PlanName { get; set; } = string.Empty;

        [FirestoreProperty]
        public long ErrorAnalysisLimit { get; set; }

        [FirestoreProperty]
        public long RepoLimit { get; set; }
    }

    [FirestoreData]
    public class UserProfile
    {
        [FirestoreProperty]
        public string Email { get; set; } = string.Empty;

        [FirestoreProperty]
        public string PlanId { get; set; } = "free";

        [FirestoreProperty]
        public long ErrorAnalysisCount { get; set; } = 0;

        [FirestoreProperty]
        public long RepoCount { get; set; } = 0;
    }

    public class AnalysisRequest
    {
        public string ErrorText { get; set; } = string.Empty;
    }

    public class PlanUpdateRequest
    {
       public string PlanName { get; set; } = string.Empty;
    }

    public class CodeRequest // Used in OAuthController
    {
        public string Code { get; set; } = string.Empty;
    }

    // ✨ --- ADD THIS CLASS --- ✨
    // Model for receiving a manually entered Personal Access Token
    public class PatRequest
    {
        public string Token { get; set; } = string.Empty;
    }
    // ✨ --- END OF ADDED CLASS --- ✨

    // (Other models like AnalysisResult, ConnectRepoRequest go here)
}