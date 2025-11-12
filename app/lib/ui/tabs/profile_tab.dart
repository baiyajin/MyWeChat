import 'dart:async';
import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../../services/websocket_service.dart';
import '../../services/api_service.dart';
import '../pages/license_manage_page.dart';
import '../pages/account_list_page.dart';

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
  bool _hasManagePermission = false;

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
    _loadManagePermission();
  }

  Future<void> _loadManagePermission() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final hasPermission = prefs.getBool('has_manage_permission') ?? false;
      setState(() {
        _hasManagePermission = hasPermission;
      });
    } catch (e) {
      print('加载管理权限失败: $e');
    }
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
              
              // 授权码管理入口（仅管理员可见，在关于下方）
              if (_hasManagePermission) ...[
                const SizedBox(height: 10),
                Container(
                  color: Colors.white,
                  child: _buildMenuItem(
                    icon: Icons.vpn_key,
                    title: '授权码管理',
                    onTap: () {
                      Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (context) => const LicenseManagePage(),
                        ),
                      );
                    },
                  ),
                ),
              ],
              
              // 切换账号入口（在授权码管理下方）
              if (hasUserData) ...[
                const SizedBox(height: 10),
                Container(
                  color: Colors.white,
                  child: _buildMenuItem(
                    icon: Icons.swap_horiz,
                    title: '切换账号',
                    onTap: () {
                      Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (context) => const AccountListPage(),
                        ),
                      );
                    },
                  ),
                ),
              ],
              
              const SizedBox(height: 10),
              
              // 系统状态区域 - 展示三端关系和状态（装饰用，透明度20%）
              Opacity(
                opacity: 0.2,
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: _buildSystemStatusDiagram(),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
  
  /// 构建系统状态关系图（圆形布局）
  Widget _buildSystemStatusDiagram() {
    final serverRunning = _systemStatus?['server']?['status'] == 'running';
    final windowsConnected = _systemStatus?['windows']?['status'] == 'connected';
    final appConnected = _systemStatus?['app']?['status'] == 'connected';
    final windowsCount = _systemStatus?['windows']?['connected_count'] ?? 0;
    final appCount = _systemStatus?['app']?['connected_count'] ?? 0;
    final serverVersion = _systemStatus?['server']?['version'] ?? '未知';
    
    // 节点大小（统一）
    const double iconSize = 32.0; // 图标大小
    const double iconRadius = iconSize / 2; // 图标半径
    const double nodeSize = iconSize + 20.0; // 节点大小（图标+边距）
    const double nodeRadius = nodeSize / 2; // 节点半径
    const double diagramSize = 250.0; // 圆形直径
    // 节点在圆形路径上的半径（图标中心到圆心的距离）- 连接线经过图标中心
    const double nodeCircleRadius = diagramSize / 2 - nodeRadius - 10;
    // 连接线半径：使用节点圆形路径半径，使连接线经过图标中心
    const double connectionLineRadius = nodeCircleRadius;
    
    return LayoutBuilder(
      builder: (context, constraints) {
        final screenWidth = constraints.maxWidth;
        final centerX = screenWidth / 2;
        final centerY = diagramSize / 2;
        
        // 计算三个节点在圆形路径上的位置（120度间隔）
        // 服务器在顶部（270度，统一使用0-2π范围）
        final serverAngle = 3 * math.pi / 2; // 270度
        final serverX = centerX + nodeCircleRadius * math.cos(serverAngle);
        final serverY = centerY + nodeCircleRadius * math.sin(serverAngle);
        
        // Windows端在左下（150度）
        final windowsAngle = 5 * math.pi / 6; // 150度
        final windowsX = centerX + nodeCircleRadius * math.cos(windowsAngle);
        final windowsY = centerY + nodeCircleRadius * math.sin(windowsAngle);
        
        // App端在右下（30度）
        final appAngle = math.pi / 6; // 30度
        final appX = centerX + nodeCircleRadius * math.cos(appAngle);
        final appY = centerY + nodeCircleRadius * math.sin(appAngle);
        
        // 计算图标的角度范围（图标半径对应的角度）
        // 使用反正切计算图标边缘的角度偏移
        final iconAngleOffset = math.atan(iconRadius / connectionLineRadius);
        
        // Windows端 -> 服务器：从Windows图标边缘开始，到服务器图标边缘结束
        // 顺时针方向：从150度顺时针到270度
        final windowsToServerStartAngle = windowsAngle + iconAngleOffset; // 从Windows图标边缘开始
        final windowsToServerEndAngle = serverAngle - iconAngleOffset; // 到服务器图标边缘结束
        
        // 服务器 -> App端：从服务器图标边缘开始，到App图标边缘结束
        // 顺时针方向：从270度顺时针到30度（需要经过360度/0度）
        // 使用390度（30度+360度）来确保顺时针方向绘制
        final serverToAppStartAngle = serverAngle + iconAngleOffset; // 从服务器图标边缘开始
        final serverToAppEndAngle = appAngle + 2 * math.pi - iconAngleOffset; // 到App图标边缘结束（使用390度确保顺时针，绘制器会按指定方向绘制）
        
        return SizedBox(
          width: double.infinity,
          height: diagramSize,
          child: Stack(
            children: [
              // 连接线：Windows端 -> 服务器（圆形弧线，在服务器图标边缘断开）
              Positioned.fill(
                child: AnimatedBuilder(
                  animation: _animation,
                  builder: (context, child) {
                    return CustomPaint(
                      painter: _CircularConnectionLinePainter(
                        centerX: centerX,
                        centerY: centerY,
                        radius: connectionLineRadius,
                        startAngle: windowsToServerStartAngle,
                        endAngle: windowsToServerEndAngle,
                        isConnected: windowsConnected,
                        animationValue: _animation.value,
                        useShortestPath: true, // Windows到服务器使用最短路径
                      ),
                    );
                  },
                ),
              ),
              
              // 连接线：服务器 -> App端（圆形弧线，从服务器图标边缘开始）
              Positioned.fill(
                child: AnimatedBuilder(
                  animation: _animation,
                  builder: (context, child) {
                    return CustomPaint(
                      painter: _CircularConnectionLinePainter(
                        centerX: centerX,
                        centerY: centerY,
                        radius: connectionLineRadius,
                        startAngle: serverToAppStartAngle,
                        endAngle: serverToAppEndAngle,
                        isConnected: appConnected,
                        animationValue: _animation.value,
                        useShortestPath: false, // 服务器到App按指定方向绘制（顺时针）
                      ),
                    );
                  },
                ),
              ),
              
              // 服务器节点
              Positioned(
                left: serverX - nodeRadius,
                top: serverY - nodeRadius,
                child: _buildStatusNode(
                  icon: Icons.dns,
                  title: '服务器',
                  status: serverRunning,
                  detail: serverVersion,
                  width: nodeSize,
                  height: nodeSize,
                ),
              ),
              
              // Windows端节点
              Positioned(
                left: windowsX - nodeRadius,
                top: windowsY - nodeRadius,
                child: _buildStatusNode(
                  icon: Icons.computer,
                  title: 'Windows端',
                  status: windowsConnected,
                  detail: windowsConnected ? '$windowsCount 个连接' : '未连接',
                  width: nodeSize,
                  height: nodeSize,
                ),
              ),
              
              // App端节点
              Positioned(
                left: appX - nodeRadius,
                top: appY - nodeRadius,
                child: _buildStatusNode(
                  icon: Icons.phone_android,
                  title: 'App端',
                  status: appConnected,
                  detail: appConnected ? '$appCount 个连接' : '未连接',
                  width: nodeSize,
                  height: nodeSize,
                ),
              ),
            ],
          ),
        );
      },
    );
  }
  
  /// 构建状态节点（仅图标，无文字，装饰用）
  Widget _buildStatusNode({
    required IconData icon,
    required String title,
    required bool status,
    required String detail,
    required double width,
    required double height,
  }) {
    // 仅显示图标，无文字
    return SizedBox(
      width: width,
      height: height,
      child: Center(
        child: Stack(
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
      // 没有用户数据，显示logo（不要背景）
      return SizedBox(
        width: 60,
        height: 60,
        child: Image.asset(
          'assets/images/logo.png',
          width: 60,
          height: 60,
          errorBuilder: (context, error, stackTrace) {
            // 如果logo图片不存在，使用图标作为fallback
            return Container(
              width: 60,
              height: 60,
              decoration: BoxDecoration(
                color: const Color(0xFF07C160),
                shape: BoxShape.circle,
              ),
              child: const Icon(
                Icons.chat_bubble,
                color: Colors.white,
                size: 30,
              ),
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

}

/// 圆形连接线绘制器
class _CircularConnectionLinePainter extends CustomPainter {
  final double centerX;
  final double centerY;
  final double radius;
  final double startAngle;
  final double endAngle;
  final bool isConnected;
  final double animationValue;
  final bool useShortestPath; // 是否使用最短路径，false时按指定方向绘制
  
  _CircularConnectionLinePainter({
    required this.centerX,
    required this.centerY,
    required this.radius,
    required this.startAngle,
    required this.endAngle,
    required this.isConnected,
    required this.animationValue,
    this.useShortestPath = true, // 默认使用最短路径
  });
  
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = isConnected ? Colors.green : Colors.red
      ..strokeWidth = 2.0
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;
    
    // 计算角度差
    double angleDiff = endAngle - startAngle;
    
    if (useShortestPath) {
      // 标准化角度到 [-π, π] 范围，选择较短的弧线路径
      while (angleDiff > math.pi) angleDiff -= 2 * math.pi;
      while (angleDiff < -math.pi) angleDiff += 2 * math.pi;
    } else {
      // 按指定方向绘制，标准化角度到 [0, 2π] 范围
      while (angleDiff < 0) angleDiff += 2 * math.pi;
      while (angleDiff > 2 * math.pi) angleDiff -= 2 * math.pi;
    }
    
    // 绘制圆形弧线
    final rect = Rect.fromCircle(
      center: Offset(centerX, centerY),
      radius: radius,
    );
    
    canvas.drawArc(
      rect,
      startAngle,
      angleDiff,
      false, // 不填充
      paint,
    );
    
    // 绘制动画效果（流动的光点）
    if (isConnected) {
      // 计算光点在弧线上的位置
      final animatedAngle = startAngle + angleDiff * animationValue;
      final animatedX = centerX + radius * math.cos(animatedAngle);
      final animatedY = centerY + radius * math.sin(animatedAngle);
      
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
  bool shouldRepaint(_CircularConnectionLinePainter oldDelegate) {
    return oldDelegate.isConnected != isConnected ||
        oldDelegate.animationValue != animationValue ||
        oldDelegate.centerX != centerX ||
        oldDelegate.centerY != centerY ||
        oldDelegate.radius != radius ||
        oldDelegate.startAngle != startAngle ||
        oldDelegate.endAngle != endAngle ||
        oldDelegate.useShortestPath != useShortestPath;
  }
}
