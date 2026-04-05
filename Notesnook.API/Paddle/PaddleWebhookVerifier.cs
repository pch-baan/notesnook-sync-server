using System;
using System.Security.Cryptography;
using System.Text;

namespace Notesnook.API.Paddle
{
    public static class PaddleWebhookVerifier
    {
        public static bool VerifySignature(string rawBody, string paddleSignatureHeader, string webhookSecret, int maxAgeSeconds = 300)
        {
            if (string.IsNullOrEmpty(paddleSignatureHeader) || string.IsNullOrEmpty(webhookSecret))
                return false;

            string? ts = null;
            string? h1 = null;

            // Parse "ts=123;h1=abc..."
            var parts = paddleSignatureHeader.Split(';');
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;

                if (kv[0] == "ts") ts = kv[1];
                else if (kv[0] == "h1") h1 = kv[1];
            }

            if (ts == null || h1 == null)
                return false;

            // Check timestamp freshness to prevent replay attacks
            if (maxAgeSeconds > 0 && long.TryParse(ts, out var timestamp))
            {
                var eventTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                if (DateTimeOffset.UtcNow - eventTime > TimeSpan.FromSeconds(maxAgeSeconds))
                    return false;
            }

            // Compute HMAC-SHA256
            var signedPayload = $"{ts}:{rawBody}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
            var computedSignature = Convert.ToHexStringLower(hash);

            // Timing-safe comparison
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computedSignature),
                Encoding.UTF8.GetBytes(h1)
            );
        }
    }
}
