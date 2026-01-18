# 恶意软件检测器配置文件
# 用于存储应用程序的设置和参数

# 界面配置
UI_CONFIG = {
    # 窗口设置
    'window_title': '高级恶意软件检测系统',
    'window_width': 900,
    'window_height': 600,
    'resizable': True,
    
    # 颜色主题 - 专业蓝色系
    'colors': {
        "primary": "#4a6fa5",       # 主色调 - 专业蓝
        "secondary": "#6c757d",     # 次要色调 - 中性灰
        "background": "#f5f7fa",    # 背景色 - 浅蓝灰
        "card_bg": "#ffffff",      # 卡片背景色 - 白色
        "text": "#333333",         # 文本色 - 深灰
        "primary_text": "#212529",  # 主文本色
        "secondary_text": "#6c757d", # 次文本色
        "border": "#e9ecef",       # 边框色 - 浅灰
        "danger": "#dc3545",       # 危险色 - 红色
        "success": "#28a745",      # 成功色 - 绿色
        "warning": "#ffc107",      # 警告色 - 黄色
        "info": "#17a2b8",         # 信息色 - 青色
        "light": "#f8f9fa",        # 浅色背景
        "dark": "#343a40",         # 深色背景
        "status_text": "#007bff",   # 状态文本色
        "text_bg": "#f8f9fa",      # 文本框背景色
        "text_fg": "#333333",      # 文本框前景色
        "warning_text": "#856404",  # 警告文本色 - 深黄
        "error_text": "#721c24",   # 错误文本色 - 深红
        "success_text": "#155724"   # 成功文本色 - 深绿
    },
    
    # 字体设置
    'font_family': 'SimHei',
    'title_font_size': 12,
    'subtitle_font_size': 10,
    'normal_font_size': 10,
    
    # 布局设置
    'padding': 10,
    'card_padding': 10,
    'button_padding': (8, 4),
    'accent_button_padding': (10, 6),
    'tab_padding': (15, 5)
}

# 模型配置
MODEL_CONFIG = {
    # 模型参数
    'random_state': 42,
    'test_size': 0.2,
    'cv_folds': 5,
    
    # 特征提取配置
    'feature_extraction': {
        'max_features': 1000,
        'ngram_range': (1, 2),
        'analyzer': 'word',
        'min_df': 2,
        'max_df': 0.9
    },
    
    # 分类器配置
    'classifier': {
        'C': 1.0,
        'kernel': 'linear',
        'probability': True
    }
}

# 文件处理配置
FILE_CONFIG = {
    # 文件类型
    'allowed_extensions': ['.exe', '.dll', '.sys', '.com', '.bat', '.ps1', '.vbs', '.js'],
    
    # 批量处理
    'batch_size': 100,
    'max_file_size_mb': 50,
    
    # 扫描设置
    'max_files_per_directory': 1000,
    'scan_recursive': True,
    'ignore_hidden_files': True,
    
    # 临时文件
    'temp_dir': '.temp',
    'clean_temp_on_exit': True
}

# 日志配置
LOG_CONFIG = {
    'log_file': 'malware_detector.log',
    'log_level': 'INFO',
    'max_log_size_mb': 10,
    'backup_count': 5,
    'log_format': '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
}

# 性能配置
PERFORMANCE_CONFIG = {
    # 线程设置
    'max_threads': 4,
    
    # 缓存设置
    'cache_enabled': True,
    'cache_size_mb': 256,
    
    # 超时设置
    'file_process_timeout_sec': 30,
    'analysis_timeout_sec': 60
}

# 评估配置
EVAL_CONFIG = {
    # 评估指标
    'metrics': ['accuracy', 'precision', 'recall', 'f1', 'roc_auc'],
    
    # 混淆矩阵
    'confusion_matrix_normalize': True,
    'confusion_matrix_cmap': 'Blues',
    
    # 交叉验证
    'cv_enabled': True,
    'cv_scoring': 'accuracy'
}

# 数据配置
DATA_CONFIG = {
    # 数据路径
    'sample_data_dir': 'sample_data',
    'model_save_path': 'models',
    'results_save_path': 'results',
    
    # 数据格式
    'csv_delimiter': ',',
    'csv_encoding': 'utf-8',
    
    # 示例文件生成
    'generate_sample_files': True,
    'num_benign_samples': 10,
    'num_malicious_samples': 5
}