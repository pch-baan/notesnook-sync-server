using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Notesnook.API.Paddle;
using Streetwriters.Common;
using Streetwriters.Common.Accessors;
using Streetwriters.Common.Enums;
using Streetwriters.Common.Models;
using Streetwriters.Common.Services;
using Streetwriters.Data.Repositories;

namespace Notesnook.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("subscriptions")]
    public class SubscriptionController : ControllerBase
    {
        private readonly Repository<Subscription> _subscriptions;
        private readonly PaddleBillingService _paddleBilling;
        private readonly WampServiceAccessor _serviceAccessor;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(
            Repository<Subscription> subscriptions,
            PaddleBillingService paddleBilling,
            WampServiceAccessor serviceAccessor,
            ILogger<SubscriptionController> logger)
        {
            _subscriptions = subscriptions;
            _paddleBilling = paddleBilling;
            _serviceAccessor = serviceAccessor;
            _logger = logger;
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest request)
        {
            if (!Constants.PADDLE_ENABLED)
                return NotFound();

            var userId = User.GetUserId();
            var priceId = PaddlePlanMapper.GetPriceId(request.Plan, request.Period);
            if (priceId == null)
                return BadRequest(new { error = "Invalid plan or period." });

            // Get user email for pre-filling checkout
            var user = await _serviceAccessor.UserAccountService.GetUserAsync("notesnook", userId);
            var customerEmail = user?.Email;

            return Ok(new CheckoutResponse
            {
                PriceId = priceId,
                CustomData = new Dictionary<string, string> { { "userId", userId } },
                CustomerEmail = customerEmail
            });
        }

        [HttpPost("cancel")]
        public async Task<IActionResult> CancelSubscription()
        {
            if (!Constants.PADDLE_ENABLED)
                return NotFound();

            var userId = User.GetUserId();
            var subscription = await _subscriptions.FindOneAsync(s => s.UserId == userId);

            if (subscription?.SubscriptionId == null)
                return BadRequest(new { error = "No active subscription found." });

            if (subscription.Status == SubscriptionStatus.CANCELED)
                return BadRequest(new { error = "Subscription is already canceled." });

            var result = await _paddleBilling.CancelSubscriptionAsync(subscription.SubscriptionId);
            if (result?.Error != null)
            {
                _logger.LogError("Failed to cancel Paddle subscription {SubscriptionId}: {Error}",
                    subscription.SubscriptionId, result.Error.Detail);
                return StatusCode(502, new { error = "Failed to cancel subscription. Please try again." });
            }

            return Ok(new { message = "Subscription will be canceled at end of billing period." });
        }

        [HttpPost("pause")]
        public async Task<IActionResult> PauseSubscription()
        {
            if (!Constants.PADDLE_ENABLED)
                return NotFound();

            var userId = User.GetUserId();
            var subscription = await _subscriptions.FindOneAsync(s => s.UserId == userId);

            if (subscription?.SubscriptionId == null)
                return BadRequest(new { error = "No active subscription found." });

            if (subscription.Status != SubscriptionStatus.ACTIVE && subscription.Status != SubscriptionStatus.TRIAL)
                return BadRequest(new { error = "Subscription cannot be paused in its current state." });

            var result = await _paddleBilling.PauseSubscriptionAsync(subscription.SubscriptionId);
            if (result?.Error != null)
            {
                _logger.LogError("Failed to pause Paddle subscription {SubscriptionId}: {Error}",
                    subscription.SubscriptionId, result.Error.Detail);
                return StatusCode(502, new { error = "Failed to pause subscription. Please try again." });
            }

            return Ok(new { message = "Subscription will be paused at end of billing period." });
        }

        [HttpPost("resume")]
        public async Task<IActionResult> ResumeSubscription()
        {
            if (!Constants.PADDLE_ENABLED)
                return NotFound();

            var userId = User.GetUserId();
            var subscription = await _subscriptions.FindOneAsync(s => s.UserId == userId);

            if (subscription?.SubscriptionId == null)
                return BadRequest(new { error = "No subscription found." });

            if (subscription.Status != SubscriptionStatus.PAUSED)
                return BadRequest(new { error = "Subscription is not paused." });

            var result = await _paddleBilling.ResumeSubscriptionAsync(subscription.SubscriptionId);
            if (result?.Error != null)
            {
                _logger.LogError("Failed to resume Paddle subscription {SubscriptionId}: {Error}",
                    subscription.SubscriptionId, result.Error.Detail);
                return StatusCode(502, new { error = "Failed to resume subscription. Please try again." });
            }

            return Ok(new { message = "Subscription resumed." });
        }

        [HttpGet("update-payment-method")]
        public async Task<IActionResult> GetUpdatePaymentMethodUrl()
        {
            if (!Constants.PADDLE_ENABLED)
                return NotFound();

            var userId = User.GetUserId();
            var subscription = await _subscriptions.FindOneAsync(s => s.UserId == userId);

            if (subscription?.UpdateURL == null)
                return BadRequest(new { error = "No subscription with payment method found." });

            return Ok(new { url = subscription.UpdateURL });
        }
    }

    public class CheckoutRequest
    {
        [JsonPropertyName("plan")]
        public SubscriptionPlan Plan { get; set; }

        [JsonPropertyName("period")]
        public string Period { get; set; } = "monthly";
    }

    public class CheckoutResponse
    {
        [JsonPropertyName("priceId")]
        public string PriceId { get; set; } = "";

        [JsonPropertyName("customData")]
        public Dictionary<string, string> CustomData { get; set; } = new();

        [JsonPropertyName("customerEmail")]
        public string? CustomerEmail { get; set; }
    }
}
