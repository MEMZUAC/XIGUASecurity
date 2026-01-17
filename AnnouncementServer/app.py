from flask import Flask, request, jsonify, render_template, redirect, url_for, session, flash
import os
import json
import uuid
from datetime import datetime
import hashlib
import re
from functools import wraps

app = Flask(__name__)
app.secret_key = 'your-secret-key-change-this-in-production'  # 在生产环境中更改此密钥

# 安全配置
app.config['SESSION_COOKIE_SECURE'] = False  # 在生产环境中设置为True（需要HTTPS）
app.config['SESSION_COOKIE_HTTPONLY'] = True
app.config['SESSION_COOKIE_SAMESITE'] = 'Lax'
app.config['PERMANENT_SESSION_LIFETIME'] = 3600  # 会话过期时间（秒）

# 配置
ANNOUNCEMENTS_FILE = 'announcements.json'
USERS_FILE = 'users.json'

# 默认管理员账户（首次运行时创建）
DEFAULT_ADMIN = {
    'username': 'admin',
    'password_hash': hashlib.sha256('admin123'.encode()).hexdigest()  # 默认密码：admin123
}

def load_announcements():
    if os.path.exists(ANNOUNCEMENTS_FILE):
        try:
            with open(ANNOUNCEMENTS_FILE, 'r', encoding='utf-8') as f:
                return json.load(f)
        except Exception as e:
            print(f"加载公告数据失败: {e}")
            return []
    return []

def save_announcements(announcements):
    try:
        with open(ANNOUNCEMENTS_FILE, 'w', encoding='utf-8') as f:
            json.dump(announcements, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print(f"保存公告数据失败: {e}")

def load_users():
    if os.path.exists(USERS_FILE):
        try:
            with open(USERS_FILE, 'r', encoding='utf-8') as f:
                return json.load(f)
        except Exception as e:
            print(f"加载用户数据失败: {e}")
    
    # 如果用户文件不存在，创建默认管理员
    users = {'users': [DEFAULT_ADMIN]}
    save_users(users)
    return users

def save_users(users):
    try:
        with open(USERS_FILE, 'w', encoding='utf-8') as f:
            json.dump(users, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print(f"保存用户数据失败: {e}")

# 身份验证装饰器
def login_required(f):
    @wraps(f)
    def decorated_function(*args, **kwargs):
        # 检查是否已登录且已验证
        if 'username' not in session or not session.get('authenticated', False):
            return redirect(url_for('login'))
        return f(*args, **kwargs)
    return decorated_function

# 输入验证函数
def validate_announcement_input(title, content):
    errors = []
    
    # 检查标题
    if not title or len(title.strip()) == 0:
        errors.append("标题不能为空")
    elif len(title) > 100:
        errors.append("标题不能超过100个字符")
    elif contains_inappropriate_content(title):
        errors.append("标题包含不当内容")
    
    # 检查内容
    if not content or len(content.strip()) == 0:
        errors.append("内容不能为空")
    elif len(content) > 5000:
        errors.append("内容不能超过5000个字符")
    elif contains_inappropriate_content(content):
        errors.append("内容包含不当内容")
    
    return errors

# 简单的内容过滤（可根据需要扩展）
def contains_inappropriate_content(text):
    # 这里可以添加更多敏感词
    inappropriate_words = ['侮辱', '垃圾', '骗子', '诈骗', '色情', '暴力']
    text_lower = text.lower()
    for word in inappropriate_words:
        if word in text_lower:
            return True
    return False

# 清理HTML输入，防止XSS
def sanitize_html(html_content):
    # 简单的HTML清理，移除脚本标签和危险属性
    html_content = re.sub(r'<script.*?>.*?</script>', '', html_content, flags=re.DOTALL | re.IGNORECASE)
    html_content = re.sub(r'on\w+\s*=', '', html_content, flags=re.IGNORECASE)
    html_content = re.sub(r'javascript:', '', html_content, flags=re.IGNORECASE)
    return html_content

# 登录路由
@app.route('/login', methods=['GET', 'POST'])
def login():
    # 清除可能存在的会话
    session.clear()
    
    if request.method == 'POST':
        username = request.form.get('username', '').strip()
        password = request.form.get('password', '')
        
        if not username or not password:
            flash('用户名和密码不能为空', 'error')
            return render_template('login.html')
        
        # 限制登录尝试次数（简单实现）
        login_attempts = session.get('login_attempts', 0)
        if login_attempts >= 5:
            flash('登录尝试次数过多，请稍后再试', 'error')
            return render_template('login.html')
        
        users = load_users()
        password_hash = hashlib.sha256(password.encode()).hexdigest()
        
        # 验证用户名和密码
        authenticated = False
        for user in users.get('users', []):
            if user.get('username') == username and user.get('password_hash') == password_hash:
                session['username'] = username
                session['authenticated'] = True
                session.permanent = True
                # 清除登录尝试计数
                session.pop('login_attempts', None)
                return redirect(url_for('admin'))
        
        # 登录失败，增加尝试次数
        session['login_attempts'] = login_attempts + 1
        flash('用户名或密码错误', 'error')
        return render_template('login.html')
    
    return render_template('login.html')

# 登出路由
@app.route('/logout')
def logout():
    # 完全清除会话
    session.clear()
    flash('已成功退出登录', 'success')
    return redirect(url_for('login'))

# API路由 - 获取公告（无需认证）
@app.route('/api/announcements', methods=['GET'])
def get_announcements():
    announcements = load_announcements()
    # 按发布日期降序排序
    announcements.sort(key=lambda x: x.get('publish_date', ''), reverse=True)
    return jsonify(announcements)

# API路由 - 获取最新公告（无需认证）
@app.route('/api/announcements/latest', methods=['GET'])
def get_latest_announcement():
    announcements = load_announcements()
    if not announcements:
        return jsonify({"error": "没有找到公告"}), 404
    
    # 按发布日期降序排序，返回最新的一条
    announcements.sort(key=lambda x: x.get('publish_date', ''), reverse=True)
    return jsonify(announcements[0])

# API路由 - 获取特定公告（无需认证）
@app.route('/api/announcements/<announcement_id>', methods=['GET'])
def get_announcement(announcement_id):
    announcements = load_announcements()
    for announcement in announcements:
        if announcement.get('id') == announcement_id:
            return jsonify(announcement)
    return jsonify({"error": "公告未找到"}), 404

# 需要认证的API路由
@app.route('/api/announcements', methods=['POST'])
@login_required
def create_announcement():
    title = request.form.get('title', '').strip()
    content = request.form.get('content', '').strip()
    is_important = request.form.get('is_important', 'false').lower() == 'true'
    
    # 验证输入
    errors = validate_announcement_input(title, content)
    if errors:
        return jsonify({"error": "输入验证失败", "details": errors}), 400
    
    # 清理HTML内容
    content = sanitize_html(content)
    
    announcements = load_announcements()
    
    new_announcement = {
        'id': str(uuid.uuid4()),
        'title': title,
        'content': content,
        'publish_date': datetime.now().strftime('%Y-%m-%d %H:%M'),
        'is_important': is_important,
        'author': session.get('username', 'Unknown')  # 记录发布者
    }
    
    announcements.append(new_announcement)
    save_announcements(announcements)
    
    return jsonify(new_announcement), 201

@app.route('/api/announcements/<announcement_id>', methods=['PUT'])
@login_required
def update_announcement(announcement_id):
    title = request.form.get('title', '').strip()
    content = request.form.get('content', '').strip()
    is_important = request.form.get('is_important', 'false').lower() == 'true'
    
    # 验证输入
    errors = validate_announcement_input(title, content)
    if errors:
        return jsonify({"error": "输入验证失败", "details": errors}), 400
    
    # 清理HTML内容
    content = sanitize_html(content)
    
    announcements = load_announcements()
    
    for announcement in announcements:
        if announcement.get('id') == announcement_id:
            announcement['title'] = title
            announcement['content'] = content
            announcement['is_important'] = is_important
            announcement['last_modified'] = datetime.now().strftime('%Y-%m-%d %H:%M')
            announcement['last_modified_by'] = session.get('username', 'Unknown')  # 记录修改者
            
            save_announcements(announcements)
            return jsonify(announcement)
    
    return jsonify({"error": "公告未找到"}), 404

@app.route('/api/announcements/<announcement_id>', methods=['DELETE'])
@login_required
def delete_announcement(announcement_id):
    announcements = load_announcements()
    
    for i, announcement in enumerate(announcements):
        if announcement.get('id') == announcement_id:
            # 从列表中删除公告
            announcements.pop(i)
            save_announcements(announcements)
            return '', 204
    
    return jsonify({"error": "公告未找到"}), 404

# 管理页面路由（需要认证）
@app.route('/admin')
@login_required
def admin():
    return render_template('admin.html', announcements=load_announcements())

# 修改密码页面
@app.route('/change_password', methods=['GET', 'POST'])
@login_required
def change_password():
    if request.method == 'POST':
        current_password = request.form.get('current_password', '')
        new_password = request.form.get('new_password', '')
        confirm_password = request.form.get('confirm_password', '')
        
        if not current_password or not new_password or not confirm_password:
            flash('所有字段都不能为空', 'error')
            return render_template('change_password.html')
        
        if new_password != confirm_password:
            flash('新密码和确认密码不匹配', 'error')
            return render_template('change_password.html')
        
        if len(new_password) < 6:
            flash('新密码长度至少为6个字符', 'error')
            return render_template('change_password.html')
        
        # 验证当前密码
        users = load_users()
        current_password_hash = hashlib.sha256(current_password.encode()).hexdigest()
        username = session.get('username')
        
        # 找到当前用户并更新密码
        user_found = False
        for i, user in enumerate(users.get('users', [])):
            if user.get('username') == username and user.get('password_hash') == current_password_hash:
                # 更新密码
                users['users'][i]['password_hash'] = hashlib.sha256(new_password.encode()).hexdigest()
                save_users(users)
                flash('密码已成功更新', 'success')
                user_found = True
                break
        
        if not user_found:
            flash('当前密码错误', 'error')
            return render_template('change_password.html')
        
        return redirect(url_for('admin'))
    
    return render_template('change_password.html')

@app.route('/')
def index():
    if 'username' in session:
        return redirect(url_for('admin'))
    return redirect(url_for('login'))

if __name__ == '__main__':
    print("公告服务器已启动，访问 http://localhost:8080 进行管理")
    print("默认管理员账户：用户名=admin，密码=admin123")
    print("首次登录后请立即修改密码！")
    app.run(host='0.0.0.0', port=8080, debug=True)