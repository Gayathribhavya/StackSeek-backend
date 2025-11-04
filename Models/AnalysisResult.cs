namespace ErrorAnalysisBackend.Models
{
    public class AnalysisResult
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string RepoId { get; set; }
        public string Summary { get; set; }
        public string FileInvolved { get; set; }
        public string FunctionInvolved { get; set; }
        public string ReproductionSteps { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
