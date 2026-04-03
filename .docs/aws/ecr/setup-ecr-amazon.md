# Hướng dẫn Setup AWS ECR cho Notesnook Sync Server

Project này có 3 custom Docker images cần push lên ECR:
- `identity-server` (Streetwriters.Identity)
- `notesnook-sync` (Notesnook.API)
- `sse-server` (Streetwriters.Messenger)

---

## Bước 1: Tạo ECR Repositories (AWS Console) ✅

1. Vào **AWS Console** → tìm **ECR** (Elastic Container Registry)
2. Chọn **Repositories** → **Create repository**
3. Tạo lần lượt 3 repos:
   - `notesnook/notesnook-sync`
   - `notesnook/identity`
   - `notesnook/sse`
4. **Visibility:** Private, còn lại để mặc định → **Create**

Sau khi tạo, copy URI của từng repo (dạng `123456789012.dkr.ecr.ap-southeast-1.amazonaws.com/notesnook/...`).

---

## Bước 2: Tạo IAM User cho GitHub Actions

### 2.1 Tạo User

1. Vào **AWS Console** → tìm **IAM** → chọn **IAM**
2. Menu trái → **Users** → **Create user**
3. **User name:** `github-actions-ecr`
4. Click **Next**
5. Chọn **Attach policies directly** → tìm và tick:
   - `AmazonEC2ContainerRegistryPowerUser`
6. Click **Next** → **Create user**

### 2.2 Tạo Access Key

1. Trong danh sách Users → click vào `github-actions-ecr`
2. Chọn tab **Security credentials**
3. Kéo xuống phần **Access keys** → click **Create access key**
4. Chọn use case: **"Application running outside AWS"** → click **Next**
5. Description tag: gõ `github-actions` (tùy chọn) → click **Create access key**
6. Màn hình hiện:

```
Access key ID:     AKIA...............
Secret access key: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

> ⚠️ **Secret access key chỉ hiển thị 1 lần duy nhất.** Click **Download .csv** để lưu ngay, hoặc copy vào nơi an toàn trước khi nhấn Done.

7. Click **Done**

---

## Bước 3: Thêm GitHub Secrets

Vào repo GitHub → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**

Thêm lần lượt các secret sau:

| Secret name | Giá trị |
|---|---|
| `AWS_ACCESS_KEY_ID` | Access key ID vừa tạo |
| `AWS_SECRET_ACCESS_KEY` | Secret access key vừa tạo |
| `AWS_ACCOUNT_ID` | 12 chữ số đầu trong ECR URI |
| `AWS_REGION` | `ap-southeast-1` |

> Các secret `EC2_HOST`, `EC2_USERNAME`, `EC2_SSH_KEY` (file PEM) giữ nguyên như cũ.

---

## Bước 4: Cập nhật GitHub Actions Workflow

Cập nhật file `.github/workflows/deploy.yml` — thay Docker Hub bằng ECR:

```yaml
name: Build & Deploy to EC2

on:
  push:
    branches: [main]

jobs:
  build-and-push:
    name: Build & Push images to ECR
    runs-on: ubuntu-latest
    strategy:
      matrix:
        include:
          - image: notesnook/notesnook-sync
            file: ./Notesnook.API/Dockerfile
            context: .

          - image: notesnook/identity
            file: ./Streetwriters.Identity/Dockerfile
            context: .

          - image: notesnook/sse
            file: ./Streetwriters.Messenger/Dockerfile
            context: .

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ secrets.AWS_REGION }}

      - name: Login to Amazon ECR
        id: login-ecr
        uses: aws-actions/amazon-ecr-login@v2

      - name: Build and push ${{ matrix.image }}
        uses: docker/build-push-action@v6
        with:
          context: ${{ matrix.context }}
          file: ${{ matrix.file }}
          push: true
          tags: ${{ secrets.AWS_ACCOUNT_ID }}.dkr.ecr.${{ secrets.AWS_REGION }}.amazonaws.com/${{ matrix.image }}:latest
          cache-from: type=gha
          cache-to: type=gha,mode=max

  deploy:
    name: Deploy to EC2
    runs-on: ubuntu-latest
    needs: build-and-push

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Copy compose files to EC2
        uses: appleboy/scp-action@v0.1.7
        with:
          host: ${{ secrets.EC2_HOST }}
          username: ${{ secrets.EC2_USERNAME }}
          key: ${{ secrets.EC2_SSH_KEY }}
          source: "docker-compose.yml,docker-compose.prod.yml"
          target: "~/notesnook"

      - name: SSH & restart containers
        uses: appleboy/ssh-action@v1.0.3
        with:
          host: ${{ secrets.EC2_HOST }}
          username: ${{ secrets.EC2_USERNAME }}
          key: ${{ secrets.EC2_SSH_KEY }}
          script: |
            cd ~/notesnook

            # Login ECR trên EC2
            aws ecr get-login-password --region ${{ secrets.AWS_REGION }} | \
              docker login --username AWS --password-stdin \
              ${{ secrets.AWS_ACCOUNT_ID }}.dkr.ecr.${{ secrets.AWS_REGION }}.amazonaws.com

            # Fetch secrets từ AWS Secrets Manager
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

## Bước 5: Cập nhật docker-compose.prod.yml

Thay `123456789012` bằng AWS Account ID của bạn:

```yaml
# docker-compose.prod.yml
services:
  identity-server:
    image: 123456789012.dkr.ecr.ap-southeast-1.amazonaws.com/notesnook/identity:latest

  notesnook-server:
    image: 123456789012.dkr.ecr.ap-southeast-1.amazonaws.com/notesnook/notesnook-sync:latest

  sse-server:
    image: 123456789012.dkr.ecr.ap-southeast-1.amazonaws.com/notesnook/sse:latest
```

---

## Bước 6: Cấp quyền cho EC2 pull ECR

EC2 cần có IAM Role với quyền pull images từ ECR.

### 6.1 Tạo IAM Role cho EC2

1. Vào **IAM** → **Roles** → **Create role**
2. **Trusted entity type:** AWS service → **EC2**
3. Click **Next** → tìm và tick policy:
   - `AmazonEC2ContainerRegistryReadOnly`
4. **Role name:** `ec2-ecr-pull-role` → **Create role**

### 6.2 Gắn Role vào EC2 instance

1. Vào **EC2** → chọn instance đang chạy
2. **Actions** → **Security** → **Modify IAM role**
3. Chọn `ec2-ecr-pull-role` → **Update IAM role**

> Sau khi gắn role, EC2 sẽ tự động có quyền pull từ ECR mà không cần cấu hình credentials thủ công.

---

## Bước 7: Kiểm tra

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
| Token ECR hết hạn (12h) | Chạy lại `aws ecr get-login-password` |
| EC2 không pull được image | Kiểm tra IAM Role đã gắn vào EC2 chưa (Bước 6) |
| GitHub Actions lỗi auth | Kiểm tra secrets `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` đúng chưa |
| Image quá lớn | Dùng multi-stage build (Dockerfile hiện tại đã tối ưu) |
| Chi phí | ECR tính phí ~$0.10/GB/tháng lưu trữ + $0.09/GB transfer |
| Scan lỗ hổng | Bật ECR image scanning: `aws ecr put-image-scanning-configuration --repository-name notesnook/notesnook-sync --image-scanning-configuration scanOnPush=true` |
