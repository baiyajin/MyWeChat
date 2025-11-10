import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';
import 'package:provider/provider.dart';
import 'package:window_manager/window_manager.dart';
import 'ui/pages/home_page.dart';
import 'ui/pages/login_page.dart';
import 'ui/pages/settings_page.dart';
import 'ui/pages/about_page.dart';
import 'ui/pages/collections_page.dart';
import 'services/websocket_service.dart';
import 'services/api_service.dart';

// 条件导入：根据平台选择不同的实现
import 'platform/platform_stub.dart'
    if (dart.library.html) 'platform/platform_web.dart';

// 全局变量，用于存储链接信息
String? _appUrl;
String? _webSocketUrl;
bool _linksDisplayed = false; // 标记是否已显示链接

void main() async {
  // 收集访问链接（仅 Web 平台）
  if (kIsWeb) {
    _collectAccessUrl();
  }
  
  // 设置窗口大小（仅 Windows 平台）
  if (!kIsWeb && defaultTargetPlatform == TargetPlatform.windows) {
    WidgetsFlutterBinding.ensureInitialized();
    await _setWindowSize();
  }
  
  runApp(const MyWeChatApp());
}

/// 设置窗口大小为 iPhone 15 Pro 尺寸（仅 Windows 平台）
Future<void> _setWindowSize() async {
  // iPhone 15 Pro: 393 x 852 (逻辑分辨率 points)
  // 物理分辨率: 2556 x 1179 pixels (@3x 缩放)
  const double width = 393.0;
  const double height = 852.0;
  
  try {
    // 使用 window_manager 设置窗口大小（仅 Windows 平台）
    await windowManager.ensureInitialized();
    
    WindowOptions windowOptions = const WindowOptions(
      size: Size(width, height),
      minimumSize: Size(width, height),
      maximumSize: Size(width, height),
      center: true,
      backgroundColor: Colors.transparent,
      skipTaskbar: false,
      titleBarStyle: TitleBarStyle.normal,
    );
    
    windowManager.waitUntilReadyToShow(windowOptions, () async {
      await windowManager.show();
      await windowManager.focus();
    });
  } catch (e) {
    print('无法设置窗口大小: $e');
  }
}

/// 收集访问链接
void _collectAccessUrl() {
  // 使用平台特定的实现
  _appUrl = getCurrentUrl();
}

/// 设置WebSocket链接
void setWebSocketUrl(String url) {
  _webSocketUrl = url;
  _displayAllLinks();
}

/// 显示所有链接信息（只显示一次）
void _displayAllLinks() {
  // 防止重复显示
  if (_linksDisplayed) {
    return;
  }
  
  // 确保应用链接已收集
  if (_appUrl == null && kIsWeb) {
    _collectAccessUrl();
  }
  
  // 如果 WebSocket 链接还未设置，延迟显示
  if (_webSocketUrl == null) {
    Future.delayed(const Duration(milliseconds: 500), () {
      if (!_linksDisplayed) {
        _displayAllLinks();
      }
    });
    return;
  }
  
  _linksDisplayed = true;
  
  print('');
  print('═══════════════════════════════════════════════════════════');
  print('  🚀 Flutter Web 应用已启动 - 所有链接信息');
  print('═══════════════════════════════════════════════════════════');
  print('');
  
  if (_appUrl != null) {
    print('  📱 应用访问链接:');
    print('     $_appUrl');
    print('');
  }
  
  if (_webSocketUrl != null) {
    print('  📡 WebSocket 服务器:');
    print('     $_webSocketUrl');
    print('');
  }
  
  print('  💡 提示: 如果浏览器未自动打开，请手动访问上述链接');
  print('');
  print('═══════════════════════════════════════════════════════════');
  print('');
}

/// MyWeChat应用主类
class MyWeChatApp extends StatelessWidget {
  const MyWeChatApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => WebSocketService()),
        ChangeNotifierProvider(create: (_) => ApiService()),
      ],
      child: MaterialApp(
        title: 'MyWeChat',
        theme: ThemeData(
          primaryColor: const Color(0xFF07C160), // 微信绿色
          colorScheme: ColorScheme.fromSeed(
            seedColor: const Color(0xFF07C160),
            brightness: Brightness.light,
          ),
          useMaterial3: true,
        ),
        home: const _AuthWrapper(),
        debugShowCheckedModeBanner: false,
        routes: {
          '/settings': (context) => const SettingsPage(),
          '/about': (context) => const AboutPage(),
          '/collections': (context) => const CollectionsPage(),
        },
      ),
    );
  }
}

/// 登录状态检查包装器
class _AuthWrapper extends StatefulWidget {
  const _AuthWrapper();

  @override
  State<_AuthWrapper> createState() => _AuthWrapperState();
}

class _AuthWrapperState extends State<_AuthWrapper> {
  bool _isLoading = true;
  bool _isLoggedIn = false;

  @override
  void initState() {
    super.initState();
    _checkLoginState();
  }

  /// 检查登录状态
  Future<void> _checkLoginState() async {
    try {
      final wsService = Provider.of<WebSocketService>(context, listen: false);
      final apiService = Provider.of<ApiService>(context, listen: false);
      
      // 先建立WebSocket连接（无论是否登录都需要），使用超时
      if (!wsService.isConnected) {
        // 将HTTP URL转换为WebSocket URL
        String wsUrl = apiService.serverUrl.replaceFirst('http://', 'ws://').replaceFirst('https://', 'wss://');
        if (!wsUrl.endsWith('/ws')) {
          wsUrl = wsUrl.endsWith('/') ? '${wsUrl}ws' : '$wsUrl/ws';
        }
        
        print('正在建立WebSocket连接: $wsUrl');
        // 使用超时连接，最多等待5秒
        final connected = await wsService.connect(wsUrl, timeout: const Duration(seconds: 5))
            .timeout(
              const Duration(seconds: 6),
              onTimeout: () {
                print('WebSocket连接超时，继续启动应用');
                return false;
              },
            );
        if (!connected) {
          print('WebSocket连接失败或超时，将显示登录页面');
          // 即使连接失败，也继续检查登录状态，允许用户使用登录页面
        }
      } else {
        print('WebSocket已连接，跳过重复连接');
      }
      
      // 检查登录状态（使用超时）
      String? wxid;
      try {
        wxid = await wsService.loadLoginState()
            .timeout(
              const Duration(seconds: 2),
              onTimeout: () {
                print('加载登录状态超时');
                return null;
              },
            );
      } catch (e) {
        print('加载登录状态失败: $e');
        wxid = null;
      }
      
      if (wxid != null && wxid.isNotEmpty) {
        // 已登录，尝试快速登录（使用超时）
        try {
          final success = await wsService.quickLogin(wxid)
              .timeout(
                const Duration(seconds: 5),
                onTimeout: () {
                  print('快速登录超时');
                  return false;
                },
              );
          if (success && wsService.myInfo != null) {
            setState(() {
              _isLoggedIn = true;
              _isLoading = false;
            });
            return;
          }
        } catch (e) {
          print('快速登录失败: $e');
        }
      }
      
      // 未登录或登录失败
      setState(() {
        _isLoggedIn = false;
        _isLoading = false;
      });
    } catch (e) {
      print('检查登录状态失败: $e');
      setState(() {
        _isLoggedIn = false;
        _isLoading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_isLoading) {
      return const Scaffold(
        body: Center(
          child: CircularProgressIndicator(),
        ),
      );
    }
    
    return _isLoggedIn ? const HomePage() : const LoginPage();
  }
}

