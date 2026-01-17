from flask import Flask, request, jsonify, render_template, redirect, url_for
import os
import json
import uuid
from datetime import datetime

app = Flask(__name__)

# 配置
ANNOUNCEMENTS_FILE = 'announcements.json'

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

# API路由
@app.route('/api/announcements', methods=['GET'])
def get_announcements():
    announcements = load_announcements()
    # 按发布日期降序排序
    announcements.sort(key=lambda x: x.get('publish_date', ''), reverse=True)
    return jsonify(announcements)

@app.route('/api/announcements/latest', methods=['GET'])
def get_latest_announcement():
    announcements = load_announcements()
    if not announcements:
        return jsonify({"error": "没有找到公告"}), 404
    
    # 按发布日期降序排序，返回最新的一条
    announcements.sort(key=lambda x: x.get('publish_date', ''), reverse=True)
    return jsonify(announcements[0])

@app.route('/api/announcements/<announcement_id>', methods=['GET'])
def get_announcement(announcement_id):
    announcements = load_announcements()
    for announcement in announcements:
        if announcement.get('id') == announcement_id:
            return jsonify(announcement)
    return jsonify({"error": "公告未找到"}), 404

@app.route('/api/announcements', methods=['POST'])
def create_announcement():
    title = request.form.get('title', '')
    content = request.form.get('content', '')
    is_important = request.form.get('is_important', 'false').lower() == 'true'
    
    if not title or not content:
        return jsonify({"error": "标题和内容不能为空"}), 400
    
    announcements = load_announcements()
    
    new_announcement = {
        'id': str(uuid.uuid4()),
        'title': title,
        'content': content,
        'publish_date': datetime.now().strftime('%Y-%m-%d %H:%M'),
        'is_important': is_important
    }
    
    announcements.append(new_announcement)
    save_announcements(announcements)
    
    return jsonify(new_announcement), 201

@app.route('/api/announcements/<announcement_id>', methods=['PUT'])
def update_announcement(announcement_id):
    title = request.form.get('title', '')
    content = request.form.get('content', '')
    is_important = request.form.get('is_important', 'false').lower() == 'true'
    
    if not title or not content:
        return jsonify({"error": "标题和内容不能为空"}), 400
    
    announcements = load_announcements()
    
    for announcement in announcements:
        if announcement.get('id') == announcement_id:
            announcement['title'] = title
            announcement['content'] = content
            announcement['is_important'] = is_important
            
            save_announcements(announcements)
            return jsonify(announcement)
    
    return jsonify({"error": "公告未找到"}), 404

@app.route('/api/announcements/<announcement_id>', methods=['DELETE'])
def delete_announcement(announcement_id):
    announcements = load_announcements()
    
    for i, announcement in enumerate(announcements):
        if announcement.get('id') == announcement_id:
            # 从列表中删除公告
            announcements.pop(i)
            save_announcements(announcements)
            return '', 204
    
    return jsonify({"error": "公告未找到"}), 404

# 管理页面路由
@app.route('/admin')
def admin():
    return render_template('admin.html', announcements=load_announcements())

@app.route('/')
def index():
    return redirect(url_for('admin'))

if __name__ == '__main__':
    print("公告服务器已启动，访问 http://localhost:8080/admin 进行管理")
    app.run(host='0.0.0.0', port=8080, debug=True)