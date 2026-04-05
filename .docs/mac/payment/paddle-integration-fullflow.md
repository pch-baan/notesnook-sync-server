# Paddle Billing Integration — Full Flow

## 1. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        NOTESNOOK SYSTEM                             │
│                                                                     │
│  ┌──────────────┐    ┌──────────────┐    ┌───────────────────────┐  │
│  │  Identity     │    │  Notesnook   │    │  Messenger (SSE)      │  │
│  │  Server       │◄──►│  API         │───►│  Server               │  │
│  │  :8264        │WAMP│  :5264       │WAMP│  :7264                │  │
│  └──────────────┘    └──────┬───────┘    └───────────┬───────────┘  │
│                             │                        │              │
│                     ┌───────┴───────┐                │              │
│                     │   MongoDB     │          SSE Events           │
│                     │   :27017      │                │              │
│                     │               │                │              │
│                     │ ┌───────────┐ │                │              │
│                     │ │subscrip-  │ │                │              │
│                     │ │tions (NEW)│ │                │              │
│                     │ └───────────┘ │                │              │
│                     │ ┌───────────┐ │                │              │
│                     │ │user_      │ │                │              │
│                     │ │settings   │ │                │              │
│                     │ └───────────┘ │                │              │
│                     └───────────────┘                │              │
│                                                      │              │
└──────────────────────────────────────────────────────┼──────────────┘
                              ▲                        │
                              │                        ▼
                    ┌─────────┴─────────┐    ┌─────────────────┐
                    │   Paddle Billing  │    │  Notesnook      │
                    │   (External)      │    │  Client App     │
                    │                   │    │  (Web/Desktop/  │
                    │  api.paddle.com   │    │   Mobile)       │
                    └───────────────────┘    └─────────────────┘
```

## 2. Three Operating Modes

```
                    ┌─────────────────────────────┐
                    │     Startup Mode Check       │
                    └──────────────┬──────────────┘
                                   │
                    ┌──────────────┼──────────────┐
                    ▼              ▼              ▼
        ┌───────────────┐ ┌──────────────┐ ┌──────────────────┐
        │  SELF_HOSTED  │ │   PADDLE     │ │  EXTERNAL WAMP   │
        │  =1           │ │   ENABLED    │ │  (default prod)  │
        ├───────────────┤ ├──────────────┤ ├──────────────────┤
        │ All users =   │ │ Subscription │ │ Query external   │
        │ BELIEVER      │ │ stored in    │ │ Subscription     │
        │               │ │ local MongoDB│ │ Server via WAMP  │
        │ No payment    │ │              │ │                  │
        │ needed        │ │ Paddle API + │ │ No code needed   │
        │               │ │ Webhooks     │ │ in this repo     │
        └───────────────┘ └──────────────┘ └──────────────────┘

        Condition:         Condition:        Condition:
        SELF_HOSTED=1      SELF_HOSTED!=1    SELF_HOSTED!=1
                           PADDLE_API_KEY    PADDLE_API_KEY
                           is set            is NOT set
```

## 3. Checkout Flow (User purchases subscription)

```
  Client App                Notesnook API             Paddle
  ──────────               ─────────────             ──────
      │                          │                      │
      │ POST /subscriptions/     │                      │
      │       checkout           │                      │
      │  { plan: PRO,            │                      │
      │    period: "yearly" }    │                      │
      │─────────────────────────►│                      │
      │                          │                      │
      │                          │ Look up user email   │
      │                          │ Map plan → price ID  │
      │                          │                      │
      │  { priceId: "pri_xxx",   │                      │
      │    customData: {         │                      │
      │      userId: "usr_123"   │                      │
      │    },                    │                      │
      │    customerEmail:        │                      │
      │      "user@email.com" }  │                      │
      │◄─────────────────────────│                      │
      │                          │                      │
      │  ┌──────────────────┐    │                      │
      │  │ Paddle.js Overlay │    │                      │
      │  │                  │    │                      │
      │  │ Paddle.Checkout  │    │                      │
      │  │  .open({         │    │                      │
      │  │   items: [{      │    │                      │
      │  │    priceId,      │    │                      │
      │  │    quantity: 1   │    │                      │
      │  │   }],            │    │                      │
      │  │   customData: {  │    │                      │
      │  │    userId        │◄──────────────────────────┤
      │  │   },             │    │   Payment processed   │
      │  │   customer: {    │    │                      │
      │  │    email         │────────────────────────►  │
      │  │   }              │    │                      │
      │  │  })              │    │                      │
      │  └──────────────────┘    │                      │
      │                          │                      │
      │                          │   Webhook:           │
      │                          │   subscription       │
      │                          │   .created           │
      │                          │◄─────────────────────│
      │                          │                      │
      │                          │ Verify signature     │
      │                          │ Extract userId from  │
      │                          │   custom_data        │
      │                          │ Map price → PRO      │
      │                          │ Map status → ACTIVE  │
      │                          │ Upsert MongoDB       │
      │                          │                      │
      │   SSE: subscription      │                      │
      │        Changed           │                      │
      │◄─────────────────────────│                      │
      │                          │                      │
      │ GET /users               │                      │
      │─────────────────────────►│                      │
      │                          │ Read subscription    │
      │                          │ from MongoDB         │
      │  { subscription: {       │                      │
      │    plan: PRO,            │                      │
      │    status: ACTIVE,       │                      │
      │    provider: PADDLE }}   │                      │
      │◄─────────────────────────│                      │
      │                          │                      │
```

## 4. Webhook Processing Flow

```
  Paddle                    PaddleWebhookController          PaddleWebhookService
  ──────                    ───────────────────────          ────────────────────
    │                              │                                │
    │ POST /paddle/webhook         │                                │
    │ Headers:                     │                                │
    │   Paddle-Signature:          │                                │
    │   ts=168...;h1=abc...        │                                │
    │ Body: { event_type:          │                                │
    │   "subscription.created",    │                                │
    │   data: {...} }              │                                │
    │─────────────────────────────►│                                │
    │                              │                                │
    │                              │ 1. Read raw body               │
    │                              │ 2. Get Paddle-Signature        │
    │                              │ 3. Verify HMAC-SHA256          │
    │                              │                                │
    │                              │    ┌──────────────────┐        │
    │                              │    │ PaddleWebhook    │        │
    │                              │    │ Verifier         │        │
    │                              │    │                  │        │
    │                              │    │ ts:rawBody       │        │
    │                              │    │    │             │        │
    │                              │    │    ▼             │        │
    │                              │    │ HMAC-SHA256      │        │
    │                              │    │ (webhook_secret) │        │
    │                              │    │    │             │        │
    │                              │    │    ▼             │        │
    │                              │    │ FixedTimeEquals  │        │
    │                              │    │ (computed, h1)   │        │
    │                              │    └───────┬──────────┘        │
    │                              │            │                   │
    │                              │     Valid? │                   │
    │                              │            │                   │
    │          200 OK              │◄───Yes─────┘                   │
    │◄─────────────────────────────│                                │
    │                              │                                │
    │            (5s max)          │ ProcessEventAsync (async)      │
    │                              │───────────────────────────────►│
    │                              │                                │
    │                              │                 Switch event_type:
    │                              │                                │
    │                              │         ┌──────────────────────┤
    │                              │         │                      │
    │                              │         ▼                      │
    │                              │  subscription.created          │
    │                              │  subscription.updated          │
    │                              │  subscription.activated        │
    │                              │  subscription.past_due         │
    │                              │  subscription.resumed          │
    │                              │         │                      │
    │                              │         ▼                      │
    │                              │  HandleSubscriptionUpdate:     │
    │                              │  ┌─────────────────────────┐   │
    │                              │  │ 1. Get paddleSubId      │   │
    │                              │  │ 2. Get userId from      │   │
    │                              │  │    custom_data          │   │
    │                              │  │ 3. Check idempotency    │   │
    │                              │  │    (occurred_at >       │   │
    │                              │  │     updatedAt?)         │   │
    │                              │  │ 4. Map price → Plan     │   │
    │                              │  │ 5. Map status           │   │
    │                              │  │ 6. Upsert MongoDB       │   │
    │                              │  │ 7. SSE notify client    │   │
    │                              │  └─────────────────────────┘   │
    │                              │                                │
    │                              │  subscription.canceled         │
    │                              │         │                      │
    │                              │         ▼                      │
    │                              │  Set status = CANCELED         │
    │                              │  Set expiryDate                │
    │                              │                                │
    │                              │  subscription.paused           │
    │                              │         │                      │
    │                              │         ▼                      │
    │                              │  Set status = PAUSED           │
    │                              │                                │
    │                              │  transaction.completed         │
    │                              │         │                      │
    │                              │         ▼                      │
    │                              │  Update orderId + expiryDate   │
    │                              │                                │
```

## 5. Subscription Lifecycle

```
    ┌─────────────────────────────────────────────────────────────┐
    │                 SUBSCRIPTION LIFECYCLE                       │
    └─────────────────────────────────────────────────────────────┘

    Paddle.js                      Paddle                   MongoDB
    Checkout ──────────────────► subscription ─────────► ┌──────────┐
    completes                    .created                │ ACTIVE   │
                                                        │ Plan:PRO │
                                                        └────┬─────┘
                                                             │
                 ┌───────────────┬───────────────┬───────────┤
                 │               │               │           │
                 ▼               ▼               ▼           ▼
           subscription    subscription    subscription   Auto-renew
           .canceled       .paused         .past_due      (monthly/
                 │               │               │        yearly)
                 ▼               ▼               ▼           │
           ┌──────────┐   ┌──────────┐   ┌──────────┐      │
           │ CANCELED │   │  PAUSED  │   │  ACTIVE  │      │
           │          │   │          │   │ (dunning)│      │
           └──────────┘   └────┬─────┘   └────┬─────┘      │
                               │              │             │
                          subscription   After 30 days      │
                          .resumed       no payment         │
                               │              │             │
                               ▼              ▼             │
                         ┌──────────┐   ┌──────────┐       │
                         │  ACTIVE  │   │ CANCELED │       │
                         └──────────┘   └──────────┘       │
                                                           │
                                                           ▼
                                                  transaction.completed
                                                           │
                                                           ▼
                                                  ┌──────────────┐
                                                  │ Update expiry│
                                                  │ + orderId    │
                                                  └──────────────┘


    ╔══════════════════════════════════════════════════╗
    ║          ACCESS CONTROL BY STATUS                ║
    ╠════════════════╦═════════════════════════════════╣
    ║ Status         ║ User Access                     ║
    ╠════════════════╬═════════════════════════════════╣
    ║ ACTIVE         ║ Full access (PRO features)      ║
    ║ TRIAL          ║ Full access (trial period)      ║
    ║ PAUSED         ║ Limited / No access             ║
    ║ CANCELED       ║ No access (downgrade FREE)      ║
    ║ EXPIRED        ║ No access                       ║
    ╚════════════════╩═════════════════════════════════╝
```

## 6. Cancel / Pause / Resume Flow

```
  Client App              SubscriptionController         PaddleBillingService      Paddle
  ──────────             ──────────────────────         ────────────────────      ──────
      │                          │                              │                   │
      │                          │                              │                   │
      │  ══════ CANCEL ════════  │                              │                   │
      │                          │                              │                   │
      │ POST /subscriptions/     │                              │                   │
      │       cancel             │                              │                   │
      │─────────────────────────►│                              │                   │
      │                          │ Find subscription            │                   │
      │                          │ in MongoDB                   │                   │
      │                          │                              │                   │
      │                          │ CancelSubscription           │                   │
      │                          │ Async(sub_xxx)               │                   │
      │                          │─────────────────────────────►│                   │
      │                          │                              │ POST /sub/cancel  │
      │                          │                              │──────────────────►│
      │                          │                              │       OK          │
      │                          │                              │◄─────────────────│
      │                          │◄─────────────────────────────│                   │
      │                          │                              │                   │
      │  { message: "will be     │                              │                   │
      │    canceled at end of    │                              │                   │
      │    billing period" }     │                              │                   │
      │◄─────────────────────────│                              │                   │
      │                          │                              │                   │
      │                    (Later, at billing period end)        │                   │
      │                          │                              │                   │
      │                          │    Webhook: subscription.canceled                │
      │                          │◄─────────────────────────────────────────────────│
      │                          │    → status = CANCELED                           │
      │   SSE: subscription      │                              │                   │
      │        Changed           │                              │                   │
      │◄─────────────────────────│                              │                   │
      │                          │                              │                   │
      │                          │                              │                   │
      │  ═══════ PAUSE ════════  │                              │                   │
      │                          │                              │                   │
      │ POST /subscriptions/     │                              │                   │
      │       pause              │                              │                   │
      │─────────────────────────►│ PauseSubscriptionAsync       │                   │
      │                          │─────────────────────────────►│ POST /sub/pause   │
      │                          │                              │──────────────────►│
      │  { message: "paused" }   │◄─────────────────────────────│◄─────────────────│
      │◄─────────────────────────│                              │                   │
      │                          │    Webhook: subscription.paused                  │
      │                          │◄─────────────────────────────────────────────────│
      │                          │    → status = PAUSED                             │
      │                          │                              │                   │
      │                          │                              │                   │
      │  ══════ RESUME ════════  │                              │                   │
      │                          │                              │                   │
      │ POST /subscriptions/     │                              │                   │
      │       resume             │                              │                   │
      │─────────────────────────►│ ResumeSubscriptionAsync      │                   │
      │                          │─────────────────────────────►│ PATCH /sub        │
      │                          │                              │──────────────────►│
      │  { message: "resumed" }  │◄─────────────────────────────│◄─────────────────│
      │◄─────────────────────────│                              │                   │
      │                          │    Webhook: subscription.resumed                 │
      │                          │◄─────────────────────────────────────────────────│
      │                          │    → status = ACTIVE                             │
      │                          │                              │                   │
```

## 7. Idempotency & Event Ordering

```
    ┌──────────────────────────────────────────────────────────────┐
    │  Paddle does NOT guarantee webhook delivery order            │
    │  → Use occurred_at to prevent duplicate & out-of-order       │
    └──────────────────────────────────────────────────────────────┘

    Webhook 1                    Webhook 2 (retry/duplicate)
    occurred_at: 1000            occurred_at: 900
         │                            │
         ▼                            ▼
    ┌─────────┐                 ┌─────────┐
    │ MongoDB │                 │ Check:  │
    │ Update  │                 │ 900 >   │
    │ updatedAt                 │ 1000?   │
    │ = 1000  │                 │ NO!     │
    └─────────┘                 │ SKIP    │
                                └─────────┘

    Webhook 3 (newer event)
    occurred_at: 1200
         │
         ▼
    ┌─────────┐
    │ Check:  │
    │ 1200 >  │
    │ 1000?   │
    │ YES!    │
    │ UPDATE  │
    │ updatedAt
    │ = 1200  │
    └─────────┘
```

## 8. Webhook Signature Verification

```
    Paddle-Signature header: "ts=1671552777;h1=eb4d0dc8abc..."

    Step 1: Parse header
    ┌──────────────────────────┐
    │ ts = "1671552777"        │
    │ h1 = "eb4d0dc8abc..."    │
    └──────────┬───────────────┘
               │
    Step 2: Build signed payload
    ┌──────────▼───────────────┐
    │ signed = ts + ":" + body │
    │ "1671552777:{...json...}"│
    └──────────┬───────────────┘
               │
    Step 3: Compute HMAC-SHA256
    ┌──────────▼───────────────┐
    │ key = PADDLE_WEBHOOK_    │
    │       SECRET             │
    │                          │
    │ hash = HMAC-SHA256(      │
    │   signed, key            │
    │ )                        │
    └──────────┬───────────────┘
               │
    Step 4: Timing-safe compare
    ┌──────────▼───────────────┐
    │ CryptographicOperations  │
    │ .FixedTimeEquals(        │
    │   computed_hash, h1      │
    │ )                        │
    │                          │
    │ Also check: timestamp    │
    │ not older than 300s      │
    └──────────┬───────────────┘
               │
               ▼
         Valid / Invalid
```

## 9. Plan Mapping

```
    ╔═══════════════════════════════════════════════════════════════════╗
    ║              Paddle Price ID → SubscriptionPlan                  ║
    ╠═══════════════════════════════╦═══════════════════════════════════╣
    ║  Env Variable                 ║  Maps to                         ║
    ╠═══════════════════════════════╬═══════════════════════════════════╣
    ║  PADDLE_PRICE_ID_PRO_MONTHLY  ║  SubscriptionPlan.PRO            ║
    ║  PADDLE_PRICE_ID_PRO_YEARLY   ║  SubscriptionPlan.PRO            ║
    ║  PADDLE_PRICE_ID_ESSENTIAL_   ║  SubscriptionPlan.ESSENTIAL      ║
    ║    MONTHLY                    ║                                  ║
    ║  PADDLE_PRICE_ID_ESSENTIAL_   ║  SubscriptionPlan.ESSENTIAL      ║
    ║    YEARLY                     ║                                  ║
    ║  PADDLE_PRICE_ID_EDUCATION_   ║  SubscriptionPlan.EDUCATION      ║
    ║    YEARLY                     ║                                  ║
    ╚═══════════════════════════════╩═══════════════════════════════════╝

    ╔═══════════════════════════════════════════════════════════════════╗
    ║              Paddle Status → SubscriptionStatus                  ║
    ╠════════════════════╦══════════════════════════════════════════════╣
    ║  Paddle Status     ║  SubscriptionStatus     Access              ║
    ╠════════════════════╬══════════════════════════════════════════════╣
    ║  trialing          ║  TRIAL                  Full access         ║
    ║  active            ║  ACTIVE                 Full access         ║
    ║  past_due          ║  ACTIVE (dunning)       Full access         ║
    ║  paused            ║  PAUSED                 No access           ║
    ║  canceled          ║  CANCELED               No access           ║
    ╚════════════════════╩══════════════════════════════════════════════╝

    ╔═══════════════════════════════════════════════════════════════════╗
    ║              Storage Limits by Plan                               ║
    ╠════════════════════╦═════════════════╦════════════════════════════╣
    ║  Plan              ║  Monthly Limit  ║  Max File Size             ║
    ╠════════════════════╬═════════════════╬════════════════════════════╣
    ║  FREE              ║  50 MB          ║  10 MB                     ║
    ║  ESSENTIAL         ║  1 GB           ║  100 MB                    ║
    ║  PRO               ║  10 GB          ║  1 GB                      ║
    ║  EDUCATION         ║  10 GB          ║  1 GB                      ║
    ║  BELIEVER          ║  25 GB          ║  5 GB                      ║
    ║  LEGACY_PRO        ║  Unlimited      ║  512 MB                    ║
    ╚════════════════════╩═════════════════╩════════════════════════════╝
```

## 10. File Structure

```
    notesnook-sync-server/
    │
    ├── Streetwriters.Common/
    │   ├── Constants.cs                    ← MODIFIED (Paddle env vars)
    │   ├── Accessors/
    │   │   └── WampServiceAccessor.cs      ← MODIFIED (skip WAMP when Paddle)
    │   ├── Models/
    │   │   └── Subscription.cs             (reused as-is)
    │   ├── Services/
    │   │   └── PaddleBillingService.cs     (reused as-is)
    │   └── Enums/
    │       └── SubscriptionProvider.cs     (PADDLE=3 already exists)
    │
    ├── Notesnook.API/
    │   ├── Constants.cs                    ← MODIFIED (SubscriptionsKey)
    │   ├── Startup.cs                      ← MODIFIED (register Paddle DI)
    │   ├── Services/
    │   │   └── UserService.cs              ← MODIFIED (Paddle mode branches)
    │   ├── Paddle/                         ← NEW DIRECTORY
    │   │   ├── PaddlePlanMapper.cs         ← NEW (price <-> plan mapping)
    │   │   ├── PaddleWebhookVerifier.cs    ← NEW (HMAC-SHA256)
    │   │   ├── PaddleWebhookEvent.cs       ← NEW (webhook DTO)
    │   │   ├── PaddleSubscriptionService.cs← NEW (local IUserSubscriptionService)
    │   │   └── PaddleWebhookService.cs     ← NEW (process all events)
    │   └── Controllers/
    │       ├── PaddleWebhookController.cs  ← NEW (POST /paddle/webhook)
    │       └── SubscriptionController.cs   ← NEW (checkout/cancel/pause/resume)
    │
    └── docker-compose.yml                  (unchanged)
```

## 11. API Endpoints

```
    ╔═══════════════════════════════════════════════════════════════════╗
    ║                    NEW API ENDPOINTS                             ║
    ╠═══════════╦═══════════════════════════════╦═══════════════════════╣
    ║  Method   ║  Path                         ║  Auth                 ║
    ╠═══════════╬═══════════════════════════════╬═══════════════════════╣
    ║  POST     ║  /paddle/webhook              ║  AllowAnonymous       ║
    ║           ║                               ║  (signature verified) ║
    ╠═══════════╬═══════════════════════════════╬═══════════════════════╣
    ║  POST     ║  /subscriptions/checkout      ║  Authorized (JWT)     ║
    ║  POST     ║  /subscriptions/cancel        ║  Authorized (JWT)     ║
    ║  POST     ║  /subscriptions/pause         ║  Authorized (JWT)     ║
    ║  POST     ║  /subscriptions/resume        ║  Authorized (JWT)     ║
    ║  GET      ║  /subscriptions/update-       ║  Authorized (JWT)     ║
    ║           ║    payment-method             ║                       ║
    ╚═══════════╩═══════════════════════════════╩═══════════════════════╝
```

## 12. Environment Variables

```
    ╔═══════════════════════════════════════════════════════════════╗
    ║              .env — Paddle Configuration                      ║
    ╠═══════════════════════════════╦═══════════════════════════════╣
    ║  Variable                     ║  Example                      ║
    ╠═══════════════════════════════╬═══════════════════════════════╣
    ║  PADDLE_API_KEY               ║  pdl_live_abc123...           ║
    ║  PADDLE_WEBHOOK_SECRET        ║  pdl_ntfset_xxx...            ║
    ║  PADDLE_ENVIRONMENT           ║  sandbox | production         ║
    ║  PADDLE_PRICE_ID_PRO_MONTHLY  ║  pri_01abc...                 ║
    ║  PADDLE_PRICE_ID_PRO_YEARLY   ║  pri_02def...                 ║
    ║  PADDLE_PRICE_ID_ESSENTIAL_   ║  pri_03ghi...                 ║
    ║    MONTHLY                    ║                               ║
    ║  PADDLE_PRICE_ID_ESSENTIAL_   ║  pri_04jkl...                 ║
    ║    YEARLY                     ║                               ║
    ║  PADDLE_PRICE_ID_EDUCATION_   ║  pri_05mno...                 ║
    ║    YEARLY                     ║                               ║
    ╚═══════════════════════════════╩═══════════════════════════════╝

    Not setting PADDLE_API_KEY → system works exactly as before
```

## 13. User Signup Flow (with Paddle mode)

```
    Client                  UsersController              UserService           MongoDB
    ──────                 ────────────────             ───────────           ───────
      │                          │                          │                    │
      │ POST /users              │                          │                    │
      │ { email, password }      │                          │                    │
      │─────────────────────────►│                          │                    │
      │                          │ CreateUserAsync()        │                    │
      │                          │─────────────────────────►│                    │
      │                          │                          │                    │
      │                          │         ┌────────────────┤                    │
      │                          │         │ Mode check:    │                    │
      │                          │         │                │                    │
      │                          │         │ SELF_HOSTED?   │                    │
      │                          │         │ → skip         │                    │
      │                          │         │                │                    │
      │                          │         │ PADDLE_ENABLED?│                    │
      │                          │         │ → Insert FREE  │                    │
      │                          │         │   subscription │ INSERT INTO        │
      │                          │         │   to MongoDB   │ subscriptions      │
      │                          │         │────────────────│────────────────►   │
      │                          │         │                │ { userId,          │
      │                          │         │ else?          │   plan: FREE,      │
      │                          │         │ → WAMP publish │   status: ACTIVE } │
      │                          │         └────────────────┤                    │
      │                          │                          │                    │
      │  { userId, tokens }      │                          │                    │
      │◄─────────────────────────│◄─────────────────────────│                    │
      │                          │                          │                    │
```

## 14. Delete User Flow (with Paddle mode)

```
    Client              UserService              PaddleBillingService    MongoDB
    ──────             ───────────              ────────────────────    ───────
      │                     │                           │                 │
      │ DELETE user         │                           │                 │
      │────────────────────►│                           │                 │
      │                     │                           │                 │
      │                     │ Delete all user data      │                 │
      │                     │ (notes, notebooks, etc.)  │                 │
      │                     │───────────────────────────────────────────►│
      │                     │                           │                 │
      │                     │ ┌─────────────────────┐   │                 │
      │                     │ │ PADDLE_ENABLED?     │   │                 │
      │                     │ │                     │   │                 │
      │                     │ │ Find subscription   │   │                 │
      │                     │ │ in MongoDB          │   │                 │
      │                     │ │──────────────────────────────────────────►│
      │                     │ │                     │   │                 │
      │                     │ │ If ACTIVE:          │   │                 │
      │                     │ │ Cancel on Paddle    │   │                 │
      │                     │ │─────────────────────►   │                 │
      │                     │ │                     │ POST /sub/cancel   │
      │                     │ │                     │───────►Paddle      │
      │                     │ │                     │◄──────            │
      │                     │ │                     │   │                 │
      │                     │ │ Delete subscription │   │                 │
      │                     │ │ from MongoDB        │   │                 │
      │                     │ │──────────────────────────────────────────►│
      │                     │ └─────────────────────┘   │                 │
      │                     │                           │                 │
      │  OK                 │                           │                 │
      │◄────────────────────│                           │                 │
      │                     │                           │                 │
```
