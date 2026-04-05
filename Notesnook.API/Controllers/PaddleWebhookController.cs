using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Notesnook.API.Paddle;
using Streetwriters.Common;

namespace Notesnook.API.Controllers
{
    [ApiController]
    [Route("paddle")]
    public class PaddleWebhookController : ControllerBase
    {
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleWebhook(
            [FromServices] PaddleWebhookService webhookService,
            [FromServices] ILogger<PaddleWebhookController> logger)
        {
            if (!Constants.PADDLE_ENABLED)
                return NotFound();

            // Read raw body for signature verification
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();

            // Verify webhook signature
            var signature = Request.Headers["Paddle-Signature"].ToString();
            if (!PaddleWebhookVerifier.VerifySignature(rawBody, signature, Constants.PADDLE_WEBHOOK_SECRET!))
            {
                logger.LogWarning("Invalid Paddle webhook signature");
                return Unauthorized();
            }

            // Deserialize and process
            var webhookEvent = JsonSerializer.Deserialize<PaddleWebhookEvent>(rawBody);
            if (webhookEvent == null)
            {
                logger.LogWarning("Failed to deserialize Paddle webhook event");
                return BadRequest();
            }

            // Process asynchronously but respond immediately (Paddle expects 2xx within 5 seconds)
            _ = Task.Run(async () =>
            {
                try
                {
                    await webhookService.ProcessEventAsync(webhookEvent);
                }
                catch (System.Exception ex)
                {
                    logger.LogError(ex, "Error processing Paddle webhook {EventType} ({EventId})",
                        webhookEvent.EventType, webhookEvent.EventId);
                }
            });

            return Ok();
        }
    }
}
