import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../tabs/contacts_tab.dart';
import '../tabs/moments_tab.dart';
import '../tabs/chat_tab.dart';
import '../tabs/profile_tab.dart';
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
    const ContactsTab(),
    const MomentsTab(),
    const ChatTab(),
    const ProfileTab(),
  ];

  @override
  void initState() {
    super.initState();
    // 延迟初始化，确保Provider已经准备好
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _initializeWebSocket();
    });
  }

  /// 初始化WebSocket连接
  Future<void> _initializeWebSocket() async {
    if (_isInitialized) return;
    
    final wsService = Provider.of<WebSocketService>(context, listen: false);
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
          
          // 点击tabbar时，请求同步相应的数据
          final wsService = Provider.of<WebSocketService>(context, listen: false);
          if (wsService.isConnected) {
            switch (index) {
              case 0: // 好友
                wsService.requestSyncContacts();
                break;
              case 1: // 朋友圈
                wsService.requestSyncMoments();
                break;
              case 2: // 聊天（使用好友列表数据）
                wsService.requestSyncContacts();
                break;
              // case 3: // 我的（不需要同步）
            }
          }
        },
        type: BottomNavigationBarType.fixed,
        selectedItemColor: const Color(0xFF07C160), // 微信绿色
        unselectedItemColor: Colors.grey,
        items: const [
          BottomNavigationBarItem(
            icon: Icon(Icons.contacts),
            label: '好友',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.photo_library),
            label: '朋友圈',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.chat),
            label: '聊天',
          ),
          BottomNavigationBarItem(
            icon: Icon(Icons.person),
            label: '我的',
          ),
        ],
      ),
    );
  }
}

