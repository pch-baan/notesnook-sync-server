using System;
using System.Threading.Tasks;
using Streetwriters.Common.Enums;
using Streetwriters.Common.Interfaces;
using Streetwriters.Common.Models;
using Streetwriters.Data.Repositories;

namespace Notesnook.API.Paddle
{
    public class PaddleSubscriptionService : IUserSubscriptionService
    {
        private readonly Repository<Subscription> _subscriptions;

        public PaddleSubscriptionService(Repository<Subscription> subscriptions)
        {
            _subscriptions = subscriptions;
        }

        public async Task<Subscription?> GetUserSubscriptionAsync(string clientId, string userId)
        {
            var subscription = await _subscriptions.FindOneAsync(s => s.UserId == userId);
            if (subscription != null)
                return subscription;

            // Return a default FREE subscription if none exists
            return new Subscription
            {
                UserId = userId,
                AppId = ApplicationType.NOTESNOOK,
                Provider = SubscriptionProvider.STREETWRITERS,
                Plan = SubscriptionPlan.FREE,
                Status = SubscriptionStatus.ACTIVE,
                StartDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ExpiryDate = 0,
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        public async Task<bool> IsUserSubscribedAsync(string clientId, string userId)
        {
            var subscription = await _subscriptions.FindOneAsync(s => s.UserId == userId);
            if (subscription == null) return false;
            return subscription.Plan != SubscriptionPlan.FREE &&
                   subscription.Status != SubscriptionStatus.EXPIRED &&
                   subscription.Status != SubscriptionStatus.CANCELED;
        }

        public Subscription TransformUserSubscription(Subscription subscription) => subscription;
    }
}
