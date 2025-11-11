import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/api_service.dart';
import '../../models/license_model.dart';

/// 授权码管理页面
class LicenseManagePage extends StatefulWidget {
  const LicenseManagePage({super.key});

  @override
  State<LicenseManagePage> createState() => _LicenseManagePageState();
}

class _LicenseManagePageState extends State<LicenseManagePage> {
  List<LicenseModel> _licenses = [];
  bool _isLoading = true;
  String _searchText = '';
  String? _statusFilter;

  @override
  void initState() {
    super.initState();
    _loadLicenses();
  }

  Future<void> _loadLicenses() async {
    setState(() {
      _isLoading = true;
    });

    try {
      final apiService = Provider.of<ApiService>(context, listen: false);
      final licenses = await apiService.getLicenses(
        status: _statusFilter,
        phone: _searchText.isNotEmpty ? _searchText : null,
      );
      setState(() {
        _licenses = licenses;
        _isLoading = false;
      });
    } catch (e) {
      setState(() {
        _isLoading = false;
      });
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('加载授权列表失败: $e')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('授权码管理'),
        backgroundColor: const Color(0xFF07C160),
        foregroundColor: Colors.white,
        actions: [
          IconButton(
            icon: const Icon(Icons.add),
            onPressed: () => _showAddLicenseDialog(),
          ),
        ],
      ),
      backgroundColor: const Color(0xFFEDEDED),
      body: Column(
        children: [
          // 搜索和筛选栏
          Container(
            padding: const EdgeInsets.all(16),
            color: Colors.white,
            child: Column(
              children: [
                TextField(
                  decoration: const InputDecoration(
                    hintText: '搜索手机号',
                    prefixIcon: Icon(Icons.search),
                    border: OutlineInputBorder(),
                  ),
                  onChanged: (value) {
                    setState(() {
                      _searchText = value;
                    });
                    _loadLicenses();
                  },
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    const Text('状态筛选: '),
                    Expanded(
                      child: DropdownButton<String>(
                        value: _statusFilter,
                        isExpanded: true,
                        hint: const Text('全部'),
                        items: const [
                          DropdownMenuItem(value: null, child: Text('全部')),
                          DropdownMenuItem(value: 'active', child: Text('有效')),
                          DropdownMenuItem(value: 'expired', child: Text('过期')),
                          DropdownMenuItem(value: 'revoked', child: Text('已撤销')),
                        ],
                        onChanged: (value) {
                          setState(() {
                            _statusFilter = value;
                          });
                          _loadLicenses();
                        },
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          // 列表
          Expanded(
            child: _isLoading
                ? const Center(child: CircularProgressIndicator())
                : _licenses.isEmpty
                    ? const Center(child: Text('暂无授权数据'))
                    : ListView.builder(
                        itemCount: _licenses.length,
                        itemBuilder: (context, index) {
                          final license = _licenses[index];
                          return _buildLicenseItem(license);
                        },
                      ),
          ),
        ],
      ),
    );
  }

  Widget _buildLicenseItem(LicenseModel license) {
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: ListTile(
        title: Text(license.phone),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 4),
            Text('授权码: ${license.licenseKey}'),
            Text('绑定微信: ${license.boundWechatPhone ?? license.phone}'),
            Text('状态: ${_getStatusText(license)}'),
            Text('过期时间: ${_formatDate(license.expireDate)}'),
            if (license.hasManagePermission)
              const Text('管理权限: 是', style: TextStyle(color: Colors.orange)),
          ],
        ),
        trailing: PopupMenuButton(
          itemBuilder: (context) => [
            const PopupMenuItem(
              value: 'edit',
              child: Text('编辑'),
            ),
            const PopupMenuItem(
              value: 'extend',
              child: Text('延期'),
            ),
            const PopupMenuItem(
              value: 'delete',
              child: Text('删除'),
            ),
          ],
          onSelected: (value) {
            if (value == 'edit') {
              _showEditLicenseDialog(license);
            } else if (value == 'extend') {
              _showExtendLicenseDialog(license);
            } else if (value == 'delete') {
              _showDeleteConfirmDialog(license);
            }
          },
        ),
      ),
    );
  }

  String _getStatusText(LicenseModel license) {
    if (license.status == 'revoked') {
      return '已撤销';
    } else if (license.isExpired) {
      return '已过期';
    } else if (license.status == 'active') {
      return '有效';
    }
    return license.status;
  }

  String _formatDate(DateTime date) {
    return '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}';
  }

  void _showAddLicenseDialog() {
    // TODO: 实现添加授权对话框
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('添加功能待实现')),
    );
  }

  void _showEditLicenseDialog(LicenseModel license) {
    // TODO: 实现编辑授权对话框
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('编辑功能待实现')),
    );
  }

  void _showExtendLicenseDialog(LicenseModel license) {
    // TODO: 实现延期对话框
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('延期功能待实现')),
    );
  }

  void _showDeleteConfirmDialog(LicenseModel license) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('确认删除'),
        content: Text('确定要删除手机号 ${license.phone} 的授权吗？'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('取消'),
          ),
          TextButton(
            onPressed: () async {
              Navigator.pop(context);
              await _deleteLicense(license);
            },
            child: const Text('删除'),
          ),
        ],
      ),
    );
  }

  Future<void> _deleteLicense(LicenseModel license) async {
    try {
      final apiService = Provider.of<ApiService>(context, listen: false);
      final success = await apiService.deleteLicense(license.id);
      if (success) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('删除成功')),
          );
          _loadLicenses();
        }
      } else {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('删除失败')),
          );
        }
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('删除失败: $e')),
        );
      }
    }
  }
}

