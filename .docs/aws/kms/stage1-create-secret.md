# Stage 1 — Tạo Secret trong AWS Secrets Manager

## Mục tiêu
Lưu toàn bộ biến môi trường từ `.env` vào AWS Secrets Manager (được encrypt bởi KMS).

## Các bước

1. Vào AWS Console → **Secrets Manager** → **Store a new secret**
2. Chọn **Other type of secret** → **Key/value**
3. Nhập từng biến theo bảng dưới
4. Encryption key: giữ nguyên **aws/secretsmanager** (KMS mặc định, free)
5. Secret name: `notesnook/prod`
6. Các bước còn lại giữ mặc định → **Store**

## Danh sách keys cần nhập

| Key | Mô tả | Required |
|---|---|---|
| `INSTANCE_NAME` | Tên instance | yes |
| `NOTESNOOK_API_SECRET` | Token signing secret (>32 chars) | yes |
| `DISABLE_SIGNUPS` | Chặn đăng ký mới (`true`/`false`) | yes |
| `SMTP_USERNAME` | Email SMTP | yes |
| `SMTP_PASSWORD` | Password SMTP | yes |
| `SMTP_HOST` | SMTP host (vd: `smtp.gmail.com`) | yes |
| `SMTP_PORT` | SMTP port (vd: `465`) | yes |
| `NOTESNOOK_APP_PUBLIC_URL` | Public URL web app | yes |
| `MONOGRAPH_PUBLIC_URL` | Public URL monograph | yes |
| `AUTH_SERVER_PUBLIC_URL` | Public URL identity server | yes |
| `ATTACHMENTS_SERVER_PUBLIC_URL` | Public URL MinIO S3 | yes |
| `MINIO_ROOT_USER` | MinIO username | no |
| `MINIO_ROOT_PASSWORD` | MinIO password | no |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | Forwarded headers | no |
| `NOTESNOOK_CORS_ORIGINS` | Allowed CORS origins | no |
| `KNOWN_PROXIES` | Known proxy IPs | no |
| `TWILIO_ACCOUNT_SID` | Twilio SID (SMS 2FA) | no |
| `TWILIO_AUTH_TOKEN` | Twilio auth token | no |
| `TWILIO_SERVICE_SID` | Twilio service SID | no |

## Lưu ý
- Secret name phải là `notesnook/prod` để khớp với script deploy
- Giá trị được encrypt tự động bằng KMS key `aws/secretsmanager`
- Có thể update secret bất kỳ lúc nào mà không cần redeploy
