import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';
import 'package:provider/provider.dart';
import 'ui/pages/home_page.dart';
import 'ui/pages/login_page.dart';
import 'ui/pages/settings_page.dart';
import 'ui/pages/about_page.dart';
import 'ui/pages/collections_page.dart';
import 'services/websocket_service.dart';
import 'services/api_service.dart';

import 'platform/platform_stub.dart'
    if (dart.library.html) 'platform/platform_web.dart';

import 'platform/window_manager_stub.dart'
    if (dart.library.html) 'platform/window_manager_stub.dart'
    if (dart.library.io) 'package:window_manager/window_manager.dart';

String? _appUrl;
String? _webSocketUrl;
bool _linksDisplayed = false;

void main() {
  if (!kIsWeb && defaultTargetPlatform == TargetPlatform.windows) {
    WidgetsFlutterBinding.ensureInitialized();
    _setWindowSizeAsync();
  }
  
  runApp(const MyWeChatApp());
  
  if (kIsWeb) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _collectAccessUrl();
    });
  }
}

void _setWindowSizeAsync() {
  _setWindowSize().catchError((_) {});
}

Future<void> _setWindowSize() async {
  const double width = 393.0;
  const double height = 852.0;
  
  try {
    await windowManager.ensureInitialized();
    
    const windowOptions = WindowOptions(
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
  } catch (_) {}
}

void _collectAccessUrl() {
  _appUrl = getCurrentUrl();
}

void setWebSocketUrl(String url) {
  _webSocketUrl = url;
  _displayAllLinks();
}

void _displayAllLinks() {
  if (_linksDisplayed) return;
  
  if (_appUrl == null && kIsWeb) {
    _collectAccessUrl();
  }
  
  if (_webSocketUrl == null) {
    Future.delayed(const Duration(milliseconds: 500), () {
      if (!_linksDisplayed) _displayAllLinks();
    });
    return;
  }
  
  _linksDisplayed = true;
}

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
          primaryColor: const Color(0xFF07C160),
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

class _AuthWrapper extends StatefulWidget {
  const _AuthWrapper();

  @override
  State<_AuthWrapper> createState() => _AuthWrapperState();
}

class _AuthWrapperState extends State<_AuthWrapper> {
  static const _webCheckDelay = Duration(milliseconds: 100);
  static const _webConnectDelay = Duration(milliseconds: 200);
  static const _webWsTimeout = Duration(seconds: 1);
  static const _webWsTotalTimeout = Duration(seconds: 2);
  static const _webLoadTimeout = Duration(seconds: 1);
  static const _webQuickLoginTimeout = Duration(seconds: 2);
  
  static const _nonWebWsTimeout = Duration(seconds: 5);
  static const _nonWebWsTotalTimeout = Duration(seconds: 6);
  static const _nonWebLoadTimeout = Duration(seconds: 2);
  static const _nonWebQuickLoginTimeout = Duration(seconds: 5);

  bool _isLoading = !kIsWeb;
  bool _isLoggedIn = false;

  @override
  void initState() {
    super.initState();
    if (kIsWeb) {
      Future.delayed(_webCheckDelay, () {
        if (mounted) {
          _checkLoginState();
        }
      });
    } else {
      Future.microtask(() => _checkLoginState());
    }
  }

  Future<void> _checkLoginState() async {
    if (!mounted) return;
    
    try {
      if (kIsWeb) {
        Future.delayed(_webConnectDelay, () async {
          if (!mounted) return;
          try {
            final wsService = Provider.of<WebSocketService>(context, listen: false);
            final apiService = Provider.of<ApiService>(context, listen: false);
            await _connectWebSocket(
              wsService,
              apiService,
              _webWsTimeout,
              _webWsTotalTimeout,
            );
          } catch (_) {
            _setLoginState(false, false);
          }
        });
      } else {
        final wsService = Provider.of<WebSocketService>(context, listen: false);
        final apiService = Provider.of<ApiService>(context, listen: false);
        await _connectWebSocket(
          wsService,
          apiService,
          _nonWebWsTimeout,
          _nonWebWsTotalTimeout,
        );
      }
    } catch (_) {
      _setLoginState(false, false);
    }
  }
  
  Future<void> _connectWebSocket(
    WebSocketService wsService,
    ApiService apiService,
    Duration wsTimeout,
    Duration wsTotalTimeout,
  ) async {
    try {
      if (!wsService.isConnected) {
        String wsUrl = apiService.serverUrl
            .replaceFirst('http://', 'ws://')
            .replaceFirst('https://', 'wss://');
        if (!wsUrl.endsWith('/ws')) {
          wsUrl = wsUrl.endsWith('/') ? '${wsUrl}ws' : '$wsUrl/ws';
        }
        
        try {
          await wsService
              .connect(wsUrl, timeout: wsTimeout)
              .timeout(wsTotalTimeout, onTimeout: () => false);
        } catch (_) {}
      }
      
      final loadTimeout = kIsWeb ? _webLoadTimeout : _nonWebLoadTimeout;
      String? wxid;
      try {
        wxid = await wsService
            .loadLoginState()
            .timeout(loadTimeout, onTimeout: () => null);
      } catch (_) {
        wxid = null;
      }
      
      if (wxid != null && wxid.isNotEmpty) {
        final quickLoginTimeout =
            kIsWeb ? _webQuickLoginTimeout : _nonWebQuickLoginTimeout;
        try {
          final success = await wsService
              .quickLogin(wxid)
              .timeout(quickLoginTimeout, onTimeout: () => false);
          
          if (success && wsService.myInfo != null) {
            _setLoginState(true, false);
            return;
          }
        } catch (_) {}
      }
      
      _setLoginState(false, false);
    } catch (_) {
      _setLoginState(false, false);
    }
  }

  void _setLoginState(bool isLoggedIn, bool isLoading) {
    if (mounted) {
      setState(() {
        _isLoggedIn = isLoggedIn;
        _isLoading = isLoading;
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


