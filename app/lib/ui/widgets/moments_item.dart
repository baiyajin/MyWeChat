import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../models/moments_model.dart';
import '../../services/websocket_service.dart';

/// 朋友圈列表项 - 模仿微信App的朋友圈UI
class MomentsItem extends StatelessWidget {
  final MomentsModel moment;

  const MomentsItem({super.key, required this.moment});

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      color: Colors.white,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // 头像和昵称（模仿微信）
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // 头像
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  border: Border.all(color: Colors.grey[300]!, width: 0.5),
                ),
                child: ClipOval(
                  child: Container(
                    color: const Color(0xFF07C160),
                    child: Center(
                      child: Text(
                        moment.nickName.isNotEmpty ? moment.nickName[0] : '?',
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 12),
              // 昵称和时间
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      moment.nickName,
                      style: const TextStyle(
                        fontWeight: FontWeight.w500,
                        fontSize: 15,
                        color: Colors.black87,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      _formatTime(moment.releaseTime),
                      style: TextStyle(
                        color: Colors.grey[600],
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          
          // 内容（模仿微信）
          if (moment.content.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(left: 52), // 与头像对齐
              child: Text(
                moment.content,
                style: const TextStyle(
                  fontSize: 15,
                  color: Colors.black87,
                  height: 1.5,
                ),
              ),
            ),
          
          const SizedBox(height: 12),
          
          // 操作按钮（模仿微信，放在右侧）
          Padding(
            padding: const EdgeInsets.only(left: 52),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                // 点赞按钮
                InkWell(
                  onTap: () {
                    Provider.of<WebSocketService>(context, listen: false)
                        .likeMomentsCommand(moment.momentId);
                  },
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                    decoration: BoxDecoration(
                      color: Colors.grey[100],
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(Icons.thumb_up, size: 16, color: Colors.grey[700]),
                        const SizedBox(width: 4),
                        Text(
                          '赞',
                          style: TextStyle(
                            fontSize: 13,
                            color: Colors.grey[700],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                // 评论按钮
                InkWell(
                  onTap: () {
                    _showCommentDialog(context);
                  },
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                    decoration: BoxDecoration(
                      color: Colors.grey[100],
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(Icons.comment, size: 16, color: Colors.grey[700]),
                        const SizedBox(width: 4),
                        Text(
                          '评论',
                          style: TextStyle(
                            fontSize: 13,
                            color: Colors.grey[700],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  /// 格式化时间
  String _formatTime(String timeStr) {
    if (timeStr.isEmpty) return '';
    try {
      // 尝试解析时间戳或时间字符串
      // TODO: 根据实际时间格式进行解析
      return timeStr;
    } catch (e) {
      return timeStr;
    }
  }

  /// 显示评论对话框
  void _showCommentDialog(BuildContext context) {
    final TextEditingController controller = TextEditingController();

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('评论'),
        content: TextField(
          controller: controller,
          decoration: const InputDecoration(
            hintText: '输入评论内容...',
          ),
          maxLines: 3,
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('取消'),
          ),
          TextButton(
            onPressed: () {
              String content = controller.text.trim();
              if (content.isNotEmpty) {
                Provider.of<WebSocketService>(context, listen: false)
                    .commentMomentsCommand(moment.momentId, content);
                Navigator.pop(context);
              }
            },
            child: const Text('发送'),
          ),
        ],
      ),
    );
  }
}

