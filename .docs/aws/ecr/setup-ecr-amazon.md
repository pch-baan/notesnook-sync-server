# Hướng dẫn Setup AWS ECR cho Notesnook Sync Server

Project này có 3 custom Docker images cần push lên ECR:
- `identity-server` (Streetwriters.Identity)
- `notesnook-sync` (Notesnook.API)
- `sse-server` (Streetwriters.Messenger)

---

## Bước 1: Cài đặt AWS CLI

```bash
# Windows (dùng installer)
# Tải tại: https://awscli.amazonaws.com/AWSCLIV2.msi

# Kiểm tra đã cài thành công
aws --version
```

---

## Bước 2: Cấu hình AWS credentials

```bash
aws configure
```

Nhập lần lượt:
```
AWS Access Key ID:     [lấy từ IAM → Users → Security credentials]
AWS Secret Access Key: [lấy từ IAM → Users → Security credentials]
Default region name:   ap-southeast-1        (Singapore, gần VN nhất)
Default output format: json
```

---

## Bước 3: Tạo ECR Repositories

```bash
# Tạo repo cho từng service
aws ecr create-repository --repository-name notesnook/identity     --region ap-southeast-1
aws ecr create-repository --repository-name notesnook/notesnook-sync --region ap-southeast-1
aws ecr create-repository --repository-name notesnook/sse           --region ap-southeast-1
```

Sau khi tạo, lệnh sẽ trả về `repositoryUri` dạng:
```
123456789012.dkr.ecr.ap-southeast-1.amazonaws.com/notesnook/identity
```

> Lưu lại Account ID (12 số) để dùng ở các bước sau.

---

## Bước 4: Đăng nhập Docker vào ECR

```bash
aws ecr get-login-password --region ap-southeast-1 | \
  docker login --username AWS --password-stdin \
  123456789012.dkr.ecr.ap-southeast-1.amazonaws.com
```

> Thay `123456789012` bằng AWS Account ID của bạn.

---

## Bước 5: Build Docker images

Chạy từ thư mục gốc `notesnook-sync-server/`:

```bash
# Build notesnook-sync (Notesnook.API)
docker build -f Notesnook.API/Dockerfile -t notesnook/notesnook-sync:latest .

# Build identity (Streetwriters.Identity)
docker build -f Streetwriters.Identity/Dockerfile -t notesnook/identity:latest .

# Build sse (Streetwriters.Messenger)
docker build -f Streetwriters.Messenger/Dockerfile -t notesnook/sse:latest .
```

---

## Bước 6: Tag và Push lên ECR

Thay `123456789012` bằng AWS Account ID của bạn:

```bash
ECR_HOST="123456789012.dkr.ecr.ap-southeast-1.amazonaws.com"

# notesnook-sync
docker tag notesnook/notesnook-sync:latest $ECR_HOST/notesnook/notesnook-sync:latest
docker push $ECR_HOST/notesnook/notesnook-sync:latest

# identity
docker tag notesnook/identity:latest $ECR_HOST/notesnook/identity:latest
docker push $ECR_HOST/notesnook/identity:latest

# sse
docker tag notesnook/sse:latest $ECR_HOST/notesnook/sse:latest
docker push $ECR_HOST/notesnook/sse:latest
```

---

## Bước 7: Tạo docker-compose.ecr.yml

Tạo file override để dùng ECR images thay vì Docker Hub:

```yaml
# docker-compose.ecr.yml
services:
  identity-server:
    image: 123456789012.dkr.ecr.ap-southeast-1.amazonaws.com/notesnook/identity:latest

  notesnook-server:
    image: 123456789012.dkr.ecr.ap-southeast-1.amazonaws.com/notesnook/notesnook-sync:latest

  sse-server:
    image: 123456789012.dkr.ecr.ap-southeast-1.amazonaws.com/notesnook/sse:latest
```

Chạy với ECR images:
```bash
docker compose -f docker-compose.yml -f docker-compose.ecr.yml up -d
```

---

## Bước 8: Kiểm tra

```bash
# Xem danh sách images trong ECR repo
aws ecr list-images --repository-name notesnook/notesnook-sync --region ap-southeast-1

# Xem tất cả repos đã tạo
aws ecr describe-repositories --region ap-southeast-1
```

---

## Lưu ý quan trọng

| Vấn đề | Giải pháp |
|---|---|
| Token hết hạn (12h) | Chạy lại lệnh `aws ecr get-login-password` ở Bước 4 |
| Image quá lớn | Dùng multi-stage build (Dockerfile hiện tại đã tối ưu) |
| Chi phí | ECR tính phí ~$0.10/GB/tháng lưu trữ + $0.09/GB transfer |
| Scan lỗ hổng | Bật ECR image scanning: `aws ecr put-image-scanning-configuration --repository-name notesnook/notesnook-sync --image-scanning-configuration scanOnPush=true` |

---

## Tham khảo nhanh

```bash
# Login ECR (làm mỗi 12h hoặc khi bị lỗi auth)
aws ecr get-login-password --region ap-southeast-1 | docker login --username AWS --password-stdin 123456789012.dkr.ecr.ap-southeast-1.amazonaws.com

# Push image mới nhất
docker build -f Notesnook.API/Dockerfile -t 123456789012.dkr.ecr.ap-southeast-1.amazonaws.com/notesnook/notesnook-sync:latest . && \
docker push 123456789012.dkr.ecr.ap-southeast-1.amazonaws.com/notesnook/notesnook-sync:latest
```
