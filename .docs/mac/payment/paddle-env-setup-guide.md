# Paddle .env Setup Guide

## Step 1: Create Paddle Sandbox Account

```
┌──────────────────────────────────────────────────────┐
│  1. Go to: https://sandbox-vendors.paddle.com        │
│  2. Click "Sign Up" → create sandbox account         │
│  3. Verify email                                     │
│  4. Login to Paddle Dashboard                        │
└──────────────────────────────────────────────────────┘

    Sandbox = test environment, NO real money
    Live    = production, real money (setup later)
```

## Step 2: Get API Key

```
    Paddle Dashboard
    │
    ├──► Developer Tools (left sidebar, near bottom)
    │    │
    │    └──► Authentication
    │         │
    │         └──► + Generate API Key
    │              │
    │              ├── Name: "Notesnook Sync Server"
    │              └── Click "Generate"
    │
    │    ┌─────────────────────────────────────────────┐
    │    │  Copy API Key immediately! Shown only once. │
    │    │                                             │
    │    │  Example: pdl_sdbx_abc123def456...          │
    │    │           ^^^^^^^^                          │
    │    │           sandbox prefix                    │
    │    └─────────────────────────────────────────────┘
    │
    └──► .env:
         PADDLE_API_KEY=pdl_sdbx_abc123def456...
         PADDLE_ENVIRONMENT=sandbox
```

## Step 3: Create Products & Prices

```
    Paddle Dashboard
    │
    ├──► Catalog (sidebar)
    │    │
    │    └──► Products
    │         │
    │         └──► + New Product
    │
    │    ╔═══════════════════════════════════════════════════╗
    │    ║  Create 3 products:                              ║
    │    ╠═══════════════════════════════════════════════════╣
    │    ║                                                   ║
    │    ║  Product 1: "Notesnook Essential"                 ║
    │    ║  ├── Tax category: "Standard digital goods"       ║
    │    ║  ├── Price 1 (Monthly):                           ║
    │    ║  │   ├── Amount: $2.49/month                      ║
    │    ║  │   ├── Billing period: Monthly                  ║
    │    ║  │   └── Copy Price ID → pri_xxxxx                ║
    │    ║  └── Price 2 (Yearly):                            ║
    │    ║      ├── Amount: $24.99/year                      ║
    │    ║      ├── Billing period: Yearly                   ║
    │    ║      └── Copy Price ID → pri_xxxxx                ║
    │    ║                                                   ║
    │    ║  Product 2: "Notesnook Pro"                       ║
    │    ║  ├── Tax category: "Standard digital goods"       ║
    │    ║  ├── Price 1 (Monthly):                           ║
    │    ║  │   ├── Amount: $4.49/month                      ║
    │    ║  │   ├── Billing period: Monthly                  ║
    │    ║  │   └── Copy Price ID → pri_xxxxx                ║
    │    ║  └── Price 2 (Yearly):                            ║
    │    ║      ├── Amount: $49.99/year                      ║
    │    ║      ├── Billing period: Yearly                   ║
    │    ║      └── Copy Price ID → pri_xxxxx                ║
    │    ║                                                   ║
    │    ║  Product 3: "Notesnook Education"                 ║
    │    ║  ├── Tax category: "Standard digital goods"       ║
    │    ║  └── Price 1 (Yearly):                            ║
    │    ║      ├── Amount: $29.99/year                      ║
    │    ║      ├── Billing period: Yearly                   ║
    │    ║      └── Copy Price ID → pri_xxxxx                ║
    │    ╚═══════════════════════════════════════════════════╝
    │
    └──► .env:
         PADDLE_PRICE_ID_ESSENTIAL_MONTHLY=pri_01jxxxxx...
         PADDLE_PRICE_ID_ESSENTIAL_YEARLY=pri_01jxxxxx...
         PADDLE_PRICE_ID_PRO_MONTHLY=pri_01jxxxxx...
         PADDLE_PRICE_ID_PRO_YEARLY=pri_01jxxxxx...
         PADDLE_PRICE_ID_EDUCATION_YEARLY=pri_01jxxxxx...
```

### How to find Price ID after creating:

```
    Products → Click on product → Prices tab
    │
    └── Each price has ID like: pri_01jxxxxxxxxx
        Click on price → ID shown at top of page
        or copy from URL
```

## Step 4: Setup Webhook (Notification Destination)

```
    Paddle Dashboard
    │
    ├──► Developer Tools
    │    │
    │    └──► Notifications
    │         │
    │         └──► + New Destination
    │
    │    ┌─────────────────────────────────────────────────┐
    │    │  Type: URL                                      │
    │    │                                                 │
    │    │  URL: https://your-server.com/paddle/webhook    │
    │    │       ──────────────────────────────────────     │
    │    │       Domain pointing to Notesnook API server   │
    │    │                                                 │
    │    │  Description: "Notesnook Sync Server"           │
    │    │                                                 │
    │    │  Events to subscribe:                           │
    │    │  ┌─────────────────────────────────────┐        │
    │    │  │ ☑ subscription.created              │        │
    │    │  │ ☑ subscription.updated              │        │
    │    │  │ ☑ subscription.activated            │        │
    │    │  │ ☑ subscription.canceled             │        │
    │    │  │ ☑ subscription.paused               │        │
    │    │  │ ☑ subscription.resumed              │        │
    │    │  │ ☑ subscription.past_due             │        │
    │    │  │ ☑ transaction.completed             │        │
    │    │  └─────────────────────────────────────┘        │
    │    │                                                 │
    │    │  Click "Save Destination"                       │
    │    └─────────────────────────────────────────────────┘
    │
    │    After saving:
    │    ┌─────────────────────────────────────────────────┐
    │    │  Secret Key appears (shown only once!)          │
    │    │                                                 │
    │    │  pdl_ntfset_xxxxxxxxxxxxxxxx...                 │
    │    │                                                 │
    │    │  COPY IMMEDIATELY!                              │
    │    └─────────────────────────────────────────────────┘
    │
    └──► .env:
         PADDLE_WEBHOOK_SECRET=pdl_ntfset_xxxxxxxxxxxxxxxx...
```

## Step 4b: Local Development — Use ngrok

```
    Problem: Paddle needs to send webhooks to a public server
    Solution: Use ngrok to tunnel localhost

    ┌──────────────────────────────────────────────────┐
    │  Terminal 1: Run Notesnook API                   │
    │  $ dotnet run --project Notesnook.API            │
    │    → Running on http://localhost:5264             │
    │                                                  │
    │  Terminal 2: Run ngrok                           │
    │  $ ngrok http 5264                               │
    │    → Forwarding: https://abc123.ngrok.io         │
    │                                                  │
    │  Use ngrok URL for Paddle webhook:               │
    │  https://abc123.ngrok.io/paddle/webhook          │
    └──────────────────────────────────────────────────┘

    Install ngrok (if not installed):
    $ brew install ngrok        # macOS
    $ ngrok config add-authtoken <your-token>
    (Get free token at https://dashboard.ngrok.com)
```

## Step 5: Complete .env Block

Add this to the end of your `.env` file:

```env
### Paddle Billing Configuration ###
# Set SELF_HOSTED=0 in docker-compose.yml to enable Paddle mode
# When PADDLE_API_KEY is set and SELF_HOSTED!=1, Paddle mode activates

# Description: Paddle API Key from Developer Tools > Authentication
# Required: yes (for Paddle mode)
PADDLE_API_KEY=

# Description: Webhook secret from Developer Tools > Notifications > Destination
# Required: yes (for Paddle mode)
PADDLE_WEBHOOK_SECRET=

# Description: "sandbox" for testing, "production" for live
# Required: yes (for Paddle mode)
PADDLE_ENVIRONMENT=sandbox

# Description: Price IDs from Catalog > Products > Prices
# Required: at least one price ID for the plans you want to offer
PADDLE_PRICE_ID_PRO_MONTHLY=
PADDLE_PRICE_ID_PRO_YEARLY=
PADDLE_PRICE_ID_ESSENTIAL_MONTHLY=
PADDLE_PRICE_ID_ESSENTIAL_YEARLY=
PADDLE_PRICE_ID_EDUCATION_YEARLY=
```

## Step 6: Enable Paddle Mode

### Option A: Docker Compose (production-like)

```
    docker-compose.yml:
    ┌─────────────────────────────────┐
    │  x-server-discovery:            │
    │    SELF_HOSTED: 0    ← Change!  │
    │    ...                          │
    └─────────────────────────────────┘

    Then: $ docker compose up
```

### Option B: Local Development (dotnet run)

```
    Set environment variables before running:

    $ export SELF_HOSTED=0
    $ export PADDLE_API_KEY=pdl_sdbx_xxx...
    $ export PADDLE_WEBHOOK_SECRET=pdl_ntfset_xxx...
    $ export PADDLE_ENVIRONMENT=sandbox
    $ export PADDLE_PRICE_ID_PRO_MONTHLY=pri_xxx...
    $ export PADDLE_PRICE_ID_PRO_YEARLY=pri_xxx...
    $ export PADDLE_PRICE_ID_ESSENTIAL_MONTHLY=pri_xxx...
    $ export PADDLE_PRICE_ID_ESSENTIAL_YEARLY=pri_xxx...
    $ export PADDLE_PRICE_ID_EDUCATION_YEARLY=pri_xxx...
    $ dotnet run --project Notesnook.API
```

## Step 7: Test

```
    1. Verify checkout endpoint:

    $ curl -X POST http://localhost:5264/subscriptions/checkout \
        -H "Authorization: Bearer <your-jwt-token>" \
        -H "Content-Type: application/json" \
        -d '{"plan": 2, "period": "yearly"}'

    Expected response:
    {
      "priceId": "pri_xxx...",
      "customData": { "userId": "..." },
      "customerEmail": "user@email.com"
    }


    2. Test webhook signature (use Paddle Dashboard):

    Developer Tools → Notifications → Click destination
    → "Send test notification" → Choose event type
    → Check server logs for "Processing Paddle webhook: ..."


    3. Full checkout test (requires frontend with Paddle.js):

    Paddle.Checkout.open({
      items: [{ priceId: "pri_xxx", quantity: 1 }],
      customData: { userId: "your-user-id" },
      customer: { email: "test@example.com" }
    });

    Sandbox test card: 4242 4242 4242 4242
    Expiry: any future date
    CVV: any 3 digits
```

## Checklist

```
    ╔══════════════════════════════════════════════════════════════╗
    ║                    SETUP CHECKLIST                           ║
    ╠══════════════════════════════════════════════════════════════╣
    ║                                                              ║
    ║  [ ] 1. Go to https://sandbox-vendors.paddle.com             ║
    ║         → Sign up → Verify email → Login                    ║
    ║                                                              ║
    ║  [ ] 2. Developer Tools → Authentication                    ║
    ║         → Generate API Key → Copy                           ║
    ║         → Paste into PADDLE_API_KEY                         ║
    ║                                                              ║
    ║  [ ] 3. Catalog → Products → New Product                    ║
    ║         → Create "Notesnook Pro" + 2 prices (monthly/yearly)║
    ║         → Copy each Price ID                                ║
    ║         → Paste into PADDLE_PRICE_ID_PRO_MONTHLY/YEARLY    ║
    ║                                                              ║
    ║  [ ] 4. Create more products "Essential" + "Education"      ║
    ║         → Copy Price IDs → Paste into .env                  ║
    ║                                                              ║
    ║  [ ] 5. (Optional for local dev) Run ngrok:                 ║
    ║         $ ngrok http 5264                                   ║
    ║                                                              ║
    ║  [ ] 6. Developer Tools → Notifications → New Destination   ║
    ║         → URL: https://xxx.ngrok.io/paddle/webhook          ║
    ║         → Select 8 events (subscription.* + transaction.*)  ║
    ║         → Save → Copy Secret Key                            ║
    ║         → Paste into PADDLE_WEBHOOK_SECRET                  ║
    ║                                                              ║
    ║  [ ] 7. docker-compose.yml: change SELF_HOSTED: 0           ║
    ║         (or run local with dotnet run + env vars)            ║
    ║                                                              ║
    ║  [ ] 8. Test: POST /subscriptions/checkout                  ║
    ║         → Verify returns priceId                             ║
    ║                                                              ║
    ╚══════════════════════════════════════════════════════════════╝
```

## Switching from Sandbox to Production

```
    When ready for real payments:

    1. Go to https://vendors.paddle.com (NOT sandbox)
    2. Complete business verification (KYC)
    3. Re-create products & prices (production has separate catalog)
    4. Generate production API key
    5. Create production webhook destination
    6. Update .env:

       PADDLE_API_KEY=pdl_live_xxx...        ← live key
       PADDLE_WEBHOOK_SECRET=pdl_ntfset_xxx...  ← new secret
       PADDLE_ENVIRONMENT=production         ← change to production
       PADDLE_PRICE_ID_PRO_MONTHLY=pri_xxx...   ← production price IDs
       PADDLE_PRICE_ID_PRO_YEARLY=pri_xxx...
       ... (all production price IDs)
```
