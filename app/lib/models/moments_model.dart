/// 朋友圈模型
class MomentsModel {
  final String id;
  final String weChatId;
  final String momentId;
  final String friendId;
  final String nickName;
  final String content;
  final String releaseTime;
  final int type;
  final String jsonText;

  MomentsModel({
    required this.id,
    required this.weChatId,
    required this.momentId,
    required this.friendId,
    required this.nickName,
    required this.content,
    required this.releaseTime,
    this.type = 0,
    this.jsonText = '',
  });

  factory MomentsModel.fromJson(Map<String, dynamic> json) {
    return MomentsModel(
      id: json['id']?.toString() ?? '',
      weChatId: json['we_chat_id']?.toString() ?? '',
      momentId: json['moment_id']?.toString() ?? '',
      friendId: json['friend_id']?.toString() ?? '',
      nickName: json['nick_name']?.toString() ?? '',
      content: json['content']?.toString() ?? '',
      releaseTime: json['release_time']?.toString() ?? '',
      type: json['moment_type'] as int? ?? 0,
      jsonText: json['json_text']?.toString() ?? '',
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'we_chat_id': weChatId,
      'moment_id': momentId,
      'friend_id': friendId,
      'nick_name': nickName,
      'content': content,
      'release_time': releaseTime,
      'moment_type': type,
      'json_text': jsonText,
    };
  }
}

