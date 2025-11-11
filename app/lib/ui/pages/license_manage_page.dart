import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../services/api_service.dart';
import '../../models/license_model.dart';
import '../../utils/license_generator.dart';

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
    final phoneController = TextEditingController();
    final licenseKeyController = TextEditingController();
    final boundWechatPhoneController = TextEditingController();
    final expireDateController = TextEditingController();
    bool hasManagePermission = false;
    
    // 默认过期时间为一年后
    final defaultExpireDate = DateTime.now().add(const Duration(days: 365));
    expireDateController.text = _formatDate(defaultExpireDate);
    DateTime selectedExpireDate = defaultExpireDate;

    showDialog(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('添加授权用户'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextField(
                  controller: phoneController,
                  decoration: const InputDecoration(
                    labelText: '登录手机号 *',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 16),
                Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: licenseKeyController,
                        decoration: const InputDecoration(
                          labelText: '授权码（留空自动生成）',
                          border: OutlineInputBorder(),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    ElevatedButton(
                      onPressed: () {
                        licenseKeyController.text = LicenseGenerator.generate();
                        setDialogState(() {});
                      },
                      child: const Text('随机生成'),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                TextField(
                  controller: boundWechatPhoneController,
                  decoration: const InputDecoration(
                    labelText: '绑定微信手机号（留空等于登录手机号）',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 16),
                Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: expireDateController,
                        decoration: const InputDecoration(
                          labelText: '过期时间 *',
                          border: OutlineInputBorder(),
                        ),
                        readOnly: true,
                        onTap: () async {
                          final date = await showDatePicker(
                            context: context,
                            initialDate: selectedExpireDate,
                            firstDate: DateTime.now(),
                            lastDate: DateTime.now().add(const Duration(days: 3650)),
                          );
                          if (date != null) {
                            selectedExpireDate = date;
                            expireDateController.text = _formatDate(date);
                            setDialogState(() {});
                          }
                        },
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                CheckboxListTile(
                  title: const Text('授权码管理权限'),
                  value: hasManagePermission,
                  onChanged: (value) {
                    setDialogState(() {
                      hasManagePermission = value ?? false;
                    });
                  },
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('取消'),
            ),
            ElevatedButton(
              onPressed: () async {
                if (phoneController.text.trim().isEmpty) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('请输入手机号')),
                  );
                  return;
                }
                
                Navigator.pop(context);
                await _createLicense(
                  phone: phoneController.text.trim(),
                  licenseKey: licenseKeyController.text.trim().isEmpty 
                      ? null 
                      : licenseKeyController.text.trim(),
                  boundWechatPhone: boundWechatPhoneController.text.trim().isEmpty
                      ? null
                      : boundWechatPhoneController.text.trim(),
                  hasManagePermission: hasManagePermission,
                  expireDate: selectedExpireDate,
                );
              },
              child: const Text('添加'),
            ),
          ],
        ),
      ),
    );
  }
  
  Future<void> _createLicense({
    required String phone,
    String? licenseKey,
    String? boundWechatPhone,
    required bool hasManagePermission,
    required DateTime expireDate,
  }) async {
    try {
      final apiService = Provider.of<ApiService>(context, listen: false);
      await apiService.createLicense(
        phone: phone,
        licenseKey: licenseKey,
        boundWechatPhone: boundWechatPhone,
        hasManagePermission: hasManagePermission,
        expireDate: expireDate,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('添加成功')),
        );
        _loadLicenses();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('添加失败: $e')),
        );
      }
    }
  }

  void _showEditLicenseDialog(LicenseModel license) {
    final licenseKeyController = TextEditingController(text: license.licenseKey);
    final boundWechatPhoneController = TextEditingController(
      text: license.boundWechatPhone ?? license.phone,
    );
    final expireDateController = TextEditingController(
      text: _formatDate(license.expireDate),
    );
    bool hasManagePermission = license.hasManagePermission;
    String status = license.status;
    DateTime selectedExpireDate = license.expireDate;

    showDialog(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: Text('编辑授权 - ${license.phone}'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: TextField(
                        controller: licenseKeyController,
                        decoration: const InputDecoration(
                          labelText: '授权码',
                          border: OutlineInputBorder(),
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    ElevatedButton(
                      onPressed: () {
                        licenseKeyController.text = LicenseGenerator.generate();
                        setDialogState(() {});
                      },
                      child: const Text('随机生成'),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                TextField(
                  controller: boundWechatPhoneController,
                  decoration: const InputDecoration(
                    labelText: '绑定微信手机号',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 16),
                TextField(
                  controller: expireDateController,
                  decoration: const InputDecoration(
                    labelText: '过期时间',
                    border: OutlineInputBorder(),
                  ),
                  readOnly: true,
                  onTap: () async {
                    final date = await showDatePicker(
                      context: context,
                      initialDate: selectedExpireDate,
                      firstDate: DateTime.now(),
                      lastDate: DateTime.now().add(const Duration(days: 3650)),
                    );
                    if (date != null) {
                      selectedExpireDate = date;
                      expireDateController.text = _formatDate(date);
                      setDialogState(() {});
                    }
                  },
                ),
                const SizedBox(height: 16),
                CheckboxListTile(
                  title: const Text('授权码管理权限'),
                  value: hasManagePermission,
                  onChanged: (value) {
                    setDialogState(() {
                      hasManagePermission = value ?? false;
                    });
                  },
                ),
                const SizedBox(height: 16),
                DropdownButtonFormField<String>(
                  value: status,
                  decoration: const InputDecoration(
                    labelText: '状态',
                    border: OutlineInputBorder(),
                  ),
                  items: const [
                    DropdownMenuItem(value: 'active', child: Text('有效')),
                    DropdownMenuItem(value: 'expired', child: Text('过期')),
                    DropdownMenuItem(value: 'revoked', child: Text('已撤销')),
                  ],
                  onChanged: (value) {
                    setDialogState(() {
                      status = value ?? 'active';
                    });
                  },
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('取消'),
            ),
            ElevatedButton(
              onPressed: () async {
                Navigator.pop(context);
                await _updateLicense(
                  license: license,
                  licenseKey: licenseKeyController.text.trim(),
                  boundWechatPhone: boundWechatPhoneController.text.trim(),
                  hasManagePermission: hasManagePermission,
                  status: status,
                  expireDate: selectedExpireDate,
                );
              },
              child: const Text('保存'),
            ),
          ],
        ),
      ),
    );
  }
  
  Future<void> _updateLicense({
    required LicenseModel license,
    required String licenseKey,
    required String boundWechatPhone,
    required bool hasManagePermission,
    required String status,
    required DateTime expireDate,
  }) async {
    try {
      final apiService = Provider.of<ApiService>(context, listen: false);
      await apiService.updateLicense(
        licenseId: license.id,
        licenseKey: licenseKey != license.licenseKey ? licenseKey : null,
        boundWechatPhone: boundWechatPhone != (license.boundWechatPhone ?? license.phone) 
            ? boundWechatPhone 
            : null,
        hasManagePermission: hasManagePermission != license.hasManagePermission 
            ? hasManagePermission 
            : null,
        status: status != license.status ? status : null,
        expireDate: expireDate != license.expireDate ? expireDate : null,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('更新成功')),
        );
        _loadLicenses();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('更新失败: $e')),
        );
      }
    }
  }

  void _showExtendLicenseDialog(LicenseModel license) {
    final daysController = TextEditingController();
    final monthsController = TextEditingController();
    final yearsController = TextEditingController(text: '1'); // 默认一年

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: Text('延期授权 - ${license.phone}'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('当前过期时间: ${_formatDate(license.expireDate)}'),
              const SizedBox(height: 16),
              TextField(
                controller: daysController,
                decoration: const InputDecoration(
                  labelText: '延期天数',
                  border: OutlineInputBorder(),
                ),
                keyboardType: TextInputType.number,
              ),
              const SizedBox(height: 16),
              TextField(
                controller: monthsController,
                decoration: const InputDecoration(
                  labelText: '延期月数',
                  border: OutlineInputBorder(),
                ),
                keyboardType: TextInputType.number,
              ),
              const SizedBox(height: 16),
              TextField(
                controller: yearsController,
                decoration: const InputDecoration(
                  labelText: '延期年数（默认1年）',
                  border: OutlineInputBorder(),
                ),
                keyboardType: TextInputType.number,
              ),
              const SizedBox(height: 16),
              Wrap(
                spacing: 8,
                children: [
                  ElevatedButton(
                    onPressed: () {
                      daysController.text = '7';
                      monthsController.text = '';
                      yearsController.text = '';
                    },
                    child: const Text('7天'),
                  ),
                  ElevatedButton(
                    onPressed: () {
                      daysController.text = '30';
                      monthsController.text = '';
                      yearsController.text = '';
                    },
                    child: const Text('30天'),
                  ),
                  ElevatedButton(
                    onPressed: () {
                      daysController.text = '';
                      monthsController.text = '1';
                      yearsController.text = '';
                    },
                    child: const Text('1个月'),
                  ),
                  ElevatedButton(
                    onPressed: () {
                      daysController.text = '';
                      monthsController.text = '';
                      yearsController.text = '1';
                    },
                    child: const Text('1年'),
                  ),
                ],
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('取消'),
          ),
          ElevatedButton(
            onPressed: () async {
              final days = daysController.text.trim().isEmpty 
                  ? null 
                  : int.tryParse(daysController.text.trim());
              final months = monthsController.text.trim().isEmpty 
                  ? null 
                  : int.tryParse(monthsController.text.trim());
              final years = yearsController.text.trim().isEmpty 
                  ? null 
                  : int.tryParse(yearsController.text.trim());
              
              if (days == null && months == null && years == null) {
                // 如果都没有输入，使用默认值1年
                Navigator.pop(context);
                await _extendLicense(license, days: null, months: null, years: 1);
              } else {
                Navigator.pop(context);
                await _extendLicense(license, days: days, months: months, years: years);
              }
            },
            child: const Text('延期'),
          ),
        ],
      ),
    );
  }
  
  Future<void> _extendLicense(
    LicenseModel license, {
    int? days,
    int? months,
    int? years,
  }) async {
    try {
      final apiService = Provider.of<ApiService>(context, listen: false);
      await apiService.extendLicense(
        licenseId: license.id,
        days: days,
        months: months,
        years: years,
      );
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('延期成功')),
        );
        _loadLicenses();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('延期失败: $e')),
        );
      }
    }
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

