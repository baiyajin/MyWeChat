<div align="center">
  <img src="app/assets/images/logo.png" alt="MyWeChat Logo" width="200">
</div>

# MyWeChat

> **我的微信我做主**

## 项目简介

MyWeChat 是一个基于微信客户端的Hook工具集，通过三端协同工作实现微信数据的同步和远程控制。项目采用现代化的技术栈，提供完整的微信数据管理和操作功能。

### 核心功能

- 🔗 **微信数据同步**：实时同步好友列表、朋友圈、聊天记录等数据
- 📱 **移动端管理**：通过Flutter应用远程查看和管理微信数据
- 🖥️ **Windows端Hook**：通过WPF应用Hook微信客户端，实现数据采集和命令执行
- 🌐 **后端服务**：Python FastAPI提供RESTful API和WebSocket服务
- 💬 **远程控制**：通过移动端发送命令，Windows端执行（发送消息、点赞、评论等）

## 项目预览

<div align="center" style="display: flex; justify-content: center; gap: 20px; flex-wrap: wrap;">
  <div style="text-align: center;">
<img width="299" height="538" alt="image" src="https://github.com/user-attachments/assets/6edc4eda-1356-4d7e-a46f-64c0a6b99ec4" />
    <p><strong>App端（移动应用）</strong><br/>登录界面 - 支持手机号登录和最近登录账号快速选择</p>
  </div>
  <div style="text-align: center;">
<img width="383" height="290" alt="image" src="https://github.com/user-attachments/assets/6a682ae8-9cf2-400e-9acd-9279e05ee34b" />
    <p><strong>Windows端（Hook客户端）</strong><br/>主界面 - 显示账号信息、连接状态和日志</p>
  </div>
</div>

### 技术架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            MyWeChat 技术架构                              │
└─────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                              App端 (Flutter)                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐ │
│  │  好友列表Tab  │  │  朋友圈Tab   │  │   聊天Tab    │  │   我的Tab   │ │
│  └──────────────┘  └──────────────┘  └──────────────┘  └─────────────┘ │
│                                                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                    UI层 (lib/ui/)                                │   │
│  │  - pages/  - tabs/  - widgets/                                   │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                  服务层 (lib/services/)                            │   │
│  │  ┌──────────────┐              ┌──────────────┐                  │   │
│  │  │  ApiService  │              │WebSocketService│                │   │
│  │  │  (HTTP请求)  │              │ (WebSocket连接)│                │   │
│  │  └──────────────┘              └──────────────┘                  │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                  数据层 (lib/models/)                             │   │
│  │  - ContactModel  - MomentsModel  - ChatMessageModel             │   │
│  └──────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘
         │                              │
         │ HTTP/REST                    │ WebSocket
         │ (获取数据)                    │ (实时同步)
         │                              │
         ▼                              ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                         Server端 (Python FastAPI)                        │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                      API层 (app/api/)                             │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │   │
│  │  │ contacts │  │ moments  │  │   tags   │  │ commands  │       │   │
│  │  │  路由    │  │  路由    │  │  路由    │  │  路由     │       │   │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘       │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                  WebSocket层 (app/websocket/)                      │   │
│  │  ┌────────────────────────────────────────────────────────────┐   │   │
│  │  │         WebSocketManager (连接管理器)                       │   │   │
│  │  │  - Windows端连接管理  - App端连接管理                       │   │   │
│  │  │  - 消息路由转发      - 实时数据同步                         │   │   │
│  │  └────────────────────────────────────────────────────────────┘   │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                   数据层 (app/models/)                             │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐         │   │
│  │  │ Contact  │  │  Moment  │  │   Tag    │  │ Command  │         │   │
│  │  │ (联系人) │  │ (朋友圈) │  │  (标签)  │  │ (命令)   │         │   │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘         │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                   数据库层 (SQLAlchemy)                            │   │
│  │                    ┌──────────────┐                              │   │
│  │                    │ SQLite/PostgreSQL │                         │   │
│  │                    │  (my_wechat.db) │                      │   │
│  │                    └──────────────┘                              │   │
│  └──────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘
         │                              │
         │ WebSocket                    │ WebSocket
         │ (接收命令)                    │ (同步数据)
         │                              │
         ▼                              ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                        Windows端 (WPF .NET 9.0)                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                    UI层 (MainWindow.xaml)                        │   │
│  │  - 账号列表  - 连接状态  - 日志显示  - 命令执行结果               │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                核心层 (Core/)                                     │   │
│  │  ┌──────────────────────┐  ┌──────────────────────┐           │   │
│  │  │ WeChatHookManager    │  │WeChatConnectionManager│           │   │
│  │  │ (Hook管理器)          │  │ (连接管理器)          │           │   │
│  │  │ - Hook注入           │  │ - WebSocket连接       │           │   │
│  │  │ - 数据采集           │  │ - 消息收发           │           │   │
│  │  │ - 命令执行           │  │ - 状态管理           │           │   │
│  │  └──────────────────────┘  └──────────────────────┘           │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                服务层 (Services/)                                  │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │   │
│  │  │ContactSync   │  │MomentsSync    │  │CommandService│          │   │
│  │  │Service       │  │Service        │  │              │          │   │
│  │  │(好友同步)    │  │(朋友圈同步)   │  │(命令处理)    │          │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘          │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                模型层 (Models/)                                   │   │
│  │  - AccountInfo  - Contact  - Moment  - Command                   │   │
│  └──────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘
         │
         │ Hook注入
         │ (内存操作)
         ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                          微信客户端 (WeChat.exe)                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │                    微信进程内存空间                                │   │
│  │  - 好友列表数据  - 朋友圈数据  - 聊天记录  - 用户信息             │   │
│  └──────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘

数据流向说明：
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
1. 数据同步流程：
   微信客户端 → (Hook) → Windows端 → (WebSocket) → Server端 → (WebSocket) → App端

2. 命令执行流程：
   App端 → (WebSocket) → Server端 → (WebSocket) → Windows端 → (Hook) → 微信客户端

3. 数据查询流程：
   App端 → (HTTP) → Server端 → (数据库) → Server端 → (HTTP) → App端
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

## 技术栈版本

### App端（Flutter）
- **Flutter SDK**: 3.4.0+
- **Dart SDK**: 3.4.0+
- **主要依赖**:
  - `http`: ^1.2.2
  - `web_socket_channel`: ^2.4.0
  - `json_annotation`: ^4.9.0
  - `provider`: ^6.1.2
  - `shared_preferences`: ^2.3.3
  - `cached_network_image`: ^3.4.1
  - `flutter_staggered_grid_view`: ^0.7.0
- **Android配置**:
  - Gradle: 8.12
  - Android Gradle Plugin: 8.9.1
  - Kotlin: 2.1.0

### Server端（Python）
- **Python**: 3.8+
- **主要依赖**:
  - `fastapi`: 0.115.6
  - `uvicorn[standard]`: 0.32.1
  - `sqlalchemy`: 2.0.36
  - `aiosqlite`: 0.20.0
  - `pydantic`: 2.10.4
  - `python-multipart`: 0.0.20

### Windows端（C#）
- **.NET**: 9.0
- **框架**: WPF (.NET 9.0-windows)
- **主要依赖**:
  - `Newtonsoft.Json`: 13.0.3

## 项目结构

```
MyWeChat/
├── app/                    # Flutter移动应用（App端）
│   ├── lib/               # Dart源代码
│   ├── android/           # Android平台配置
│   ├── windows/           # Windows平台配置
│   ├── web/               # Web平台配置
│   └── assets/            # 资源文件
│
├── server/                 # Python后端服务（Server端）
│   ├── app/               # 应用代码
│   │   ├── api/           # API接口
│   │   ├── models/        # 数据模型
│   │   └── websocket/     # WebSocket服务
│   ├── static/            # 静态文件
│   └── requirements.txt   # Python依赖
│
├── windows/                # WPF Windows应用（Windows端）
│   └── MyWeChat.Windows/  # WPF项目
│       ├── Core/          # 核心模块
│       ├── Services/      # 业务服务
│       └── Models/        # 数据模型
│
└── reverse-engineering/    # 逆向工程相关文件
```

## 环境要求

### 通用要求
- Windows 10/11（Windows端必需）
- 已安装微信客户端（Windows端必需）

### App端要求
- Flutter SDK 3.4.0+
- Dart SDK 3.4.0+
- Android Studio / Xcode（用于移动端开发）
- Android 5.0+ / iOS 11.0+（移动端运行）

### Server端要求
- Python 3.8+
- pip（Python包管理器）

### Windows端要求
- .NET 9.0 SDK
- Visual Studio 2022+（推荐，支持 .NET 9.0）
- 管理员权限（Hook注入需要）

## 快速开始

### 1. Server端（后端服务）

Server端是项目的核心，需要首先启动。

#### 安装依赖

**方法1：使用批处理文件（推荐）**
```bash
cd server
双击运行：安装依赖.bat
```

**方法2：手动安装**
```bash
cd server
pip install -r requirements.txt
```

**方法3：使用虚拟环境（推荐生产环境）**
```bash
cd server
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

#### 启动服务器

**方法1：使用批处理文件（推荐）**
```bash
cd server
双击运行：启动服务器.bat
```

**方法2：使用run.py**
```bash
cd server
python run.py
```

**方法3：使用uvicorn直接启动**
```bash
cd server
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

服务器启动后，访问以下地址：
- API文档（Swagger UI）：http://localhost:8000/docs
- API文档（ReDoc）：http://localhost:8000/redoc
- WebSocket端点：ws://localhost:8000/ws

#### 配置说明

默认配置：
- 服务器地址：`0.0.0.0:8000`
- 数据库：SQLite（`my_wechat.db`）

如需修改配置，编辑 `server/run.py`：
```python
uvicorn.run(
    "app.main:app",
    host="0.0.0.0",      # 修改主机地址
    port=8000,           # 修改端口
    reload=True,         # 开发模式（生产环境设为False）
)
```

### 2. Windows端（Hook客户端）

Windows端负责Hook微信客户端，同步数据到服务器，并接收App端的命令。

#### 安装依赖

1. **安装.NET 9.0 SDK**
   - 如果未安装，从[微软官网](https://dotnet.microsoft.com/download/dotnet/9.0)下载安装
   - 验证安装：`dotnet --version`（应显示 9.0.x）

2. **还原NuGet包**
   - 在Visual Studio中打开 `windows/MyWeChat.Windows/MyWeChat.Windows.sln`
   - 右键项目 → 选择"还原NuGet包"
   - 或使用命令行：`dotnet restore`

#### 启动应用

**方法1：Visual Studio运行（推荐开发）**
1. 打开 `windows/MyWeChat.Windows/MyWeChat.Windows.sln`（解决方案文件）
   - 推荐使用 `.sln` 文件，也可以打开 `.csproj` 文件
2. 按 `F5` 运行（或菜单：调试 > 开始调试）
3. **重要**：程序会自动请求管理员权限（通过 app.manifest 配置），按 F5 时会弹出 UAC 提示，点击"是"即可。Hook注入需要管理员权限。

**方法2：直接运行exe**
1. 编译项目（生成 > 重新生成解决方案）
2. 找到 `windows/MyWeChat.Windows/bin/Debug/MyWeChat.Windows.exe`
3. 右键以管理员身份运行

**方法3：命令行运行**
```bash
cd windows/MyWeChat.Windows/bin/Debug
# 以管理员权限运行
MyWeChat.Windows.exe
```

#### 配置说明

编辑 `windows/MyWeChat.Windows/App.config` 配置服务器地址：
```xml
<appSettings>
  <add key="ServerUrl" value="http://localhost:8000" />
  <add key="WebSocketUrl" value="ws://localhost:8000/ws" />
  <add key="CallbackPort" value="6060" />
</appSettings>
```

#### 注意事项

- ⚠️ **必须以管理员权限运行**：Hook注入需要管理员权限
- ⚠️ **服务器连接**：确保Server端已启动并可以访问
- ⚠️ **防火墙设置**：可能需要允许程序通过防火墙

### 3. App端（移动应用）

App端使用Flutter开发，支持Android、iOS、Windows和Web平台。

#### 安装依赖

**方法1：使用批处理文件（推荐）**
```bash
cd app
双击运行：安装依赖.bat
```

**方法2：手动安装**
```bash
cd app
flutter pub get
```

#### 配置平台支持

如果首次运行，需要配置平台支持：
```bash
cd app
flutter create . --platforms=android,windows,web --no-overwrite
```

或使用批处理文件：
```bash
cd app
双击运行：配置平台支持.bat
```

#### 启动应用

**方法1：使用批处理文件（推荐）**
```bash
cd app
双击运行：启动应用.bat
```

**方法2：手动启动**
```bash
cd app
flutter run
```

**方法3：指定设备运行**
```bash
cd app
# 查看可用设备
flutter devices

# 指定设备运行
flutter run -d windows      # Windows平台
flutter run -d chrome       # Chrome浏览器
flutter run -d edge          # Edge浏览器
flutter run -d <device_id>  # 指定设备ID
```

#### 配置服务器地址

编辑 `app/lib/services/api_service.dart` 和 `app/lib/services/websocket_service.dart`，修改服务器地址：
```dart
// 默认配置
static const String baseUrl = 'http://localhost:8000';
static const String wsUrl = 'ws://localhost:8000/ws';
```

#### 开发调试

- **热重载**：运行应用后，按 `r` 键进行热重载，按 `R` 键进行热重启
- **查看日志**：`flutter logs`
- **清理构建**：`flutter clean && flutter pub get`

## 使用流程

1. **启动Server端**
   ```bash
   cd server
   python run.py
   ```
   确保服务器运行在 `http://localhost:8000`

2. **启动Windows端**
   - 以管理员权限运行 `MyWeChat.Windows.exe`
   - 确保微信客户端已启动
   - Windows端会自动连接服务器并开始同步数据

3. **启动App端**
   ```bash
   cd app
   flutter run
   ```
   - App端会自动连接服务器
   - 可以查看同步的好友列表、朋友圈等数据
   - 可以发送命令控制Windows端执行操作

## 打包部署

### Server端打包

#### 使用Docker（推荐）

1. 创建Dockerfile：
```dockerfile
FROM python:3.9-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY . .
EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
```

2. 构建和运行：
```bash
docker build -t sales-champion-server .
docker run -d -p 8000:8000 --name sales-champion-server sales-champion-server
```

#### 使用Gunicorn（生产环境）

```bash
pip install gunicorn
gunicorn app.main:app -w 4 -k uvicorn.workers.UvicornWorker -b 0.0.0.0:8000
```

### Windows端打包

1. **在Visual Studio中**
   - 右键项目 → 发布
   - 选择发布目标（文件夹、Web服务器等）
   - 配置发布设置并发布

2. **使用dotnet命令行（推荐）**
   ```bash
   cd windows/MyWeChat.Windows
   dotnet publish -c Release -r win-x64 --self-contained false
   ```

3. **使用MSBuild命令行**
   ```bash
   cd windows
   dotnet build MyWeChat.Windows/MyWeChat.Windows.csproj -c Release
   ```

4. **手动打包**
   - 编译Release版本
   - 复制以下文件到目标目录：
     - `bin/Release/MyWeChat.Windows.exe`
     - `bin/Release/MyWeChat.Windows.exe.config`
     - `bin/Release/*.dll`（依赖的DLL）

### App端打包

#### Android打包

```bash
cd app
# 构建APK
flutter build apk --release

# 构建App Bundle（用于Google Play）
flutter build appbundle --release
```

输出文件：
- APK: `app/build/app/outputs/flutter-apk/app-release.apk`
- App Bundle: `app/build/app/outputs/bundle/release/app-release.aab`

#### iOS打包

```bash
cd app
flutter build ios --release
```

然后在Xcode中：
1. 打开 `app/ios/Runner.xcworkspace`
2. 选择 Product > Archive
3. 选择 Distribute App

#### Windows打包

```bash
cd app
flutter build windows --release
```

输出文件：`app/build/windows/runner/Release/`

## 常见问题

### Server端

**Q: 端口被占用怎么办？**
A: 修改 `server/run.py` 中的端口号，或关闭占用端口的程序。

**Q: 数据库文件在哪里？**
A: 默认在 `server/my_wechat.db`，SQLite数据库文件。

**Q: 如何查看API文档？**
A: 启动服务器后访问 http://localhost:8000/docs

### Windows端

**Q: Hook失败怎么办？**
A: 
- 确保以管理员权限运行
- 确保微信客户端已启动
- 确保已安装 .NET 9.0 SDK

**Q: 编译失败，提示找不到 .NET 9.0？**
A: 
- 安装 .NET 9.0 SDK：https://dotnet.microsoft.com/download/dotnet/9.0
- 验证安装：`dotnet --version`（应显示 9.0.x）
- 在 Visual Studio 中，确保安装了 .NET 9.0 工作负载

**Q: 无法连接服务器？**
A: 
- 检查Server端是否已启动
- 检查 `App.config` 中的服务器地址配置
- 检查防火墙设置

**Q: 如何查看日志？**
A: 日志文件在 `bin/Debug/Logs/` 目录下

### App端

**Q: 无法连接服务器？**
A: 
- 检查Server端是否已启动
- 检查服务器地址配置（`api_service.dart` 和 `websocket_service.dart`）
- 如果使用移动设备，确保服务器地址是局域网IP而非localhost

**Q: 平台不支持怎么办？**
A: 运行 `flutter create . --platforms=android,windows,web --no-overwrite` 配置平台支持

**Q: 构建失败？**
A: 
- 运行 `flutter clean && flutter pub get`
- 检查Flutter SDK版本是否符合要求
- 查看具体错误信息

## 开发指南

### 代码结构

- **App端**：Flutter/Dart 3.4+，使用Provider进行状态管理
- **Server端**：Python 3.8+ FastAPI 0.115+，使用SQLAlchemy 2.0进行数据库操作
- **Windows端**：C# .NET 9.0 WPF，使用MVVM模式，SDK风格项目文件

### API接口

Server端提供以下API接口：
- `/api/contacts` - 联系人相关接口
- `/api/moments` - 朋友圈相关接口
- `/api/tags` - 标签相关接口
- `/api/commands` - 命令相关接口
- `/ws` - WebSocket端点

详细API文档请访问：http://localhost:8000/docs

### 数据模型

- **Contact** - 联系人信息
- **Moment** - 朋友圈信息
- **Tag** - 标签信息
- **ChatMessage** - 聊天消息

### WebSocket消息格式

```json
{
  "type": "command",
  "action": "send_message",
  "data": {
    "wxid": "xxx",
    "message": "hello"
  }
}
```

## 注意事项

1. **法律合规**：本工具仅供学习和研究使用，请遵守相关法律法规
2. **数据安全**：注意保护用户隐私和数据安全
3. **生产环境**：生产环境部署时请关闭调试模式，配置安全措施
4. **版本兼容**：确保三端版本兼容，建议使用相同版本
5. **权限管理**：Windows端需要管理员权限，请谨慎使用

## 贡献指南

欢迎提交Issue和Pull Request！

## 许可证

本项目仅供学习和研究使用。

## 联系方式

如有问题，请提交Issue或联系项目维护者。

---

**⚠️ 免责声明**：本工具仅供学习和研究使用，使用者需自行承担使用风险，开发者不对任何损失负责。

