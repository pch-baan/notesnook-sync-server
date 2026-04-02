# AWS ECR (Elastic Container Registry)

## ECR là gì?

AWS ECR là dịch vụ **registry (kho lưu trữ) container image** được quản lý hoàn toàn bởi Amazon Web Services.

---

## Mục đích chính

- **Lưu trữ Docker images** (và OCI images) trên cloud một cách an toàn, private
- **Tích hợp với AWS ecosystem**: ECS, EKS, Lambda, CodePipeline dễ dàng pull image từ ECR
- **Thay thế cho Docker Hub** trong môi trường doanh nghiệp/production

---

## Cách hoạt động cơ bản

```
Build Docker image
      ↓
docker tag my-app:latest 123456789.dkr.ecr.ap-southeast-1.amazonaws.com/my-app:latest
      ↓
docker push → ECR repository
      ↓
ECS / EKS pull image từ ECR để chạy container
```

---

## So sánh ECR vs Docker Hub

| Tiêu chí | AWS ECR | Docker Hub |
|---|---|---|
| Bảo mật | IAM-based, private mặc định | Public mặc định |
| Tích hợp AWS | Tốt nhất | Cần cấu hình thêm |
| Giá | Trả theo dung lượng | Free tier giới hạn pull |
| Scan lỗ hổng | Có sẵn (ECR image scanning) | Chỉ có ở plan trả phí |

---

## Tóm lại

> ECR = **Docker Hub** nhưng **private, bảo mật, tích hợp sẵn với AWS** — dùng khi deploy ứng dụng container lên AWS.
