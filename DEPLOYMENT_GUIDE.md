# XIGUASecurity 反馈频道集成说明

## 概述

已成功将TCP反馈频道服务器集成到XIGUASecurity杀毒软件中，解决了原有WebSocket实现的4KB传输限制问题，并实现了完整的用户名系统、消息去重和已读/未读状态跟踪功能。

## 部署步骤

### 1. 服务器端部署

1. 将以下文件上传到服务器：
   - `feedback_tcp_server.py` - TCP服务器主程序
   - `feedback_server.sh` - 启动脚本（Linux）或创建Windows批处理脚本

2. 在服务器上运行：
   ```bash
   # 安装Python依赖
   pip install asyncio
   
   # 启动服务器
   python3 feedback_tcp_server.py
   ```

3. 或者使用启动脚本（Linux）：
   ```bash
   chmod +x feedback_server.sh
   ./feedback_server.sh start
   ```

### 2. 客户端配置

1. 在XIGUASecurity中，反馈频道已自动配置为使用TCP协议
2. 默认服务器地址：localhost:8765
3. 首次使用时，需要设置用户名

### 3. 修改服务器地址

如果需要连接到不同的服务器：

1. 打开XIGUASecurity
2. 导航到反馈频道页面
3. 点击"设置"按钮
4. 修改服务器地址和端口
5. 点击"保存"

## 功能特性

### 1. 用户名系统
- 自定义用户名，本地存储
- 用户头像自动生成
- 防止同一用户多处登录

### 2. 消息系统
- 消息去重，避免重复发送
- 实时广播所有在线用户
- 消息持久化存储
- 支持文件传输（最大20KB）

### 3. 已读/未读状态
- 每条消息显示已读用户数
- 实时更新已读状态

### 4. 无传输限制
- 使用TCP Socket协议，无4KB限制
- 自定义消息分帧协议
- 支持大文件和长消息传输

## 文件说明

### 服务器端文件
- `feedback_tcp_server.py` - TCP服务器主程序
- `feedback_server.sh` - Linux启动脚本
- `README_FeedbackServer.md` - 详细文档

### 客户端文件
- `XIGUASecurity/FeedbackTCPClient.cs` - TCP客户端类
- `XIGUASecurity/BugReportPage.xaml.cs` - 反馈页面UI实现
- `XIGUASecurity/BugReportPage.xaml` - 反馈页面UI定义

## 故障排除

### 1. 连接问题
- 检查服务器是否正常运行
- 确认防火墙设置
- 验证网络连接

### 2. 消息丢失
- 检查服务器日志
- 确认消息格式正确
- 验证网络稳定性

### 3. 性能问题
- 监控服务器资源使用
- 优化消息处理逻辑
- 考虑负载均衡

## 安全注意事项

1. **服务器部署**：
   - 建议使用防火墙限制访问端口
   - 考虑使用SSL/TLS加密（可扩展实现）

2. **客户端验证**：
   - 服务器不验证用户名唯一性（可扩展实现）
   - 建议添加用户认证机制（可扩展实现）

## 扩展功能

1. **文件传输**：
   - 可扩展支持更大文件传输
   - 实现文件分块传输

2. **私聊功能**：
   - 可扩展实现一对一私聊
   - 添加群组功能

3. **消息加密**：
   - 可添加端到端加密
   - 实现消息签名验证

## 维护说明

1. **数据备份**：
   - 定期备份`feedback_data/`目录
   - 包含`messages.json`和`users.json`

2. **日志监控**：
   - 监控`feedback_server.log`
   - 定期清理旧日志

3. **性能监控**：
   - 监控服务器资源使用
   - 根据需要调整配置