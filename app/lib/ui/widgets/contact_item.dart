import 'package:flutter/material.dart';
import '../../models/contact_model.dart';

/// 联系人列表项 - 模仿微信App的好友列表项UI
class ContactItem extends StatelessWidget {
  final ContactModel contact;

  const ContactItem({super.key, required this.contact});

  @override
  Widget build(BuildContext context) {
    String displayName = contact.remark.isNotEmpty ? contact.remark : contact.nickName;
    
    return InkWell(
      onTap: () {
        // 打开聊天页面
        Navigator.pushNamed(context, '/chat', arguments: contact);
      },
      child: Container(
        color: Colors.white,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Row(
          children: [
            // 头像（圆形，带边框）
            Container(
              width: 44,
              height: 44,
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
                          return _buildDefaultAvatar(displayName);
                        },
                      )
                    : _buildDefaultAvatar(displayName),
              ),
            ),
            const SizedBox(width: 12),
            // 昵称和微信号
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    displayName,
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w500,
                      color: Colors.black87,
                    ),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  if (contact.friendNo.isNotEmpty) ...[
                    const SizedBox(height: 2),
                    Text(
                      contact.friendNo,
                      style: TextStyle(
                        fontSize: 13,
                        color: Colors.grey[600],
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ],
              ),
            ),
            // 右箭头
            Icon(
              Icons.chevron_right,
              color: Colors.grey[400],
              size: 20,
            ),
          ],
        ),
      ),
    );
  }

  /// 构建默认头像
  Widget _buildDefaultAvatar(String name) {
    return Container(
      color: const Color(0xFF07C160),
      child: Center(
        child: Text(
          name.isNotEmpty ? name[0] : '?',
          style: const TextStyle(
            color: Colors.white,
            fontSize: 18,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),
    );
  }
}

