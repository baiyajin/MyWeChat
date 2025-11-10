import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/websocket_service.dart';
import '../../services/api_service.dart';
import '../../models/contact_model.dart';

/// 聊天Tab - 模仿微信App的聊天列表UI
class ChatTab extends StatefulWidget {
  const ChatTab({super.key});

  @override
  State<ChatTab> createState() => _ChatTabState();
}

class _ChatTabState extends State<ChatTab> {
  @override
  void initState() {
    super.initState();
    // 页面初始化时，通过WebSocket请求同步联系人数据
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _requestSyncContacts();
    });
  }

  /// 通过WebSocket请求同步联系人数据
  void _requestSyncContacts() {
    try {
      final wsService = Provider.of<WebSocketService>(context, listen: false);
      final weChatId = wsService.currentWeChatId;
      if (weChatId != null && weChatId.isNotEmpty) {
        wsService.requestSyncContacts(weChatId);
      } else {
        print('无法请求同步联系人数据，微信账号ID为空');
      }
    } catch (e) {
      print('请求同步联系人数据失败: $e');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFEDEDED), // 微信灰色背景
      appBar: AppBar(
        title: const Text('微信'),
        backgroundColor: const Color(0xFF07C160), // 微信绿色
        foregroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.add),
            onPressed: () {
              // 添加聊天
            },
          ),
        ],
      ),
      body: Consumer<WebSocketService>(
        builder: (context, wsService, child) {
          if (wsService.contacts.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.chat_bubble_outline, size: 64, color: Colors.grey[400]),
                  const SizedBox(height: 16),
                  Text(
                    '暂无聊天',
                    style: TextStyle(color: Colors.grey[600], fontSize: 16),
                  ),
                ],
              ),
            );
          }

          // 显示最近聊天的好友列表（模仿微信）
          return ListView.separated(
            itemCount: wsService.contacts.length,
            separatorBuilder: (context, index) => Divider(
              height: 1,
              indent: 80, // 与头像对齐
              color: Colors.grey[200],
            ),
            itemBuilder: (context, index) {
              ContactModel contact = wsService.contacts[index];
              return InkWell(
                onTap: () {
                  // 打开聊天页面
                  Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (context) => ChatDetailPage(contact: contact),
                    ),
                  );
                },
                child: Container(
                  color: Colors.white,
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                  child: Row(
                    children: [
                      // 头像（圆形，带边框）
                      Container(
                        width: 48,
                        height: 48,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          border: Border.all(color: Colors.grey[300]!, width: 0.5),
                        ),
                        child: ClipOval(
                          child: contact.avatar.isNotEmpty
                              ? Image.network(
                                  contact.avatar,
                                  fit: BoxFit.cover,
                                  errorBuilder: (context, error, stackTrace) {
                                    return Container(
                                      color: const Color(0xFF07C160),
                                      child: Center(
                                        child: Text(
                                          contact.nickName.isNotEmpty ? contact.nickName[0] : '?',
                                          style: const TextStyle(
                                            color: Colors.white,
                                            fontSize: 20,
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                                      ),
                                    );
                                  },
                                )
                              : Container(
                                  color: const Color(0xFF07C160),
                                  child: Center(
                                    child: Text(
                                      contact.nickName.isNotEmpty ? contact.nickName[0] : '?',
                                      style: const TextStyle(
                                        color: Colors.white,
                                        fontSize: 20,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                  ),
                                ),
                        ),
                      ),
                      const SizedBox(width: 12),
                      // 昵称和最后消息
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    contact.remark.isNotEmpty ? contact.remark : contact.nickName,
                                    style: const TextStyle(
                                      fontSize: 16,
                                      fontWeight: FontWeight.w500,
                                      color: Colors.black87,
                                    ),
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                                Text(
                                  '10:00', // TODO: 显示最后消息时间
                                  style: TextStyle(
                                    fontSize: 12,
                                    color: Colors.grey[600],
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Text(
                              '最后消息...', // TODO: 显示最后消息内容
                              style: TextStyle(
                                fontSize: 14,
                                color: Colors.grey[600],
                              ),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              );
            },
          );
        },
      ),
    );
  }
}

/// 聊天详情页面
class ChatDetailPage extends StatefulWidget {
  final ContactModel contact;

  const ChatDetailPage({super.key, required this.contact});

  @override
  State<ChatDetailPage> createState() => _ChatDetailPageState();
}

class _ChatDetailPageState extends State<ChatDetailPage> {
  final TextEditingController _messageController = TextEditingController();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.contact.remark.isNotEmpty 
            ? widget.contact.remark 
            : widget.contact.nickName),
        backgroundColor: const Color(0xFF07C160),
        foregroundColor: Colors.white,
      ),
      body: Column(
        children: [
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: const [
                // 聊天消息列表
                // TODO: 实现聊天消息显示
              ],
            ),
          ),
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.white,
              boxShadow: [
                BoxShadow(
                  color: Colors.grey.withOpacity(0.3),
                  blurRadius: 4,
                  offset: const Offset(0, -2),
                ),
              ],
            ),
            child: Row(
              children: [
                IconButton(
                  icon: const Icon(Icons.add),
                  onPressed: () {
                    // 打开附件选择
                  },
                ),
                Expanded(
                  child: TextField(
                    controller: _messageController,
                    decoration: const InputDecoration(
                      hintText: '输入消息...',
                      border: OutlineInputBorder(),
                      contentPadding: EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                    ),
                  ),
                ),
                IconButton(
                  icon: const Icon(Icons.send, color: Color(0xFF07C160)),
                  onPressed: () {
                    String message = _messageController.text.trim();
                    if (message.isNotEmpty) {
                      // 发送消息
                      Provider.of<WebSocketService>(context, listen: false)
                          .sendMessageCommand(widget.contact.friendId, message);
                      _messageController.clear();
                    }
                  },
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  @override
  void dispose() {
    _messageController.dispose();
    super.dispose();
  }
}

