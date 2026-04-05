using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Streetwriters.Common;
using Streetwriters.Common.Enums;

namespace Notesnook.API.Paddle
{
    public static class PaddlePlanMapper
    {
        private static Dictionary<string, SubscriptionPlan>? _priceIdToPlan;
        private static Dictionary<(SubscriptionPlan, string), string>? _planToPriceId;

        private static void EnsureInitialized()
        {
            if (_priceIdToPlan != null) return;

            _priceIdToPlan = new Dictionary<string, SubscriptionPlan>();
            _planToPriceId = new Dictionary<(SubscriptionPlan, string), string>();

            var mappings = new (string? priceId, SubscriptionPlan plan, string period)[]
            {
                (Constants.PADDLE_PRICE_ID_PRO_MONTHLY, SubscriptionPlan.PRO, "monthly"),
                (Constants.PADDLE_PRICE_ID_PRO_YEARLY, SubscriptionPlan.PRO, "yearly"),
                (Constants.PADDLE_PRICE_ID_ESSENTIAL_MONTHLY, SubscriptionPlan.ESSENTIAL, "monthly"),
                (Constants.PADDLE_PRICE_ID_ESSENTIAL_YEARLY, SubscriptionPlan.ESSENTIAL, "yearly"),
                (Constants.PADDLE_PRICE_ID_EDUCATION_YEARLY, SubscriptionPlan.EDUCATION, "yearly"),
            };

            foreach (var (priceId, plan, period) in mappings)
            {
                if (string.IsNullOrEmpty(priceId)) continue;
                _priceIdToPlan[priceId] = plan;
                _planToPriceId[(plan, period)] = priceId;
            }
        }

        public static SubscriptionPlan GetPlanFromPriceId(string? priceId)
        {
            EnsureInitialized();
            if (priceId != null && _priceIdToPlan!.TryGetValue(priceId, out var plan))
                return plan;
            return SubscriptionPlan.FREE;
        }

        public static string? GetPriceId(SubscriptionPlan plan, string period)
        {
            EnsureInitialized();
            _planToPriceId!.TryGetValue((plan, period), out var priceId);
            return priceId;
        }

        public static SubscriptionPlan GetPlanFromItems(JsonElement items)
        {
            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("price", out var price) &&
                        price.TryGetProperty("id", out var id))
                    {
                        return GetPlanFromPriceId(id.GetString());
                    }
                }
            }
            return SubscriptionPlan.FREE;
        }

        public static SubscriptionStatus MapPaddleStatus(string? paddleStatus)
        {
            return paddleStatus switch
            {
                "trialing" => SubscriptionStatus.TRIAL,
                "active" => SubscriptionStatus.ACTIVE,
                "past_due" => SubscriptionStatus.ACTIVE, // user still has access during dunning
                "paused" => SubscriptionStatus.PAUSED,
                "canceled" => SubscriptionStatus.CANCELED,
                _ => SubscriptionStatus.EXPIRED
            };
        }
    }
}
