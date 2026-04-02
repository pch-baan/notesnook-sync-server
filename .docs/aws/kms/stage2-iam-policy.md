# Stage 2 — Tạo IAM Policy

## Mục tiêu
Tạo policy giới hạn quyền chỉ được đọc secret `notesnook/prod`.

## Các bước

1. Vào AWS Console → **IAM** → **Policies** → **Create policy**
2. Chọn tab **JSON**
3. Dán policy JSON bên dưới vào
4. Click **Next** → đặt tên `NotesnookSecretsReadPolicy` → **Create policy**

## Policy JSON

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": "secretsmanager:GetSecretValue",
      "Resource": "arn:aws:secretsmanager:*:*:secret:notesnook/prod*"
    }
  ]
}
```

## Giải thích

- `secretsmanager:GetSecretValue` — chỉ cho phép đọc, không cho sửa/xóa
- `Resource` — chỉ áp dụng cho secret có tên bắt đầu bằng `notesnook/prod`
- `*` ở region và account ID để dùng được mọi region (có thể giới hạn hơn nếu muốn)

## Tùy chọn: giới hạn theo region và account

Thay `*:*` bằng region và account ID cụ thể để bảo mật hơn:

```json
"Resource": "arn:aws:secretsmanager:ap-southeast-1:123456789012:secret:notesnook/prod*"
```
