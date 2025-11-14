import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/api_service.dart';
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
  String? _currentCommandId;

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
    // 检查是否有命令结果（通过轮询方式检查）
    if (_currentCommandId != null && !_isLoading) {
      // 如果已经有命令ID，继续检查结果
      _checkCommandResult(_currentCommandId!);
    }
  }

  /// 加载日志
  Future<void> _loadLogs() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
      _logContent = '';
    });

    try {
      final apiService = Provider.of<ApiService>(context, listen: false);
      
      // 发送get_logs命令
      final response = await apiService.sendCommand(
        commandType: 'get_logs',
        commandData: {},
      );

      if (response != null && response['command_id'] != null) {
        _currentCommandId = response['command_id'] as String;
        
        // 等待命令结果（通过WebSocket接收）
        _waitForCommandResult(_currentCommandId!);
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

  /// 等待命令结果（通过WebSocket或轮询）
  void _waitForCommandResult(String commandId) {
    // 先尝试通过WebSocket接收（如果WebSocket已连接）
    // 同时启动轮询作为备用方案
    _startPolling(commandId);
  }

  /// 开始轮询命令结果
  void _startPolling(String commandId) {
    Future.delayed(const Duration(milliseconds: 1000), () {
      if (mounted && _currentCommandId == commandId) {
        _checkCommandResult(commandId);
      }
    });
  }

  /// 检查命令结果
  Future<void> _checkCommandResult(String commandId) async {
    try {
      final apiService = Provider.of<ApiService>(context, listen: false);
      final result = await apiService.getCommand(commandId);

      if (result != null) {
        if (result['status'] == 'completed') {
          // 解析结果
          final resultData = result['result'];
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
        } else if (result['status'] == 'error') {
          setState(() {
            _isLoading = false;
            _errorMessage = result['result'] ?? '获取日志失败';
          });
        } else {
          // 还在处理中，继续轮询
          _startPolling(commandId);
        }
      }
    } catch (e) {
      setState(() {
        _isLoading = false;
        _errorMessage = '检查命令结果失败: $e';
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

