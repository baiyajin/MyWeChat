import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:cached_network_image/cached_network_image.dart';
import '../../services/websocket_service.dart';
import '../../services/api_service.dart';

/// 账号历史列表页面
class AccountListPage extends StatefulWidget {
  const AccountListPage({super.key});

  @override
  State<AccountListPage> createState() => _AccountListPageState();
}

class _AccountListPageState extends State<AccountListPage> {
  List<Map<String, dynamic>> _accounts = [];
  bool _isLoading = true;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _loadAccounts();
  }

  Future<void> _loadAccounts() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final apiService = Provider.of<ApiService>(context, listen: false);
      final accounts = await apiService.getAllAccounts();
      
      if (mounted) {
        setState(() {
          _accounts = accounts;
          _isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _errorMessage = '加载账号列表失败: $e';
          _isLoading = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFEDEDED),
      appBar: AppBar(
        title: const Text('切换账号'),
        backgroundColor: const Color(0xFF07C160),
        foregroundColor: Colors.white,
        elevation: 0,
        actions: [
          // 退出登录按钮
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: '退出登录',
            onPressed: () => _showLogoutDialog(context),
          ),
        ],
      ),
      body: _isLoading
          ? const Center(
              child: CircularProgressIndicator(),
            )
          : _errorMessage != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(
                        _errorMessage!,
                        style: TextStyle(color: Colors.red[700]),
                      ),
                      const SizedBox(height: 16),
                      ElevatedButton(
                        onPressed: _loadAccounts,
                        child: const Text('重试'),
                      ),
                    ],
                  ),
                )
              : _accounts.isEmpty
                  ? const Center(
                      child: Text('暂无账号'),
                    )
                  : RefreshIndicator(
                      onRefresh: _loadAccounts,
                      child: ListView.builder(
                        itemCount: _accounts.length,
                        itemBuilder: (context, index) {
                          final account = _accounts[index];
                          return _buildAccountItem(context, account);
                        },
                      ),
                    ),
    );
  }

  /// 构建账号列表项
  Widget _buildAccountItem(BuildContext context, Map<String, dynamic> account) {
    final wsService = Provider.of<WebSocketService>(context, listen: false);
    final currentWxid = wsService.currentWeChatId;
    final isCurrent = account['wxid'] == currentWxid;
    final avatarUrl = account['avatar']?.toString();
    final nickname = account['nickname']?.toString() ?? '未知';
    final phone = account['phone']?.toString() ?? '';
    final wxid = account['wxid']?.toString() ?? '';

    return Container(
      color: Colors.white,
      margin: const EdgeInsets.only(bottom: 1),
      child: InkWell(
        onTap: () => _showQuickLoginDialog(context, account),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          child: Row(
            children: [
              // 头像
              ClipOval(
                child: avatarUrl != null && avatarUrl.isNotEmpty
                    ? CachedNetworkImage(
                        imageUrl: avatarUrl,
                        width: 50,
                        height: 50,
                        fit: BoxFit.cover,
                        placeholder: (context, url) => Container(
                          width: 50,
                          height: 50,
                          color: Colors.grey[200],
                          child: const Center(
                            child: CircularProgressIndicator(strokeWidth: 2),
                          ),
                        ),
                        errorWidget: (context, url, error) => Container(
                          width: 50,
                          height: 50,
                          decoration: BoxDecoration(
                            color: const Color(0xFF07C160),
                            shape: BoxShape.circle,
                          ),
                          child: const Icon(
                            Icons.person,
                            color: Colors.white,
                            size: 25,
                          ),
                        ),
                      )
                    : Container(
                        width: 50,
                        height: 50,
                        decoration: BoxDecoration(
                          color: const Color(0xFF07C160),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(
                          Icons.person,
                          color: Colors.white,
                          size: 25,
                        ),
                      ),
              ),
              const SizedBox(width: 12),
              // 账号信息
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            nickname,
                            style: const TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w500,
                              color: Colors.black87,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        if (isCurrent)
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 8,
                              vertical: 2,
                            ),
                            decoration: BoxDecoration(
                              color: const Color(0xFF07C160),
                              borderRadius: BorderRadius.circular(10),
                            ),
                            child: const Text(
                              '当前',
                              style: TextStyle(
                                fontSize: 12,
                                color: Colors.white,
                              ),
                            ),
                          ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      phone.isNotEmpty ? phone : wxid,
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
              // 右箭头
              Icon(
                Icons.chevron_right,
                color: Colors.grey[400],
              ),
            ],
          ),
        ),
      ),
    );
  }

  /// 显示快速登录确认弹窗
  Future<void> _showQuickLoginDialog(
    BuildContext context,
    Map<String, dynamic> account,
  ) async {
    final wsService = Provider.of<WebSocketService>(context, listen: false);
    final currentWxid = wsService.currentWeChatId;
    final isCurrent = account['wxid'] == currentWxid;

    if (isCurrent) {
      // 如果是当前账号，显示提示
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('当前已是此账号'),
            duration: Duration(seconds: 2),
          ),
        );
      }
      return;
    }

    final avatarUrl = account['avatar']?.toString();
    final nickname = account['nickname']?.toString() ?? '未知';
    final phone = account['phone']?.toString() ?? '';
    final wxid = account['wxid']?.toString() ?? '';

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('快速登录'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            // 头像
            ClipOval(
              child: avatarUrl != null && avatarUrl.isNotEmpty
                  ? CachedNetworkImage(
                      imageUrl: avatarUrl,
                      width: 60,
                      height: 60,
                      fit: BoxFit.cover,
                      placeholder: (context, url) => Container(
                        width: 60,
                        height: 60,
                        color: Colors.grey[200],
                        child: const Center(
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                      ),
                      errorWidget: (context, url, error) => Container(
                        width: 60,
                        height: 60,
                        decoration: BoxDecoration(
                          color: const Color(0xFF07C160),
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(
                          Icons.person,
                          color: Colors.white,
                          size: 30,
                        ),
                      ),
                    )
                  : Container(
                      width: 60,
                      height: 60,
                      decoration: BoxDecoration(
                        color: const Color(0xFF07C160),
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(
                        Icons.person,
                        color: Colors.white,
                        size: 30,
                      ),
                    ),
            ),
            const SizedBox(height: 12),
            // 昵称
            Text(
              nickname,
              style: const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w500,
              ),
            ),
            const SizedBox(height: 4),
            // 手机号或wxid
            Text(
              phone.isNotEmpty ? phone : wxid,
              style: TextStyle(
                fontSize: 14,
                color: Colors.grey[600],
              ),
            ),
            const SizedBox(height: 16),
            const Text('确定要切换到此账号吗？'),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('取消'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.of(context).pop(true),
            style: ElevatedButton.styleFrom(
              backgroundColor: const Color(0xFF07C160),
              foregroundColor: Colors.white,
            ),
            child: const Text('确定'),
          ),
        ],
      ),
    );

    if (confirmed == true && mounted) {
      // 显示加载提示
      showDialog(
        context: context,
        barrierDismissible: false,
        builder: (context) => const Center(
          child: CircularProgressIndicator(),
        ),
      );

      try {
        final success = await wsService.quickLogin(wxid);
        
        if (mounted) {
          Navigator.of(context).pop(); // 关闭加载提示
          
          if (success && wsService.myInfo != null) {
            // 保存登录状态
            await wsService.saveLoginState();
            
            // 显示成功提示
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('切换账号成功'),
                duration: Duration(seconds: 2),
              ),
            );
            
            // 返回上一页
            Navigator.of(context).pop();
          } else {
            // 显示失败提示
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('切换账号失败'),
                duration: Duration(seconds: 2),
              ),
            );
          }
        }
      } catch (e) {
        if (mounted) {
          Navigator.of(context).pop(); // 关闭加载提示
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text('切换账号失败: $e'),
              duration: const Duration(seconds: 2),
            ),
          );
        }
      }
    }
  }

  /// 显示退出登录确认弹窗
  Future<void> _showLogoutDialog(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('退出登录'),
        content: const Text('确定要退出登录吗？退出后将返回登录页面。'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('取消'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text(
              '退出',
              style: TextStyle(color: Colors.red),
            ),
          ),
        ],
      ),
    );

    if (confirmed == true && mounted) {
      final wsService = Provider.of<WebSocketService>(context, listen: false);
      
      // 清除登录状态
      await wsService.clearLoginState();
      
      // 跳转到登录页面
      Navigator.of(context).pushReplacementNamed('/');
    }
  }
}

