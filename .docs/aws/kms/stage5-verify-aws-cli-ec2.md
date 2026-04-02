# Stage 5 — Kiểm tra AWS CLI trên EC2

## Mục tiêu
Đảm bảo EC2 có AWS CLI và IAM Role hoạt động đúng trước khi chạy deploy.

## SSH vào EC2

```bash
ssh -i hieu-phanchi.pem ubuntu@<EC2_IP>
```

## Kiểm tra AWS CLI

```bash
aws --version
```

Nếu chưa có, cài đặt:

```bash
sudo apt update && sudo apt install -y awscli
```

Hoặc cài bản mới nhất (v2):

```bash
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install
aws --version
```

## Kiểm tra IAM Role

```bash
aws sts get-caller-identity
```

Kết quả mong đợi có `NotesnookEC2Role` trong Arn:
```json
{
    "UserId": "AROAXXXXXXXXXXXXXXXXX:i-xxxxxxxxxxxxxxxxx",
    "Account": "123456789012",
    "Arn": "arn:aws:sts::123456789012:assumed-role/NotesnookEC2Role/i-xxxxxxxxxxxxxxxxx"
}
```

## Test fetch secret

```bash
aws secretsmanager get-secret-value \
  --region ap-southeast-1 \
  --secret-id notesnook/prod \
  --query SecretString \
  --output text
```

Kết quả mong đợi: JSON chứa tất cả key-value đã lưu ở Stage 1.

## Test tạo .env

```bash
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
print('✓ .env created')
"

cat .env
```

## Troubleshooting

| Lỗi | Nguyên nhân | Cách fix |
|---|---|---|
| `Unable to locate credentials` | IAM Role chưa gắn vào EC2 | Làm lại Stage 3b |
| `AccessDeniedException` | Policy thiếu quyền | Kiểm tra lại Stage 2 |
| `ResourceNotFoundException` | Tên secret sai | Kiểm tra lại `notesnook/prod` ở Stage 1 |
| `aws: command not found` | Chưa cài AWS CLI | Cài đặt theo hướng dẫn trên |
