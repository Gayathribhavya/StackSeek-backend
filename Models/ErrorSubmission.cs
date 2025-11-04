using System; // Make sure this is included for DateTime

namespace ErrorAnalysisBackend.Models
{
    public class ErrorSubmission
    {
        // FIX: Initialize string properties to string.Empty
        public string Id { get; set; } = string.Empty; 
        
        public string RepoId { get; set; } = string.Empty;
        
        public string ErrorMessage { get; set; } = string.Empty;
        
        public DateTime SubmittedAt { get; set; } // This is okay, DateTime is a struct
    }
}