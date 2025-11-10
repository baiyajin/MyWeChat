import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/websocket_service.dart';
import '../../models/moments_model.dart';
import '../widgets/moments_item.dart';

/// 朋友圈Tab - 模仿微信App的朋友圈UI
class MomentsTab extends StatefulWidget {
  const MomentsTab({super.key});

  @override
  State<MomentsTab> createState() => _MomentsTabState();
}

class _MomentsTabState extends State<MomentsTab> {
  @override
  void initState() {
    super.initState();
    // 页面初始化时，如果已连接，请求同步朋友圈
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final wsService = Provider.of<WebSocketService>(context, listen: false);
      if (wsService.isConnected) {
        wsService.requestSyncMoments();
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFEDEDED), // 微信灰色背景
      appBar: AppBar(
        title: const Text('朋友圈'),
        backgroundColor: const Color(0xFF07C160), // 微信绿色
        foregroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.camera_alt),
            onPressed: () {
              // 打开发布朋友圈页面
              // TODO: 实现发布朋友圈功能
            },
          ),
        ],
      ),
      body: Consumer<WebSocketService>(
        builder: (context, wsService, child) {
          if (!wsService.isConnected) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.wifi_off, size: 64, color: Colors.grey[400]),
                  const SizedBox(height: 16),
                  Text(
                    '未连接到服务器',
                    style: TextStyle(color: Colors.grey[600], fontSize: 16),
                  ),
                ],
              ),
            );
          }

          if (wsService.moments.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.photo_library_outlined, size: 64, color: Colors.grey[400]),
                  const SizedBox(height: 16),
                  Text(
                    '暂无朋友圈',
                    style: TextStyle(color: Colors.grey[600], fontSize: 16),
                  ),
                  const SizedBox(height: 8),
                  TextButton(
                    onPressed: () {
                      wsService.requestSyncMoments();
                    },
                    child: const Text('点击刷新'),
                  ),
                ],
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: () async {
              // 刷新朋友圈
              wsService.requestSyncMoments();
              await Future.delayed(const Duration(milliseconds: 500));
            },
            child: ListView.builder(
              padding: const EdgeInsets.symmetric(vertical: 8),
              itemCount: wsService.moments.length,
              itemBuilder: (context, index) {
                MomentsModel moment = wsService.moments[index];
                return MomentsItem(moment: moment);
              },
            ),
          );
        },
      ),
    );
  }
}

