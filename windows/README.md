# Windows端 - MyWeChat

## 项目概述
Windows端使用.NET 9.0 + WPF开发，负责Hook微信客户端，同步数据到服务器，接收App端命令并执行。

## 技术栈
- .NET 9.0
- C# WPF
- P/Invoke调用原生DLL
- WebSocket客户端

## 主要功能
1. Hook微信客户端
2. 同步好友列表到服务器
3. 同步朋友圈到服务器
4. 同步标签到服务器
5. 接收App端命令（通过WebSocket）
6. 执行命令（发送消息、点赞、评论、发朋友圈等）

## 项目结构
```
windows/
├── SalesChampion.Windows/        # 主项目
│   ├── Core/                     # 核心模块
│   │   ├── DLLWrapper/          # DLL封装
│   │   ├── Hook/                 # Hook管理
│   │   └── Connection/           # 连接管理
│   ├── Services/                 # 业务服务
│   ├── Models/                   # 数据模型
│   ├── UI/                       # UI界面
│   └── Utils/                    # 工具类
└── DLLs/                         # DLL文件目录
```

## 运行要求
- Windows 10/11
- .NET 9.0 SDK
- Visual Studio 2022或更高版本（推荐）
- 已安装微信客户端

## 安装依赖

### 1. 安装.NET 9.0 SDK
如果未安装，从[微软官网](https://dotnet.microsoft.com/download/dotnet/9.0)下载安装。

### 2. 还原NuGet包
在Visual Studio中：
1. 右键项目 "SalesChampion.Windows"
2. 选择 "还原NuGet包"
3. 等待完成

或在包管理器控制台：
```powershell
Update-Package -reinstall
```

## 启动应用

### 方法1：Visual Studio运行
1. 打开 `windows/SalesChampion.Windows/SalesChampion.Windows.sln`（解决方案文件）
   - 或者也可以打开 `SalesChampion.Windows.csproj`（项目文件），但推荐使用 `.sln` 文件
2. 按 `F5` 运行（或菜单：调试 > 开始调试）
3. **重要**：程序会自动请求管理员权限（通过 app.manifest 配置），按 F5 时会弹出 UAC 提示，点击"是"即可。Hook注入需要管理员权限。

### 方法2：直接运行exe
1. 编译项目（生成 > 重新生成解决方案）
2. 找到 `bin/Debug/SalesChampion.Windows.exe`
3. 右键以管理员身份运行

### 方法3：命令行运行
```bash
cd windows/SalesChampion.Windows/bin/Debug
SalesChampion.Windows.exe
```

## 编译项目

### 在Visual Studio中
1. 菜单：生成 > 清理解决方案
2. 菜单：生成 > 重新生成解决方案
3. 或按 `Ctrl+Shift+B`

### 使用MSBuild命令行
```bash
cd windows
msbuild SalesChampion.Windows/SalesChampion.Windows.csproj /p:Configuration=Release /p:Platform=AnyCPU
```

## 打包应用

### 方法1：Visual Studio发布
1. 右键项目 > 发布
2. 选择发布目标（文件夹、Web服务器等）
3. 配置发布设置
4. 点击发布

### 方法2：使用MSBuild打包
```bash
cd windows
msbuild SalesChampion.Windows/SalesChampion.Windows.csproj /p:Configuration=Release /p:Platform=AnyCPU /t:Publish
```

### 方法3：手动打包
1. 编译Release版本
2. 复制以下文件到目标目录：
   - `bin/Release/SalesChampion.Windows.exe`
   - `bin/Release/SalesChampion.Windows.exe.config`
   - `bin/Release/*.dll`（依赖的DLL）
   - `DLLs/` 目录（所有版本的DLL文件）
   - `packages/` 目录（NuGet包）

### 创建安装包（可选）
使用工具如：
- Inno Setup
- NSIS
- WiX Toolset

创建安装程序，包含：
- 主程序文件
- DLL文件
- .NET Framework 4.8安装检查
- 快捷方式
- 卸载程序

## 配置文件

编辑 `App.config` 配置服务器地址：
```xml
<appSettings>
  <add key="ServerUrl" value="http://localhost:8000" />
  <add key="WebSocketUrl" value="ws://localhost:8000/ws" />
  <add key="CallbackPort" value="6060" />
</appSettings>
```

## 注意事项

1. **必须以管理员权限运行**：Hook注入需要管理员权限
2. **服务器连接**：确保服务器已启动并可以访问
3. **防火墙设置**：可能需要允许程序通过防火墙

