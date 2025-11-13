import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:cached_network_image/cached_network_image.dart';
import '../../services/websocket_service.dart';
import '../../services/api_service.dart';
import '../pages/home_page.dart';

/// 登录页面
class LoginPage extends StatefulWidget {
  const LoginPage({super.key});

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final TextEditingController _phoneController = TextEditingController();
  final TextEditingController _licenseKeyController = TextEditingController();
  bool _isLoading = false;
  String? _errorMessage;
  List<Map<String, dynamic>> _loginHistory = [];
  bool _rememberAccount = false;

  @override
  void initState() {
    super.initState();
    // 确保 _rememberAccount 始终有值
    _rememberAccount = false;
    _loadLoginHistory();
    _loadRememberedAccount();
  }

  @override
  void dispose() {
    _phoneController.dispose();
    _licenseKeyController.dispose();
    super.dispose();
  }

  /// 加载登录历史
  Future<void> _loadLoginHistory() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final historyJson = prefs.getString('login_history');
      if (historyJson != null) {
        final List<dynamic> historyList = jsonDecode(historyJson);
        setState(() {
          _loginHistory = historyList
              .map((item) => item as Map<String, dynamic>)
              .toList();
        });
      }
    } catch (e) {
      print('加载登录历史失败: $e');
    }
  }

  /// 加载记住的账号信息
  Future<void> _loadRememberedAccount() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final rememberedPhone = prefs.getString('remembered_phone');
      final rememberedLicenseKey = prefs.getString('remembered_license_key');
      
      if (rememberedPhone != null && rememberedLicenseKey != null) {
        setState(() {
          _phoneController.text = rememberedPhone;
          _licenseKeyController.text = rememberedLicenseKey;
          _rememberAccount = true;
        });
      }
    } catch (e) {
      print('加载记住的账号信息失败: $e');
    }
  }

  /// 保存记住的账号信息
  Future<void> _saveRememberedAccount(String phone, String licenseKey) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      if (_rememberAccount) {
        await prefs.setString('remembered_phone', phone);
        await prefs.setString('remembered_license_key', licenseKey);
        print('已保存记住的账号信息');
      } else {
        await prefs.remove('remembered_phone');
        await prefs.remove('remembered_license_key');
        print('已清除记住的账号信息');
      }
    } catch (e) {
      print('保存记住的账号信息失败: $e');
    }
  }

  /// 保存登录历史
  Future<void> _saveLoginHistory(Map<String, dynamic> accountInfo) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final historyJson = prefs.getString('login_history');
      List<Map<String, dynamic>> historyList = [];
      
      if (historyJson != null) {
        final List<dynamic> decoded = jsonDecode(historyJson);
        historyList = decoded
            .map((item) => item as Map<String, dynamic>)
            .toList();
      }
      
      // 移除已存在的相同wxid的记录
      historyList.removeWhere((item) => item['wxid'] == accountInfo['wxid']);
      
      // 添加到最前面
      historyList.insert(0, {
        'wxid': accountInfo['wxid'],
        'nickname': accountInfo['nickname'] ?? '',
        'avatar': accountInfo['avatar'] ?? '',
        'phone': accountInfo['phone'] ?? '',
        'login_time': DateTime.now().toIso8601String(),
      });
      
      // 最多保留10条历史记录
      if (historyList.length > 10) {
        historyList = historyList.sublist(0, 10);
      }
      
      final updatedJson = jsonEncode(historyList);
      await prefs.setString('login_history', updatedJson);
      
      setState(() {
        _loginHistory = historyList;
      });
    } catch (e) {
      print('保存登录历史失败: $e');
    }
  }

  /// 删除登录历史
  Future<void> _deleteLoginHistory(String wxid) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final historyJson = prefs.getString('login_history');
      if (historyJson != null) {
        final List<dynamic> decoded = jsonDecode(historyJson);
        List<Map<String, dynamic>> historyList = decoded
            .map((item) => item as Map<String, dynamic>)
            .toList();
        
        // 移除指定wxid的记录
        historyList.removeWhere((item) => item['wxid'] == wxid);
        
        final updatedJson = jsonEncode(historyList);
        await prefs.setString('login_history', updatedJson);
        
        setState(() {
          _loginHistory = historyList;
        });
        
        print('已删除登录历史: $wxid');
      }
    } catch (e) {
      print('删除登录历史失败: $e');
    }
  }

  /// 登录（手机号+授权码）
  Future<void> _login() async {
    final phone = _phoneController.text.trim();
    final licenseKey = _licenseKeyController.text.trim();
    
    if (phone.isEmpty) {
      setState(() {
        _errorMessage = '请输入手机号';
      });
      return;
    }

    if (licenseKey.isEmpty) {
      setState(() {
        _errorMessage = '请输入授权码';
      });
      return;
    }

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final wsService = Provider.of<WebSocketService>(context, listen: false);
      final apiService = Provider.of<ApiService>(context, listen: false);
      
      // 检查WebSocket连接状态（备用检查）
      if (!wsService.isConnected) {
        // 如果未连接，尝试建立连接
        String wsUrl = apiService.serverUrl.replaceFirst('http://', 'ws://').replaceFirst('https://', 'wss://');
        if (!wsUrl.endsWith('/ws')) {
          wsUrl = wsUrl.endsWith('/') ? '${wsUrl}ws' : '$wsUrl/ws';
        }
        
        print('WebSocket未连接，正在建立连接: $wsUrl');
        final connected = await wsService.connect(wsUrl);
        if (!connected) {
          setState(() {
            _errorMessage = 'WebSocket连接失败，请检查网络连接';
            _isLoading = false;
          });
          return;
        }
      }
      
      // 使用手机号+授权码登录
      final success = await wsService.login(phone, licenseKey);
      
      if (success) {
        // 保存或清除记住的账号信息
        await _saveRememberedAccount(phone, licenseKey);
        
        // 登录成功后，根据手机号从服务器获取账号信息
        try {
          final accountInfo = await apiService.getAccountInfo(phone: phone);
          if (accountInfo != null && accountInfo['phone'] == phone) {
            // 更新WebSocketService中的账号信息
            wsService.updateMyInfo(accountInfo);
            // 保存登录历史
            await _saveLoginHistory(accountInfo);
            print('登录成功，已更新账号信息: ${accountInfo['nickname']} (${accountInfo['phone']})');
          } else {
            print('警告: 未找到手机号为 $phone 的账号信息，可能是新账号还没有同步，等待服务器同步');
            // 清除可能存在的旧账号信息，避免显示错误的账号
            // 等待服务器通过 sync_my_info 消息同步正确的账号信息
            wsService.updateMyInfo({});
          }
        } catch (e) {
          print('获取账号信息失败: $e');
          // 即使获取账号信息失败，也允许登录，因为可能是新账号还没有同步
          // 清除可能存在的旧账号信息，避免显示错误的账号
          // 等待服务器通过 sync_my_info 消息同步正确的账号信息
          wsService.updateMyInfo({});
        }
        
        // 保存登录状态
        await wsService.saveLoginState();
        
        // 跳转到主页
        if (mounted) {
          Navigator.of(context).pushReplacement(
            MaterialPageRoute(builder: (_) => const HomePage()),
          );
        }
      } else {
        setState(() {
          _errorMessage = '手机号或授权码错误';
        });
      }
    } catch (e) {
      setState(() {
        _errorMessage = '登录失败: $e';
      });
    } finally {
      setState(() {
        _isLoading = false;
      });
    }
  }

  /// 快速登录
  Future<void> _quickLogin(Map<String, dynamic> accountInfo) async {
    final wxid = accountInfo['wxid'] as String?;
    if (wxid == null || wxid.isEmpty) {
      setState(() {
        _errorMessage = '账号信息不完整';
      });
      return;
    }

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final wsService = Provider.of<WebSocketService>(context, listen: false);
      final apiService = Provider.of<ApiService>(context, listen: false);
      
      // 检查WebSocket连接状态（备用检查）
      if (!wsService.isConnected) {
        // 如果未连接，尝试建立连接
        String wsUrl = apiService.serverUrl.replaceFirst('http://', 'ws://').replaceFirst('https://', 'wss://');
        if (!wsUrl.endsWith('/ws')) {
          wsUrl = wsUrl.endsWith('/') ? '${wsUrl}ws' : '$wsUrl/ws';
        }
        
        print('WebSocket未连接，正在建立连接: $wsUrl');
        final connected = await wsService.connect(wsUrl);
        if (!connected) {
          setState(() {
            _errorMessage = 'WebSocket连接失败，请检查网络连接';
            _isLoading = false;
          });
          return;
        }
      }
      
      final success = await wsService.quickLogin(wxid);
      
      if (success && wsService.myInfo != null) {
        // 更新登录历史
        await _saveLoginHistory(wsService.myInfo!);
        
        // 保存登录状态
        await wsService.saveLoginState();
        
        // 跳转到主页
        if (mounted) {
          Navigator.of(context).pushReplacement(
            MaterialPageRoute(builder: (_) => const HomePage()),
          );
        }
      } else {
        setState(() {
          _errorMessage = '快速登录失败';
        });
      }
    } catch (e) {
      setState(() {
        _errorMessage = '快速登录失败: $e';
      });
    } finally {
      setState(() {
        _isLoading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFEDEDED),
      body: SafeArea(
          child: Column(
            children: [
              const SizedBox(height: 60),
              // Logo - 无背景
              Image.asset(
                'assets/images/logo.png',
                width: 80,
                height: 80,
              fit: BoxFit.contain,
                errorBuilder: (context, error, stackTrace) {
                // 如果logo图片加载失败，使用图标作为fallback
                  return const Icon(
                    Icons.chat_bubble,
                    size: 80,
                    color: Color(0xFF07C160),
                  );
                },
              ),
              const SizedBox(height: 60),
              
              // 登录历史列表 - 微信风格
              if (_loginHistory.isNotEmpty) ...[
                Container(
                  margin: const EdgeInsets.symmetric(horizontal: 20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        '最近登录',
                        style: TextStyle(
                          fontSize: 14,
                          color: Color(0xFF999999),
                        ),
                      ),
                      const SizedBox(height: 12),
                      ..._loginHistory.map((account) => _buildHistoryItem(account)),
                      const SizedBox(height: 20),
                    ],
                  ),
                ),
              ],
              
              // 登录表单 - 微信风格
            Expanded(
              child: SingleChildScrollView(
                child: Container(
                margin: const EdgeInsets.symmetric(horizontal: 20),
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(4),
                ),
                child: Column(
                    mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // 手机号输入 - 微信风格（下划线输入框）
                    TextField(
                      controller: _phoneController,
                      keyboardType: TextInputType.phone,
                      style: const TextStyle(fontSize: 16),
                      inputFormatters: [
                        FilteringTextInputFormatter.digitsOnly, // 只允许数字
                      ],
                      decoration: InputDecoration(
                        hintText: '手机号',
                        hintStyle: const TextStyle(
                          color: Color(0xFF999999),
                          fontSize: 16,
                        ),
                        prefixIcon: const Icon(
                          Icons.phone_android,
                          color: Color(0xFF999999),
                          size: 20,
                        ),
                        border: UnderlineInputBorder(
                          borderSide: BorderSide(
                            color: Colors.grey[300]!,
                            width: 0.5,
                          ),
                        ),
                        enabledBorder: UnderlineInputBorder(
                          borderSide: BorderSide(
                            color: Colors.grey[300]!,
                            width: 0.5,
                          ),
                        ),
                        focusedBorder: const UnderlineInputBorder(
                          borderSide: BorderSide(
                            color: Color(0xFF07C160),
                            width: 1,
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(height: 20),
                    
                    // 授权码输入 - 微信风格
                    TextField(
                      controller: _licenseKeyController,
                            style: const TextStyle(fontSize: 16),
                      inputFormatters: [
                        FilteringTextInputFormatter.deny(RegExp(r'\s')), // 禁止空格
                      ],
                            decoration: InputDecoration(
                        hintText: '授权码',
                              hintStyle: const TextStyle(
                                color: Color(0xFF999999),
                                fontSize: 16,
                              ),
                              prefixIcon: const Icon(
                          Icons.vpn_key,
                                color: Color(0xFF999999),
                                size: 20,
                              ),
                              border: UnderlineInputBorder(
                                borderSide: BorderSide(
                                  color: Colors.grey[300]!,
                                  width: 0.5,
                                ),
                              ),
                              enabledBorder: UnderlineInputBorder(
                                borderSide: BorderSide(
                                  color: Colors.grey[300]!,
                                  width: 0.5,
                                ),
                              ),
                              focusedBorder: const UnderlineInputBorder(
                                borderSide: BorderSide(
                                  color: Color(0xFF07C160),
                                  width: 1,
                                ),
                              ),
                            ),
                    ),
                    const SizedBox(height: 16),
                    
                    // 记住账号复选框
                    Row(
                      children: [
                        Checkbox(
                          value: _rememberAccount,
                          tristate: false,
                          onChanged: (bool? value) {
                            setState(() {
                              _rememberAccount = value ?? false;
                            });
                          },
                          activeColor: const Color(0xFF07C160),
                        ),
                        const Text(
                          '记住账号',
                          style: TextStyle(
                            fontSize: 14,
                            color: Color(0xFF333333),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 20),
                    
                    // 错误提示
                    if (_errorMessage != null)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 16),
                        child: Text(
                          _errorMessage!,
                          style: const TextStyle(
                            color: Colors.red,
                            fontSize: 12,
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ),
                    
                    // 登录按钮 - 微信风格（绿色圆角按钮）
                    SizedBox(
                      height: 44,
                      child: ElevatedButton(
                        onPressed: _isLoading ? null : _login,
                        style: ElevatedButton.styleFrom(
                          backgroundColor: const Color(0xFF07C160),
                          foregroundColor: Colors.white,
                          disabledBackgroundColor: const Color(0xFFB2E5B8),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(4),
                          ),
                          elevation: 0,
                        ),
                        child: _isLoading
                            ? const SizedBox(
                                height: 20,
                                width: 20,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  valueColor: AlwaysStoppedAnimation<Color>(Colors.white),
                                ),
                              )
                            : const Text(
                                '登录',
                                style: TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.w500,
                                ),
                              ),
                      ),
                    ),
                  ],
                  ),
                ),
                ),
              ),
            ],
        ),
      ),
    );
  }

  /// 构建历史记录项 - 微信风格
  Widget _buildHistoryItem(Map<String, dynamic> account) {
    final avatar = account['avatar'] as String? ?? '';
    final nickname = account['nickname'] as String? ?? '未知';
    final phone = account['phone'] as String? ?? '';
    final wxid = account['wxid'] as String? ?? '';
    
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 16),
      margin: const EdgeInsets.only(bottom: 1),
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(
          bottom: BorderSide(
            color: Color(0xFFE5E5E5),
            width: 0.5,
          ),
        ),
      ),
      child: Row(
        children: [
          // 头像 - 微信风格（圆形）
          ClipOval(
            child: avatar.isNotEmpty
                ? CachedNetworkImage(
                    imageUrl: avatar,
                    width: 45,
                    height: 45,
                    fit: BoxFit.cover,
                    errorWidget: (context, url, error) => Container(
                      width: 45,
                      height: 45,
                      color: const Color(0xFF07C160),
                      child: const Icon(
                        Icons.person,
                        color: Colors.white,
                        size: 25,
                      ),
                    ),
                  )
                : Container(
                    width: 45,
                    height: 45,
                    color: const Color(0xFF07C160),
                    child: const Icon(
                      Icons.person,
                      color: Colors.white,
                      size: 25,
                    ),
                  ),
          ),
          const SizedBox(width: 12),
          // 昵称和手机号 - 可点击区域
          Expanded(
            child: InkWell(
              onTap: () => _quickLogin(account),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    nickname,
                    style: const TextStyle(
                      fontSize: 16,
                      color: Colors.black87,
                    ),
                  ),
                  if (phone.isNotEmpty) ...[
                    const SizedBox(height: 4),
                    Text(
                      phone,
                      style: const TextStyle(
                        fontSize: 14,
                        color: Color(0xFF999999),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
          // 删除按钮
          IconButton(
            icon: const Icon(
              Icons.delete_outline,
              color: Color(0xFF999999),
              size: 20,
            ),
            onPressed: () {
              if (wxid.isNotEmpty) {
                _deleteLoginHistory(wxid);
              }
            },
            padding: EdgeInsets.zero,
            constraints: const BoxConstraints(
              minWidth: 40,
              minHeight: 40,
            ),
          ),
          // 右箭头 - 微信风格
          InkWell(
            onTap: () => _quickLogin(account),
            child: const Icon(
              Icons.chevron_right,
              color: Color(0xFFC7C7CC),
              size: 20,
            ),
          ),
        ],
      ),
    );
  }
}

