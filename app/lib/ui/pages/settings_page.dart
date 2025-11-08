import 'dart:async';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/websocket_service.dart';
import '../../services/api_service.dart';

/// 设置页面
class SettingsPage extends StatefulWidget {
  const SettingsPage({super.key});

  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('设置'),
        backgroundColor: const Color(0xFF07C160),
        foregroundColor: Colors.white,
      ),
      backgroundColor: const Color(0xFFEDEDED),
      body: Consumer2<WebSocketService, ApiService>(
        builder: (context, wsService, apiService, child) {
          return ListView(
            children: [
              const SizedBox(height: 10),
              
              // 服务器配置区域
              Container(
                color: Colors.white,
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      '服务器配置',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: Colors.black87,
                      ),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                '服务器地址',
                                style: TextStyle(
                                  fontSize: 14,
                                  color: Colors.grey[600],
                                ),
                              ),
                              const SizedBox(height: 4),
                              Text(
                                apiService.serverUrl,
                                style: const TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.w500,
                                  color: Colors.black87,
                                ),
                              ),
                            ],
                          ),
                        ),
                        ElevatedButton(
                          onPressed: () {
                            _showServerConfigDialog(context, apiService, wsService);
                          },
                          style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFF07C160),
                            foregroundColor: Colors.white,
                          ),
                          child: const Text('修改'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ],
          );
        },
      ),
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
            border: OutlineInputBorder(),
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
}

