#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
XIGUASecurity 反馈频道 TCP Socket 服务器
实现用户名系统、消息去重、已读/未读状态跟踪等功能
避免4KB传输限制
"""

import asyncio
import socket
import json
import uuid
import time
from datetime import datetime
from typing import Dict, List, Set, Optional
import logging
import os
import hashlib
import random
import struct
import base64
import threading
from http.server import HTTPServer, SimpleHTTPRequestHandler

# 配置日志
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler("feedback_server.log", encoding='utf-8'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

class FileHTTPRequestHandler(SimpleHTTPRequestHandler):
    """自定义HTTP请求处理器，用于处理文件下载"""
    
    def __init__(self, *args, data_dir=None, **kwargs):
        self.data_dir = data_dir or "feedback_data"
        super().__init__(*args, **kwargs)
    
    def do_GET(self):
        """处理GET请求"""
        # 检查是否是文件下载请求
        if self.path.startswith('/download'):
            self.handle_file_download()
        elif self.path.startswith('/files/'):
            self.handle_file_download()
        else:
            super().do_GET()
    
    def do_POST(self):
        """处理POST请求"""
        # 文件上传功能已删除
        self.send_error(404, "Not Found")
        return
    
    def handle_file_upload(self):
        """处理文件上传请求"""
        import cgi
        import io
        
        logger.info(f"收到文件上传请求: {self.path}")
        logger.info(f"Content-Type: {self.headers.get('Content-Type', '')}")
        logger.info(f"Content-Length: {self.headers.get('Content-Length', '0')}")
        
        try:
            # 解析multipart/form-data
            content_type = self.headers.get('Content-Type', '')
            if not content_type.startswith('multipart/form-data'):
                logger.error(f"不支持的Content-Type: {content_type}")
                self.send_error(400, "Content-Type must be multipart/form-data")
                return
            
            # 读取请求体
            content_length = int(self.headers.get('Content-Length', 0))
            body = self.rfile.read(content_length)
            logger.info(f"接收到 {len(body)} 字节的数据")
            
            # 使用cgi模块解析multipart数据
            environ = {
                'REQUEST_METHOD': 'POST',
                'CONTENT_TYPE': content_type,
                'CONTENT_LENGTH': str(content_length)
            }
            
            # 创建FieldStorage对象来解析multipart数据
            form = cgi.FieldStorage(
                fp=io.BytesIO(body),
                environ=environ,
                keep_blank_values=True
            )
            
            # 提取文件和用户名
            file_name = None
            file_data = None
            username = None
            
            if 'file' in form:
                file_item = form['file']
                file_name = file_item.filename
                file_data = file_item.value
                logger.info(f"文件名: {file_name}, 大小: {len(file_data) if file_data else 0}")
            
            if 'username' in form:
                username = form['username'].value
                logger.info(f"用户名: {username}")
            
            if not file_name or not file_data:
                logger.error("文件上传缺少文件名或文件内容")
                self.send_error(400, "Missing file name or file content")
                return
            
            # 创建文件存储目录
            files_dir = os.path.join(self.data_dir, "files")
            os.makedirs(files_dir, exist_ok=True)
            
            # 保存文件到服务端
            timestamp = int(time.time())
            # 生成一个简单的文件名，避免中文和特殊字符
            file_extension = os.path.splitext(file_name)[1]  # 获取文件扩展名
            simple_file_name = f"file_{timestamp}{file_extension}"
            file_path = os.path.join(files_dir, simple_file_name)
            with open(file_path, 'wb') as f:
                f.write(file_data)
            
            logger.info(f"用户 {username or '匿名'} 上传了文件: {file_name} ({len(file_data)} bytes)")
            
            # 创建文件消息
            file_id = f"file_{username or 'anonymous'}_{timestamp}"
            file_message = {
                "type": "file",
                "id": file_id,
                "name": file_name,
                "size": len(file_data),
                "username": username or "匿名",
                "timestamp": datetime.now().isoformat()
            }
            
            # 保存文件消息到历史记录
            server = getattr(self, 'server', None)
            if server and hasattr(server, 'feedback_server'):
                feedback_server = server.feedback_server
                feedback_server.messages[file_id] = file_message
                feedback_server.save_data()
                
                # 广播文件消息给所有客户端
                _ = asyncio.create_task(feedback_server.broadcast_message(file_message))
            
            # 返回成功响应
            response = {
                "success": True,
                "file_id": file_id,
                "message": "文件上传成功"
            }
            
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(json.dumps(response).encode('utf-8'))
            
            logger.info(f"文件上传成功: {file_name} -> {simple_file_name}")
            
        except Exception as e:
            logger.error(f"处理文件上传时出错: {str(e)}")
            self.send_error(500, f"Internal server error: {str(e)}")
    
    def handle_file_download(self):
        """处理文件下载请求"""
        # 添加调试日志
        logger.info(f"收到文件下载请求: {self.path}")
        
        # 从URL中提取文件名
        file_name = None
        if self.path.startswith('/download'):
            # 解析查询参数
            from urllib.parse import urlparse, parse_qs
            parsed_url = urlparse(self.path)
            query_params = parse_qs(parsed_url.query)
            logger.info(f"解析查询参数: {query_params}")
            
            if 'file_name' in query_params:
                file_name = query_params['file_name'][0]
                logger.info(f"找到file_name参数: {file_name}")
            elif 'file_id' in query_params:
                # 兼容旧的file_id参数，尝试从file_id中提取文件名
                file_id = query_params['file_id'][0]
                logger.info(f"找到file_id参数: {file_id}")
                if file_id.startswith('file_'):
                    parts = file_id.split('_')
                    if len(parts) >= 3:
                        file_name = '_'.join(parts[2:])  # 跳过file_和username部分
        elif self.path.startswith('/files/'):
            file_name = self.path[7:]  # 去掉'/files/'前缀
        
        if not file_name:
            logger.error("未找到文件名参数")
            self.send_error(400, "Missing file_name parameter")
            return
        
        # 构建文件路径
        files_dir = os.path.join(self.data_dir, "files")
        file_path = os.path.join(files_dir, file_name)
        logger.info(f"尝试查找文件: {file_path}")
        
        # 检查文件是否存在
        if not os.path.exists(file_path) or not os.path.isfile(file_path):
            # 如果直接通过文件名找不到，尝试遍历目录查找匹配的文件
            logger.info(f"直接查找失败，尝试遍历目录查找")
            if os.path.exists(files_dir):
                logger.info(f"目录中的文件: {os.listdir(files_dir)}")
                for filename in os.listdir(files_dir):
                    # 尝试匹配简单文件名或原始文件名
                    if filename == file_name:
                        file_path = os.path.join(files_dir, filename)
                        file_name = filename
                        logger.info(f"找到匹配文件: {file_path}")
                        break
                    # 如果是简单文件名（如file_123456.ext），尝试匹配
                    elif filename.startswith("file_") and file_name.startswith("file_"):
                        if filename == file_name:
                            file_path = os.path.join(files_dir, filename)
                            file_name = filename
                            logger.info(f"找到匹配的简单文件名: {file_path}")
                            break
            
            # 再次检查文件是否存在
            if not os.path.exists(file_path) or not os.path.isfile(file_path):
                logger.error(f"文件不存在: {file_path}")
                self.send_error(404, f"File not found: {file_name}")
                return
        
        logger.info(f"准备发送文件: {file_path}")
        # 发送文件
        try:
            with open(file_path, 'rb') as f:
                self.send_response(200)
                self.send_header('Content-Type', 'application/octet-stream')
                self.send_header('Content-Disposition', f'attachment; filename="{file_name}"')
                fs = os.fstat(f.fileno())
                self.send_header('Content-Length', str(fs.st_size))
                self.end_headers()
                self.wfile.write(f.read())
            logger.info(f"文件发送成功: {file_path}")
        except Exception as e:
            logger.error(f"发送文件时出错: {str(e)}")
            self.send_error(500, f"Internal server error: {str(e)}")
    
    def log_message(self, format, *args):
        """重写日志方法，避免打印到控制台"""
        pass

class TCPMessageProtocol:
    """TCP消息协议，处理消息分帧和传输"""
    
    @staticmethod
    def encode_message(message: dict) -> bytes:
        """将消息字典编码为字节流"""
        # 将字典转换为JSON字符串
        json_str = json.dumps(message, ensure_ascii=False)
        # 转换为UTF-8字节
        message_bytes = json_str.encode('utf-8')
        # 添加4字节长度前缀（网络字节序）
        length_prefix = struct.pack('!I', len(message_bytes))
        return length_prefix + message_bytes
    
    @staticmethod
    async def decode_message(reader: asyncio.StreamReader) -> Optional[dict]:
        """从字节流解码消息字典"""
        try:
            # 读取4字节长度前缀
            length_data = await reader.readexactly(4)
            if len(length_data) != 4:
                logger.error(f"读取长度前缀失败，期望4字节，实际{len(length_data)}字节")
                return None
                
            message_length = struct.unpack('!I', length_data)[0]
            
            # 检查消息长度的合理性
            if message_length <= 0 or message_length > 20 * 1024 * 1024:  # 限制最大20MB
                logger.error(f"消息长度不合理: {message_length}")
                return None
            
            # 读取消息内容
            message_data = await reader.readexactly(message_length)
            if len(message_data) != message_length:
                logger.error(f"读取消息数据失败，期望{message_length}字节，实际{len(message_data)}字节")
                return None
                
            json_str = message_data.decode('utf-8')
            return json.loads(json_str)
        except (asyncio.IncompleteReadError, struct.error, json.JSONDecodeError, UnicodeDecodeError) as e:
            logger.error(f"解码消息失败: {e}")
            return None
        except Exception as e:
            logger.error(f"解码消息时发生未知错误: {e}")
            return None

class FeedbackTCPServer:
    def __init__(self):
        # 存储连接的客户端 {writer: client_info}
        self.clients: Dict[asyncio.StreamWriter, Dict] = {}
        
        # 存储用户信息 {username: user_info}
        self.users: Dict[str, Dict] = {}
        
        # 存储消息 {message_id: message_info}
        self.messages: Dict[str, Dict] = {}
        
        # 存储已读状态 {message_id: {set of usernames}}
        self.read_status: Dict[str, Set[str]] = {}
        
        # 存储用户名到Writer的映射 {username: writer}
        self.username_to_writer: Dict[str, asyncio.StreamWriter] = {}
        
        # 数据持久化路径
        self.data_dir = "feedback_data"
        self.data_file = os.path.join(self.data_dir, "feedback_data.json")
        self.users_file = os.path.join(self.data_dir, "users.json")
        
        # HTTP服务器相关
        self.http_server = None
        self.http_thread = None
        
        # 确保数据目录存在
        os.makedirs(self.data_dir, exist_ok=True)
        
        # 加载已有数据
        self.load_data()
    
    def load_data(self):
        """从文件加载已有数据"""
        try:
            # 加载消息
            if os.path.exists(self.data_file):
                with open(self.data_file, 'r', encoding='utf-8') as f:
                    messages_data = json.load(f)
                    for msg_id, msg_info in messages_data.items():
                        self.messages[msg_id] = msg_info
                        # 初始化已读状态
                        self.read_status[msg_id] = set(msg_info.get("read_by", []))
                logger.info(f"加载了 {len(self.messages)} 条历史消息")
            else:
                logger.info("消息文件不存在，将创建新的消息文件")
            
            # 加载用户
            if os.path.exists(self.users_file):
                with open(self.users_file, 'r', encoding='utf-8') as f:
                    self.users = json.load(f)
                logger.info(f"加载了 {len(self.users)} 个用户信息")
            else:
                logger.info("用户文件不存在，将创建新的用户文件")
        except Exception as e:
            logger.error(f"加载数据失败: {e}")
    
    def save_data(self):
        """保存数据到文件"""
        try:
            # 保存消息
            messages_data = {}
            for msg_id, msg_info in self.messages.items():
                messages_data[msg_id] = msg_info.copy()
                # 保存已读状态
                messages_data[msg_id]["read_by"] = list(self.read_status.get(msg_id, set()))
            
            with open(self.data_file, 'w', encoding='utf-8') as f:
                json.dump(messages_data, f, ensure_ascii=False, indent=2)
            
            # 保存用户
            with open(self.users_file, 'w', encoding='utf-8') as f:
                json.dump(self.users, f, ensure_ascii=False, indent=2)
                
            logger.info(f"数据保存成功: {len(self.messages)} 条消息, {len(self.users)} 个用户")
        except Exception as e:
            logger.error(f"保存数据失败: {e}")
    
    def generate_avatar(self, username):
        """根据用户名生成头像URL"""
        # 使用用户名的哈希值生成一个稳定的头像
        hash_value = int(hashlib.md5(username.encode()).hexdigest(), 16)
        # 使用一些免费的头像服务
        avatar_services = [
            f"https://api.dicebear.com/7.x/avataaars/svg?seed={hash_value}",
            f"https://api.dicebear.com/7.x/fun-emoji/svg?seed={hash_value}",
            f"https://api.dicebear.com/7.x/bottts/svg?seed={hash_value}",
            f"https://api.dicebear.com/7.x/lorelei/svg?seed={hash_value}",
            f"https://api.dicebear.com/7.x/notionists/svg?seed={hash_value}"
        ]
        return random.choice(avatar_services)
    
    async def register_client(self, writer, username):
        """注册新客户端"""
        logger.info(f"开始注册用户: {username}")
        
        # 如果用户已存在，踢掉旧连接
        if username in self.username_to_writer:
            old_writer = self.username_to_writer[username]
            if old_writer != writer and old_writer in self.clients:
                logger.info(f"用户 {username} 已在其他地方登录，踢掉旧连接")
                await self.send_message(old_writer, {
                    "type": "error",
                    "message": "同一用户在其他地方登录"
                })
                old_writer.close()
                await old_writer.wait_closed()
                del self.clients[old_writer]
        
        # 创建或更新用户信息
        if username not in self.users:
            self.users[username] = {
                "username": username,
                "avatar": self.generate_avatar(username),
                "first_seen": datetime.now().isoformat(),
                "last_seen": datetime.now().isoformat()
            }
            logger.info(f"创建新用户信息: {username}")
        else:
            self.users[username]["last_seen"] = datetime.now().isoformat()
            logger.info(f"更新用户最后在线时间: {username}")
        
        # 存储客户端信息
        self.clients[writer] = {
            "username": username,
            "joined_at": datetime.now().isoformat()
        }
        
        # 更新用户名到Writer的映射
        self.username_to_writer[username] = writer
        
        # 保存用户数据
        self.save_data()
        
        logger.info(f"用户 {username} 已连接")
        
        # 返回用户信息和最近的消息
        recent_messages = self.get_recent_messages(50)  # 返回最近50条消息
        logger.info(f"为用户 {username} 准备了 {len(recent_messages)} 条历史消息")
        
        response = {
            "type": "register_success",
            "user": self.users[username],
            "recent_messages": recent_messages
        }
        
        logger.info(f"准备发送注册成功响应给用户: {username}")
        return response
    
    def get_recent_messages(self, limit=50):
        """获取最近的消息"""
        # 按时间戳排序消息
        sorted_messages = sorted(
            self.messages.values(),
            key=lambda x: x.get("timestamp", 0),
            reverse=True
        )
        
        recent_messages = sorted_messages[:limit]
        
        # 为每条消息添加已读状态和用户信息
        result = []
        for msg in recent_messages:
            msg_id = msg["id"]
            username = msg["username"]
            message_type = msg.get("type", "message")
            
            # 获取已读用户数
            read_by_count = len(self.read_status.get(msg_id, set()))
            total_users = len(self.users)
            
            # 添加用户信息
            user_info = self.users.get(username, {
                "username": username,
                "avatar": self.generate_avatar(username)
            })
            
            # 处理不同类型的消息
            if message_type == "file":
                # 对于文件消息，需要生成下载URL
                file_name = msg.get("name", "")
                file_size = msg.get("size", 0)
                file_id = msg.get("id", "")
                
                # 查找实际的文件名
                files_dir = os.path.join(self.data_dir, "files")
                actual_file_name = None
                
                if os.path.exists(files_dir):
                    # 首先尝试通过文件大小匹配
                    for filename in os.listdir(files_dir):
                        file_path = os.path.join(files_dir, filename)
                        if os.path.isfile(file_path) and os.path.getsize(file_path) == file_size:
                            actual_file_name = filename
                            break
                    
                    # 如果通过大小找不到，尝试通过文件ID匹配
                    if not actual_file_name and file_id:
                        # 从文件ID中提取时间戳
                        if "_" in file_id:
                            timestamp = file_id.split("_")[-1]
                            # 查找包含此时间戳的文件
                            for filename in os.listdir(files_dir):
                                if timestamp in filename:
                                    actual_file_name = filename
                                    break
                
                if actual_file_name:
                    download_url = f"http://103.118.245.82:8889/download?file_name={actual_file_name}"
                    result.append({
                        "id": msg_id,
                        "type": "file_download_url",
                        "file_id": msg_id,
                        "name": file_name,
                        "size": file_size,
                        "url": download_url,
                        "username": username,
                        "user_info": user_info,
                        "timestamp": msg["timestamp"],
                        "read_by_count": read_by_count,
                        "total_users": total_users
                    })
                else:
                    # 如果找不到文件，仍然返回基本信息
                    result.append({
                        "id": msg_id,
                        "type": "file",
                        "name": file_name,
                        "size": file_size,
                        "username": username,
                        "user_info": user_info,
                        "timestamp": msg["timestamp"],
                        "read_by_count": read_by_count,
                        "total_users": total_users
                    })
            else:
                # 普通文本消息
                result.append({
                    "id": msg_id,
                    "content": msg.get("content", ""),
                    "type": message_type,
                    "name": msg.get("name", ""),
                    "size": msg.get("size", 0),
                    "username": username,
                    "user_info": user_info,
                    "timestamp": msg["timestamp"],
                    "read_by_count": read_by_count,
                    "total_users": total_users
                })
        
        # 按时间戳正序返回
        return sorted(result, key=lambda x: x.get("timestamp", 0))
    
    async def handle_message(self, writer, message_data):
        """处理客户端发送的消息"""
        username = self.clients[writer]["username"]
        
        # 生成消息ID（基于内容和时间戳，避免重复）
        content = message_data.get("content", "").strip()
        if not content:
            return None  # 忽略空消息
        
        # 创建消息哈希以检测重复
        message_hash = hashlib.md5(f"{username}{content}{int(time.time()//10)}".encode()).hexdigest()
        message_id = f"{username}_{message_hash[:8]}"
        
        # 检查是否是重复消息
        if message_id in self.messages:
            logger.info(f"忽略重复消息: {message_id}")
            return None
        
        # 创建新消息
        message_info = {
            "id": message_id,
            "content": content,
            "username": username,
            "timestamp": datetime.now().isoformat()
        }
        
        # 存储消息
        self.messages[message_id] = message_info
        self.read_status[message_id] = {username}  # 发送者自动标记为已读
        
        # 保存数据
        self.save_data()
        
        logger.info(f"收到来自 {username} 的消息: {content[:50]}...")
        
        # 准备广播的消息
        user_info = self.users.get(username, {
            "username": username,
            "avatar": self.generate_avatar(username)
        })
        
        broadcast_message = {
            "type": "new_message",
            "id": message_id,
            "content": content,
            "username": username,
            "user_info": user_info,
            "timestamp": message_info["timestamp"],
            "read_by_count": 1,
            "total_users": len(self.users)
        }
        
        return broadcast_message
    
    async def mark_message_read(self, writer, message_id):
        """标记消息为已读"""
        username = self.clients[writer]["username"]
        
        if message_id in self.messages and message_id in self.read_status:
            if username not in self.read_status[message_id]:
                self.read_status[message_id].add(username)
                
                # 保存数据
                self.save_data()
                
                # 计算已读状态
                read_by_count = len(self.read_status[message_id])
                total_users = len(self.users)
                
                logger.info(f"用户 {username} 标记消息 {message_id} 为已读")
                
                # 返回更新后的状态
                return {
                    "type": "read_status_update",
                    "message_id": message_id,
                    "read_by_count": read_by_count,
                    "total_users": total_users
                }
        
        return None
    
    async def send_message(self, writer, message):
        """向特定客户端发送消息"""
        if writer.is_closing():
            return False
            
        try:
            message_bytes = TCPMessageProtocol.encode_message(message)
            writer.write(message_bytes)
            await writer.drain()
            return True
        except Exception as e:
            logger.error(f"发送消息到客户端失败: {e}")
            return False
    
    async def broadcast(self, message, exclude_writer=None):
        """向所有连接的客户端广播消息"""
        if not message:
            return
            
        # 向所有客户端发送消息
        disconnected = []
        for writer, client_info in self.clients.items():
            if writer != exclude_writer:
                success = await self.send_message(writer, message)
                if not success:
                    disconnected.append(writer)
        
        # 清理断开的连接
        for writer in disconnected:
            await self.unregister_client(writer)
    
    async def handle_file_upload(self, writer, message_data):
        """处理文件上传"""
        username = self.clients[writer]["username"]
        
        # 获取文件信息
        file_name = message_data.get("name", "")
        file_size = message_data.get("size", 0)
        file_content = message_data.get("content", "")
        
        if not file_name or not file_content:
            logger.error(f"文件上传缺少必要信息: {file_name}")
            return None
        
        try:
            # 解码Base64内容
            file_data = base64.b64decode(file_content)
            
            # 创建文件存储目录
            files_dir = os.path.join(self.data_dir, "files")
            os.makedirs(files_dir, exist_ok=True)
            
            # 保存文件到服务端
            timestamp = int(time.time())
            # 生成一个简单的文件名，避免中文和特殊字符
            file_extension = os.path.splitext(file_name)[1]  # 获取文件扩展名
            simple_file_name = f"file_{timestamp}{file_extension}"
            file_path = os.path.join(files_dir, simple_file_name)
            with open(file_path, 'wb') as f:
                f.write(file_data)
            
            logger.info(f"用户 {username} 上传了文件: {file_name} ({file_size} bytes)")
            
            # 创建文件消息
            file_id = f"file_{username}_{timestamp}"
            file_message = {
                "type": "file",
                "id": file_id,
                "name": file_name,
                "size": file_size,
                "username": username,
                "timestamp": datetime.now().isoformat()
            }
            
            # 保存文件消息到历史记录
            self.messages[file_id] = file_message
            self.save_data()
            
            # 返回文件消息用于广播
            return {
                "type": "file",
                "id": file_id,
                "name": file_name,
                "size": file_size,
                "username": username,
                "user_info": self.users.get(username, {
                    "username": username,
                    "avatar": self.generate_avatar(username)
                }),
                "timestamp": datetime.now().isoformat()
            }
        except Exception as e:
            logger.error(f"处理文件上传失败: {e}")
            return None
    
    async def handle_file_download(self, writer, message_data):
        """处理文件下载请求"""
        username = self.clients[writer]["username"]
        file_id = message_data.get("file_id", "")
        
        logger.info(f"收到来自用户 {username} 的文件下载请求: {file_id}")
        
        if not file_id:
            logger.error(f"文件下载请求缺少文件ID")
            return
        
        try:
            # 从消息记录中查找文件名
            file_name = None
            logger.info(f"当前消息记录中的文件ID: {list(self.messages.keys())}")
            
            if file_id in self.messages:
                file_name = self.messages[file_id].get("name")
                logger.info(f"找到文件名: {file_name}")
            else:
                logger.error(f"文件ID {file_id} 不在消息记录中")
            
            if not file_name:
                logger.error(f"找不到文件ID {file_id} 对应的文件名")
                await self.send_message(writer, {
                    "type": "error",
                    "message": f"文件不存在: {file_id}"
                })
                return
            
            # 查找文件
            files_dir = os.path.join(self.data_dir, "files")
            file_path = os.path.join(files_dir, file_name)
            
            logger.info(f"查找文件路径: {file_path}")
            
            if not os.path.exists(file_path):
                logger.error(f"文件不存在: {file_path}")
                await self.send_message(writer, {
                    "type": "error",
                    "message": f"文件不存在: {file_name}"
                })
                return
            
            # 读取文件内容
            with open(file_path, 'rb') as f:
                file_data = f.read()
            
            # 获取文件大小
            file_size = len(file_data)
            
            # 生成下载URL（使用HTTP协议）
            # 查找实际的文件名
            actual_file_name = None
            if os.path.exists(files_dir):
                for filename in os.listdir(files_dir):
                    # 尝试匹配文件（通过大小或时间戳）
                    file_path = os.path.join(files_dir, filename)
                    if os.path.isfile(file_path) and os.path.getsize(file_path) == file_size:
                        actual_file_name = filename
                        break
            
            if not actual_file_name:
                logger.error(f"找不到对应的文件: {file_name}")
                await self.send_message(writer, {
                    "type": "error",
                    "message": f"找不到对应的文件: {file_name}"
                })
                return
            
            download_url = f"http://103.118.245.82:8889/download?file_name={actual_file_name}"
            
            # 发送下载链接而不是文件内容
            download_message = {
                "type": "file_download_url",
                "file_id": file_id,
                "name": file_name,
                "size": file_size,
                "url": download_url
            }
            
            logger.info(f"准备发送下载链接消息: {download_message}")
            
            try:
                success = await self.send_message(writer, download_message)
                if success:
                    logger.info(f"下载链接消息发送成功")
                else:
                    logger.error(f"下载链接消息发送失败")
            except Exception as e:
                logger.error(f"发送下载链接消息时发生异常: {e}")
            
            logger.info(f"用户 {username} 获取文件下载链接: {file_name}")
        except Exception as e:
            logger.error(f"处理文件下载失败: {e}")
            await self.send_message(writer, {
                "type": "error",
                "message": f"下载文件失败: {str(e)}"
            })
    
    async def unregister_client(self, writer):
        """注销客户端"""
        if writer not in self.clients:
            return
            
        username = self.clients[writer]["username"]
        
        # 更新用户最后在线时间
        if username in self.users:
            self.users[username]["last_seen"] = datetime.now().isoformat()
        
        # 从连接列表中移除
        del self.clients[writer]
        
        # 从用户名映射中移除
        if username in self.username_to_writer and self.username_to_writer[username] == writer:
            del self.username_to_writer[username]
        
        # 保存数据
        self.save_data()
        
        logger.info(f"用户 {username} 已断开连接")
        
        # 通知其他客户端用户离线
        await self.broadcast({
            "type": "user_offline",
            "username": username
        })
    
    async def handle_client(self, reader, writer):
        """处理客户端连接"""
        addr = writer.get_extra_info('peername')
        logger.info(f"新客户端连接: {addr}")
        
        try:
            # 等待用户注册
            register_message = await TCPMessageProtocol.decode_message(reader)
            if not register_message:
                logger.error(f"客户端 {addr} 发送的注册消息无效")
                await self.send_message(writer, {
                    "type": "error",
                    "message": "无效的注册消息"
                })
                writer.close()
                await writer.wait_closed()
                return
                
            if register_message.get("type") != "register" or not register_message.get("username"):
                logger.error(f"客户端 {addr} 发送的注册消息格式不正确")
                await self.send_message(writer, {
                    "type": "error",
                    "message": "注册消息格式不正确"
                })
                writer.close()
                await writer.wait_closed()
                return
            
            username = register_message["username"].strip()
            if not username:
                logger.error(f"客户端 {addr} 提供的用户名为空")
                await self.send_message(writer, {
                    "type": "error",
                    "message": "用户名不能为空"
                })
                writer.close()
                await writer.wait_closed()
                return
            
            # 注册客户端
            response = await self.register_client(writer, username)
            success = await self.send_message(writer, response)
            
            if not success:
                logger.error(f"向客户端 {addr} 发送注册响应失败")
                writer.close()
                await writer.wait_closed()
                return
                
            logger.info(f"成功向客户端 {addr} 发送注册响应")
            
            # 通知其他客户端有新用户上线
            await self.broadcast({
                "type": "user_online",
                "username": username,
                "user_info": self.users[username]
            }, exclude_writer=writer)
            
            # 处理客户端消息
            while not writer.is_closing():
                try:
                    # 读取消息
                    message_data = await TCPMessageProtocol.decode_message(reader)
                    if not message_data:
                        logger.info(f"客户端 {addr} 连接已关闭")
                        break
                    
                    msg_type = message_data.get("type")
                    logger.info(f"收到来自客户端 {addr} 的消息类型: {msg_type}")
                    
                    if msg_type == "message":
                        # 处理新消息
                        broadcast_msg = await self.handle_message(writer, message_data)
                        if broadcast_msg:
                            await self.broadcast(broadcast_msg)
                    
                    elif msg_type == "file":
                        # 处理文件上传
                        file_msg = await self.handle_file_upload(writer, message_data)
                        if file_msg:
                            await self.broadcast(file_msg)
                    
                    elif msg_type == "download_file":
                        # 处理文件下载请求
                        logger.info(f"处理文件下载请求: {message_data}")
                        await self.handle_file_download(writer, message_data)
                    
                    elif msg_type == "mark_read":
                        # 标记消息为已读
                        read_update = await self.mark_message_read(writer, message_data.get("message_id"))
                        if read_update:
                            await self.broadcast(read_update)
                    
                    elif msg_type == "ping":
                        # 心跳包
                        await self.send_message(writer, {"type": "pong"})
                    else:
                        logger.warning(f"未知消息类型: {msg_type}")
                        
                except Exception as e:
                    logger.error(f"处理客户端 {addr} 消息时出错: {e}")
                    break
        
        except Exception as e:
            logger.error(f"处理客户端 {addr} 连接时出错: {e}")
        finally:
            # 注销客户端
            await self.unregister_client(writer)
            if not writer.is_closing():
                writer.close()
                await writer.wait_closed()
    
    def start_http_server(self):
        """启动HTTP服务器"""
        try:
            # 创建自定义请求处理器的工厂函数
            def handler_factory(*args, **kwargs):
                handler = FileHTTPRequestHandler(*args, data_dir=self.data_dir, **kwargs)
                # 将feedback_server实例附加到HTTP服务器上，以便在处理请求时可以访问
                handler.server.feedback_server = self
                return handler
            
            # 创建HTTP服务器
            self.http_server = HTTPServer(('103.118.245.82', 8889), handler_factory)
            
            # 在单独的线程中运行HTTP服务器
            self.http_thread = threading.Thread(target=self.http_server.serve_forever)
            self.http_thread.daemon = True
            self.http_thread.start()
            
            logger.info("HTTP文件服务器已启动，监听 http://103.118.245.82:8889")
        except Exception as e:
            logger.error(f"启动HTTP服务器失败: {e}")
    
    def stop_http_server(self):
        """停止HTTP服务器"""
        if self.http_server:
            self.http_server.shutdown()
            self.http_server.server_close()
            logger.info("HTTP文件服务器已停止")
    
    async def stop_server(self):
        """停止服务器"""
        logger.info("服务器已停止")
        # 停止HTTP服务器
        self.stop_http_server()
    
    async def start_server(self, host="103.118.245.82", port=8888):
        """启动服务器"""
        logger.info(f"启动反馈频道TCP服务器 {host}:{port}")
        
        # 启动HTTP服务器
        self.start_http_server()
        
        # 启动TCP服务器
        self.server = await asyncio.start_server(
            self.handle_client, host, port
        )
        
        addr = self.server.sockets[0].getsockname()
        logger.info(f"服务器已启动，监听 {addr}")
    
    async def run(self, host="103.118.245.82", port=8888):
        """运行服务器"""
        await self.start_server(host, port)
        
        try:
            async with self.server:
                await self.server.serve_forever()
        except KeyboardInterrupt:
            logger.info("接收到中断信号，正在关闭服务器...")
        finally:
            await self.stop_server()

if __name__ == "__main__":
    server = FeedbackTCPServer()
    try:
        asyncio.run(server.run("103.118.245.82", 8888))
    except KeyboardInterrupt:
        logger.info("服务器已停止")
    except Exception as e:
        logger.error(f"服务器运行出错: {e}")