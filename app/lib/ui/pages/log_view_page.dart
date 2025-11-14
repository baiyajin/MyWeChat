import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/websocket_service.dart';

/// 日志查看页面
class LogViewPage extends StatefulWidget {
  const LogViewPage({super.key});

  @override
  State<LogViewPage> createState() => _LogViewPageState();
}

class _LogViewPageState extends State<LogViewPage> {
  String _logContent = '';
  bool _isLoading = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _loadLogs();
    _setupWebSocketListener();
  }

  /// 设置WebSocket监听，接收命令结果
  void _setupWebSocketListener() {
    final wsService = Provider.of<WebSocketService>(context, listen: false);
    wsService.addListener(_onWebSocketUpdate);
  }

  @override
  void dispose() {
    final wsService = Provider.of<WebSocketService>(context, listen: false);
    wsService.removeListener(_onWebSocketUpdate);
    super.dispose();
  }

  /// WebSocket更新回调
  void _onWebSocketUpdate() {
    // WebSocket更新时，命令结果会通过sendCommandAsync的Future返回，这里不需要处理
  }

  /// 加载日志
  Future<void> _loadLogs() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
      _logContent = '';
    });

    try {
      final wsService = Provider.of<WebSocketService>(context, listen: false);
      
      // 检查WebSocket是否已连接
      if (!wsService.isConnected) {
        setState(() {
          _isLoading = false;
          _errorMessage = 'WebSocket未连接，请先登录';
        });
        return;
      }
      
      // 通过WebSocket发送get_logs命令（不指定target_we_chat_id，服务端会根据登录手机号自动匹配）
      final result = await wsService.sendCommandAsync(
        'get_logs',
        {},
        '', // target_we_chat_id为空，服务端会根据登录手机号匹配Windows端
      );

      if (result != null) {
        final status = result['status'] as String? ?? '';
        final resultData = result['result'];
        
        if (status == 'completed') {
          // 解析结果
          String resultStr = '';
          
          if (resultData is String) {
            try {
              final resultJson = jsonDecode(resultData);
              resultStr = resultJson['decrypted_log_content'] ?? resultData;
            } catch (e) {
              resultStr = resultData;
            }
          } else if (resultData is Map) {
            resultStr = resultData['decrypted_log_content'] ?? resultData.toString();
          } else {
            resultStr = resultData.toString();
          }

          setState(() {
            _isLoading = false;
            _logContent = resultStr;
          });
        } else if (status == 'error') {
          setState(() {
            _isLoading = false;
            _errorMessage = resultData?.toString() ?? '获取日志失败';
          });
        } else {
          setState(() {
            _isLoading = false;
            _errorMessage = '未知的命令状态: $status';
          });
        }
      } else {
        setState(() {
          _isLoading = false;
          _errorMessage = '发送命令失败';
        });
      }
    } catch (e) {
      setState(() {
        _isLoading = false;
        _errorMessage = '加载日志失败: $e';
      });
    }
  }


  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('查看日志'),
        backgroundColor: const Color(0xFF07C160),
        foregroundColor: Colors.white,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: _isLoading ? null : _loadLogs,
            tooltip: '刷新',
          ),
        ],
      ),
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(),
            )
          : _errorMessage != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(
                        Icons.error_outline,
                        size: 64,
                        color: Colors.grey[400],
                      ),
                      const SizedBox(height: 16),
                      Text(
                        _errorMessage!,
                        style: TextStyle(
                          color: Colors.grey[600],
                          fontSize: 16,
                        ),
                      ),
                      const SizedBox(height: 16),
                      ElevatedButton(
                        onPressed: _loadLogs,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: const Color(0xFF07C160),
                          foregroundColor: Colors.white,
                        ),
                        child: const Text('重试'),
                      ),
                    ],
                  ),
                )
              : _logContent.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(
                            Icons.description_outlined,
                            size: 64,
                            color: Colors.grey[400],
                          ),
                          const SizedBox(height: 16),
                          Text(
                            '暂无日志内容',
                            style: TextStyle(
                              color: Colors.grey[600],
                              fontSize: 16,
                            ),
                          ),
                        ],
                      ),
                    )
                  : Container(
                      color: Colors.black,
                      padding: const EdgeInsets.all(8),
                      child: SingleChildScrollView(
                        child: SelectableText(
                          _logContent,
                          style: const TextStyle(
                            color: Colors.green,
                            fontSize: 12,
                            fontFamily: 'monospace',
                          ),
                        ),
                      ),
                    ),
    );
  }
}

