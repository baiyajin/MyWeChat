/// 联系人模型
class ContactModel {
  final String id;
  final String weChatId;
  final String friendId;
  final String nickName;
  final String remark;
  final String avatar;
  final String city;
  final String province;
  final String country;
  final int sex;
  final String labelIds;
  final String friendNo;
  final bool isNewFriend;

  ContactModel({
    required this.id,
    required this.weChatId,
    required this.friendId,
    required this.nickName,
    this.remark = '',
    this.avatar = '',
    this.city = '',
    this.province = '',
    this.country = '',
    this.sex = 0,
    this.labelIds = '',
    this.friendNo = '',
    this.isNewFriend = false,
  });

  factory ContactModel.fromJson(Map<String, dynamic> json) {
    return ContactModel(
      id: json['id']?.toString() ?? '',
      weChatId: json['we_chat_id']?.toString() ?? '',
      friendId: json['friend_id']?.toString() ?? '',
      nickName: json['nick_name']?.toString() ?? '',
      remark: json['remark']?.toString() ?? '',
      avatar: json['avatar']?.toString() ?? '',
      city: json['city']?.toString() ?? '',
      province: json['province']?.toString() ?? '',
      country: json['country']?.toString() ?? '',
      sex: json['sex'] as int? ?? 0,
      labelIds: json['label_ids']?.toString() ?? '',
      friendNo: json['friend_no']?.toString() ?? '',
      isNewFriend: json['is_new_friend']?.toString() == '1',
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'we_chat_id': weChatId,
      'friend_id': friendId,
      'nick_name': nickName,
      'remark': remark,
      'avatar': avatar,
      'city': city,
      'province': province,
      'country': country,
      'sex': sex,
      'label_ids': labelIds,
      'friend_no': friendNo,
      'is_new_friend': isNewFriend ? '1' : '0',
    };
  }
}

