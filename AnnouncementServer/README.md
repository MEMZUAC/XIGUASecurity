# 公告管理系统

这是一个简单的公告管理系统，用于为XIGUASecurity应用程序提供公告功能。

## 功能特点

- 支持富文本编辑（使用CKEditor）
- 支持多图片上传
- 支持标记重要公告
- RESTful API接口
- 响应式Web管理界面

## 安装与运行

### 1. 安装依赖

```bash
pip install -r requirements.txt
```

### 2. 运行服务器

```bash
python app.py
```

服务器将在 `http://localhost:8080` 启动。

### 3. 访问管理界面

打开浏览器，访问 `http://localhost:8080/admin` 进行公告管理。

## API接口

### 获取所有公告

```
GET /api/announcements
```

### 获取最新公告

```
GET /api/announcements/latest
```

### 获取特定公告

```
GET /api/announcements/{id}
```

### 创建公告

```
POST /api/announcements
Content-Type: multipart/form-data

参数:
- title: 公告标题
- content: 公告内容
- is_important: 是否为重要公告 (true/false)
- images: 图片文件 (可选，支持多张)
```

### 更新公告

```
PUT /api/announcements/{id}
Content-Type: multipart/form-data

参数:
- title: 公告标题
- content: 公告内容
- is_important: 是否为重要公告 (true/false)
- images: 新增的图片文件 (可选，支持多张)
```

### 删除公告

```
DELETE /api/announcements/{id}
```

## 客户端配置

在XIGUASecurity客户端中，确保`AnnouncementService.cs`中的服务器地址与实际运行地址一致：

```csharp
private const string DEFAULT_SERVER_URL = "http://localhost:8080/api/announcements";
```

## 注意事项

- 上传的图片存储在 `uploads` 目录中
- 公告数据保存在 `announcements.json` 文件中
- 支持的图片格式：PNG, JPG, JPEG, GIF, WebP
- 单个文件最大大小：16MB