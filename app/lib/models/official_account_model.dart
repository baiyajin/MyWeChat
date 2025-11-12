/// 公众号文章模型
class OfficialAccountArticle {
  final String title;
  final String url;
  final String cover;
  final String summary;
  final String pubTime;

  OfficialAccountArticle({
    required this.title,
    required this.url,
    required this.cover,
    required this.summary,
    required this.pubTime,
  });

  factory OfficialAccountArticle.fromJson(Map<String, dynamic> json) {
    return OfficialAccountArticle(
      title: json['title']?.toString() ?? '',
      url: json['url']?.toString() ?? '',
      cover: json['cover']?.toString() ?? '',
      summary: json['summary']?.toString() ?? '',
      pubTime: json['pub_time']?.toString() ?? '0',
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'title': title,
      'url': url,
      'cover': cover,
      'summary': summary,
      'pub_time': pubTime,
    };
  }
}

/// 公众号消息模型
class OfficialAccountMessage {
  final String accountName;
  final String publisherUsername;
  final String publisherNickname;
  final String fromWxid;
  final String msgid;
  final String wechatId;
  final String receiveTime;
  final List<OfficialAccountArticle> articles;

  OfficialAccountMessage({
    required this.accountName,
    required this.publisherUsername,
    required this.publisherNickname,
    required this.fromWxid,
    required this.msgid,
    required this.wechatId,
    required this.receiveTime,
    required this.articles,
  });

  factory OfficialAccountMessage.fromJson(Map<String, dynamic> json) {
    List<OfficialAccountArticle> articlesList = [];
    if (json['articles'] != null && json['articles'] is List) {
      articlesList = (json['articles'] as List)
          .map((item) => OfficialAccountArticle.fromJson(item as Map<String, dynamic>))
          .toList();
    }

    return OfficialAccountMessage(
      accountName: json['account_name']?.toString() ?? '',
      publisherUsername: json['publisher_username']?.toString() ?? '',
      publisherNickname: json['publisher_nickname']?.toString() ?? '',
      fromWxid: json['from_wxid']?.toString() ?? '',
      msgid: json['msgid']?.toString() ?? '',
      wechatId: json['wechat_id']?.toString() ?? '',
      receiveTime: json['receive_time']?.toString() ?? '',
      articles: articlesList,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'account_name': accountName,
      'publisher_username': publisherUsername,
      'publisher_nickname': publisherNickname,
      'from_wxid': fromWxid,
      'msgid': msgid,
      'wechat_id': wechatId,
      'receive_time': receiveTime,
      'articles': articles.map((article) => article.toJson()).toList(),
    };
  }
}

