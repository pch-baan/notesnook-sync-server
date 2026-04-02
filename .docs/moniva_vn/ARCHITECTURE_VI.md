# Notesnook Sync Server — Phân tích kiến trúc (Tiếng Việt)

> Giải thích đơn giản như cho trẻ 5 tuổi, nhưng đủ chi tiết để lập trình viên hiểu flow thực tế.

---

## Mục lục

1. [Hình dung tổng thể](#1-hình-dung-tổng-thể)
2. [Đăng ký và đăng nhập](#2-đăng-ký-và-đăng-nhập)
3. [Đồng bộ ghi chú](#3-đồng-bộ-ghi-chú)
4. [Upload và download file đính kèm](#4-upload-và-download-file-đính-kèm)
5. [Thông báo real-time](#5-thông-báo-real-time)
6. [Các service nói chuyện với nhau như thế nào](#6-các-service-nói-chuyện-với-nhau-như-thế-nào)
7. [Dữ liệu được lưu trữ như thế nào](#7-dữ-liệu-được-lưu-trữ-như-thế-nào)
8. [Công việc nền tự động](#8-công-việc-nền-tự-động)
9. [Bảo mật và mã hóa](#9-bảo-mật-và-mã-hóa)
10. [Vòng đời đầy đủ: từ mở app đến xem ghi chú](#10-vòng-đời-đầy-đủ-từ-mở-app-đến-xem-ghi-chú)

---

## 1. Hình dung tổng thể

### Ví von đơn giản

Tưởng tượng hệ thống này như một **tòa nhà văn phòng** với nhiều phòng ban:

| Phòng ban | Tên thật | Làm gì |
|---|---|---|
| Phòng bảo vệ / lễ tân | `Streetwriters.Identity` | Kiểm tra xem bạn là ai, cấp thẻ vào cửa |
| Kho lưu trữ ghi chú | `Notesnook.API` | Nhận, lưu, gửi ghi chú của bạn |
| Phòng phát thanh nội bộ | `Streetwriters.Messenger` | Thông báo cho các thiết bị khác khi có thay đổi |
| Kho chứa đồ | `MinIO (S3)` | Lưu ảnh, file đính kèm |
| Tủ hồ sơ | `MongoDB` | Lưu toàn bộ dữ liệu |
| Bảng thông báo nhanh | `Redis` | Giúp nhiều máy chủ nói chuyện với nhau qua SignalR |

### Sơ đồ tổng thể

```
Điện thoại / Máy tính của bạn
        │
        ├──► identity-server (port 8264)   ← Đăng nhập, xác thực
        │
        ├──► notesnook-server (port 5264)  ← Đồng bộ ghi chú, upload file
        │         │
        │         ├── MongoDB              ← Lưu ghi chú (đã mã hóa)
        │         ├── MinIO               ← Lưu file đính kèm
        │         └── Redis               ← Kết nối SignalR đa máy chủ
        │
        ├──► sse-server (port 7264)        ← Nhận thông báo real-time
        │
        └──► monograph-server (port 6264)  ← Xuất bản ghi chú công khai
```

---

## 2. Đăng ký và đăng nhập

### 2.1 Đăng ký tài khoản

**Như trẻ 5 tuổi:** Bạn điền tờ đơn, nhân viên lễ tân ghi tên bạn vào sổ, tạo cho bạn một chiếc thẻ (token), rồi gửi email để xác nhận.

**Thực tế trong code:**

```
App gửi POST /users (Notesnook.API)
  │
  ├── UserService.CreateUserAsync()
  │     ├── Gọi sang Identity Server qua WAMP
  │     ├── Tạo user trong MongoDB (AspNetCore.Identity)
  │     ├── Hash mật khẩu bằng Argon2
  │     └── Tạo UserSettings (lưu encryption keys, storage limit)
  │
  ├── Gửi email xác nhận
  │     └── Nếu SELF_HOSTED=1: tự động xác nhận luôn, không cần email
  │
  └── Tạo Subscription (gói dịch vụ)
```

**File liên quan:**
- `Notesnook.API/Controllers/UsersController.cs`
- `Streetwriters.Identity/Services/UserAccountService.cs`

---

### 2.2 Xác nhận email

```
GET /account/confirm?userId=X&code=Y
  │
  ├── Kiểm tra mã xác nhận (token)
  ├── Đánh dấu EmailConfirmed = true
  └── Tự động bật MFA qua Email
```

---

### 2.3 Đăng nhập

**Như trẻ 5 tuổi:** Bạn đưa thẻ ID, bảo vệ kiểm tra, đưa lại cho bạn một chiếc thẻ tạm thời (access token) để dùng trong ngày.

**Thực tế trong code:**

```
App gửi yêu cầu lấy token (OAuth2 Authorization Code Flow)
  │
  ├── Identity Server (IdentityServer4) xác thực user + password
  ├── Trả về JWT access token
  │     └── Chứa: sub (userId), client_id, jti (token ID), exp (hết hạn)
  │
  └── Mỗi request sau đó: gửi kèm token trong header Authorization: Bearer <token>
        └── Notesnook.API tự xác thực bằng cách gọi /connect/introspect (không tự decode JWT)
              └── Kết quả cache 30 phút để giảm tải Identity Server
```

---

### 2.4 Xác thực 2 bước (MFA)

Ba phương thức hỗ trợ:

| Phương thức | Cách hoạt động |
|---|---|
| **Email OTP** | Gửi mã 6 số qua email, hết hạn sau vài phút |
| **SMS OTP** | Gửi mã qua Twilio SMS |
| **Authenticator App (TOTP)** | Quét QR code, dùng app như Google Authenticator |

Ngoài ra có **Recovery codes** — mã dự phòng một lần dùng.

---

## 3. Đồng bộ ghi chú

### 3.1 Hình dung

**Như trẻ 5 tuổi:** Bạn có 3 cuốn tập ở nhà (điện thoại, laptop, máy tính bảng). Khi bạn viết vào 1 cuốn, người phụ tá chép vào 2 cuốn còn lại. Máy chủ là người phụ tá đó.

### 3.2 Đăng ký thiết bị

Mỗi thiết bị phải đăng ký trước khi đồng bộ:

```
POST /devices?deviceId=<uuid>
  └── Tạo SyncDevice { UserId, DeviceId, LastAccessTime, IsSyncReset: true }
        └── IsSyncReset=true = thiết bị mới, cần tải toàn bộ dữ liệu về
```

---

### 3.3 Kéo dữ liệu về (Pull Sync)

**Như trẻ 5 tuổi:** Bạn hỏi "có gì mới không?", người phụ tá gói tất cả thứ bạn chưa có thành từng túi nhỏ (chunk) rồi đưa lần lượt cho bạn.

**Kết nối qua SignalR** (WebSocket hai chiều, giao thức MessagePack):

```
Client kết nối WebSocket → /hubs/sync/v2
Client gọi: RequestFetchV3(deviceId)

Server xử lý:
  1. Kiểm tra device có tồn tại không
  2. Lấy danh sách ID chưa đồng bộ từ DeviceIdsChunk (collection MongoDB)
  3. Chuyển "unsynced" → "pending"
  4. Đóng gói từng nhóm (tối đa 7MB / gói) từ 11 loại dữ liệu:
       settingitem, attachment, note, notebook, content,
       shortcut, reminder, color, tag, vault, relation
  5. Gửi từng gói qua: Clients.Caller.SendItems(chunk)
  6. Chờ client xác nhận nhận được → gửi gói tiếp
  7. Sau cùng: gửi monographs và inbox items
  8. Xóa "pending" IDs
  9. Đặt IsSyncReset = false
```

**Tại sao chia gói 7MB?** Để tránh timeout và giúp mạng chậm vẫn đồng bộ được.

---

### 3.4 Đẩy dữ liệu lên (Push Sync)

**Như trẻ 5 tuổi:** Bạn viết xong, đưa tờ giấy cho người phụ tá. Người phụ tá cất vào tủ, rồi thông báo cho 2 cuốn tập kia biết có nội dung mới.

```
Client gọi: PushItems(deviceId, { type: "note", items: [...] })

Server xử lý:
  1. Xác định loại dữ liệu (note / notebook / attachment / ...)
  2. UpsertMany() — lưu vào MongoDB (thêm mới hoặc cập nhật)
  3. Commit qua IUnitOfWork (có transaction trong môi trường production)
  4. Thêm ID vừa lưu vào DeviceIdsChunk của TẤT CẢ thiết bị khác (trừ thiết bị gửi)
  5. Trả về số lượng item đã lưu thành công
```

Sau đó client gọi thêm:

```
Client gọi: PushCompleted(deviceId)
  └── Server phát: Clients.OthersInGroup(userId).PushCompletedV2(deviceId)
        └── Tất cả thiết bị khác của user nhận được thông báo → tự kéo dữ liệu mới về
```

**Group** trong SignalR = `userId` — mọi thiết bị của cùng user ở trong cùng 1 group.

---

### 3.5 Theo dõi trạng thái đồng bộ: DeviceIdsChunk

Đây là cơ chế cốt lõi giúp mỗi thiết bị biết mình còn thiếu gì:

```
Collection: DeviceIdsChunk
{
  UserId: "abc",
  DeviceId: "phone-123",
  Key: "unsynced",        ← hoặc "pending"
  Ids: ["id1", "id2", ...]  ← tối đa 25.000 ID / chunk
}
```

- **unsynced**: những item thiết bị này chưa nhận được
- **pending**: đang trong quá trình gửi (chờ xác nhận)
- Nếu kết nối đứt giữa chừng: pending → rollback về unsynced, gửi lại

---

## 4. Upload và download file đính kèm

### 4.1 Hình dung

**Như trẻ 5 tuổi:** Máy chủ không lưu file trực tiếp. Nó chỉ đưa cho bạn một "giấy thông hành tạm thời" (presigned URL) để bạn tự đến kho (MinIO) lấy hoặc cất đồ.

### 4.2 Upload file nhỏ (single-part)

```
1. Client: PUT /s3?name=<tên_file>  (không có body, chỉ có Content-Length)
2. Server: tạo presigned URL → trả về cho client
3. Client: PUT file thẳng lên MinIO qua presigned URL
4. Server xác nhận:
   ├── GetObjectSizeAsync() — kiểm tra file đã lên chưa
   ├── Kiểm tra dung lượng lưu trữ còn lại (quota)
   └── Cập nhật UserSettings.StorageLimit.Value += fileSize
```

### 4.3 Upload file lớn (multipart)

```
1. GET /s3/multipart?name=X&parts=N
   └── Server: khởi tạo multipart upload trên S3, trả về { UploadId, Parts: [url1, url2, ...] }

2. Client PUT từng phần vào URL tương ứng

3. POST /s3/multipart  (hoàn tất)
   ├── Lấy kích thước từ S3 (ListParts)
   ├── Kiểm tra quota
   ├── Gọi S3 CompleteMultipartUploadAsync
   └── Cập nhật StorageLimit
```

### 4.4 Download file

```
GET /s3?name=<tên_file>
  └── Server trả về presigned GET URL (hết hạn sau 1 giờ)
        └── Client tải trực tiếp từ MinIO
```

### 4.5 Tại sao có 2 MinIO client?

```
Internal Client  → dùng URL nội bộ (http://notesnook-s3:9000) để tạo presigned URL trong Docker network
External Client  → dùng URL công khai (ATTACHMENTS_SERVER_PUBLIC_URL) để client bên ngoài truy cập
```

Nếu chỉ dùng 1 client, chữ ký (HMAC) trong presigned URL sẽ không khớp khi hostname thay đổi.

---

## 5. Thông báo real-time

### 5.1 Hình dung

**Như trẻ 5 tuổi:** Bạn bật radio (SSE). Khi có tin tức mới (thiết bị khác đồng bộ xong), đài phát thanh (Messenger) sẽ đọc tin vào radio của bạn ngay lập tức.

### 5.2 SSE (Server-Sent Events)

Client mở kết nối một chiều, server đẩy events xuống:

```
GET /sse  (Authorization: Bearer <token>)
  └── Kết nối mở, client "nghe" liên tục

Server gửi:
  ├── Heartbeat mỗi 5 giây: { type: "heartbeat", data: { t: timestamp } }
  ├── Logout event: { type: "logout", data: { reason: "..." } }
  └── Sync notification: { type: "pushCompleted" }
```

### 5.3 WAMP (nội bộ giữa các service)

**Như trẻ 5 tuổi:** Các phòng ban trong tòa nhà dùng bộ đàm nội bộ để nhắn tin cho nhau mà không cần gặp mặt.

```
Notesnook.API → publish lên topic "co.streetwriters.sse.send"
  └── Messenger nhận message → lọc theo userId → gửi SSE đến client đúng user
```

**Các topic WAMP quan trọng:**

| Topic | Mục đích |
|---|---|
| `co.streetwriters.sse.send` | Gửi SSE event đến client |
| `co.streetwriters.identity.users` | Tạo/xóa user |
| `co.streetwriters.identity.clear_cache` | Xóa cache introspection trên các service |
| `co.streetwriters.subscriptions.*` | Quản lý gói dịch vụ |

---

## 6. Các service nói chuyện với nhau như thế nào

### Sơ đồ giao tiếp

```
                  ┌─────────────────┐
                  │  Notesnook.API  │
                  └────────┬────────┘
                           │
          ┌────────────────┼────────────────┐
          │ HTTP           │ WAMP           │ WAMP
          ▼                ▼                ▼
  ┌───────────────┐ ┌────────────┐ ┌──────────────────┐
  │ Identity      │ │ Messenger  │ │ Subscription     │
  │ (introspect)  │ │ (SSE/WAMP) │ │ (gói dịch vụ)   │
  └───────────────┘ └────────────┘ └──────────────────┘
```

### Notesnook.API → Identity Server

- **Mục đích:** Xác thực token của mỗi request
- **Cách:** HTTP POST `/connect/introspect`
- **Tối ưu:** Kết quả được cache **30 phút** để không gọi lại mỗi request

### Notesnook.API → Messenger (WAMP)

- Khi push sync xong: phát `SSE pushCompleted` đến các thiết bị khác
- Khi cần logout: phát `SSE logout` đến tất cả thiết bị

### Identity Server → Messenger (WAMP)

- Khi đổi email / reset mật khẩu: gửi `logout` đến tất cả client
- Xóa cache introspection trên tất cả service

---

## 7. Dữ liệu được lưu trữ như thế nào

### 7.1 SyncItem — ghi chú (đã mã hóa)

```json
{
  "UserId": "user-abc",
  "ItemId": "note-xyz",
  "IV": "...",          // vector khởi tạo mã hóa
  "Cipher": "...",      // nội dung đã mã hóa, server KHÔNG đọc được
  "Algorithm": "AES",
  "Length": 1024,
  "DateSynced": "2026-04-02T..."
}
```

**Quan trọng:** Server chỉ lưu blob đã mã hóa. Không bao giờ thấy nội dung thật của ghi chú.

### 7.2 SyncDevice — thiết bị

```json
{
  "UserId": "user-abc",
  "DeviceId": "phone-123",
  "LastAccessTime": "2026-04-02T...",
  "IsSyncReset": false,
  "AppVersion": "3.0.0"
}
```

### 7.3 UserSettings — cài đặt + encryption keys

```json
{
  "UserId": "user-abc",
  "Salt": "...",
  "VaultKey": "...",          // key mã hóa vault, đã encrypt
  "AttachmentsKey": "...",
  "DataEncryptionKey": "...",
  "StorageLimit": {
    "Value": 104857600,       // bytes đã dùng
    "UpdatedAt": "2026-03-01T..."  // reset hàng tháng
  }
}
```

### 7.4 DeviceIdsChunk — theo dõi đồng bộ

```json
{
  "UserId": "user-abc",
  "DeviceId": "phone-123",
  "Key": "unsynced",
  "Ids": ["id1", "id2", "id3"]   // tối đa 25.000 IDs / document
}
```

### 7.5 Các collection MongoDB chính

| Collection | Nội dung |
|---|---|
| `notes` | Ghi chú |
| `notebooks` | Sổ tay |
| `content` | Nội dung ghi chú (lớn hơn, tách riêng) |
| `attachments` | Metadata file đính kèm |
| `settingsv2` | Cài đặt người dùng |
| `sync_devices` | Danh sách thiết bị |
| `inbox_items` | Item nhận từ Inbox API |
| `inbox_api_keys` | API keys cho Inbox |

---

## 8. Công việc nền tự động

Dùng **Quartz.NET** để lên lịch chạy tự động:

### DeviceCleanupJob (Notesnook.API)

```
Chạy định kỳ → Tìm các SyncDevice có LastAccessTime > 1 tháng → Xóa
  └── Đồng thời xóa DeviceIdsChunk liên quan
```

**Mục đích:** Dọn dẹp thiết bị bỏ hoang để không tích lũy dữ liệu vô ích.

### TokenCleanupJob (Streetwriters.Identity)

```
Chạy định kỳ → Xóa các OAuth2 grant / refresh token đã hết hạn khỏi MongoDB
```

**Mục đích:** Giữ database gọn nhẹ, không để token cũ chiếm chỗ mãi.

---

## 9. Bảo mật và mã hóa

### 9.1 End-to-End Encryption (E2EE)

**Như trẻ 5 tuổi:** Bạn khóa nhật ký bằng chìa khóa của riêng bạn trước khi gửi đến người giữ hộ. Người giữ hộ không có chìa khóa, không đọc được gì cả.

- Mọi ghi chú đều được mã hóa **trên thiết bị** trước khi gửi lên server
- Server chỉ lưu `IV + Cipher` — **không bao giờ** thấy nội dung thật
- Keys mã hóa lưu trong `UserSettings` dưới dạng đã được mã hóa thêm một lần nữa

### 9.2 Xác thực token

```
Mỗi request → Authorization: Bearer <JWT>
  └── Notesnook.API gọi Identity /connect/introspect
        ├── Hợp lệ → cache 30 phút, tiếp tục xử lý
        └── Không hợp lệ → 401 Unauthorized
```

### 9.3 Quản lý session

```
POST /account/sessions/clear?all=false
  ├── all=false → xóa session của thiết bị khác, giữ lại session hiện tại
  └── all=true  → đăng xuất tất cả thiết bị

→ Publish WAMP message → Messenger gửi SSE logout đến các thiết bị bị xóa session
```

---

## 10. Vòng đời đầy đủ: từ mở app đến xem ghi chú

```
Bước 1: MỞ APP lần đầu
  └── App gửi POST /users → Tạo tài khoản, nhận token

Bước 2: XÁC NHẬN EMAIL
  └── Click link email → GET /account/confirm → Tài khoản kích hoạt

Bước 3: ĐĂNG NHẬP
  └── App đổi username/password lấy JWT access token qua OAuth2

Bước 4: ĐĂNG KÝ THIẾT BỊ
  └── POST /devices?deviceId=<uuid> → SyncDevice được tạo, IsSyncReset=true

Bước 5: TẢI DỮ LIỆU VỀ (lần đầu)
  └── SignalR → RequestFetchV3(deviceId)
        └── Server đóng gói TẤT CẢ ghi chú thành chunk 7MB
              └── Gửi lần lượt → Client lưu local → IsSyncReset=false

Bước 6: VIẾT GHI CHÚ MỚI
  └── App mã hóa ghi chú → SignalR PushItems()
        └── Server lưu MongoDB → Đánh dấu "unsynced" cho các thiết bị khác
              └── SignalR PushCompleted() → Thiết bị khác nhận thông báo

Bước 7: ĐIỆN THOẠI NHẬN THÔNG BÁO
  └── SSE stream nhận "pushCompleted"
        └── Điện thoại gọi RequestFetchV3() → Kéo ghi chú mới về

Bước 8: ĐÍNH KÈM ẢNH
  └── PUT /s3?name=photo.jpg → Nhận presigned URL
        └── PUT ảnh thẳng lên MinIO
              └── Server xác nhận → cập nhật StorageLimit

Bước 9: MỞ APP TRÊN MÁY TÍNH
  └── Kết nối SSE → nhận heartbeat mỗi 5 giây
        └── Khi có ghi chú mới → SSE pushCompleted → tự đồng bộ
```

---

## Tóm tắt những điểm quan trọng nhất

| Điểm | Ý nghĩa |
|---|---|
| **E2EE** | Server không đọc được ghi chú của bạn |
| **DeviceIdsChunk** | Mỗi thiết bị có danh sách riêng "còn thiếu cái gì" |
| **SignalR groups** | Mọi thiết bị của cùng user ở chung 1 group → thông báo lẫn nhau |
| **WAMP message bus** | Các service giao tiếp nội bộ mà không cần biết địa chỉ của nhau |
| **Presigned URL** | File không đi qua server, trực tiếp lên/xuống MinIO |
| **OAuth2 Introspection cache** | 30 phút cache → Identity Server không bị quá tải |
| **Chunk 7MB** | Tránh timeout, hỗ trợ mạng chậm |
| **MongoDB transactions** | Chỉ bật ở production (cần replica set) |
