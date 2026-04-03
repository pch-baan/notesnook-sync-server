```
 ┌─────────────────────────────────────────────────────────────────┐
  │                        GIT PUSH to main                         │
  └─────────────────────────┬───────────────────────────────────────┘
                            │
                            ▼
  ┌─────────────────────────────────────────────────────────────────┐
  │                    GITHUB ACTIONS                                │
  │                                                                  │
  │  1. Configure AWS credentials                                    │
  │  2. Login Amazon ECR                                             │
  │  3. Build & push Docker image lên ECR                           │
  │  4. SCP compose files lên EC2                                    │
  │  5. SSH vào EC2                                                  │
  └─────────────────────────┬───────────────────────────────────────┘
                            │ SSH
                            ▼
  ┌─────────────────────────────────────────────────────────────────┐
  │                       EC2 (Ubuntu)                               │
  │                                                                  │
  │  Bước 1: Login ECR                                              │
  │          (dùng IAM Role — không cần password)                    │
  │                                                                  │
  │  Bước 2: EC2 gọi AWS Secrets Manager                            │
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
  │  Bước 3: python3 parse JSON → KEY=VALUE                         │
  │                 │                                               │
  │                 ▼                                               │
  │  Bước 4: eval $(...) + set -a → load vào RAM (shell env)        │
  │                 │                                               │
  │                 │  KHÔNG ghi ra file .env                       │
  │                 │  Chỉ tồn tại trong RAM                        │
  │                 ▼                                               │
  │  Bước 5: docker compose pull  ← pull image từ ECR              │
  │                 │                                               │
  │                 ▼                                               │
  │  Bước 6: docker compose up                                      │
  │          → docker-compose.yml dùng ${VAR}                       │
  │          → đọc env từ shell RAM                                 │
  │          → containers chạy với đúng config                      │
  │                                                                  │
  │  Bước 7: docker image prune -f                                  │
  └─────────────────────────────────────────────────────────────────┘


  TRƯỚC (cách cũ):              SAU (cách mới):
  ─────────────────             ─────────────────
  Docker Hub                    Amazon ECR
        │                             │
        ▼                             ▼
  Secrets Manager               Secrets Manager
        │                             │
        ▼                             ▼
    file .env  ← trên disk        RAM (shell env)  ← không có file
        │                             │
        ▼                             ▼
  docker compose                docker compose
  (env_file: .env)              (environment: ${VAR})
```