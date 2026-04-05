# Local development overrides
# NOTE: DotNetEnv.Load() OVERRIDES env vars from launchSettings!
# Do NOT put MONGODB_CONNECTION_STRING here — each service has its own in launchSettings.json
SELF_HOSTED=0
NOTESNOOK_SERVER_PORT=5264
NOTESNOOK_SERVER_HOST=localhost
IDENTITY_SERVER_PORT=8264
IDENTITY_SERVER_HOST=localhost
IDENTITY_SERVER_URL=http://localhost:8264
SSE_SERVER_PORT=7264
SSE_SERVER_HOST=localhost
S3_ACCESS_KEY_ID=minioadmin
S3_ACCESS_KEY=minioadmin123
S3_SERVICE_URL=http://localhost:9000
S3_INTERNAL_SERVICE_URL=http://localhost:9000
S3_REGION=us-east-1
S3_BUCKET_NAME=attachments
S3_INTERNAL_BUCKET_NAME=attachments
SIGNALR_REDIS_CONNECTION_STRING=localhost:6379



---


#---

# Description: Paddle API Key from Developer Tools > Authentication
# Required: yes (for Paddle mode)
PADDLE_API_KEY=pdl_sdbx_apikey_01knexb425m6zs9mkrq53ar5jt_HZyVgRHXmpW9yfQpmXWBaw_APo


# Description: Webhook secret from Developer Tools > Notifications > Destination
# Required: yes (for Paddle mode)
PADDLE_WEBHOOK_SECRET=

# Description: "sandbox" for testing, "production" for live
# Required: yes (for Paddle mode)
PADDLE_ENVIRONMENT=sandbox

# Description: Price IDs from Catalog > Products > Prices
# Required: at least one price ID
PADDLE_PRICE_ID_ESSENTIAL_MONTHLY=pri_01knexpea190b07xnf2pnzy1jv
PADDLE_PRICE_ID_ESSENTIAL_YEARLY=pri_01knexrtk4xmw5f3qx1pjnfw1y
PADDLE_PRICE_ID_PRO_MONTHLY=pri_01knexwa1jz8730kbdg02gepwj
PADDLE_PRICE_ID_PRO_YEARLY=pri_01knexx47yv6y5hbv6bh61bjzt
PADDLE_PRICE_ID_EDUCATION_YEARLY=pri_01knexz88zzjbvnf9atz4yxz69