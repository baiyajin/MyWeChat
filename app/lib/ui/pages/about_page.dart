import 'package:flutter/material.dart';

/// 关于页面
class AboutPage extends StatelessWidget {
  const AboutPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('关于'),
        backgroundColor: const Color(0xFF07C160),
        foregroundColor: Colors.white,
      ),
      backgroundColor: const Color(0xFFEDEDED),
      body: Center(
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
    );
  }
}

