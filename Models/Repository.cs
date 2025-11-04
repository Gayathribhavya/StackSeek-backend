// Models/Repository.cs
using Google.Cloud.Firestore;
using System;

namespace ErrorAnalysisBackend.Models
{
    [FirestoreData]
    public class Repository
    {
        // ✅ Add this property to hold Firestore document ID
        [FirestoreDocumentId]
        public string Id { get; set; }

        [FirestoreProperty]
        public string RepoUrl { get; set; }

        [FirestoreProperty]
        public string Platform { get; set; }

        [FirestoreProperty]
        public bool IsPrivate { get; set; }

        [FirestoreProperty]
        public Timestamp SubmittedAt { get; set; }
    }
}
