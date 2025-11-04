namespace ErrorAnalysisBackend.Models
{
    public class AzureManualConnectModel
    {
        public string RepoUrl { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty; // This will hold the PAT
        public bool IsPrivate { get; set; } // Optional: Add if you track private status
    }
}