import 'dart:async';
import 'dart:math' as math;
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

class _ProfileTabState extends State<ProfileTab> with SingleTickerProviderStateMixin {
  Timer? _statusTimer;
  Map<String, dynamic>? _systemStatus;
  late AnimationController _animationController;
  late Animation<double> _animation;

  @override
  void initState() {
    super.initState();
    _animationController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat();
    _animation = Tween<double>(begin: 0.0, end: 1.0).animate(
      CurvedAnimation(parent: _animationController, curve: Curves.linear),
    );
    _startStatusTimer();
    _refreshStatus();
  }

  @override
  void dispose() {
    _statusTimer?.cancel();
    _animationController.dispose();
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
              
              const SizedBox(height: 10),
              
              // 系统状态区域 - 展示三端关系和状态
              Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      '系统状态',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: Colors.black87,
                      ),
                    ),
                    const SizedBox(height: 16),
                    // 三端关系图（三角布局）
                    _buildSystemStatusDiagram(),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
  
  /// 构建系统状态关系图（三角布局）
  Widget _buildSystemStatusDiagram() {
    final serverRunning = _systemStatus?['server']?['status'] == 'running';
    final windowsConnected = _systemStatus?['windows']?['status'] == 'connected';
    final appConnected = _systemStatus?['app']?['status'] == 'connected';
    final windowsCount = _systemStatus?['windows']?['connected_count'] ?? 0;
    final appCount = _systemStatus?['app']?['connected_count'] ?? 0;
    final serverVersion = _systemStatus?['server']?['version'] ?? '未知';
    
    // 节点大小（统一）
    const double nodeSize = 100.0;
    const double nodeHeight = 120.0;
    const double diagramHeight = 280.0;
    const double iconSize = 32.0;
    const double iconCenterY = iconSize / 2; // 图标中心Y坐标（相对于节点顶部）
    
    return LayoutBuilder(
      builder: (context, constraints) {
        final screenWidth = constraints.maxWidth;
        final centerX = screenWidth / 2;
        final leftX = nodeSize / 2 + 16; // Windows节点中心X
        final rightX = screenWidth - nodeSize / 2 - 16; // App节点中心X
        
        // 计算图标中心位置
        // 服务器图标中心（顶部节点）
        final serverIconX = centerX;
        final serverIconY = iconCenterY;
        
        // Windows图标中心（左下节点）
        final windowsIconX = leftX;
        final windowsIconY = diagramHeight - nodeHeight + iconCenterY;
        
        // App图标中心（右下节点）
        final appIconX = rightX;
        final appIconY = diagramHeight - nodeHeight + iconCenterY;
        
        return SizedBox(
          width: double.infinity,
          height: diagramHeight,
          child: Stack(
            children: [
              // 连接线：服务器 -> Windows端
              Positioned.fill(
                child: AnimatedBuilder(
                  animation: _animation,
                  builder: (context, child) {
                    return CustomPaint(
                      painter: _ConnectionLinePainter(
                        startX: serverIconX,
                        startY: serverIconY,
                        endX: windowsIconX,
                        endY: windowsIconY,
                        isConnected: windowsConnected,
                        animationValue: _animation.value,
                      ),
                    );
                  },
                ),
              ),
              
              // 连接线：服务器 -> App端
              Positioned.fill(
                child: AnimatedBuilder(
                  animation: _animation,
                  builder: (context, child) {
                    return CustomPaint(
                      painter: _ConnectionLinePainter(
                        startX: serverIconX,
                        startY: serverIconY,
                        endX: appIconX,
                        endY: appIconY,
                        isConnected: appConnected,
                        animationValue: _animation.value,
                      ),
                    );
                  },
                ),
              ),
              
              // 服务器节点（上方）
              Positioned(
                top: 0,
                left: 0,
                right: 0,
                child: Center(
                  child: _buildStatusNode(
                    icon: Icons.dns,
                    title: '服务器',
                    status: serverRunning,
                    detail: serverVersion,
                    width: nodeSize,
                    height: nodeHeight,
                  ),
                ),
              ),
              
              // Windows端节点（左下）
              Positioned(
                bottom: 0,
                left: 16,
                child: _buildStatusNode(
                  icon: Icons.computer,
                  title: 'Windows端',
                  status: windowsConnected,
                  detail: windowsConnected ? '$windowsCount 个连接' : '未连接',
                  width: nodeSize,
                  height: nodeHeight,
                ),
              ),
              
              // App端节点（右下）
              Positioned(
                bottom: 0,
                right: 16,
                child: _buildStatusNode(
                  icon: Icons.phone_android,
                  title: 'App端',
                  status: appConnected,
                  detail: appConnected ? '$appCount 个连接' : '未连接',
                  width: nodeSize,
                  height: nodeHeight,
                ),
              ),
            ],
          ),
        );
      },
    );
  }
  
  /// 构建状态节点
  Widget _buildStatusNode({
    required IconData icon,
    required String title,
    required bool status,
    required String detail,
    required double width,
    required double height,
  }) {
    return SizedBox(
      width: width,
      height: height,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          // 图标和状态指示
          Stack(
            alignment: Alignment.center,
            children: [
              Icon(
                icon,
                size: 32,
                color: status ? Colors.green[700] : Colors.grey[600],
              ),
              Positioned(
                right: 0,
                top: 0,
                child: Container(
                  width: 12,
                  height: 12,
                  decoration: BoxDecoration(
                    color: status ? Colors.green : Colors.red,
                    shape: BoxShape.circle,
                    border: Border.all(
                      color: Colors.white,
                      width: 2,
                    ),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          // 标题
          Text(
            title,
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.bold,
              color: status ? Colors.green[700] : Colors.grey[700],
            ),
            textAlign: TextAlign.center,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
          const SizedBox(height: 4),
          // 详情
          Expanded(
            child: Text(
              detail,
              style: TextStyle(
                fontSize: 11,
                color: Colors.grey[600],
              ),
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
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

/// 连接线绘制器
class _ConnectionLinePainter extends CustomPainter {
  final double startX;
  final double startY;
  final double endX;
  final double endY;
  final bool isConnected;
  final double animationValue;
  
  _ConnectionLinePainter({
    required this.startX,
    required this.startY,
    required this.endX,
    required this.endY,
    required this.isConnected,
    required this.animationValue,
  });
  
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = isConnected ? Colors.green : Colors.red
      ..strokeWidth = 2.0
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;
    
    // 绘制主连接线
    canvas.drawLine(
      Offset(startX, startY),
      Offset(endX, endY),
      paint,
    );
    
    // 绘制动画效果（流动的光点）
    if (isConnected) {
      // 计算光点位置
      final dx = endX - startX;
      final dy = endY - startY;
      final distance = math.sqrt(dx * dx + dy * dy);
      final animatedDistance = distance * animationValue;
      final ratio = animatedDistance / distance;
      
      final animatedX = startX + dx * ratio;
      final animatedY = startY + dy * ratio;
      
      // 绘制光晕效果（外层）
      final glowPaint = Paint()
        ..color = Colors.green.withOpacity(0.3)
        ..style = PaintingStyle.fill;
      canvas.drawCircle(
        Offset(animatedX, animatedY),
        8.0,
        glowPaint,
      );
      
      // 绘制流动的光点（内层）
      final animatedPaint = Paint()
        ..color = Colors.green
        ..style = PaintingStyle.fill;
      canvas.drawCircle(
        Offset(animatedX, animatedY),
        4.0,
        animatedPaint,
      );
    }
  }
  
  @override
  bool shouldRepaint(_ConnectionLinePainter oldDelegate) {
    return oldDelegate.isConnected != isConnected ||
        oldDelegate.animationValue != animationValue ||
        oldDelegate.startX != startX ||
        oldDelegate.startY != startY ||
        oldDelegate.endX != endX ||
        oldDelegate.endY != endY;
  }
}
