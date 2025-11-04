using Google.Cloud.Firestore;

namespace ErrorAnalysisBackend.Models
{
    [FirestoreData] // Maps this class to Firestore documents
    public class UserAnalysis
    {
        [FirestoreProperty("userId")] // Maps to the 'userId' field in Firestore
        public string UserId { get; set; } = string.Empty; // Default to empty string

        [FirestoreProperty("plan")] // Maps to the 'plan' field
        public string Plan { get; set; } = "Free"; // Defaults to the "Free" plan

        [FirestoreProperty("analysisCount")] // Maps to the 'analysisCount' field
        public long AnalysisCount { get; set; } = 0; // Defaults to 0 analyses used

        [FirestoreProperty("repoCount")] // Maps to the 'repoCount' field
        public long RepoCount { get; set; } = 0; // Defaults to 0 repositories connected

        [FirestoreProperty("analysisLimit")] // Maps to the 'analysisLimit' field
        public int AnalysisLimit { get; set; } = 5; // Default Free plan analysis limit

        [FirestoreProperty("repoLimit")] // Maps to the 'repoLimit' field
        public int RepoLimit { get; set; } = 5; // Default Free plan repository limit

        [FirestoreProperty("lastUpdated")] // Maps to the 'lastUpdated' field
        public Timestamp LastUpdated { get; set; } = Timestamp.GetCurrentTimestamp(); // Default to now
    }
}