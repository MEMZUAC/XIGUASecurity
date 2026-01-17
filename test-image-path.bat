@echo off
echo 测试公告API...
curl -X GET http://localhost:8080/api/announcements/latest

echo.
echo 如果上面的JSON中images字段包含完整URL（如http://localhost:8080/uploads/xxx.jpg），则图片路径正确
echo 如果是相对路径（如/uploads/xxx.jpg），则需要修改服务端代码

pause