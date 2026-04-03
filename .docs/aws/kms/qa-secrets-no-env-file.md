# Q&A — AWS Secrets Manager & Bảo mật .env

## Q: KMS mặc định là gì?

Khi lưu secret vào AWS Secrets Manager mà không chỉ định KMS key riêng, AWS tự động dùng:

```
aws/secretsmanager
```

Đây là **AWS Managed Key** — do AWS tạo và quản lý hoàn toàn, không mất thêm chi phí, không cần cấu hình.

---

## Q: Encrypt là gì?

**Encrypt** = **mã hóa** — biến dữ liệu gốc thành dạng không đọc được nếu không có key.

```
Dữ liệu gốc:   MONGODB_PASSWORD=abc123
Sau encrypt:   X9#kLm2$pQr7...  ← vô nghĩa nếu không có key
Sau decrypt:   MONGODB_PASSWORD=abc123  ← khôi phục lại khi có key
```

Trong hệ thống này:
- `.env` được encrypt và lưu trong Secrets Manager
- EC2 (IAM Role) fetch về → AWS tự decrypt → nhận `.env` gốc
- Nếu ai hack vào AWS storage, họ chỉ thấy dữ liệu đã mã hóa

---

## Q: SSH vào EC2 vẫn đọc được .env — vậy encrypt có tác dụng gì?

Đúng, SSH vào EC2 vẫn đọc được — và đó là **bình thường**. Encrypt không ẩn dữ liệu khỏi người có quyền truy cập hợp lệ.

| Tình huống | Không dùng Secrets Manager | Dùng Secrets Manager |
|---|---|---|
| Repo GitHub bị lộ | Lộ secrets | An toàn |
| AWS S3/storage bị hack | N/A | An toàn (đã mã hóa) |
| Người khác xem AWS Console | Thấy plaintext | Cần quyền KMS |
| SSH vào EC2 | Đọc được | Đọc được (bình thường) |

---

## Q: Làm sao để không có file .env trên EC2?

Thay vì ghi ra file, inject secrets trực tiếp vào RAM (shell environment) rồi truyền vào `docker compose`.

### So sánh các cách

| Cách | Ghi disk | Special chars | Bảo mật |
|---|---|---|---|
| `export + xargs` | Không | Dễ lỗi | Tốt |
| `mktemp` | **Có** (tạm) | OK | Trung bình |
| `source <(...)` | Không | OK | **Tốt nhất** |

### Cách tốt nhất: `source <(...)`

```bash
set -a
source <(aws secretsmanager get-secret-value \
  --region ap-southeast-1 \
  --secret-id notesnook/prod \
  --query SecretString \
  --output text | python3 -c "
import sys, json
secrets = json.load(sys.stdin)
for k, v in secrets.items():
    print(f'{k}={v}')
")
set +a

rm -f .env

docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --remove-orphans
docker image prune -f
```

**Tại sao `set -a` / `set +a`?**
- `set -a` → tự động export tất cả variables vừa source vào shell env
- `source <(...)` → load KEY=VALUE vào shell, không ghi file
- `set +a` → tắt auto-export
- `docker compose` kế thừa env từ shell → không cần file `.env`

---

## Flow hoạt động tổng thể

```
┌─────────────────────────────────────────────────────────────────┐
│                        GIT PUSH to main                         │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                    GITHUB ACTIONS                                │
│                                                                  │
│  1. Build Docker image                                           │
│  2. Push lên Docker Hub                                          │
│  3. SSH vào EC2                                                  │
└─────────────────────────┬───────────────────────────────────────┘
                          │ SSH
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                       EC2 (Ubuntu)                               │
│                                                                  │
│  Bước 1: EC2 gọi AWS Secrets Manager                            │
│          (dùng IAM Role — không cần password)                    │
│                    │                                             │
│                    ▼                                             │
│  ┌─────────────────────────────────┐                            │
│  │     AWS Secrets Manager         │                            │
│  │  (encrypted bằng KMS)           │                            │
│  │  NOTESNOOK_API_SECRET=xxx       │                            │
│  │  SMTP_PASSWORD=xxx              │  ← lưu ở đây, an toàn     │
│  │  MINIO_ROOT_PASSWORD=xxx        │                            │
│  └──────────────┬──────────────────┘                            │
│                 │ decrypt & trả về JSON                         │
│                 ▼                                               │
│  Bước 2: python3 parse JSON → KEY=VALUE                         │
│                 │                                               │
│                 ▼                                               │
│  Bước 3: source <(...) → load vào RAM (shell env)               │
│                 │                                               │
│                 │  KHÔNG ghi ra file .env                       │
│                 │  Chỉ tồn tại trong RAM                        │
│                 ▼                                               │
│  Bước 4: docker compose up                                      │
│          → đọc env từ shell (RAM)                               │
│          → containers chạy với đúng config                      │
│                                                                  │
│  Bước 5: rm -f .env  (xóa file cũ nếu còn)                     │
└─────────────────────────────────────────────────────────────────┘


TRƯỚC (cách cũ):              SAU (cách mới):
─────────────────             ─────────────────
Secrets Manager               Secrets Manager
      │                             │
      ▼                             ▼
  file .env  ← trên disk        RAM (shell env)  ← không có file
      │                             │
      ▼                             ▼
docker compose                docker compose
```

---

## Thay đổi đã áp dụng vào deploy.yml

File `.github/workflows/deploy.yml` — step "SSH & restart containers":

**Trước:**
```yaml
script: |
  cd ~/notesnook

  aws secretsmanager get-secret-value \
    --region ap-southeast-1 \
    --secret-id notesnook/prod \
    --query SecretString \
    --output text | python3 -c "
  import sys, json
  secrets = json.load(sys.stdin)
  with open('.env', 'w') as f:
      for k, v in secrets.items():
          f.write(f'{k}={v}\n')
  "

  docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
  docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --remove-orphans
  docker image prune -f
```

**Sau:**
```yaml
script: |
  cd ~/notesnook

  set -a
  source <(aws secretsmanager get-secret-value \
    --region ap-southeast-1 \
    --secret-id notesnook/prod \
    --query SecretString \
    --output text | python3 -c "
  import sys, json
  secrets = json.load(sys.stdin)
  for k, v in secrets.items():
      print(f'{k}={v}')
  ")
  set +a

  rm -f .env

  docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
  docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --remove-orphans
  docker image prune -f
```

---

## Kiểm tra sau khi deploy

SSH vào EC2 và chạy:

```bash
ls -la ~/notesnook/.env
```

Kết quả mong đợi:
```
ls: cannot access '/home/ubuntu/notesnook/.env': No such file or directory
```

Nếu không có file `.env` → thành công.
