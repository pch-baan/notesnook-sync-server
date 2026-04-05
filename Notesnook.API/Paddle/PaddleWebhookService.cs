using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Streetwriters.Common;
using Streetwriters.Common.Enums;
using Streetwriters.Common.Messages;
using Streetwriters.Common.Models;
using Streetwriters.Data.Repositories;

namespace Notesnook.API.Paddle
{
    public class PaddleWebhookService
    {
        private readonly Repository<Subscription> _subscriptions;
        private readonly ILogger<PaddleWebhookService> _logger;

        public PaddleWebhookService(
            Repository<Subscription> subscriptions,
            ILogger<PaddleWebhookService> logger)
        {
            _subscriptions = subscriptions;
            _logger = logger;
        }

        public async Task ProcessEventAsync(PaddleWebhookEvent webhookEvent)
        {
            _logger.LogInformation("Processing Paddle webhook: {EventType} ({EventId})",
                webhookEvent.EventType, webhookEvent.EventId);

            switch (webhookEvent.EventType)
            {
                case "subscription.created":
                case "subscription.activated":
                case "subscription.updated":
                case "subscription.past_due":
                case "subscription.resumed":
                    await HandleSubscriptionUpdateAsync(webhookEvent);
                    break;

                case "subscription.canceled":
                    await HandleSubscriptionCanceledAsync(webhookEvent);
                    break;

                case "subscription.paused":
                    await HandleSubscriptionPausedAsync(webhookEvent);
                    break;

                case "transaction.completed":
                    await HandleTransactionCompletedAsync(webhookEvent);
                    break;

                default:
                    _logger.LogInformation("Ignoring unhandled Paddle event type: {EventType}", webhookEvent.EventType);
                    break;
            }
        }

        private async Task HandleSubscriptionUpdateAsync(PaddleWebhookEvent webhookEvent)
        {
            var data = webhookEvent.Data;
            var paddleSubId = GetStringProperty(data, "id");
            var userId = GetUserIdFromCustomData(data);
            var paddleStatus = GetStringProperty(data, "status");

            if (paddleSubId == null)
            {
                _logger.LogWarning("Paddle subscription event missing subscription ID");
                return;
            }

            if (userId == null)
            {
                // Try to find userId from existing subscription record
                var existing = await _subscriptions.FindOneAsync(s => s.SubscriptionId == paddleSubId);
                userId = existing?.UserId;
            }

            if (userId == null)
            {
                _logger.LogWarning("Cannot determine userId for Paddle subscription {SubscriptionId}", paddleSubId);
                return;
            }

            var subscription = await _subscriptions.FindOneAsync(s => s.UserId == userId);
            var occurredAtMs = webhookEvent.OccurredAt.ToUnixTimeMilliseconds();

            // Idempotency: skip stale events
            if (subscription != null && subscription.UpdatedAt > occurredAtMs)
            {
                _logger.LogInformation("Skipping stale event for subscription {SubscriptionId}", paddleSubId);
                return;
            }

            var plan = SubscriptionPlan.FREE;
            if (data.TryGetProperty("items", out var items))
                plan = PaddlePlanMapper.GetPlanFromItems(items);

            var status = PaddlePlanMapper.MapPaddleStatus(paddleStatus);
            var startDate = GetDateTimeProperty(data, "started_at");
            var nextBilledAt = GetDateTimeProperty(data, "next_billed_at");
            var cancelUrl = GetManagementUrl(data, "cancel");
            var updateUrl = GetManagementUrl(data, "update_payment_method");

            // Calculate expiry from current_billing_period or next_billed_at
            long expiryDate = 0;
            if (data.TryGetProperty("current_billing_period", out var billingPeriod) &&
                billingPeriod.TryGetProperty("ends_at", out var endsAt) &&
                endsAt.ValueKind == JsonValueKind.String)
            {
                if (DateTimeOffset.TryParse(endsAt.GetString(), out var endsAtDto))
                    expiryDate = endsAtDto.ToUnixTimeMilliseconds();
            }
            else if (nextBilledAt > 0)
            {
                expiryDate = nextBilledAt;
            }

            // Handle trial expiry
            long trialExpiry = 0;
            if (paddleStatus == "trialing" && expiryDate > 0)
                trialExpiry = expiryDate;

            if (subscription == null)
            {
                subscription = new Subscription
                {
                    UserId = userId,
                    AppId = ApplicationType.NOTESNOOK,
                    Provider = SubscriptionProvider.PADDLE,
                    SubscriptionId = paddleSubId,
                    Plan = plan,
                    Status = status,
                    StartDate = startDate,
                    ExpiryDate = expiryDate,
                    TrialExpiryDate = trialExpiry,
                    CancelURL = cancelUrl,
                    UpdateURL = updateUrl,
                    UpdatedAt = occurredAtMs
                };
                await _subscriptions.InsertAsync(subscription);
            }
            else
            {
                subscription.Provider = SubscriptionProvider.PADDLE;
                subscription.SubscriptionId = paddleSubId;
                subscription.Plan = plan;
                subscription.Status = status;
                if (startDate > 0) subscription.StartDate = startDate;
                subscription.ExpiryDate = expiryDate;
                subscription.TrialExpiryDate = trialExpiry;
                if (cancelUrl != null) subscription.CancelURL = cancelUrl;
                if (updateUrl != null) subscription.UpdateURL = updateUrl;
                subscription.UpdatedAt = occurredAtMs;

                await _subscriptions.UpsertAsync(subscription, s => s.UserId == userId);
            }

            await NotifySubscriptionChangedAsync(userId);
            _logger.LogInformation("Updated subscription for user {UserId}: {Plan} ({Status})", userId, plan, status);
        }

        private async Task HandleSubscriptionCanceledAsync(PaddleWebhookEvent webhookEvent)
        {
            var data = webhookEvent.Data;
            var paddleSubId = GetStringProperty(data, "id");
            var userId = GetUserIdFromCustomData(data);
            var occurredAtMs = webhookEvent.OccurredAt.ToUnixTimeMilliseconds();

            var subscription = await FindSubscriptionAsync(paddleSubId, userId);
            if (subscription == null)
            {
                _logger.LogWarning("No subscription found for canceled event: {SubscriptionId}", paddleSubId);
                return;
            }

            if (subscription.UpdatedAt > occurredAtMs) return;

            subscription.Status = SubscriptionStatus.CANCELED;
            subscription.UpdatedAt = occurredAtMs;

            var canceledAt = GetDateTimeProperty(data, "canceled_at");
            if (canceledAt > 0)
                subscription.ExpiryDate = canceledAt;

            await _subscriptions.UpsertAsync(subscription, s => s.UserId == subscription.UserId);
            await NotifySubscriptionChangedAsync(subscription.UserId);
            _logger.LogInformation("Subscription canceled for user {UserId}", subscription.UserId);
        }

        private async Task HandleSubscriptionPausedAsync(PaddleWebhookEvent webhookEvent)
        {
            var data = webhookEvent.Data;
            var paddleSubId = GetStringProperty(data, "id");
            var userId = GetUserIdFromCustomData(data);
            var occurredAtMs = webhookEvent.OccurredAt.ToUnixTimeMilliseconds();

            var subscription = await FindSubscriptionAsync(paddleSubId, userId);
            if (subscription == null)
            {
                _logger.LogWarning("No subscription found for paused event: {SubscriptionId}", paddleSubId);
                return;
            }

            if (subscription.UpdatedAt > occurredAtMs) return;

            subscription.Status = SubscriptionStatus.PAUSED;
            subscription.UpdatedAt = occurredAtMs;

            await _subscriptions.UpsertAsync(subscription, s => s.UserId == subscription.UserId);
            await NotifySubscriptionChangedAsync(subscription.UserId);
            _logger.LogInformation("Subscription paused for user {UserId}", subscription.UserId);
        }

        private async Task HandleTransactionCompletedAsync(PaddleWebhookEvent webhookEvent)
        {
            var data = webhookEvent.Data;
            var transactionId = GetStringProperty(data, "id");
            var subscriptionId = GetStringProperty(data, "subscription_id");

            if (subscriptionId == null) return;

            var subscription = await _subscriptions.FindOneAsync(s => s.SubscriptionId == subscriptionId);
            if (subscription == null) return;

            var occurredAtMs = webhookEvent.OccurredAt.ToUnixTimeMilliseconds();
            if (subscription.UpdatedAt > occurredAtMs) return;

            subscription.OrderId = transactionId;
            subscription.UpdatedAt = occurredAtMs;

            // Update expiry from billing_period if available
            if (data.TryGetProperty("billing_period", out var billingPeriod) &&
                billingPeriod.TryGetProperty("ends_at", out var endsAt) &&
                endsAt.ValueKind == JsonValueKind.String)
            {
                if (DateTimeOffset.TryParse(endsAt.GetString(), out var endsAtDto))
                    subscription.ExpiryDate = endsAtDto.ToUnixTimeMilliseconds();
            }

            await _subscriptions.UpsertAsync(subscription, s => s.UserId == subscription.UserId);
            _logger.LogInformation("Transaction {TransactionId} completed for subscription {SubscriptionId}",
                transactionId, subscriptionId);
        }

        private async Task<Subscription?> FindSubscriptionAsync(string? paddleSubId, string? userId)
        {
            if (userId != null)
            {
                var sub = await _subscriptions.FindOneAsync(s => s.UserId == userId);
                if (sub != null) return sub;
            }
            if (paddleSubId != null)
                return await _subscriptions.FindOneAsync(s => s.SubscriptionId == paddleSubId);
            return null;
        }

        private static async Task NotifySubscriptionChangedAsync(string userId)
        {
            try
            {
                await WampServers.MessengerServer.PublishMessageAsync(
                    MessengerServerTopics.SendSSETopic,
                    new SendSSEMessage
                    {
                        SendToAll = false,
                        UserId = userId,
                        Message = new Message
                        {
                            Type = "subscriptionChanged",
                            Data = "{}"
                        }
                    });
            }
            catch
            {
                // SSE notification is best-effort; don't fail webhook processing
            }
        }

        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            return null;
        }

        private static long GetDateTimeProperty(JsonElement element, string propertyName)
        {
            var value = GetStringProperty(element, propertyName);
            if (value != null && DateTimeOffset.TryParse(value, out var dto))
                return dto.ToUnixTimeMilliseconds();
            return 0;
        }

        private static string? GetUserIdFromCustomData(JsonElement data)
        {
            if (data.TryGetProperty("custom_data", out var customData) &&
                customData.ValueKind == JsonValueKind.Object &&
                customData.TryGetProperty("userId", out var userId) &&
                userId.ValueKind == JsonValueKind.String)
            {
                return userId.GetString();
            }
            return null;
        }

        private static string? GetManagementUrl(JsonElement data, string urlKey)
        {
            if (data.TryGetProperty("management_urls", out var urls) &&
                urls.ValueKind == JsonValueKind.Object &&
                urls.TryGetProperty(urlKey, out var url) &&
                url.ValueKind == JsonValueKind.String)
            {
                return url.GetString();
            }
            return null;
        }
    }
}
