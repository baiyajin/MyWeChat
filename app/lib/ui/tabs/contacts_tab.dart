import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/websocket_service.dart';
import '../../services/api_service.dart';
import '../../models/contact_model.dart';
import '../widgets/contact_item.dart';

/// 好友列表Tab - 模仿微信App的好友列表UI
class ContactsTab extends StatefulWidget {
  const ContactsTab({super.key});

  @override
  State<ContactsTab> createState() => _ContactsTabState();
}

class _ContactsTabState extends State<ContactsTab> {
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
        title: const Text('通讯录'),
        backgroundColor: const Color(0xFF07C160), // 微信绿色
        foregroundColor: Colors.white,
        elevation: 0,
      ),
      body: Consumer<WebSocketService>(
        builder: (context, wsService, child) {
          if (wsService.contacts.isEmpty) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Icons.contacts_outlined, size: 64, color: Colors.grey[400]),
                  const SizedBox(height: 16),
                  Text(
                    '暂无好友',
                    style: TextStyle(color: Colors.grey[600], fontSize: 16),
                  ),
                  const SizedBox(height: 8),
                  TextButton(
                    onPressed: () {
                      _requestSyncContacts();
                    },
                    child: const Text('点击刷新'),
                  ),
                ],
              ),
            );
          }

          // 按首字母分组排序（模仿微信）
          final sortedContacts = List<ContactModel>.from(wsService.contacts);
          sortedContacts.sort((a, b) {
            String nameA = a.remark.isNotEmpty ? a.remark : a.nickName;
            String nameB = b.remark.isNotEmpty ? b.remark : b.nickName;
            return nameA.compareTo(nameB);
          });

          // 按首字母分组
          Map<String, List<ContactModel>> groupedContacts = {};
          for (var contact in sortedContacts) {
            String name = contact.remark.isNotEmpty ? contact.remark : contact.nickName;
            String firstLetter = _getFirstLetter(name);
            if (!groupedContacts.containsKey(firstLetter)) {
              groupedContacts[firstLetter] = [];
            }
            groupedContacts[firstLetter]!.add(contact);
          }

          // 按字母顺序排序
          final sortedKeys = groupedContacts.keys.toList()..sort();

          return Column(
            children: [
              // 搜索框（模仿微信）
              Container(
                color: Colors.white,
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                child: TextField(
                  decoration: InputDecoration(
                    hintText: '搜索',
                    prefixIcon: Icon(Icons.search, color: Colors.grey[600]),
                    filled: true,
                    fillColor: const Color(0xFFEDEDED),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(6),
                      borderSide: BorderSide.none,
                    ),
                    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                  ),
                ),
              ),
              // 好友列表（带字母索引）
              Expanded(
                child: ListView.builder(
                  itemCount: sortedKeys.length,
                  itemBuilder: (context, index) {
                    String letter = sortedKeys[index];
                    List<ContactModel> contacts = groupedContacts[letter]!;
                    return Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        // 字母索引标题
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                          color: const Color(0xFFEDEDED),
                          child: Text(
                            letter,
                            style: TextStyle(
                              fontSize: 14,
                              fontWeight: FontWeight.bold,
                              color: Colors.grey[700],
                            ),
                          ),
                        ),
                        // 该字母下的好友列表
                        ...contacts.map((contact) => ContactItem(contact: contact)),
                      ],
                    );
                  },
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  /// 获取首字母（中文取拼音首字母，英文取首字母）
  String _getFirstLetter(String name) {
    if (name.isEmpty) return '#';
    String firstChar = name[0];
    // 如果是中文字符，返回拼音首字母（简化处理，返回第一个字符）
    if (firstChar.codeUnitAt(0) >= 0x4E00 && firstChar.codeUnitAt(0) <= 0x9FFF) {
      // 中文，返回第一个字符（可以后续优化为拼音首字母）
      return firstChar;
    } else if (firstChar.codeUnitAt(0) >= 65 && firstChar.codeUnitAt(0) <= 90) {
      // 大写字母
      return firstChar;
    } else if (firstChar.codeUnitAt(0) >= 97 && firstChar.codeUnitAt(0) <= 122) {
      // 小写字母，转大写
      return firstChar.toUpperCase();
    } else {
      // 其他字符
      return '#';
    }
  }
}

