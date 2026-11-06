#!/bin/bash
echo "===== processes ====="
ps -ef | grep -E "FundAnalysis|dotnet" | grep -v grep
echo "===== web files ====="
ls -la /www/wwwroot/fund/web 2>&1 | head -20
echo "===== api files ====="
ls -la /www/wwwroot/fund/api 2>&1 | head -20
echo "===== http root ====="
curl -s -o /tmp/vout.html -w "HTTP_%{http_code}\n" http://127.0.0.1:7789/
head -5 /tmp/vout.html
echo "===== http api ====="
curl -s -o /dev/null -w "HTTP_%{http_code}\n" http://127.0.0.1:7789/api/Quote/indices 2>/dev/null || true
echo "===== done ====="
