# App端 - MyWeChat移动应用

## 项目概述
App端使用Flutter开发，从服务器获取数据并展示，发送操作命令到服务器。

## 技术栈
- Flutter
- Dart
- WebSocket客户端
- HTTP客户端

## 主要功能
1. 展示好友列表
2. 展示朋友圈
3. 聊天功能（发送消息、文件）
4. 点赞朋友圈
5. 评论朋友圈
6. 发布朋友圈

## 项目结构
```
app/
├── lib/
│   ├── models/                  # 数据模型
│   ├── services/                # 业务服务
│   ├── ui/                      # UI界面
│   └── utils/                   # 工具类
└── assets/                      # 资源文件
```

## 运行要求
- Flutter SDK 3.0+
- Android Studio / Xcode
- Android 5.0+ / iOS 11.0+

## 安装依赖

### 方法1：使用批处理文件（推荐）
```bash
双击运行：安装依赖.bat
```

### 方法2：手动安装
```bash
cd app
flutter pub get
```

## 启动应用

### 方法1：使用批处理文件（推荐）
```bash
双击运行：启动应用.bat
```

### 方法2：手动启动
```bash
cd app
flutter run
```

### 指定设备运行
```bash
# 查看可用设备
flutter devices

# 指定设备运行
flutter run -d <device_id>
```

## 打包应用

### Android打包

#### 1. 生成签名密钥
```bash
keytool -genkey -v -keystore ~/upload-keystore.jks -keyalg RSA -keysize 2048 -validity 10000 -alias upload
```

#### 2. 配置签名
在 `android/app/build.gradle` 中配置签名信息：
```gradle
android {
    ...
    signingConfigs {
        release {
            keyAlias upload
            keyPassword <your-key-password>
            storeFile <path-to-keystore-file>
            storePassword <your-store-password>
        }
    }
    buildTypes {
        release {
            signingConfig signingConfigs.release
        }
    }
}
```

#### 3. 构建APK
```bash
cd app
flutter build apk --release
```

#### 4. 构建App Bundle（用于Google Play）
```bash
flutter build appbundle --release
```

输出文件：
- APK: `app/build/app/outputs/flutter-apk/app-release.apk`
- App Bundle: `app/build/app/outputs/bundle/release/app-release.aab`

### iOS打包

#### 1. 配置证书和描述文件
在Xcode中配置签名证书和描述文件。

#### 2. 构建IPA
```bash
cd app
flutter build ios --release
```

#### 3. 使用Xcode打包
1. 打开 `app/ios/Runner.xcworkspace`
2. 选择 Product > Archive
3. 选择 Distribute App
4. 选择分发方式（App Store、Ad Hoc、Enterprise等）

输出文件：
- IPA: 通过Xcode Organizer导出

### Windows打包（如果支持）
```bash
cd app
flutter build windows --release
```

输出文件：
- 可执行文件：`app/build/windows/runner/Release/`

### macOS打包（如果支持）
```bash
cd app
flutter build macos --release
```

输出文件：
- 应用程序：`app/build/macos/Build/Products/Release/`

## 开发调试

### 热重载
运行应用后，按 `r` 键进行热重载，按 `R` 键进行热重启。

### 查看日志
```bash
flutter logs
```

### 清理构建
```bash
flutter clean
flutter pub get
```

