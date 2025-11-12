import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../tabs/contacts_tab.dart';
import '../tabs/moments_tab.dart';
import '../tabs/chat_tab.dart';
import '../tabs/profile_tab.dart';
import '../tabs/official_account_tab.dart';
import '../../services/websocket_service.dart';
import '../../services/api_service.dart';

/// 主页
class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  int _currentIndex = 0;
  bool _isInitialized = false;

  final List<Widget> _tabs = [
    const ChatTab(),              // 微信（第一个）
    const ContactsTab(),          // 通讯录（第二个）
    const MomentsTab(),            // 发现（第三个）
    const OfficialAccountTab(),   // 公众号（第四个）
    const ProfileTab(),           // 我（第五个）
  ];

  @override
  void initState() {
    super.initState();
    // 延迟初始化，确保Provider已经准备好
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _initializeWebSocket();
    });
  }

  /// 通过WebSocket请求同步联系人数据
  void _requestSyncContacts(WebSocketService wsService) {
    try {
      final weChatId = wsService.currentWeChatId;
      if (weChatId != null && weChatId.isNotEmpty) {
        wsService.requestSyncContacts(weChatId);
      } else {
        print('无法请求同步联系人数据，微信账号ID为空');
      }
    } catch (e) {
      print('请求同步联系人数据失败: $e');
    }
  }

  /// 通过WebSocket请求同步朋友圈数据
  void _requestSyncMoments(WebSocketService wsService) {
    try {
      final weChatId = wsService.currentWeChatId;
      if (weChatId != null && weChatId.isNotEmpty) {
        wsService.requestSyncMoments(weChatId);
      } else {
        print('无法请求同步朋友圈数据，微信账号ID为空');
      }
    } catch (e) {
      print('请求同步朋友圈数据失败: $e');
    }
  }

  /// 从服务器加载账号信息
  Future<void> _loadAccountInfoFromServer(ApiService apiService, WebSocketService wsService) async {
    try {
      // 优先使用当前登录的wxid获取账号信息
      final currentWxid = wsService.currentWeChatId;
      Map<String, dynamic>? accountInfo;
      
      if (currentWxid != null && currentWxid.isNotEmpty) {
        accountInfo = await apiService.getAccountInfo(wxid: currentWxid);
      }
      
      // 如果通过wxid获取失败，尝试获取最新的账号信息（兼容旧逻辑）
      if (accountInfo == null) {
        accountInfo = await apiService.getAccountInfo();
      }
      
      if (accountInfo != null) {
        // 更新WebSocketService中的账号信息
        wsService.updateMyInfo(accountInfo);
      }
    } catch (e) {
      print('从服务器加载账号信息失败: $e');
    }
  }

  /// 初始化WebSocket连接
  Future<void> _initializeWebSocket() async {
    if (_isInitialized) return;
    
    final wsService = Provider.of<WebSocketService>(context, listen: false);
    
    // 如果WebSocket已连接，跳过重复连接
    if (wsService.isConnected) {
      print('WebSocket已连接，跳过重复连接');
      _isInitialized = true;
      return;
    }
    
    final apiService = Provider.of<ApiService>(context, listen: false);
    
    // 将HTTP URL转换为WebSocket URL
    String wsUrl = apiService.serverUrl.replaceFirst('http://', 'ws://').replaceFirst('https://', 'wss://');
    if (!wsUrl.endsWith('/ws')) {
      wsUrl = wsUrl.endsWith('/') ? '${wsUrl}ws' : '$wsUrl/ws';
    }
    
    // 不在这里打印，统一在链接汇总中显示
    await wsService.connect(wsUrl);
    _isInitialized = true;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: _tabs[_currentIndex],
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: _currentIndex,
        onTap: (index) {
          setState(() {
            _currentIndex = index;
          });
          
          // 点击tabbar时，通过WebSocket实时同步数据（除了"我"tab从数据库获取）
          final wsService = Provider.of<WebSocketService>(context, listen: false);
          final apiService = Provider.of<ApiService>(context, listen: false);
          switch (index) {
            case 0: // 微信（聊天）- 通过WebSocket实时同步联系人
              _requestSyncContacts(wsService);
              break;
            case 1: // 通讯录（好友）- 通过WebSocket实时同步联系人
              _requestSyncContacts(wsService);
              break;
            case 2: // 发现（朋友圈）- 通过WebSocket实时同步朋友圈
              _requestSyncMoments(wsService);
              break;
            case 3: // 公众号 - 不需要主动请求，消息会自动同步
              break;
            case 4: // 我（从数据库获取账号信息）
              _loadAccountInfoFromServer(apiService, wsService);
              break;
          }
        },
        type: BottomNavigationBarType.fixed,
        selectedItemColor: const Color(0xFF07C160), // 微信绿色
        unselectedItemColor: Colors.grey,
        items: [
          const BottomNavigationBarItem(
            icon: Icon(Icons.chat_bubble_outline),
            activeIcon: Icon(Icons.chat_bubble),
            label: '微信',
          ),
          const BottomNavigationBarItem(
            icon: Icon(Icons.contacts_outlined),
            activeIcon: Icon(Icons.contacts),
            label: '通讯录',
          ),
          const BottomNavigationBarItem(
            icon: Icon(Icons.explore_outlined),
            activeIcon: Icon(Icons.explore),
            label: '发现',
          ),
          const BottomNavigationBarItem(
            icon: Icon(Icons.article_outlined),
            activeIcon: Icon(Icons.article),
            label: '公众号',
          ),
          const BottomNavigationBarItem(
            icon: Icon(Icons.person_outline),
            activeIcon: Icon(Icons.person),
            label: '我',
          ),
        ],
      ),
    );
  }
}

