# Server端 - MyWeChat后端服务

## 项目概述
Server端使用Python FastAPI开发，提供RESTful API和WebSocket服务，存储Windows端同步的数据，转发App端命令。

## 技术栈
- Python 3.8+
- FastAPI
- SQLAlchemy (ORM)
- WebSocket
- PostgreSQL/SQLite

## 主要功能
1. 接收Windows端同步的好友、朋友圈、标签数据
2. 接收App端发送的操作命令
3. 通过WebSocket转发命令到Windows端
4. 提供数据查询API（好友列表、朋友圈、聊天记录）
5. WebSocket双向通信

## 项目结构
```
server/
├── app/                          # 应用代码
│   ├── api/                     # API接口
│   ├── models/                  # 数据模型
│   ├── websocket/               # WebSocket服务
│   └── main.py                  # 主应用
├── static/                       # 静态文件
├── requirements.txt             # 依赖包
└── run.py                       # 启动脚本
```

## 运行要求
- Python 3.8+
- SQLite（默认）或PostgreSQL

## 安装依赖

### 方法1：使用批处理文件（推荐）
```bash
双击运行：安装依赖.bat
```

### 方法2：手动安装
```bash
cd server
pip install -r requirements.txt
```

### 使用虚拟环境（推荐）
```bash
# 创建虚拟环境
python -m venv venv

# 激活虚拟环境
# Windows:
venv\Scripts\activate
# Linux/Mac:
source venv/bin/activate

# 安装依赖
pip install -r requirements.txt
```

## 启动服务器

### 方法1：使用批处理文件（推荐）
```bash
双击运行：启动服务器.bat
```

### 方法2：使用run.py
```bash
cd server
python run.py
```

### 方法3：使用uvicorn直接启动
```bash
cd server
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

### 方法4：使用FastAPI CLI
```bash
cd server
fastapi dev app/main.py
```

## 配置说明

### 数据库配置
默认使用SQLite，数据库文件：`my_wechat.db`

如需使用PostgreSQL，修改 `app/models/database.py`：
```python
DATABASE_URL = "postgresql://user:password@localhost/dbname"
```

### 服务器配置
修改 `run.py` 中的配置：
```python
if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)
```

## API文档

启动服务器后，访问：
- Swagger UI: http://localhost:8000/docs
- ReDoc: http://localhost:8000/redoc

## 打包部署

### 方法1：使用Docker（推荐）

#### 1. 创建Dockerfile
```dockerfile
FROM python:3.9-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

EXPOSE 8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
```

#### 2. 构建镜像
```bash
docker build -t sales-champion-server .
```

#### 3. 运行容器
```bash
docker run -d -p 8000:8000 --name sales-champion-server sales-champion-server
```

### 方法2：使用systemd（Linux）

#### 1. 创建服务文件
`/etc/systemd/system/sales-champion.service`:
```ini
[Unit]
Description=Sales Champion Server
After=network.target

[Service]
Type=simple
User=www-data
WorkingDirectory=/path/to/server
ExecStart=/path/to/venv/bin/uvicorn app.main:app --host 0.0.0.0 --port 8000
Restart=always

[Install]
WantedBy=multi-user.target
```

#### 2. 启动服务
```bash
sudo systemctl enable sales-champion
sudo systemctl start sales-champion
```

### 方法3：使用Nginx反向代理

#### Nginx配置
```nginx
server {
    listen 80;
    server_name your-domain.com;

    location / {
        proxy_pass http://127.0.0.1:8000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /ws {
        proxy_pass http://127.0.0.1:8000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

### 方法4：使用Gunicorn（生产环境）

#### 1. 安装Gunicorn
```bash
pip install gunicorn
```

#### 2. 启动服务
```bash
gunicorn app.main:app -w 4 -k uvicorn.workers.UvicornWorker -b 0.0.0.0:8000
```

## 开发调试

### 启用热重载
```bash
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

### 查看日志
```bash
# 查看实时日志
tail -f logs/app.log
```

### 数据库迁移（如果使用Alembic）
```bash
# 创建迁移
alembic revision --autogenerate -m "description"

# 执行迁移
alembic upgrade head
```

## 注意事项

1. **生产环境**：不要使用 `--reload` 参数
2. **数据库备份**：定期备份数据库文件
3. **安全配置**：配置CORS、认证等安全措施
4. **性能优化**：使用连接池、缓存等优化性能
5. **日志管理**：配置日志轮转和清理策略

