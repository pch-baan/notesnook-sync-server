```
echo "=== CPU ===" && top -bn1 | grep "Cpu(s)" && echo "=== RAM ===" && free -h && echo "=== DISK ===" && df -h
```