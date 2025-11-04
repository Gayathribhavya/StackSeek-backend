namespace ErrorAnalysisBackend.Models
{
    public class PlanDetails
    {
        // Default to 0, will be overridden by values in UserAnalysisService
        public int AnalysisLimit { get; set; } = 0;
        public int RepoLimit { get; set; } = 0;
    }
}