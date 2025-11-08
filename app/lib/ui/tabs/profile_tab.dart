import 'dart:async';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:cached_network_image/cached_network_image.dart';
import '../../services/websocket_service.dart';
import '../../services/api_service.dart';

/// 我的Tab - 模仿微信App的"我"页面UI
class ProfileTab extends StatefulWidget {
  const ProfileTab({super.key});

  @override
  State<ProfileTab> createState() => _ProfileTabState();
}

class _ProfileTabState extends State<ProfileTab> {
  Timer? _statusTimer;
  Map<String, dynamic>? _systemStatus;

  @override
  void initState() {
    super.initState();
    _startStatusTimer();
    _refreshStatus();
  }

  @override
  void dispose() {
    _statusTimer?.cancel();
    super.dispose();
  }

  void _startStatusTimer() {
    _statusTimer = Timer.periodic(const Duration(seconds: 3), (timer) {
      _refreshStatus();
    });
  }

  Future<void> _refreshStatus() async {
    final apiService = Provider.of<ApiService>(context, listen: false);
    final status = await apiService.getStatus();
    if (mounted) {
      setState(() {
        _systemStatus = status;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFEDEDED),
      body: Consumer2<WebSocketService, ApiService>(
        builder: (context, wsService, apiService, child) {
          final myInfo = wsService.myInfo;
          final hasUserData = myInfo != null && 
                              (myInfo['nickname'] != null || myInfo['wxid'] != null);
          
          return ListView(
            children: [
              // 顶部用户信息区域 - 模仿微信的"我"页面顶部
              Container(
                color: Colors.white,
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
                child: Row(
                  children: [
                    // 头像
                    _buildAvatar(myInfo?['avatar']?.toString()),
                    const SizedBox(width: 16),
                    // 昵称和微信号
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          // 昵称或项目名称
                          Text(
                            hasUserData 
                                ? (myInfo?['nickname']?.toString() ?? '未知昵称')
                                : 'MyWeChat',
                            style: const TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.w500,
                              color: Colors.black87,
                            ),
                          ),
                          const SizedBox(height: 8),
                          // 微信号或项目描述
                          Row(
                            children: [
                              Text(
                                hasUserData
                                    ? '微信号: ${myInfo?['wxid']?.toString() ?? myInfo?['account']?.toString() ?? '未知'}'
                                    : '微信数据同步工具',
                                style: TextStyle(
                                  fontSize: 14,
                                  color: Colors.grey[600],
                                ),
                              ),
                              if (hasUserData) ...[
                                const SizedBox(width: 8),
                                Icon(
                                  Icons.qr_code_2,
                                  size: 16,
                                  color: Colors.grey[600],
                                ),
                              ],
                            ],
                          ),
                        ],
                      ),
                    ),
                    // 右箭头
                    Icon(
                      Icons.chevron_right,
                      color: Colors.grey[400],
                    ),
                  ],
                ),
              ),
              
              const SizedBox(height: 10),
              
              // 功能入口区域 - 只保留收藏
              Container(
                color: Colors.white,
                child: _buildMenuItem(
                  icon: Icons.collections,
                  title: '收藏',
                  onTap: () {
                    Navigator.pushNamed(context, '/collections');
                  },
                ),
              ),
              
              const SizedBox(height: 10),
              
              // 设置入口
              Container(
                color: Colors.white,
                child: _buildMenuItem(
                  icon: Icons.settings,
                  title: '设置',
                  onTap: () {
                    Navigator.pushNamed(context, '/settings');
                  },
                ),
              ),
              
              const SizedBox(height: 10),
              
              // 关于入口
              Container(
                color: Colors.white,
                child: _buildMenuItem(
                  icon: Icons.info_outline,
                  title: '关于',
                  onTap: () {
                    Navigator.pushNamed(context, '/about');
                  },
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  /// 构建头像
  Widget _buildAvatar(String? avatarUrl) {
    final hasUserData = avatarUrl != null && avatarUrl.isNotEmpty;
    
    if (hasUserData) {
      // 有用户数据，显示头像
      return ClipOval(
        child: CachedNetworkImage(
          imageUrl: avatarUrl,
          width: 60,
          height: 60,
          fit: BoxFit.cover,
          placeholder: (context, url) => Container(
            width: 60,
            height: 60,
            color: Colors.grey[200],
            child: const Center(
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
          ),
          errorWidget: (context, url, error) => Container(
            width: 60,
            height: 60,
            decoration: BoxDecoration(
              color: const Color(0xFF07C160),
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.person,
              color: Colors.white,
              size: 30,
            ),
          ),
        ),
      );
    } else {
      // 没有用户数据，显示logo
      return Container(
        width: 60,
        height: 60,
        decoration: BoxDecoration(
          color: const Color(0xFF07C160),
          shape: BoxShape.circle,
        ),
        child: Image.asset(
          'assets/images/logo.png',
          width: 40,
          height: 40,
          errorBuilder: (context, error, stackTrace) {
            // 如果logo图片不存在，使用图标作为fallback
            return const Icon(
              Icons.chat_bubble,
              color: Colors.white,
              size: 30,
            );
          },
        ),
      );
    }
  }

  /// 构建菜单项
  Widget _buildMenuItem({
    required IconData icon,
    required String title,
    required VoidCallback onTap,
  }) {
    return InkWell(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        child: Row(
          children: [
            Icon(
              icon,
              size: 24,
              color: Colors.black87,
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Text(
                title,
                style: const TextStyle(
                  fontSize: 16,
                  color: Colors.black87,
                ),
              ),
            ),
            Icon(
              Icons.chevron_right,
              color: Colors.grey[400],
            ),
          ],
        ),
      ),
    );
  }

  /// 构建分割线
  Widget _buildDivider() {
    return Divider(
      height: 1,
      thickness: 0.5,
      indent: 56, // 与图标对齐
      color: Colors.grey[200],
    );
  }

  /// 显示设置对话框
  void _showSettingsDialog(
    BuildContext context,
    ApiService apiService,
    WebSocketService wsService,
  ) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('设置'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                '服务器配置',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                apiService.serverUrl,
                style: TextStyle(
                  fontSize: 14,
                  color: Colors.grey[600],
                ),
              ),
              const SizedBox(height: 16),
              ElevatedButton(
                onPressed: () {
                  Navigator.pop(context);
                  _showServerConfigDialog(context, apiService, wsService);
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF07C160),
                  foregroundColor: Colors.white,
                ),
                child: const Text('修改服务器地址'),
              ),
              const SizedBox(height: 16),
              const Text(
                '系统状态',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 8),
              _buildStatusItem(
                '服务器',
                _systemStatus?['server']?['status'] == 'running',
                _systemStatus?['server']?['version'] ?? '未知',
              ),
              const SizedBox(height: 8),
              _buildStatusItem(
                'Windows端',
                _systemStatus?['windows']?['status'] == 'connected',
                _systemStatus?['windows']?['connected_count'] != null
                    ? '连接数: ${_systemStatus?['windows']?['connected_count']}'
                    : '未连接',
              ),
              const SizedBox(height: 8),
              _buildStatusItem(
                'App端',
                _systemStatus?['app']?['status'] == 'connected',
                _systemStatus?['app']?['connected_count'] != null
                    ? '连接数: ${_systemStatus?['app']?['connected_count']}'
                    : '未连接',
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('关闭'),
          ),
        ],
      ),
    );
  }

  /// 构建状态项
  Widget _buildStatusItem(String title, bool isConnected, String detail) {
    return Row(
      children: [
        Icon(
          isConnected ? Icons.check_circle : Icons.cancel,
          color: isConnected ? Colors.green : Colors.red,
          size: 20,
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: const TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                ),
              ),
              Text(
                detail,
                style: TextStyle(
                  fontSize: 12,
                  color: Colors.grey[600],
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  /// 显示服务器配置对话框
  void _showServerConfigDialog(
    BuildContext context,
    ApiService apiService,
    WebSocketService wsService,
  ) {
    final TextEditingController controller = TextEditingController(text: apiService.serverUrl);

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('服务器配置'),
        content: TextField(
          controller: controller,
          decoration: const InputDecoration(
            labelText: '服务器地址',
            hintText: 'http://localhost:8000',
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('取消'),
          ),
          TextButton(
            onPressed: () async {
              final newUrl = controller.text.trim();
              if (newUrl.isNotEmpty) {
                // 更新API服务地址
                apiService.setServerUrl(newUrl);
                
                // 断开现有WebSocket连接
                wsService.disconnect();
                
                // 构建WebSocket地址
                String wsUrl = newUrl.replaceFirst('http://', 'ws://').replaceFirst('https://', 'wss://');
                if (!wsUrl.endsWith('/ws')) {
                  wsUrl = '$wsUrl/ws';
                }
                
                // 重新连接WebSocket
                await wsService.connect(wsUrl);
                
                // 刷新状态
                _refreshStatus();
                
                if (context.mounted) {
                  Navigator.pop(context);
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('服务器配置已更新，正在重新连接...')),
                  );
                }
              }
            },
            child: const Text('确定'),
          ),
        ],
      ),
    );
  }

  /// 显示关于对话框
  void _showAboutDialog(BuildContext context) {
    showAboutDialog(
      context: context,
      applicationName: 'MyWeChat',
      applicationVersion: '1.0.0',
      applicationIcon: Image.asset(
        'assets/images/logo.png',
        width: 64,
        height: 64,
        errorBuilder: (context, error, stackTrace) {
          // 如果logo图片不存在，使用图标作为fallback
          return Container(
            width: 64,
            height: 64,
            decoration: BoxDecoration(
              color: const Color(0xFF07C160),
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(
              Icons.chat_bubble,
              color: Colors.white,
              size: 40,
            ),
          );
        },
      ),
    );
  }
}
