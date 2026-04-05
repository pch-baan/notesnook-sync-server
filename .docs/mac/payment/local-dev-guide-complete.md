# Paddle Payment — Local Development Complete Guide

> Tài liệu ghi lại toàn bộ Q&A, problems, cách xử lý, và các câu lệnh
> để chạy thành công full payment flow trên local (macOS + Rider IDE).
> Ngày: 2026-04-05

---

## Mục lục

1. [Prerequisites](#1-prerequisites)
2. [Khởi động Infrastructure (Docker)](#2-khởi-động-infrastructure-docker)
3. [Problem: MongoDB Replica Set hostname](#3-problem-mongodb-replica-set-hostname)
4. [Cấu hình launchSettings.json](#4-cấu-hình-launchsettingsjson)
5. [Problem: DotNetEnv override launchSettings](#5-problem-dotnetenv-override-launchsettings)
6. [Problem: Identity Server issuer mismatch](#6-problem-identity-server-issuer-mismatch)
7. [Problem: MongoDBConfiguration.Database null](#7-problem-mongodbconfigurationdatabase-null)
8. [Chạy từ Rider IDE](#8-chạy-từ-rider-ide)
9. [Setup ngrok tunnel](#9-setup-ngrok-tunnel)
10. [Paddle Dashboard Setup](#10-paddle-dashboard-setup)
11. [Problem: Checkout "Something went wrong"](#11-problem-checkout-something-went-wrong)
12. [Test Full Checkout Flow](#12-test-full-checkout-flow)
13. [Verify Webhook hoạt động](#13-verify-webhook-hoạt-động)
14. [Tất cả câu lệnh test](#14-tất-cả-câu-lệnh-test)
15. [Troubleshooting Checklist](#15-troubleshooting-checklist)
16. [Quick Start (cho lần sau)](#16-quick-start-cho-lần-sau)

---

## 1. Prerequisites

### Tools cần cài:
```bash
dotnet --version    # .NET 9.0.x
docker --version    # Docker Desktop
ngrok version       # ngrok 3.x
```

### Cài ngrok (nếu chưa có):
```bash
brew install ngrok
ngrok config add-authtoken <your-token>
# Lấy free token tại https://dashboard.ngrok.com
```

### Paddle Sandbox Account:
- Đăng ký tại: https://sandbox-vendors.paddle.com
- Tạo Products, Prices, API Key, Client Token (xem section 10)

---

## 2. Khởi động Infrastructure (Docker)

Chỉ chạy MongoDB, Redis, MinIO qua Docker. Identity Server và Notesnook.API chạy từ Rider.

### Mở Docker Desktop:
```bash
open -a Docker
# Chờ Docker icon trên menu bar ngừng animate
```

### Chạy infrastructure containers:
```bash
cd /Users/phanchihieu/z4l_dev/moniva_vn/notesnook-sync-server
docker compose up -d notesnook-db notesnook-s3 setup-s3 notesnook-redis
```

### Kiểm tra containers:
```bash
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

Expected output:
```
NAMES                                     STATUS                    PORTS
notesnook-sync-server-notesnook-s3-1      Up (healthy)              0.0.0.0:9000->9000, 0.0.0.0:9090->9090
notesnook-sync-server-notesnook-redis-1   Up (healthy)              0.0.0.0:6379->6379
notesnook-sync-server-notesnook-db-1      Up (healthy)              0.0.0.0:27017->27017
```

### Chờ MongoDB healthy (quan trọng!):
```bash
# MongoDB cần ~60s để init replica set lần đầu
for i in $(seq 1 12); do
  STATUS=$(docker inspect --format='{{.State.Health.Status}}' notesnook-sync-server-notesnook-db-1 2>/dev/null)
  echo "$i. MongoDB: $STATUS"
  if [ "$STATUS" = "healthy" ]; then echo "Ready!"; break; fi
  sleep 10
done
```

### KHÔNG chạy Identity Server trong Docker:
```bash
# ĐỪNG chạy lệnh này — sẽ gây issuer mismatch (xem section 6)
# docker compose up -d identity-server  ← KHÔNG DÙNG
```

---

## 3. Problem: MongoDB Replica Set hostname

### Vấn đề:
Khi MongoDB được init bởi docker-compose, replica set member host được set là `notesnook-db:27017` (Docker internal hostname). Khi kết nối từ localhost, MongoDB driver discover replica set và cố kết nối tới `notesnook-db:27017` → fail vì hostname đó không resolve được ngoài Docker network.

### Lỗi:
```
System.TimeoutException: A timeout occurred after 30000ms selecting a server...
EndPoint: "Unspecified/notesnook-db:27017"
SocketException: nodename nor servname provided, or not known
```

### Giải pháp: Reconfig replica set dùng localhost
```bash
docker exec notesnook-sync-server-notesnook-db-1 mongosh --quiet --eval '
var cfg = rs.conf();
cfg.members[0].host = "localhost:27017";
rs.reconfig(cfg, {force: true});
print("Reconfigured to localhost:27017");
print(JSON.stringify(rs.conf().members[0].host));
'
```

### Verify:
```bash
docker exec notesnook-sync-server-notesnook-db-1 mongosh --quiet --eval 'rs.conf().members[0].host'
# Expected: "localhost:27017"
```

### Lưu ý:
- Chỉ cần làm **1 lần** sau khi tạo Docker volume mới
- Nếu xóa volume (`docker compose down -v`) thì phải làm lại
- Nếu lại chạy `docker compose up -d identity-server` hoặc `notesnook-server`, chúng có thể reconfig lại thành `notesnook-db:27017`

---

## 4. Cấu hình launchSettings.json

### Identity Server (`Streetwriters.Identity/Properties/launchSettings.json`):
```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "Streetwriters.Identity": {
      "commandName": "Project",
      "launchBrowser": false,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "MONGODB_CONNECTION_STRING": "mongodb://localhost:27017/identity?replSet=rs0",
        "MONGODB_DATABASE_NAME": "identity",
        "NOTESNOOK_API_SECRET": "9e3f4047804e4a2a90623eec54f90013396d401bee73e626312423576307f355",
        "NOTESNOOK_SERVER_PORT": "5264",
        "NOTESNOOK_SERVER_HOST": "localhost",
        "IDENTITY_SERVER_PORT": "8264",
        "IDENTITY_SERVER_HOST": "localhost",
        "IDENTITY_SERVER_URL": "http://localhost:8264",
        "SSE_SERVER_PORT": "7264",
        "SSE_SERVER_HOST": "localhost",
        "SELF_HOSTED": "0",
        "SMTP_USERNAME": "edricphan.dev@gmail.com",
        "SMTP_PASSWORD": "roqsgxxcdymxqyjd",
        "SMTP_HOST": "smtp.gmail.com",
        "SMTP_PORT": "465",
        "NOTESNOOK_APP_HOST": "https://app.notesnook.com",
        "DISABLE_SIGNUPS": "false"
      }
    }
  }
}
```

**Quan trọng**: `MONGODB_CONNECTION_STRING` phải có `/identity` trong URL path — đây là database name cho IdentityServer4.MongoDB.

### Notesnook.API (`Notesnook.API/Properties/launchSettings.json`):
```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "Notesnook.API": {
      "commandName": "Project",
      "launchBrowser": false,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "MONGODB_CONNECTION_STRING": "mongodb://localhost:27017/?replSet=rs0",
        "MONGODB_DATABASE_NAME": "notesnook",
        "NOTESNOOK_API_SECRET": "9e3f4047804e4a2a90623eec54f90013396d401bee73e626312423576307f355",
        "NOTESNOOK_SERVER_PORT": "5264",
        "NOTESNOOK_SERVER_HOST": "localhost",
        "IDENTITY_SERVER_PORT": "8264",
        "IDENTITY_SERVER_HOST": "localhost",
        "IDENTITY_SERVER_URL": "http://localhost:8264",
        "SSE_SERVER_PORT": "7264",
        "SSE_SERVER_HOST": "localhost",
        "SELF_HOSTED": "0",
        "S3_ACCESS_KEY_ID": "minioadmin",
        "S3_ACCESS_KEY": "minioadmin123",
        "S3_SERVICE_URL": "http://localhost:9000",
        "S3_INTERNAL_SERVICE_URL": "http://localhost:9000",
        "S3_REGION": "us-east-1",
        "S3_BUCKET_NAME": "attachments",
        "S3_INTERNAL_BUCKET_NAME": "attachments",
        "SIGNALR_REDIS_CONNECTION_STRING": "localhost:6379",
        "PADDLE_API_KEY": "pdl_sdbx_apikey_01knexb425m6zs9mkrq53ar5jt_HZyVgRHXmpW9yfQpmXWBaw_APo",
        "PADDLE_WEBHOOK_SECRET": "pdl_ntfset_01knf00ey5h44y41f7hrd1vwsw_eicv3BPu/K+YmkYRFQSlBKnFE3QIduDn",
        "PADDLE_ENVIRONMENT": "sandbox",
        "PADDLE_PRICE_ID_ESSENTIAL_MONTHLY": "pri_01knexpea190b07xnf2pnzy1jv",
        "PADDLE_PRICE_ID_ESSENTIAL_YEARLY": "pri_01knexrtk4xmw5f3qx1pjnfw1y",
        "PADDLE_PRICE_ID_PRO_MONTHLY": "pri_01knexwa1jz8730kbdg02gepwj",
        "PADDLE_PRICE_ID_PRO_YEARLY": "pri_01knexx47yv6y5hbv6bh61bjzt",
        "PADDLE_PRICE_ID_EDUCATION_YEARLY": "pri_01knexz88zzjbvnf9atz4yxz69"
      }
    }
  }
}
```

**Quan trọng**: `SELF_HOSTED` = `"0"` để bật Paddle mode. Nếu = `"1"` thì tất cả users đều là BELIEVER, không cần payment.

---

## 5. Problem: DotNetEnv override launchSettings

### Vấn đề:
`Program.cs` của cả Identity và API có code:
```csharp
#if (DEBUG || STAGING)
    DotNetEnv.Env.TraversePath().Load(".env.local");
#else
    DotNetEnv.Env.TraversePath().Load(".env");
#endif
```

`DotNetEnv.Load()` version 2.3.0 mặc định **OVERRIDE** env vars đã có (từ launchSettings). Nghĩa là `.env.local` sẽ ghi đè tất cả env vars mà Rider đã set từ launchSettings.json.

Vấn đề: `.env.local` là shared file cho tất cả services, nhưng mỗi service cần `MONGODB_CONNECTION_STRING` khác nhau:
- Identity: `mongodb://localhost:27017/identity?replSet=rs0` (có `/identity`)
- API: `mongodb://localhost:27017/?replSet=rs0` (không có database name)

### Giải pháp: Sửa `Load()` thành `NoClobber()`
Sửa cả 2 file Program.cs:

**`Streetwriters.Identity/Program.cs`** (line 37):
```csharp
// Trước:
DotNetEnv.Env.TraversePath().Load(".env.local");

// Sau:
DotNetEnv.Env.NoClobber().TraversePath().Load(".env.local");
```

**`Notesnook.API/Program.cs`** (line 34):
```csharp
// Trước:
DotNetEnv.Env.TraversePath().Load(".env.local");

// Sau:
DotNetEnv.Env.NoClobber().TraversePath().Load(".env.local");
```

### Kết quả:
`NoClobber()` = nếu env var đã tồn tại (từ launchSettings), KHÔNG override.
Thứ tự ưu tiên: launchSettings.json > .env.local > .env

### Build lại sau khi sửa:
```bash
dotnet build Notesnook.sln
# Phải 0 Error(s)
```

---

## 6. Problem: Identity Server issuer mismatch

### Vấn đề:
Nếu chạy Identity Server trong Docker (via `docker compose up -d identity-server`), OpenID Connect discovery document trả về:
```json
{"issuer": "http://identity-server:8264", ...}
```

Nhưng Notesnook.API gọi `http://localhost:8264` → OAuth2 Introspection middleware reject vì issuer name khác authority.

### Lỗi:
```
System.InvalidOperationException: Policy error while contacting the discovery endpoint
http://localhost:8264/: Issuer name does not match authority: http://identity-server:8264
```

### Giải pháp:
**KHÔNG chạy Identity Server trong Docker** khi develop local. Chạy từ Rider IDE để issuer = `http://localhost:8264`.

```
Infrastructure (Docker):
├── MongoDB    → localhost:27017
├── Redis      → localhost:6379
└── MinIO      → localhost:9000

Services (Rider IDE):
├── Identity   → localhost:8264  ← chạy từ Rider
└── API        → localhost:5264  ← chạy từ Rider
```

---

## 7. Problem: MongoDBConfiguration.Database null

### Vấn đề:
Identity Server dùng `IdentityServer4.MongoDB` package, cần database name trong MongoDB connection string URL path.

### Lỗi:
```
System.ArgumentNullException: MongoDBConfiguration.Database cannot be null. (Parameter 'settings')
at IdentityServer4.MongoDB.DbContexts.MongoDBContextBase..ctor(IOptions`1 settings)
```

### Nguyên nhân:
Connection string thiếu database name: `mongodb://localhost:27017/?replSet=rs0`
`IdentityServer4.MongoDB` parse database name từ URL path, không dùng `MONGODB_DATABASE_NAME` env var.

### Giải pháp:
Identity Server cần connection string CÓ database name:
```
mongodb://localhost:27017/identity?replSet=rs0
                         ^^^^^^^^^ database name
```

Đảm bảo launchSettings.json của Identity có:
```json
"MONGODB_CONNECTION_STRING": "mongodb://localhost:27017/identity?replSet=rs0"
```

Và `.env.local` KHÔNG set `MONGODB_CONNECTION_STRING` (vì mỗi service cần giá trị khác nhau, nên để launchSettings xử lý).

---

## 8. Chạy từ Rider IDE

### Tạo Run Configurations:

1. **Run → Edit Configurations...**

2. **Tạo config cho Identity:**
   - Click "+" → **.NET Launch Settings Profile**
   - Project: **Streetwriters.Identity**
   - Launch profile: **Streetwriters.Identity (Project)**
   - OK

3. **Tạo config cho API:**
   - Click "+" → **.NET Launch Settings Profile**
   - Project: **Notesnook.API**
   - Launch profile: **Notesnook.API (Project)**
   - OK

4. **Tạo Compound config (chạy cả 2):**
   - Click "+" → **Compound**
   - Name: **Notesnook Full Stack**
   - Add: **Streetwriters.Identity**
   - Add: **Notesnook.API**
   - OK

### Chạy:
- Chọn **"Notesnook Full Stack"** ở dropdown trên toolbar
- Click ▶ Run (hoặc Shift+F10)
- Cả 2 service chạy song song

### Verify trong Rider console:
- Tab **Streetwriters.Identity**: chờ `Now listening on: http://[::]:8264`
- Tab **Notesnook.API**: chờ `Now listening on: http://[::]:5264`

### Health check:
```bash
curl http://localhost:8264/health   # → Healthy
curl http://localhost:5264/health   # → Healthy
```

---

## 9. Setup ngrok tunnel

### Tại sao cần:
Paddle webhook cần gửi HTTP POST đến server public. Local server (localhost:5264) không thể nhận được. ngrok tạo tunnel public → localhost.

### Chạy ngrok:
```bash
ngrok http 5264
```

### Lấy URL:
```bash
# Từ ngrok dashboard tại http://localhost:4040
# Hoặc:
curl -s http://localhost:4040/api/tunnels | grep -o '"public_url":"[^"]*"'
```

Output ví dụ:
```
"public_url":"https://changeless-unpalliated-maile.ngrok-free.dev"
```

### Webhook URL:
```
https://changeless-unpalliated-maile.ngrok-free.dev/paddle/webhook
```

### Verify ngrok forward đúng:
```bash
curl -s https://changeless-unpalliated-maile.ngrok-free.dev/health
# → Healthy
```

### Lưu ý:
- ngrok free URL thay đổi mỗi lần restart → phải update webhook destination trong Paddle Dashboard
- ngrok phải chạy trong khi test webhook
- Có thể xem requests tại http://localhost:4040 (ngrok web inspector)

---

## 10. Paddle Dashboard Setup

### 10.1 Tạo Sandbox Account
- Vào: https://sandbox-vendors.paddle.com
- Sign up → Verify email → Login

### 10.2 Lấy API Key (Server-side)
- Developer Tools → Authentication → Generate API Key
- Copy ngay (chỉ hiện 1 lần)
- Dạng: `pdl_sdbx_apikey_01knexb425m6zs9mkrq53ar5jt_...`
- Paste vào launchSettings: `PADDLE_API_KEY`

### 10.3 Lấy Client Token (Client-side, cho Paddle.js)
- Developer Tools → Authentication → Client-side tokens
- Copy token dạng: `test_3d7bff14084c16c37786a6c702b`
- Dùng trong HTML test page

### 10.4 Tạo Products & Prices
- Catalog → Products → + New Product
- Tạo 3 products với prices:

| Product | Price | Billing | Price ID env var |
|---------|-------|---------|------------------|
| Notesnook Essential | $2.49/month | Monthly | `PADDLE_PRICE_ID_ESSENTIAL_MONTHLY` |
| Notesnook Essential | $24.99/year | Yearly | `PADDLE_PRICE_ID_ESSENTIAL_YEARLY` |
| Notesnook Pro | $5.00/month | Monthly | `PADDLE_PRICE_ID_PRO_MONTHLY` |
| Notesnook Pro | $49.99/year | Yearly | `PADDLE_PRICE_ID_PRO_YEARLY` |
| Notesnook Education | $29.99/year | Yearly | `PADDLE_PRICE_ID_EDUCATION_YEARLY` |

- Tax category: "Standard digital goods" cho tất cả
- Copy mỗi Price ID (dạng `pri_01jxxxxxxxxx`)

### 10.5 Set Default Payment Link
- Checkout → Checkout settings
- Default payment link: nhập `https://localhost` (hoặc bất kỳ URL nào)
- Save
- **Nếu không set → checkout sẽ lỗi "Something went wrong"** (xem section 11)

### 10.6 Tạo Webhook Destination
- Developer Tools → Notifications → + New destination
- Type: URL
- URL: `https://<ngrok-subdomain>.ngrok-free.dev/paddle/webhook`
- Description: "Notesnook Sync Server"
- Events: chọn tất cả subscription.* + transaction.*
- Save

### 10.7 Lấy Webhook Secret
Secret key KHÔNG hiện popup sau khi save. Có 2 cách lấy:

**Cách 1: Trong Dashboard**
- Click vào destination vừa tạo
- Tìm "Endpoint secret key" hoặc "Secret"

**Cách 2: Qua API**
```bash
curl -s "https://sandbox-api.paddle.com/notification-settings" \
  -H "Authorization: Bearer <PADDLE_API_KEY>" | python3 -c "
import sys, json
data = json.loads(sys.stdin.read())
for d in data['data']:
    print(f\"ID: {d['id']}\")
    print(f\"URL: {d['destination']}\")
    print(f\"Secret: {d['endpoint_secret_key']}\")
    print()
"
```

- Copy secret (dạng: `pdl_ntfset_01knf00ey5h44y41f7hrd1vwsw_eicv3BPu/K+YmkYRFQSlBKnFE3QIduDn`)
- Paste vào launchSettings: `PADDLE_WEBHOOK_SECRET`

---

## 11. Problem: Checkout "Something went wrong"

### Vấn đề:
Khi click checkout button trên test page, Paddle overlay hiện lỗi: "Something went wrong. Please try again later."

### Debug:
```bash
# Tạo transaction qua API để xem lỗi cụ thể
curl -s -X POST "https://sandbox-api.paddle.com/transactions" \
  -H "Authorization: Bearer <PADDLE_API_KEY>" \
  -H "Content-Type: application/json" \
  -d '{
    "items": [{"price_id": "pri_01knexwa1jz8730kbdg02gepwj", "quantity": 1}]
  }'
```

### Lỗi trả về:
```json
{
  "error": {
    "code": "transaction_default_checkout_url_not_set",
    "detail": "A Default Payment Link has not yet been defined within the Paddle Dashboard"
  }
}
```

### Giải pháp:
- Paddle Dashboard → Checkout → Checkout settings
- Set "Default payment link" = `https://localhost`
- Save

---

## 12. Test Full Checkout Flow

### 12.1 Test HTML Page
File: `.docs/mac/payment/test-checkout.html`

```html
<!DOCTYPE html>
<html>
<head>
    <title>Paddle Checkout Test</title>
    <script src="https://cdn.paddle.com/paddle/v2/paddle.js"></script>
</head>
<body>
    <h1>Paddle Checkout Test</h1>
    <button onclick="checkout('pri_01knexwa1jz8730kbdg02gepwj', 'PRO Monthly')">PRO Monthly</button>
    <button onclick="checkout('pri_01knexx47yv6y5hbv6bh61bjzt', 'PRO Yearly')">PRO Yearly</button>
    <button onclick="checkout('pri_01knexpea190b07xnf2pnzy1jv', 'Essential Monthly')">Essential Monthly</button>

    <pre id="log"></pre>

    <script>
        Paddle.Environment.set("sandbox");
        Paddle.Initialize({
            token: "test_3d7bff14084c16c37786a6c702b",
            eventCallback: function(event) {
                document.getElementById('log').textContent += JSON.stringify(event, null, 2) + '\n';
            }
        });

        function checkout(priceId, label) {
            document.getElementById('log').textContent = 'Opening checkout for ' + label + '...\n';
            Paddle.Checkout.open({
                items: [{ priceId: priceId, quantity: 1 }],
                customData: { userId: "<USER_ID_FROM_SIGNUP>" },
                customer: { email: "<EMAIL_FROM_SIGNUP>" }
            });
        }
    </script>

    <h3>Sandbox Test Card</h3>
    <pre>
    Card:   4242 4242 4242 4242
    Expiry: Any future date (e.g. 12/28)
    CVV:    Any 3 digits (e.g. 123)
    Name:   Any name
    </pre>
</body>
</html>
```

### 12.2 Full Test Sequence

```bash
# Step 1: Signup
curl -s -X POST http://localhost:5264/users \
  -d "email=webhook-test@example.com" \
  -d "password=TestPass123!" \
  -d "client_id=notesnook"

# Response:
# {"access_token":"5473C0C8...","user_id":"69d26fdc...","errors":null}
# Lưu lại: TOKEN và USER_ID

# Step 2: Verify user có FREE subscription
TOKEN="5473C0C8DAC58461B7A1A671EC6CD788CC45F1E7FE10594830D9CBDB4D90842E"

curl -s http://localhost:5264/users \
  -H "Authorization: Bearer $TOKEN"

# Verify: subscription.plan = 0 (FREE), subscription.provider = 0 (STREETWRITERS)

# Step 3: Get checkout config
curl -s -X POST http://localhost:5264/subscriptions/checkout \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"plan":2,"period":"monthly"}'

# Response:
# {"priceId":"pri_01knexwa1jz8730kbdg02gepwj",
#  "customData":{"userId":"69d26fdc..."},
#  "customerEmail":"webhook-test@example.com"}

# Step 4: Open test-checkout.html in browser
# Update userId and email in the HTML file
# Click "PRO Monthly" → Fill test card → Pay

# Step 5: After payment, verify subscription updated
curl -s http://localhost:5264/users \
  -H "Authorization: Bearer $TOKEN"

# Verify: subscription.plan = 2 (PRO), subscription.provider = 3 (PADDLE)
# subscription.subscriptionId = "sub_01knf0jyw6..."
```

### Sandbox Test Card:
```
Card:   4242 4242 4242 4242
Expiry: 12/28 (any future date)
CVV:    123 (any 3 digits)
Name:   Test User (any name)
```

---

## 13. Verify Webhook hoạt động

### 13.1 Kiểm tra qua API response
```bash
TOKEN="<your_token>"
curl -s http://localhost:5264/users -H "Authorization: Bearer $TOKEN"
```

Trước checkout:
```json
{
  "subscription": {
    "provider": 0,    // STREETWRITERS (from signup)
    "plan": 0,        // FREE
    "status": 0,      // ACTIVE
    "subscriptionId": null
  },
  "totalStorage": 52428800  // 50MB (FREE tier)
}
```

Sau checkout (webhook đã xử lý):
```json
{
  "subscription": {
    "provider": 3,    // PADDLE ← webhook updated
    "plan": 2,        // PRO ← webhook updated
    "status": 0,      // ACTIVE
    "subscriptionId": "sub_01knf0jyw6..." // ← webhook set
  },
  "totalStorage": 10737418240  // 10GB (PRO tier)
}
```

### 13.2 Kiểm tra trực tiếp MongoDB
```bash
docker exec notesnook-sync-server-notesnook-db-1 mongosh --quiet \
  "mongodb://localhost:27017/notesnook" --eval '
var sub = db.subscriptions.findOne({UserId: "<USER_ID>"});
printjson(sub);
'
```

### 13.3 Kiểm tra qua Paddle API
```bash
# List notifications sent to our webhook
curl -s "https://sandbox-api.paddle.com/notifications?notification_setting_id=ntfset_01knf00ey5h44y41f7hrd1vwsw" \
  -H "Authorization: Bearer pdl_sdbx_apikey_01knexb425m6zs9mkrq53ar5jt_HZyVgRHXmpW9yfQpmXWBaw_APo"
```

### 13.4 Kiểm tra trong Paddle Dashboard
- **Notifications**: Developer Tools → Notifications → Click destination → xem events "delivered"
- **Subscriptions**: Subscriptions (sidebar) → thấy subscription "Active"
- **Transactions**: Transactions (sidebar) → thấy transaction "completed"
- **Customers**: Customers (sidebar) → thấy customer email

### 13.5 Test webhook signature rejection
```bash
# Gửi request giả với signature invalid → phải trả về 401
curl -s -o /dev/null -w "%{http_code}" -X POST \
  http://localhost:5264/paddle/webhook \
  -H "Content-Type: application/json" \
  -H "Paddle-Signature: ts=123;h1=invalid" \
  -d '{"event_type":"test"}'

# Expected: 401
```

---

## 14. Tất cả câu lệnh test

### Infrastructure:
```bash
# Start
docker compose up -d notesnook-db notesnook-s3 setup-s3 notesnook-redis

# Reconfig MongoDB replica set (1 lần)
docker exec notesnook-sync-server-notesnook-db-1 mongosh --quiet --eval '
var cfg = rs.conf();
cfg.members[0].host = "localhost:27017";
rs.reconfig(cfg, {force: true});
'

# Stop
docker compose down

# Stop + xóa data (phải reconfig lại)
docker compose down -v
```

### ngrok:
```bash
# Start
ngrok http 5264

# Get URL
curl -s http://localhost:4040/api/tunnels | grep -o '"public_url":"[^"]*"'

# Web inspector
open http://localhost:4040
```

### Health checks:
```bash
curl http://localhost:8264/health   # Identity
curl http://localhost:5264/health   # API
```

### Signup:
```bash
curl -s -X POST http://localhost:5264/users \
  -d "email=test@example.com" \
  -d "password=TestPass123!" \
  -d "client_id=notesnook"
```

### Get user (with subscription):
```bash
curl -s http://localhost:5264/users \
  -H "Authorization: Bearer <TOKEN>"
```

### Checkout config:
```bash
# PRO Monthly
curl -s -X POST http://localhost:5264/subscriptions/checkout \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"plan":2,"period":"monthly"}'

# PRO Yearly
curl -s -X POST http://localhost:5264/subscriptions/checkout \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"plan":2,"period":"yearly"}'

# Essential Monthly
curl -s -X POST http://localhost:5264/subscriptions/checkout \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"plan":1,"period":"monthly"}'
```

### Cancel subscription:
```bash
curl -s -X POST http://localhost:5264/subscriptions/cancel \
  -H "Authorization: Bearer <TOKEN>"
```

### Pause subscription:
```bash
curl -s -X POST http://localhost:5264/subscriptions/pause \
  -H "Authorization: Bearer <TOKEN>"
```

### Resume subscription:
```bash
curl -s -X POST http://localhost:5264/subscriptions/resume \
  -H "Authorization: Bearer <TOKEN>"
```

### Update payment method URL:
```bash
curl -s http://localhost:5264/subscriptions/update-payment-method \
  -H "Authorization: Bearer <TOKEN>"
```

### Check MongoDB subscription:
```bash
docker exec notesnook-sync-server-notesnook-db-1 mongosh --quiet \
  "mongodb://localhost:27017/notesnook" --eval '
db.subscriptions.find().forEach(function(s) {
  printjson({
    userId: s.UserId,
    plan: s.Plan,
    status: s.Status,
    provider: s.Provider,
    subscriptionId: s.SubscriptionId
  });
});
'
```

### Check Paddle notifications:
```bash
curl -s "https://sandbox-api.paddle.com/notification-settings" \
  -H "Authorization: Bearer <PADDLE_API_KEY>"
```

### Token introspection (debug auth):
```bash
curl -s -X POST http://localhost:8264/connect/introspect \
  -d "token=<TOKEN>" \
  -d "client_id=notesnook" \
  -d "client_secret=<NOTESNOOK_API_SECRET>"
```

---

## 15. Troubleshooting Checklist

| Lỗi | Nguyên nhân | Giải pháp |
|-----|-------------|-----------|
| `nodename nor servname provided: notesnook-db` | MongoDB replica set dùng Docker hostname | Reconfig: `rs.reconfig({members[0].host: "localhost:27017"})` |
| `Issuer name does not match authority` | Identity chạy trong Docker, issuer = `identity-server:8264` | Chạy Identity từ Rider, không dùng Docker |
| `MongoDBConfiguration.Database cannot be null` | Identity connection string thiếu `/identity` | Set `MONGODB_CONNECTION_STRING=mongodb://localhost:27017/identity?replSet=rs0` |
| `.env.local` override launchSettings | DotNetEnv.Load() mặc định clobber | Sửa thành `DotNetEnv.Env.NoClobber().TraversePath().Load(...)` |
| Checkout "Something went wrong" | Paddle chưa set Default Payment Link | Dashboard → Checkout → Checkout settings → set URL |
| Webhook 401 Unauthorized | Sai webhook secret hoặc chưa set | Lấy secret từ API: GET `/notification-settings` |
| Webhook không nhận | ngrok đã tắt hoặc URL thay đổi | Restart ngrok, update URL trong Paddle Dashboard |
| 500 trên GET /users | Token expired hoặc Identity chưa chạy | Check `curl localhost:8264/health`, lấy token mới |
| Subscription vẫn FREE sau checkout | Webhook chưa processed | Check Rider console log, check ngrok inspector |

---

## 16. Quick Start (cho lần sau)

```bash
# 1. Mở Docker Desktop
open -a Docker

# 2. Start infrastructure
cd /Users/phanchihieu/z4l_dev/moniva_vn/notesnook-sync-server
docker compose up -d notesnook-db notesnook-s3 setup-s3 notesnook-redis

# 3. Chờ MongoDB healthy
docker inspect --format='{{.State.Health.Status}}' notesnook-sync-server-notesnook-db-1
# Nếu chưa healthy, chờ ~60s

# 4. Check MongoDB replica set (chỉ cần nếu volume mới)
docker exec notesnook-sync-server-notesnook-db-1 mongosh --quiet --eval 'rs.conf().members[0].host'
# Nếu = "notesnook-db:27017" → reconfig:
docker exec notesnook-sync-server-notesnook-db-1 mongosh --quiet --eval '
var cfg = rs.conf();
cfg.members[0].host = "localhost:27017";
rs.reconfig(cfg, {force: true});
'

# 5. Start ngrok
ngrok http 5264
# Copy URL, update Paddle Dashboard nếu URL thay đổi

# 6. Mở Rider → chọn "Notesnook Full Stack" → Run ▶
# Chờ cả 2 service hiện "Now listening on..."

# 7. Verify
curl http://localhost:8264/health   # → Healthy
curl http://localhost:5264/health   # → Healthy

# 8. Test
curl -s -X POST http://localhost:5264/users \
  -d "email=test@example.com" \
  -d "password=TestPass123!" \
  -d "client_id=notesnook"

# Done! Sẵn sàng develop.
```

---

## Tổng kết Architecture khi chạy local

```
┌──────────────────────────────────────────────────────┐
│  Docker Desktop                                       │
│  ├── MongoDB     localhost:27017  (replica set rs0)   │
│  ├── Redis       localhost:6379                       │
│  └── MinIO (S3)  localhost:9000                       │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│  Rider IDE                                            │
│  ├── Streetwriters.Identity   localhost:8264          │
│  └── Notesnook.API            localhost:5264          │
│       ├── Paddle Mode (SELF_HOSTED=0)                │
│       ├── Webhook: /paddle/webhook                   │
│       └── Subscription CRUD: /subscriptions/*        │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│  ngrok                                                │
│  https://xxx.ngrok-free.dev → localhost:5264          │
│  (Paddle gửi webhook qua đây)                        │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│  Paddle Sandbox                                       │
│  api.paddle.com ← API calls                          │
│  Paddle.js     ← Client checkout                     │
│  Webhooks      → ngrok → localhost:5264              │
└──────────────────────────────────────────────────────┘
```
