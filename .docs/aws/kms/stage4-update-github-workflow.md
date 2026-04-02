# Stage 4 — Cập nhật GitHub Actions Workflow

## Mục tiêu
Thay vì dùng `.env` có sẵn trên EC2, workflow sẽ tự fetch secret từ Secrets Manager và tạo `.env` mới mỗi lần deploy.

## File cần sửa
`.github/workflows/deploy.yml`

## Thay đổi

Tìm step **"SSH & restart containers"** và thay bằng nội dung sau:

```yaml
      - name: SSH & restart containers
        uses: appleboy/ssh-action@v1.0.3
        with:
          host: ${{ secrets.EC2_HOST }}
          username: ${{ secrets.EC2_USERNAME }}
          key: ${{ secrets.EC2_SSH_KEY }}
          script: |
            cd ~/notesnook

            # Fetch secrets từ AWS Secrets Manager và tạo .env
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
            print('✓ .env created from Secrets Manager')
            "

            docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
            docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --remove-orphans
            docker image prune -f
```

## Lưu ý
- Thay `ap-southeast-1` bằng AWS region bạn đang dùng
- EC2 dùng IAM Role để xác thực với Secrets Manager (không cần AWS keys trong GitHub Secrets)
- File `.env` được tạo mới mỗi lần deploy → luôn đồng bộ với giá trị mới nhất trong Secrets Manager

## GitHub Secrets cần có (không thay đổi so với trước)

| Secret | Mô tả |
|---|---|
| `EC2_HOST` | IP hoặc domain EC2 |
| `EC2_USERNAME` | SSH username (thường là `ubuntu` hoặc `ec2-user`) |
| `EC2_SSH_KEY` | Private key SSH |
| `DOCKER_USERNAME` | Docker Hub username |
| `DOCKER_PASSWORD` | Docker Hub password |

Không cần thêm `AWS_ACCESS_KEY_ID` hay `AWS_SECRET_ACCESS_KEY` vì EC2 dùng IAM Role.
