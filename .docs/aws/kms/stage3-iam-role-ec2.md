# Stage 3 — Tạo IAM Role và gắn vào EC2

## Mục tiêu
Cho phép EC2 instance tự gọi Secrets Manager mà không cần AWS access key.

---

## Bước 3a — Tạo IAM Role

1. IAM → **Roles** → **Create role**
2. **Trusted entity type**: AWS service
3. **Use case**: EC2 → **Next**
4. Tìm và chọn `NotesnookSecretsReadPolicy` → **Next**
5. Role name: `NotesnookEC2Role` → **Create role**

---

## Bước 3b — Gắn Role vào EC2 instance

1. EC2 Console → **Instances** → chọn instance đang chạy notesnook
2. **Actions** → **Security** → **Modify IAM role**
3. Chọn `NotesnookEC2Role` → **Update IAM role**

---

## Kiểm tra

SSH vào EC2 và chạy lệnh sau (không cần configure AWS credentials):

```bash
aws sts get-caller-identity
```

Kết quả mong đợi:
```json
{
    "UserId": "AROAXXXXXXXXXXXXXXXXX:i-xxxxxxxxxxxxxxxxx",
    "Account": "123456789012",
    "Arn": "arn:aws:sts::123456789012:assumed-role/NotesnookEC2Role/i-xxxxxxxxxxxxxxxxx"
}
```

Test lấy secret:
```bash
aws secretsmanager get-secret-value \
  --region ap-southeast-1 \
  --secret-id notesnook/prod \
  --query SecretString \
  --output text
```

---

## Lưu ý
- EC2 instance chỉ có thể đọc secret, không sửa/xóa được
- Không cần lưu AWS access key trên EC2 hay trong GitHub Secrets
- Nếu EC2 chưa có AWS CLI (package `awscli` không có trong apt), cài AWS CLI v2 từ Amazon:
  ```bash
  sudo apt install unzip -y
  curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
  unzip awscliv2.zip
  sudo ./aws/install
  aws --version
  ```
