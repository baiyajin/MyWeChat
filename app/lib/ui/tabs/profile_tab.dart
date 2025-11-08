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
}
