@echo off
echo 启动公告服务器...
cd /d "c:\Users\MEMZ-UAC\OneDrive\Desktop\Xdows-Security-master\AnnouncementServer"
start python app.py

echo 等待服务器启动...
timeout /t 3

echo 测试API...
curl -X GET http://localhost:8080/api/announcements/latest

pause