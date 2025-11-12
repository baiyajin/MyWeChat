/// 聊天消息模型
class ChatMessageModel {
  final String id;
  final String weChatId;
  final String fromWeChatId;
  final String toWeChatId;
  final String messageId;
  final String content;
  final int messageType;
  final String sendTime;
  final bool isRead;

  ChatMessageModel({
    required this.id,
    required this.weChatId,
    required this.fromWeChatId,
    required this.toWeChatId,
    required this.messageId,
    required this.content,
    this.messageType = 1,
    required this.sendTime,
    this.isRead = false,
  });

  factory ChatMessageModel.fromJson(Map<String, dynamic> json) {
    return ChatMessageModel(
      id: json['id']?.toString() ?? '',
      weChatId: json['we_chat_id']?.toString() ?? '',
      fromWeChatId: json['from_we_chat_id']?.toString() ?? '',
      toWeChatId: json['to_we_chat_id']?.toString() ?? '',
      messageId: json['message_id']?.toString() ?? '',
      content: json['content']?.toString() ?? '',
      messageType: json['message_type'] as int? ?? 1,
      sendTime: json['send_time']?.toString() ?? '',
      isRead: (json['is_read'] as int? ?? 0) == 1,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'we_chat_id': weChatId,
      'from_we_chat_id': fromWeChatId,
      'to_we_chat_id': toWeChatId,
      'message_id': messageId,
      'content': content,
      'message_type': messageType,
      'send_time': sendTime,
      'is_read': isRead ? 1 : 0,
    };
  }
}

