import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:url_launcher/url_launcher.dart' as url_launcher;
import '../../services/websocket_service.dart';
import '../../models/official_account_model.dart';

/// 公众号Tab - 模仿微信App的公众号UI
class OfficialAccountTab extends StatefulWidget {
  const OfficialAccountTab({super.key});

  @override
  State<OfficialAccountTab> createState() => _OfficialAccountTabState();
}

class _OfficialAccountTabState extends State<OfficialAccountTab> {
  // 按公众号分组存储消息
  Map<String, List<OfficialAccountMessage>> _groupedMessages = {};

  @override
  void initState() {
    super.initState();
  }

  void _updateMessages(List<OfficialAccountMessage> messages) {
    setState(() {
      _groupedMessages.clear();
      for (var message in messages) {
        String key = message.accountName.isNotEmpty 
            ? message.accountName 
            : message.publisherNickname.isNotEmpty 
                ? message.publisherNickname 
                : message.fromWxid;
        
        if (!_groupedMessages.containsKey(key)) {
          _groupedMessages[key] = [];
        }
        _groupedMessages[key]!.add(message);
      }
    });
  }

  /// 打开文章链接
  Future<void> _openArticle(String url) async {
    if (url.isEmpty) return;
    
    try {
      final uri = Uri.parse(url);
      if (await url_launcher.canLaunchUrl(uri)) {
        await url_launcher.launchUrl(uri, mode: url_launcher.LaunchMode.externalApplication);
      } else {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('无法打开链接')),
          );
        }
      }
    } catch (e) {
      print('打开文章链接失败: $e');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('打开链接失败: $e')),
        );
      }
    }
  }

  /// 格式化发布时间
  String _formatPubTime(String pubTimeStr) {
    try {
      int timestamp = int.tryParse(pubTimeStr) ?? 0;
      if (timestamp == 0) return '';
      
      DateTime pubTime = DateTime.fromMillisecondsSinceEpoch(timestamp * 1000);
      DateTime now = DateTime.now();
      Duration diff = now.difference(pubTime);
      
      if (diff.inDays == 0) {
        return '今天 ${pubTime.hour.toString().padLeft(2, '0')}:${pubTime.minute.toString().padLeft(2, '0')}';
      } else if (diff.inDays == 1) {
        return '昨天 ${pubTime.hour.toString().padLeft(2, '0')}:${pubTime.minute.toString().padLeft(2, '0')}';
      } else if (diff.inDays < 7) {
        return '${diff.inDays}天前';
      } else {
        return '${pubTime.month}/${pubTime.day}';
      }
    } catch (e) {
      return '';
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFEDEDED), // 微信灰色背景，与其他tabbar页面一致
      appBar: AppBar(
        title: const Text('公众号'),
        backgroundColor: const Color(0xFF07C160), // 微信绿色，与其他tabbar页面一致
        foregroundColor: Colors.white,
        elevation: 0,
      ),
      body: Consumer<WebSocketService>(
        builder: (context, wsService, child) {
          // 从WebSocketService获取公众号消息
          final messages = wsService.officialAccountMessages;
          
          // 更新本地分组
          if (messages.isNotEmpty) {
            WidgetsBinding.instance.addPostFrameCallback((_) {
              _updateMessages(messages);
            });
          }

          if (_groupedMessages.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.article_outlined, size: 64, color: Colors.grey[400]),
                  const SizedBox(height: 16),
                  Text(
                    '暂无公众号消息',
                    style: TextStyle(color: Colors.grey[600], fontSize: 16),
                  ),
                ],
              ),
            );
          }

          // 按公众号分组显示
          return ListView.builder(
            padding: const EdgeInsets.symmetric(vertical: 8),
            itemCount: _groupedMessages.length,
            itemBuilder: (context, index) {
              String accountName = _groupedMessages.keys.elementAt(index);
              List<OfficialAccountMessage> accountMessages = _groupedMessages[accountName]!;
              
              // 获取最新的消息（用于显示公众号信息）
              OfficialAccountMessage latestMessage = accountMessages.first;
              
              return _buildAccountSection(accountName, accountMessages, latestMessage);
            },
          );
        },
      ),
    );
  }

  /// 构建公众号分组
  Widget _buildAccountSection(
    String accountName,
    List<OfficialAccountMessage> messages,
    OfficialAccountMessage latestMessage,
  ) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        // 公众号头部信息
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          color: Colors.white,
          child: Row(
            children: [
              // 公众号头像（如果有）
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: const Color(0xFF07C160),
                  borderRadius: BorderRadius.circular(4),
                ),
                child: const Icon(
                  Icons.article,
                  color: Colors.white,
                  size: 24,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      accountName,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w500,
                        color: Colors.black87,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '${messages.length}条新消息',
                      style: TextStyle(
                        fontSize: 12,
                        color: Colors.grey[600],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
        const Divider(height: 1),
        
        // 文章列表
        ...messages.expand((message) => message.articles.map((article) {
          return _buildArticleItem(article, message);
        })),
        
        const SizedBox(height: 8),
      ],
    );
  }

  /// 构建文章项（模仿微信UI）
  Widget _buildArticleItem(
    OfficialAccountArticle article,
    OfficialAccountMessage message,
  ) {
    return InkWell(
      onTap: () => _openArticle(article.url),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        color: Colors.white,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // 封面图
            if (article.cover.isNotEmpty)
              ClipRRect(
                borderRadius: BorderRadius.circular(4),
                child: CachedNetworkImage(
                  imageUrl: article.cover,
                  width: 100,
                  height: 75,
                  fit: BoxFit.cover,
                  placeholder: (context, url) => Container(
                    width: 100,
                    height: 75,
                    color: Colors.grey[200],
                    child: const Center(
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  ),
                  errorWidget: (context, url, error) => Container(
                    width: 100,
                    height: 75,
                    color: Colors.grey[200],
                    child: const Icon(Icons.image_not_supported, color: Colors.grey),
                  ),
                ),
              )
            else
              Container(
                width: 100,
                height: 75,
                decoration: BoxDecoration(
                  color: Colors.grey[200],
                  borderRadius: BorderRadius.circular(4),
                ),
                child: const Icon(Icons.image, color: Colors.grey),
              ),
            
            const SizedBox(width: 12),
            
            // 文章信息
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // 标题
                  Text(
                    article.title,
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w500,
                      color: Colors.black87,
                      height: 1.3,
                    ),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 6),
                  
                  // 摘要
                  if (article.summary.isNotEmpty)
                    Text(
                      article.summary,
                      style: TextStyle(
                        fontSize: 13,
                        color: Colors.grey[600],
                        height: 1.3,
                      ),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  
                  const SizedBox(height: 6),
                  
                  // 发布时间
                  Row(
                    children: [
                      Text(
                        _formatPubTime(article.pubTime),
                        style: TextStyle(
                          fontSize: 12,
                          color: Colors.grey[500],
                        ),
                      ),
                      const Spacer(),
                      Icon(
                        Icons.chevron_right,
                        size: 16,
                        color: Colors.grey[400],
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

