# Check & Kill Processes — Quick Reference

## Check ports đang dùng

```bash
# Notesnook.API
lsof -i :5264

# Identity Server
lsof -i :8264

# MongoDB
lsof -i :27017

# Redis
lsof -i :6379

# MinIO (S3)
lsof -i :9000

# ngrok
lsof -i :4040

# Check bất kỳ port nào
lsof -i :<PORT>
```

## Check processes

```bash
# dotnet services
ps aux | grep "dotnet.*Notesnook\|dotnet.*Identity" | grep -v grep

# ngrok
ps aux | grep "[n]grok"

# Docker containers
docker ps --format "table {{.Names}}\t{{.Ports}}"
```

## Kill processes

```bash
# Kill Notesnook.API
pkill -f "dotnet.*Notesnook"

# Kill Identity Server
pkill -f "dotnet.*Identity"

# Kill ngrok
pkill ngrok

# Kill process by PID
kill <PID>

# Kill process on specific port
kill $(lsof -t -i :<PORT>)

# Stop all Docker containers
docker compose down

# Stop specific Docker container
docker stop <container_name>
```

## Check tất cả cùng lúc

```bash
echo "=== Port 5264 (API) ===" && lsof -i :5264 2>/dev/null || echo "Free"
echo "=== Port 8264 (Identity) ===" && lsof -i :8264 2>/dev/null || echo "Free"
echo "=== Port 27017 (MongoDB) ===" && lsof -i :27017 2>/dev/null || echo "Free"
echo "=== Port 6379 (Redis) ===" && lsof -i :6379 2>/dev/null || echo "Free"
echo "=== Port 9000 (MinIO) ===" && lsof -i :9000 2>/dev/null || echo "Free"
echo "=== Port 4040 (ngrok) ===" && lsof -i :4040 2>/dev/null || echo "Free"
echo "=== dotnet ===" && ps aux | grep "dotnet.*Notesnook\|dotnet.*Identity" | grep -v grep || echo "None"
echo "=== ngrok ===" && ps aux | grep "[n]grok" || echo "None"
echo "=== Docker ===" && docker ps --format "table {{.Names}}\t{{.Ports}}" 2>/dev/null || echo "Not running"
```

## Kill tất cả cùng lúc

```bash
pkill -f "dotnet.*Notesnook"
pkill -f "dotnet.*Identity"
pkill ngrok
docker compose down
```
