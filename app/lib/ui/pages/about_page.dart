import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../../services/websocket_service.dart';
import 'license_manage_page.dart';

/// 关于页面
class AboutPage extends StatefulWidget {
  const AboutPage({super.key});

  @override
  State<AboutPage> createState() => _AboutPageState();
}

class _AboutPageState extends State<AboutPage> {
  bool _hasManagePermission = false;

  @override
  void initState() {
    super.initState();
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
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('关于'),
        backgroundColor: const Color(0xFF07C160),
        foregroundColor: Colors.white,
      ),
      backgroundColor: const Color(0xFFEDEDED),
      body: Column(
        children: [
          // 主要内容
          Expanded(
            child: Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  // Logo
                  Container(
                    width: 120,
                    height: 120,
                    decoration: BoxDecoration(
                      color: const Color(0xFF07C160),
                      borderRadius: BorderRadius.circular(24),
                    ),
                    child: Image.asset(
                      'assets/images/logo.png',
                      width: 80,
                      height: 80,
                      errorBuilder: (context, error, stackTrace) {
                        // 如果logo图片不存在，使用图标作为fallback
                        return const Icon(
                          Icons.chat_bubble,
                          color: Colors.white,
                          size: 60,
                        );
                      },
                    ),
                  ),
                  const SizedBox(height: 24),
                  
                  // 应用名称
                  const Text(
                    'MyWeChat',
                    style: TextStyle(
                      fontSize: 28,
                      fontWeight: FontWeight.bold,
                      color: Colors.black87,
                    ),
                  ),
                  const SizedBox(height: 8),
                  
                  // 版本号
                  const Text(
                    '版本 1.0.0',
                    style: TextStyle(
                      fontSize: 16,
                      color: Colors.grey,
                    ),
                  ),
                  const SizedBox(height: 32),
                  
                  // 描述
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 32),
                    child: Text(
                      '微信数据同步工具\n帮助您更好地管理和同步微信数据',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        fontSize: 14,
                        color: Colors.grey[600],
                        height: 1.5,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          
          // 授权码管理入口（仅管理员可见）
          if (_hasManagePermission) ...[
            Container(
              margin: const EdgeInsets.only(bottom: 16),
              child: ListTile(
                leading: const Icon(Icons.vpn_key, color: Color(0xFF07C160)),
                title: const Text('授权码管理'),
                trailing: const Icon(Icons.chevron_right),
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
        ],
      ),
    );
  }
}

