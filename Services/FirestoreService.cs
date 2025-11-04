using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using ErrorAnalysisBackend.Models; // Your models

namespace ErrorAnalysisBackend.Services
{
    public class FirestoreService
    {
        private readonly FirestoreDb _firestoreDb;
        private readonly UserAnalysisService _userAnalysisService; // Still needs UserAnalysisService for CreateAnalysisAsync

        public FirestoreService(FirestoreDb firestoreDb, UserAnalysisService userAnalysisService)
        {
            _firestoreDb = firestoreDb;
            _userAnalysisService = userAnalysisService;
        }

        public FirestoreDb GetDb() // Optional helper
        {
            return _firestoreDb;
        }

        /// <summary>
        /// Creates a new analysis result document in Firestore.
        /// IMPORTANT: Assumes usage limit check was done *before* calling this.
        /// </summary>
        public async Task<string> CreateAnalysisAsync(AnalysisResult newAnalysis)
        {
            if (newAnalysis == null || string.IsNullOrEmpty(newAnalysis.UserId))
            {
                throw new ArgumentNullException(nameof(newAnalysis), "Analysis result or UserId cannot be null.");
            }

            try
            {
                newAnalysis.Timestamp = DateTime.UtcNow; // Set timestamp
                var collection = _firestoreDb.Collection("analysis_results");
                var docRef = await collection.AddAsync(newAnalysis);

                // We still need to increment the count AFTER saving the result successfully.
                // Call the correct method name from UserAnalysisService.
                var (_, _, _) = await _userAnalysisService.CheckAndIncrementUsage(newAnalysis.UserId);
                // Note: If CheckAndIncrementUsage fails here (e.g., limit *just* reached), the analysis is saved but the count isn't incremented.
                // More robust logic might involve a Firestore Transaction.

                return docRef.Id;
            }
            catch(Exception ex)
            {
                 Console.WriteLine($"Error creating analysis result for user {newAnalysis.UserId}: {ex.Message}");
                 throw; // Re-throw the exception so the controller knows something went wrong
            }
        }

        /// <summary>
        /// Saves authentication details for various providers securely.
        /// </summary>
        public async Task SaveProviderAuthAsync(string uid, string provider, string token, string username, string email)
        {
            // ... (Keep the SaveProviderAuthAsync method exactly as provided in the previous response) ...
            if (string.IsNullOrWhiteSpace(uid)) throw new ArgumentException("uid required");
            if (string.IsNullOrWhiteSpace(provider)) throw new ArgumentException("provider required");

            provider = provider.Trim().ToLowerInvariant(); // Normalize provider name

            var userDocRef = _firestoreDb.Collection("users").Document(uid);
            var privateDataRef = userDocRef.Collection("privateData").Document("oauthTokens");
            var publicProviderRef = userDocRef.Collection("authProviders").Document(provider);

            var batch = _firestoreDb.StartBatch();

            // Prepare data for the private token document
            var tokenData = new Dictionary<string, object>();
            string tokenFieldName = GetTokenFieldName(provider);
            if (!string.IsNullOrEmpty(tokenFieldName) && !string.IsNullOrEmpty(token)) // Check token isn't null/empty
            {
                tokenData[tokenFieldName] = token;
                batch.Set(privateDataRef, tokenData, SetOptions.MergeAll); // Merge token data
            }

            // Prepare data for the public provider info document
             var publicProviderData = new Dictionary<string, object>
             {
                 { "provider", provider },
                 { "username", username ?? "unknown" },
                 // Only add email if it's not null or empty
                 { "email", !string.IsNullOrEmpty(email) ? email : null },
                 { "savedAt", Timestamp.GetCurrentTimestamp() }
             };
             // Remove null email entry if it exists to avoid storing nulls
             if (publicProviderData["email"] == null)
             {
                 publicProviderData.Remove("email");
             }
             batch.Set(publicProviderRef, publicProviderData, SetOptions.Overwrite); // Overwrite public info

             // Update the main user document's connectedProviders array
            batch.Update(userDocRef, "connectedProviders", FieldValue.ArrayUnion(provider));

            await batch.CommitAsync();
            Console.WriteLine($"[Firestore] Saved provider auth for user={uid}, provider={provider}");
        }


        // ... (Keep other methods like SaveTwoFactorSecretAsync, GetTwoFactorSecretAsync, Enable/Disable 2FA, DeleteUserDataAsync) ...
        // ... (Ensure they use try/catch and the DeleteCollectionAsync helper) ...

         public async Task SaveTwoFactorSecretAsync(string uid, string secret)
         {
              var userDocRef = _firestoreDb.Collection("users").Document(uid);
              // Store hashed secret in privateData
              var privateDataRef = userDocRef.Collection("privateData").Document("security");
              await privateDataRef.SetAsync(new { HashedTwoFactorSecret = HashSecret(secret) }, SetOptions.MergeAll); // Example hashing
              // Don't store raw secret directly
         }

         public async Task<string?> GetTwoFactorSecretAsync(string uid) // Should return HASHED secret
         {
              var privateDataRef = _firestoreDb.Collection("users").Document(uid).Collection("privateData").Document("security");
              var doc = await privateDataRef.GetSnapshotAsync();
              if (!doc.Exists || !doc.TryGetValue("HashedTwoFactorSecret", out string hashedSecret))
                  return null;
              return hashedSecret;
         }

         // Basic placeholder for hashing - USE A PROPER LIBRARY like BCrypt.Net
         private string HashSecret(string secret) {
             // REPLACE THIS with a real hashing implementation
             return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secret)).Reverse().ToString() ?? string.Empty;
         }


         public async Task EnableTwoFactorAsync(string uid)
         {
              var userDocRef = _firestoreDb.Collection("users").Document(uid);
              await userDocRef.UpdateAsync(new Dictionary<string, object>
              {
                  { "twoFactorEnabled", true },
                  { "twoFactorSetupDate", Timestamp.GetCurrentTimestamp() }
              });
         }

         public async Task DisableTwoFactorAsync(string uid)
         {
              var userDocRef = _firestoreDb.Collection("users").Document(uid);
               var privateDataRef = userDocRef.Collection("privateData").Document("security");
              await userDocRef.UpdateAsync(new Dictionary<string, object>
              {
                  { "twoFactorEnabled", false },
                  // { "twoFactorSecret", FieldValue.Delete }, // Don't delete from main doc if moved
                  { "twoFactorSetupDate", FieldValue.Delete }
              });
              // Delete from privateData
              await privateDataRef.UpdateAsync("HashedTwoFactorSecret", FieldValue.Delete);
         }


        public async Task DeleteUserDataAsync(string uid)
        {
            try {
                var userDocRef = _firestoreDb.Collection("users").Document(uid);

                // Batch delete subcollections
                await DeleteCollectionAsync(userDocRef.Collection("uploadedRepos"));
                await DeleteCollectionAsync(userDocRef.Collection("authProviders"));
                await DeleteCollectionAsync(userDocRef.Collection("privateData"));

                // Delete the main user document
                await userDocRef.DeleteAsync();

                // Delete related repositories in batches
                Query reposQuery = _firestoreDb.Collection("repositories").WhereEqualTo("userId", uid);
                await DeleteQueryBatchAsync(reposQuery);

                Console.WriteLine($"[Firestore] Deleted all data for user {uid}");
            } catch (Exception ex) {
                 Console.WriteLine($"Error deleting user data for {uid}: {ex.Message}");
                 // Decide if you need to re-throw
            }
        }

        // --- Helper Methods ---

        private string GetTokenFieldName(string provider)
        {
            switch (provider?.ToLowerInvariant()) // Add null check
            {
                case "github": return "GitHubToken";
                case "bitbucket": return "BitbucketToken";
                case "gitlab": return "GitLabToken";
                case "azure_devops": return "AzureDevOpsToken";
                default: return null;
            }
        }

        private async Task DeleteCollectionAsync(CollectionReference collectionReference, int batchSize = 100)
        {
             QuerySnapshot snapshot = await collectionReference.Limit(batchSize).GetSnapshotAsync();
             IReadOnlyList<DocumentSnapshot> documents = snapshot.Documents;
             while (documents.Count > 0)
             {
                 var batch = _firestoreDb.StartBatch();
                 foreach (var document in documents) { batch.Delete(document.Reference); }
                 await batch.CommitAsync();
                 snapshot = await collectionReference.Limit(batchSize).GetSnapshotAsync();
                 documents = snapshot.Documents;
             }
        }
         // Helper to delete documents from a query in batches
        private async Task DeleteQueryBatchAsync(Query query, int batchSize = 100)
        {
            QuerySnapshot snapshot = await query.Limit(batchSize).GetSnapshotAsync();
            IReadOnlyList<DocumentSnapshot> documents = snapshot.Documents;
            while (documents.Count > 0)
            {
                var batch = _firestoreDb.StartBatch();
                foreach (var document in documents) { batch.Delete(document.Reference); }
                await batch.CommitAsync();
                snapshot = await query.Limit(batchSize).GetSnapshotAsync();
                documents = snapshot.Documents;
            }
        }
    }
}