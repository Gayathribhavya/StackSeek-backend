using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErrorAnalysisBackend.Models;

namespace ErrorAnalysisBackend.Services
{
    public class UserAnalysisService
    {
        private readonly FirestoreDb _db;

        public UserAnalysisService(FirestoreDb db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // Renamed/Corrected for consistency (uses Plan uppercase)
        public async Task<(bool success, string message, long newCount)> CheckAndIncrementUsage(string userId)
        {
            // ... (Logic as previously provided, using Plan class) ...
             if (string.IsNullOrEmpty(userId)) return (false, "User ID cannot be null or empty.", 0);
            try
            {
                var userRef = _db.Collection("users").Document(userId);
                var userDoc = await userRef.GetSnapshotAsync();
                if (!userDoc.Exists) return (false, "User profile not found.", 0);

                var userData = userDoc.ConvertTo<UserProfile>();
                long currentCount = userData.ErrorAnalysisCount;
                string planId = userData.PlanId ?? "free";

                var planRef = _db.Collection("plans").Document(planId);
                var planDoc = await planRef.GetSnapshotAsync();
                if (!planDoc.Exists) return (false, $"Subscription plan '{planId}' not found.", 0);

                var planData = planDoc.ConvertTo<Plan>(); // <-- Uses corrected Plan class
                long planLimit = planData.ErrorAnalysisLimit;

                if (planLimit == -1 || currentCount < planLimit)
                {
                    await userRef.UpdateAsync("ErrorAnalysisCount", FieldValue.Increment(1));
                    return (true, "Analysis permitted.", currentCount + 1);
                }
                else
                {
                    return (false, "You have reached your error analysis limit. Please upgrade.", currentCount);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CheckAndIncrementUsage for user {userId}: {ex.Message}");
                return (false, $"An internal error occurred while checking usage.", 0);
            }
        }

        public async Task<(bool success, string message, long newCount)> CheckAndIncrementRepoCount(string userId)
        {
             // ... (Logic as previously provided, using Plan uppercase) ...
             if (string.IsNullOrEmpty(userId)) return (false, "User ID cannot be null or empty.", 0);
            try
            {
                var userRef = _db.Collection("users").Document(userId);
                var userDoc = await userRef.GetSnapshotAsync();
                if (!userDoc.Exists) return (false, "User profile not found.", 0);

                var userData = userDoc.ConvertTo<UserProfile>();
                long currentRepoCount = userData.RepoCount;
                string planId = userData.PlanId ?? "free";

                var planRef = _db.Collection("plans").Document(planId);
                var planDoc = await planRef.GetSnapshotAsync();
                if (!planDoc.Exists) return (false, $"Subscription plan '{planId}' not found.", 0);

                var planData = planDoc.ConvertTo<Plan>(); // <-- Uses corrected Plan class
                long repoLimit = planData.RepoLimit;

                if (repoLimit == -1 || currentRepoCount < repoLimit)
                {
                    await userRef.UpdateAsync("RepoCount", FieldValue.Increment(1));
                    return (true, "Repo limit check passed and count incremented.", currentRepoCount + 1);
                }
                else
                {
                    return (false, "You have reached your repository limit. Please upgrade.", currentRepoCount);
                }
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Error in CheckAndIncrementRepoCount for user {userId}: {ex.Message}");
                return (false, $"An internal error occurred while checking repo limit.", 0);
            }
        }

        // Method signature used in the controller: SetUserPlanAsync(string targetUserId, string planName)
        public async Task SetUserPlanAsync(string userId, string planName)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(planName))
            {
                throw new ArgumentException("User ID and Plan Name are required.");
            }
            var planRef = _db.Collection("plans").Document(planName);
            var planDoc = await planRef.GetSnapshotAsync();
            if (!planDoc.Exists)
            {
                throw new ArgumentException($"Plan '{planName}' does not exist in Firestore 'plans' collection.");
            }
            var userRef = _db.Collection("users").Document(userId);
            var userSnap = await userRef.GetSnapshotAsync();
            if (!userSnap.Exists)
            {
                 throw new ArgumentException($"User '{userId}' does not exist.");
            }
            await userRef.UpdateAsync("PlanId", planName);
        }

        public async Task DecrementRepoCountAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            try
            {
                var userRef = _db.Collection("users").Document(userId);
                await userRef.UpdateAsync("RepoCount", FieldValue.Increment(-1));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decrementing repo count for user {userId}: {ex.Message}");
            }
        }

         // Method signature used in the controller: GetTopUsersAsync(int count)
         public async Task<List<UserProfile>> GetTopUsersAsync(int count)
         {
             var usersRef = _db.Collection("users");
             QuerySnapshot snapshot = await usersRef
                                            .OrderByDescending("ErrorAnalysisCount")
                                            .Limit(count)
                                            .GetSnapshotAsync();
             return snapshot.Documents.Select(doc => doc.ConvertTo<UserProfile>()).ToList();
         }
    }
}