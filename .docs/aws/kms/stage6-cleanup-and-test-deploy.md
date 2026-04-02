# Stage 6 — Dọn dẹp và Test Deploy

## Mục tiêu
Xóa `.env` cũ trên EC2 và xác nhận pipeline deploy hoàn chỉnh hoạt động.

## Bước 6a — Xóa .env cũ trên EC2

SSH vào EC2:

```bash
ssh -i hieu-phanchi.pem ubuntu@<EC2_IP>
cd ~/notesnook

# Backup trước khi xóa (phòng hờ)
cp .env .env.backup-$(date +%Y%m%d)

# Xóa .env cũ
rm .env
```

## Bước 6b — Xóa .env khỏi repo (nếu đang track)

Trên máy local:

```bash
# Kiểm tra xem .env có đang được git track không
git ls-files .env

# Nếu có, xóa khỏi git (giữ file trên disk)
git rm --cached .env

# Đảm bảo .env trong .gitignore
echo ".env" >> .gitignore
echo ".env.local" >> .gitignore

git add .gitignore
git commit -m "chore: remove .env from git tracking, use Secrets Manager"
```

## Bước 6c — Push và kiểm tra pipeline

```bash
git push origin main
```

Vào GitHub → **Actions** → theo dõi workflow chạy.

Kiểm tra log của step **"SSH & restart containers"**, phải thấy:
```
✓ .env created from Secrets Manager
```

## Bước 6d — Xác nhận containers chạy đúng

SSH vào EC2:

```bash
cd ~/notesnook
docker compose -f docker-compose.yml -f docker-compose.prod.yml ps
```

Tất cả services phải ở trạng thái `Up`.

## Bước 6e — Xóa backup sau khi xác nhận ổn

```bash
rm ~/notesnook/.env.backup-*
```

---

## Tóm tắt luồng sau khi hoàn thành

```
git push to main
    │
    ▼
GitHub Actions: Build & push Docker images lên Docker Hub
    │
    ▼
GitHub Actions: SSH vào EC2
    │
    ▼
EC2 (IAM Role) → gọi AWS Secrets Manager → fetch notesnook/prod
    │
    ▼
Tạo .env mới từ secrets
    │
    ▼
docker compose pull + up
    │
    ▼
Containers chạy với config mới nhất
```

---

## Khi cần cập nhật config

Không cần push code, chỉ cần:
1. Vào AWS Secrets Manager → chọn `notesnook/prod` → **Edit secret value**
2. Cập nhật giá trị
3. Push một commit nhỏ (hoặc trigger deploy thủ công) để áp dụng
