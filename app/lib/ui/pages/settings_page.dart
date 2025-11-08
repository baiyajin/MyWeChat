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
              
              const SizedBox(height: 10),
              
              // 系统状态区域
              Container(
                color: Colors.white,
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
                    const SizedBox(height: 12),
                    _buildStatusItem(
                      '服务器',
                      _systemStatus?['server']?['status'] == 'running',
                      _systemStatus?['server']?['version'] ?? '未知',
                    ),
                    const SizedBox(height: 12),
                    _buildStatusItem(
                      'Windows端',
                      _systemStatus?['windows']?['status'] == 'connected',
                      _systemStatus?['windows']?['connected_count'] != null
                          ? '连接数: ${_systemStatus?['windows']?['connected_count']}'
                          : '未连接',
                    ),
                    const SizedBox(height: 12),
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
            ],
          );
        },
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
          size: 24,
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: const TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w500,
                  color: Colors.black87,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                detail,
                style: TextStyle(
                  fontSize: 14,
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
}

