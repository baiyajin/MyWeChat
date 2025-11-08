import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';
import 'package:provider/provider.dart';
import 'ui/pages/home_page.dart';
import 'ui/pages/settings_page.dart';
import 'ui/pages/about_page.dart';
import 'ui/pages/collections_page.dart';
import 'services/websocket_service.dart';
import 'services/api_service.dart';
import 'dart:html' as html;

// 全局变量，用于存储链接信息
String? _appUrl;
String? _webSocketUrl;
bool _linksDisplayed = false; // 标记是否已显示链接

void main() {
  // 收集访问链接（仅 Web 平台）
  if (kIsWeb) {
    _collectAccessUrl();
  }
  
  runApp(const MyWeChatApp());
}

/// 收集访问链接
void _collectAccessUrl() {
  try {
    final uri = html.window.location;
    
    // 直接使用 uri.href，然后修复端口重复问题
    String href = uri.href ?? '';
    
    // 修复端口重复问题：匹配 localhost:端口:端口 的模式
    // 例如：http://localhost:57625:57625 -> http://localhost:57625
    if (href.contains('://')) {
      // 使用正则表达式修复重复的端口号
      _appUrl = href.replaceAll(RegExp(r':(\d+):\1(?=/|$)'), ':\$1');
      
      // 如果仍有重复（更复杂的情况），使用更通用的方法
      final parts = _appUrl!.split(':');
      if (parts.length > 3) {
        // 有重复端口，只保留第一个
        final protocol = parts[0];
        final host = parts[1].replaceAll('//', '');
        final port = parts[2];
        final rest = parts.sublist(3).join(':');
        _appUrl = '$protocol://$host:$port$rest';
      }
    } else {
      // 如果 href 为空，手动构建
      String port = '';
      final portNum = uri.port;
      if (portNum != 0 && portNum != 80 && portNum != 443) {
        port = ':${portNum}';
      }
      
      String pathname = uri.pathname ?? '';
      if (pathname.isEmpty || pathname == '/') {
        pathname = '';
      }
      
      _appUrl = '${uri.protocol}//${uri.host}$port$pathname';
    }
  } catch (e) {
    print('无法获取访问链接: $e');
  }
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
        home: const HomePage(),
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

