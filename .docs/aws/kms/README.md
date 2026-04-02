# AWS Secrets Manager Setup (thay thế .env trên EC2)

## Tổng quan

Thay vì lưu file `.env` trực tiếp trên EC2, toàn bộ secrets được lưu trong **AWS Secrets Manager** (encrypted bằng KMS). Mỗi lần deploy, GitHub Actions SSH vào EC2 và tự động fetch secrets để tạo `.env` mới.

## Kiến trúc

```
git push to main
    → GitHub Actions
    → SSH vào EC2
    → EC2 (IAM Role) gọi AWS Secrets Manager
    → Tạo .env từ secrets
    → docker compose up
```

## Các stage

| Stage | File | Mô tả |
|---|---|---|
| 1 | [stage1-create-secret.md](stage1-create-secret.md) | Tạo secret `notesnook/prod` trong Secrets Manager |
| 2 | [stage2-iam-policy.md](stage2-iam-policy.md) | Tạo IAM Policy giới hạn quyền đọc secret |
| 3 | [stage3-iam-role-ec2.md](stage3-iam-role-ec2.md) | Tạo IAM Role và gắn vào EC2 instance |
| 4 | [stage4-update-github-workflow.md](stage4-update-github-workflow.md) | Cập nhật workflow deploy |
| 5 | [stage5-verify-aws-cli-ec2.md](stage5-verify-aws-cli-ec2.md) | Kiểm tra AWS CLI và IAM Role trên EC2 |
| 6 | [stage6-cleanup-and-test-deploy.md](stage6-cleanup-and-test-deploy.md) | Dọn dẹp .env cũ và test toàn bộ pipeline |

## Lợi ích

- Không có secrets trong repo hay trên disk EC2 lâu dài
- Cập nhật config không cần redeploy (chỉ cần trigger deploy lại)
- Audit log đầy đủ qua CloudTrail
- Encrypted bằng KMS tự động

## Lưu ý quan trọng

- Xóa credentials thật ra khỏi `.env.example` trong repo
- Secret name phải là `notesnook/prod` để khớp với script
- Thay `ap-southeast-1` đúng với AWS region của bạn
