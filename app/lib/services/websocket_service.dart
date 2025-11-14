import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:web_socket_channel/web_socket_channel.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../models/contact_model.dart';
import '../models/moments_model.dart';
import '../models/official_account_model.dart';
import '../main.dart' as main_app;
import '../utils/encryption_service.dart';

/// WebSocket服务
/// 负责与服务器建立WebSocket连接，接收数据并发送命令
/// 支持WebSocket密钥交换协议
class WebSocketService extends ChangeNotifier {
  WebSocketChannel? _channel;
  bool _isConnected = false;
  String _serverUrl = 'ws://localhost:8000/ws';
  final EncryptionService _encryptionService = EncryptionService();

  List<ContactModel> _contacts = [];
  List<MomentsModel> _moments = [];
  List<OfficialAccountMessage> _officialAccountMessages = [];
  Map<String, dynamic>? _myInfo;
  String? _currentWeChatId; // 当前微信账号ID
  String? _loggedInPhone; // 当前登录的手机号

  bool get isConnected => _isConnected;
  List<ContactModel> get contacts => _contacts;
  List<MomentsModel> get moments => _moments;
  List<OfficialAccountMessage> get officialAccountMessages => _officialAccountMessages;
  Map<String, dynamic>? get myInfo => _myInfo;

  /// 连接WebSocket服务器
  Future<bool> connect(String serverUrl, {Duration timeout = const Duration(seconds: 5)}) async {
    Completer<bool> connectionCompleter = Completer<bool>();
    bool connectionFailed = false;
    
    try {
      // 如果已经连接，先断开
      if (_isConnected) {
        disconnect();
      }

      _serverUrl = serverUrl;
      
      // 创建连接
      try {
        _channel = WebSocketChannel.connect(Uri.parse(serverUrl));
      } catch (e) {
        print('创建WebSocket通道失败: $e');
        _isConnected = false;
        notifyListeners();
        return false;
      }

      // 设置消息监听器
      StreamSubscription? subscription;
      bool firstMessageReceived = false;
      
      subscription = _channel!.stream.listen(
        (message) {
          // 收到第一条消息，说明连接成功
          if (!firstMessageReceived) {
            firstMessageReceived = true;
            if (!connectionCompleter.isCompleted) {
              connectionCompleter.complete(true);
            }
          }
          _handleMessage(message);
        },
        onError: (error) {
          print('WebSocket连接错误: $error');
          connectionFailed = true;
          _isConnected = false;
          notifyListeners();
          if (!connectionCompleter.isCompleted) {
            connectionCompleter.complete(false);
          }
        },
        onDone: () {
          print('WebSocket连接已关闭');
          _isConnected = false;
          notifyListeners();
          if (!connectionCompleter.isCompleted && !connectionFailed) {
            connectionCompleter.complete(false);
          }
        },
        cancelOnError: false,
      );

      // 等待连接建立，使用超时
      try {
        // 在 Web 平台上，使用更短的等待时间，避免浏览器显示"响应时间太长"
        final isWeb = kIsWeb;
        final initialDelay = isWeb ? const Duration(milliseconds: 100) : const Duration(milliseconds: 200);
        final checkDelay = isWeb ? const Duration(milliseconds: 150) : const Duration(milliseconds: 300);
        
        // 先等待一小段时间，让连接有机会建立（在浏览器中，连接是异步的）
        await Future.delayed(initialDelay);
        
        // 检查是否已经连接失败
        if (connectionFailed) {
          print('WebSocket连接已失败');
          subscription?.cancel();
          if (_channel != null) {
            try {
              _channel!.sink.close();
            } catch (_) {}
            _channel = null;
          }
          _isConnected = false;
          notifyListeners();
          return false;
        }
        
        // 等待一小段时间，确保接收任务已启动（用于接收RSA公钥）
        await Future.delayed(const Duration(milliseconds: 100));
        
        // 尝试发送客户端类型消息来验证连接（明文，密钥交换前）
        // 如果连接成功，发送消息不会抛出异常
        // 如果连接失败，onError 会被调用，connectionFailed 会被设置为 true
        try {
          _channel!.sink.add(jsonEncode({
            'type': 'client_type',
            'client_type': 'app',
          }));
        } catch (e) {
          print('发送客户端类型消息失败: $e');
          connectionFailed = true;
          if (!connectionCompleter.isCompleted) {
            connectionCompleter.complete(false);
          }
        }
        
        // 再等待一小段时间，检查是否有错误
        await Future.delayed(checkDelay);
        
        // 如果连接失败，直接返回
        if (connectionFailed) {
          print('WebSocket连接失败（检测到错误）');
          subscription?.cancel();
          if (_channel != null) {
            try {
              _channel!.sink.close();
            } catch (_) {}
            _channel = null;
          }
          _isConnected = false;
          notifyListeners();
          return false;
        }
        
        // 如果收到第一条消息，说明连接成功
        // 否则，如果发送消息成功且没有错误，也认为连接成功
        // 使用超时等待，但即使超时，如果发送成功且没有错误，也认为连接成功
        bool connected = false;
        try {
          // 在 Web 平台上，使用更短的超时时间
          final waitTimeout = isWeb ? const Duration(milliseconds: 1000) : const Duration(milliseconds: 2000);
          connected = await connectionCompleter.future.timeout(
            waitTimeout,
            onTimeout: () {
              // 超时不一定意味着连接失败
              // 如果发送消息成功且没有错误，连接可能已经建立
              // 服务器可能不会立即响应，所以超时也视为成功（如果发送成功）
              return !connectionFailed; // 如果发送成功且没有错误，认为连接成功
            },
          );
        } catch (e) {
          print('等待连接确认时出错: $e');
          // 如果发送消息成功且没有错误，仍然认为连接成功
          connected = !connectionFailed;
        }
        
        if (!connected) {
          print('WebSocket连接失败或超时');
          subscription?.cancel();
          if (_channel != null) {
            try {
              _channel!.sink.close();
            } catch (_) {}
            _channel = null;
          }
          _isConnected = false;
          notifyListeners();
          return false;
        }
        
        // 连接成功
        _isConnected = true;
        notifyListeners();
        
        // 设置WebSocket链接到全局变量
        if (kIsWeb) {
          main_app.setWebSocketUrl(_serverUrl);
        }
        
        // 加载本地保存的账号信息
        await _loadMyInfoFromLocal();
        
        return true;
      } on TimeoutException catch (e) {
        print('WebSocket连接超时: $e');
        subscription?.cancel();
        if (_channel != null) {
          try {
            _channel!.sink.close();
          } catch (_) {}
          _channel = null;
        }
        _isConnected = false;
        notifyListeners();
        return false;
      }
    } catch (e) {
      print('WebSocket连接失败: $e');
      if (!connectionCompleter.isCompleted) {
        connectionCompleter.complete(false);
      }
      _isConnected = false;
      if (_channel != null) {
        try {
          _channel!.sink.close();
        } catch (_) {}
        _channel = null;
      }
      notifyListeners();
      return false;
    }
  }
  

  /// 发送客户端类型（明文，密钥交换前）
  void _sendClientType() {
    if (_channel != null) {
      try {
        _sendMessagePlain({
          'type': 'client_type',
          'client_type': 'app',
        });
        // 不单独打印，已在连接信息中显示
      } catch (e) {
        print('发送客户端类型失败: $e');
      }
    }
  }

  /// 断开连接
  void disconnect() {
    _channel?.sink.close();
    _channel = null;
    _isConnected = false;
    notifyListeners();
  }

  /// 处理密钥交换消息
  bool _handleKeyExchangeMessage(Map<String, dynamic> data) {
    try {
      final type = data['type'] as String?;

      // 处理RSA公钥消息
      if (type == 'rsa_public_key') {
        final publicKeyPem = data['public_key'] as String?;
        if (publicKeyPem != null && publicKeyPem.isNotEmpty) {
          // 设置服务器公钥
          if (_encryptionService.setServerPublicKey(publicKeyPem)) {
            print('已接收服务器RSA公钥');

            // 生成随机会话密钥（32字节）
            final sessionKey = _encryptionService.generateSessionKey();

            // 使用RSA公钥加密会话密钥
            final encryptedSessionKey = _encryptionService.encryptSessionKey(sessionKey);
            if (encryptedSessionKey != null) {
              // 设置会话密钥
              _encryptionService.setSessionKey(sessionKey);

              // 发送加密的会话密钥给服务器（明文，密钥交换阶段）
              _sendMessagePlain({
                'type': 'session_key',
                'encrypted_key': encryptedSessionKey,
              });

              print('已发送加密的会话密钥给服务器');
              return true;
            }
          }
        }
      }

      // 处理密钥交换成功消息
      if (type == 'key_exchange_success') {
        print('密钥交换成功，后续通讯将使用会话密钥加密');
        return true;
      }

      return false;
    } catch (e) {
      print('处理密钥交换消息失败: $e');
      return false;
    }
  }

  /// 发送明文消息（用于密钥交换阶段）
  void _sendMessagePlain(Map<String, dynamic> message) {
    if (_channel != null) {
      try {
        _channel!.sink.add(jsonEncode(message));
      } catch (e) {
        print('发送WebSocket明文消息失败: $e');
      }
    }
  }

  /// 处理接收到的消息
  void _handleMessage(dynamic message) {
    try {
      print('========== 收到WebSocket消息 ==========');
      print('原始消息: $message');
      print('消息类型: ${message.runtimeType}');
      
      // 尝试解密消息（如果已设置会话密钥）
      String decryptedMessage;
      try {
        final messageObj = jsonDecode(message);
        if (messageObj is Map &&
            messageObj['encrypted'] == true &&
            messageObj['data'] != null) {
          // 加密消息，需要解密（使用会话密钥）
          final encryptedData = messageObj['data'] as String;
          final decrypted = _encryptionService.decryptStringForCommunication(encryptedData);
          if (decrypted != null) {
            decryptedMessage = decrypted;
          } else {
            // 解密失败，使用原始消息
            decryptedMessage = message;
          }
        } else {
          // 非加密消息或格式不正确，直接使用原始消息
          decryptedMessage = message;
        }
      } catch (e) {
        // 解析失败，可能是非JSON格式的明文消息，直接使用
        decryptedMessage = message;
      }
      
      Map<String, dynamic> data = jsonDecode(decryptedMessage);
      print('解析后的数据: $data');

      String type = data['type'] ?? '';
      print('消息类型: $type');

      // 处理密钥交换消息
      if (_handleKeyExchangeMessage(data)) {
        // 密钥交换消息已处理，不继续处理
        return;
      }

      switch (type) {
        case 'sync_contacts':
          print('处理好友列表同步，数据数量: ${data['data']?.length ?? 0}');
          if (data['data'] != null && data['data'] is List) {
            _handleContactsSync(data['data']);
          } else {
            print('警告: sync_contacts 数据格式不正确，data 不是 List 类型');
            print('data 类型: ${data['data'].runtimeType}');
            print('data 内容: ${data['data']}');
          }
          break;
        case 'sync_moments':
          print('处理朋友圈同步，数据数量: ${data['data']?.length ?? 0}');
          _handleMomentsSync(data['data']);
          break;
        case 'sync_chat_message':
          print('处理聊天消息同步');
          _handleChatMessageSync(data['data']);
          break;
        case 'sync_official_account':
          print('处理公众号消息同步');
          _handleOfficialAccountSync(data['data']);
          break;
        case 'sync_tags':
          print('处理标签同步');
          _handleTagsSync(data['data']);
          break;
        case 'sync_my_info':
          print('处理我的信息同步');
          if (data['data'] != null && data['data'] is Map) {
            _handleMyInfoSync(data['data']);
          } else {
            print('警告: sync_my_info 数据格式不正确，data 不是 Map 类型');
            print('data 类型: ${data['data'].runtimeType}');
            print('data 内容: ${data['data']}');
          }
          break;
        case 'command_result':
          _handleCommandResult(data);
          break;
        case 'login_response':
          _handleLoginResponse(data);
          break;
        case 'verify_login_code_response':
          _handleVerifyLoginCodeResponse(data);
          break;
        case 'quick_login_response':
          _handleQuickLoginResponse(data);
          break;
        default:
          print('未知消息类型: $type');
      }
      print('========== 消息处理完成 ==========');
    } catch (e) {
      print('处理消息失败: $e');
      print('消息内容: $message');
      print('堆栈跟踪: ${StackTrace.current}');
    }
  }
  
  // 登录相关的Completer
  Completer<bool>? _loginCompleter;
  Completer<bool>? _verifyLoginCodeCompleter;
  Completer<bool>? _quickLoginCompleter;
  
  // 命令结果相关的Completer（command_id -> Completer）
  final Map<String, Completer<Map<String, dynamic>>> _commandCompleters = {};
  
  /// 处理登录响应
  void _handleLoginResponse(Map<String, dynamic> data) {
    final success = data['success'] as bool? ?? false;
    final message = data['message'] as String? ?? '';
    final hasManagePermission = data['has_manage_permission'] as bool? ?? false;
    print('登录响应: success=$success, message=$message, hasManagePermission=$hasManagePermission');
    
    if (success) {
      // 保存管理权限到本地
      _saveManagePermission(hasManagePermission);
    }
    
    if (_loginCompleter != null && !_loginCompleter!.isCompleted) {
      _loginCompleter!.complete(success);
    }
  }
  
  /// 保存管理权限到本地
  Future<void> _saveManagePermission(bool hasPermission) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setBool('has_manage_permission', hasPermission);
      print('管理权限已保存: $hasPermission');
    } catch (e) {
      print('保存管理权限失败: $e');
    }
  }
  
  /// 处理验证登录码响应
  void _handleVerifyLoginCodeResponse(Map<String, dynamic> data) {
    final success = data['success'] as bool? ?? false;
    final message = data['message'] as String? ?? '';
    print('验证登录码响应: success=$success, message=$message');
    
    if (success) {
      final accountInfo = data['account_info'] as Map<String, dynamic>?;
      if (accountInfo != null) {
        _myInfo = accountInfo;
        _currentWeChatId = accountInfo['wxid']?.toString();
        _saveMyInfoToLocal(accountInfo);
        notifyListeners();
      }
    }
    
    if (_verifyLoginCodeCompleter != null && !_verifyLoginCodeCompleter!.isCompleted) {
      _verifyLoginCodeCompleter!.complete(success);
    }
  }
  
  /// 处理快速登录响应
  void _handleQuickLoginResponse(Map<String, dynamic> data) {
    final success = data['success'] as bool? ?? false;
    final message = data['message'] as String? ?? '';
    print('快速登录响应: success=$success, message=$message');
    
    if (success) {
      final accountInfo = data['account_info'] as Map<String, dynamic>?;
      if (accountInfo != null) {
        // 更新登录手机号（快速登录时使用账号信息中的手机号）
        final accountPhone = accountInfo['phone']?.toString() ?? '';
        if (accountPhone.isNotEmpty) {
          _loggedInPhone = accountPhone;
        }
        
        _myInfo = accountInfo;
        _currentWeChatId = accountInfo['wxid']?.toString();
        _saveMyInfoToLocal(accountInfo);
        notifyListeners();
      }
    }
    
    if (_quickLoginCompleter != null && !_quickLoginCompleter!.isCompleted) {
      _quickLoginCompleter!.complete(success);
    }
  }
  
  /// 登录（手机号+授权码）
  Future<bool> login(String phone, String licenseKey) async {
    _loginCompleter = Completer<bool>();
    _loggedInPhone = phone; // 保存登录的手机号
    _sendMessage({
      'type': 'login',
      'phone': phone,
      'license_key': licenseKey,
    });
    
    try {
      return await _loginCompleter!.future.timeout(
        const Duration(seconds: 30),
        onTimeout: () {
          print('登录超时');
          return false;
        },
      );
    } catch (e) {
      print('登录失败: $e');
      return false;
    }
  }
  
  /// 快速登录
  Future<bool> quickLogin(String wxid) async {
    _quickLoginCompleter = Completer<bool>();
    _sendMessage({
      'type': 'quick_login',
      'wxid': wxid,
    });
    
    try {
      return await _quickLoginCompleter!.future.timeout(
        const Duration(seconds: 30),
        onTimeout: () {
          print('快速登录超时');
          return false;
        },
      );
    } catch (e) {
      print('快速登录失败: $e');
      return false;
    }
  }
  
  /// 保存登录状态
  Future<void> saveLoginState() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      if (_currentWeChatId != null) {
        await prefs.setString('logged_in_wxid', _currentWeChatId!);
        print('登录状态已保存: $_currentWeChatId');
      }
    } catch (e) {
      print('保存登录状态失败: $e');
    }
  }
  
  /// 加载登录状态
  Future<String?> loadLoginState() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final wxid = prefs.getString('logged_in_wxid');
      if (wxid != null) {
        _currentWeChatId = wxid;
        print('已加载登录状态: $wxid');
      }
      return wxid;
    } catch (e) {
      print('加载登录状态失败: $e');
      return null;
    }
  }
  
  /// 清除登录状态
  Future<void> clearLoginState() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.remove('logged_in_wxid');
      _currentWeChatId = null;
      _myInfo = null;
      _loggedInPhone = null; // 清除登录手机号
      notifyListeners();
      print('登录状态已清除');
    } catch (e) {
      print('清除登录状态失败: $e');
    }
  }

  /// 处理联系人同步
  void _handleContactsSync(List<dynamic> contactsData) {
    try {
      print('========== 开始处理好友列表同步 ==========');
      print('原始数据数量: ${contactsData.length}');
      print('原始数据: $contactsData');
      
      // 检查数据是否属于当前登录的账号
      if (contactsData.isNotEmpty) {
        final firstItem = contactsData[0] as Map<String, dynamic>;
        final weChatId = firstItem['we_chat_id'] ?? firstItem['weChatId'];
        if (weChatId != null && weChatId.toString() != _currentWeChatId) {
          print('跳过不属于当前账号的联系人数据: weChatId=$weChatId, currentWeChatId=$_currentWeChatId');
          return;
        }
      }
      
      // 如果是分批同步，需要合并到现有列表
      List<ContactModel> newContacts = contactsData
          .map((json) {
            try {
              print('正在解析好友数据: $json');
              ContactModel contact = ContactModel.fromJson(json as Map<String, dynamic>);
              print('解析成功: ${contact.nickName} (${contact.id})');
              return contact;
            } catch (e) {
              print('解析好友数据失败: $e');
              print('失败的数据: $json');
              print('堆栈跟踪: ${StackTrace.current}');
              return null;
            }
          })
          .where((contact) => contact != null)
          .cast<ContactModel>()
          .toList();
      
      print('成功解析好友数量: ${newContacts.length}');
      print('解析后的好友列表: ${newContacts.map((c) => '${c.nickName} (${c.id})').join(', ')}');
      
      // 合并到现有列表（去重）
      Map<String, ContactModel> contactMap = {};
      for (var contact in _contacts) {
        contactMap[contact.id] = contact;
      }
      for (var contact in newContacts) {
        contactMap[contact.id] = contact;
      }
      
      _contacts = contactMap.values.toList();
      print('合并后好友总数: ${_contacts.length}');
      print('合并后的好友列表: ${_contacts.map((c) => '${c.nickName} (${c.id})').join(', ')}');
      print('========== 好友列表同步完成 ==========');
      
      notifyListeners();
      print('已通知监听器更新UI');
    } catch (e) {
      print('处理好友列表同步失败: $e');
      print('堆栈跟踪: ${StackTrace.current}');
    }
  }
  
  /// 处理聊天消息同步
  void _handleChatMessageSync(Map<String, dynamic> messageData) {
    // TODO: 实现聊天消息同步
    print('收到聊天消息: $messageData');
  }
  
  /// 处理标签同步
  void _handleTagsSync(List<dynamic> tagsData) {
    // TODO: 实现标签同步
    print('收到标签同步: ${tagsData.length} 个标签');
  }
  
  /// 处理我的信息同步
  void _handleMyInfoSync(Map<String, dynamic> myInfoData) {
    try {
      print('========== [App端] 收到我的信息同步 ==========');
      print('[App端] wxid: ${myInfoData['wxid']}');
      print('[App端] nickname: ${myInfoData['nickname']}');
      print('[App端] phone: ${myInfoData['phone']}');
      print('[App端] avatar: ${myInfoData['avatar'] != null ? "有头像" : "无头像"}');
      
      // ========== 二次验证：验证账号信息的手机号是否匹配当前登录的手机号 ==========
      // 注意：服务器端已经做了主要验证，这里是二次防护，确保数据安全
      final accountPhone = myInfoData['phone']?.toString() ?? '';
      if (_loggedInPhone != null && _loggedInPhone!.isNotEmpty) {
        if (accountPhone != _loggedInPhone) {
          print('警告: [二次验证] 账号信息手机号($accountPhone)与登录手机号($_loggedInPhone)不匹配，忽略此同步消息');
          print('提示: 服务器端应该已经过滤了不匹配的数据，如果收到此警告，请检查服务器端验证逻辑');
          return;
        }
        print('[App端] [二次验证] 手机号匹配: $accountPhone == $_loggedInPhone');
      } else {
        print('[App端] [二次验证] 当前未登录，跳过手机号验证');
      }
      
      _myInfo = myInfoData;
      // 更新当前微信账号ID
      _currentWeChatId = myInfoData['wxid']?.toString() ?? myInfoData['account']?.toString();
      print('[App端] 我的信息已更新: $_myInfo');
      print('[App端] 当前微信账号ID: $_currentWeChatId');
      
      // 保存账号信息到本地
      _saveMyInfoToLocal(myInfoData);
      
      notifyListeners();
      print('[App端] 已通知监听器更新UI');
      print('========== [App端] 我的信息同步完成 ==========');
    } catch (e) {
      print('处理我的信息同步失败: $e');
      print('堆栈跟踪: ${StackTrace.current}');
    }
  }

  /// 保存账号信息到本地
  Future<void> _saveMyInfoToLocal(Map<String, dynamic> myInfoData) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      // 将Map转换为JSON字符串保存
      final jsonString = jsonEncode(myInfoData);
      await prefs.setString('my_info', jsonString);
      print('账号信息已保存到本地');
    } catch (e) {
      print('保存账号信息到本地失败: $e');
    }
  }

  /// 从本地加载账号信息
  Future<void> _loadMyInfoFromLocal() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final jsonString = prefs.getString('my_info');
      if (jsonString != null && jsonString.isNotEmpty) {
        final myInfoData = jsonDecode(jsonString) as Map<String, dynamic>;
        // 只有在没有当前账号信息时才从本地加载（避免覆盖登录时获取的账号信息）
        if (_myInfo == null && _currentWeChatId == null) {
          _myInfo = myInfoData;
          // 更新当前微信账号ID
          _currentWeChatId = myInfoData['wxid']?.toString() ?? myInfoData['account']?.toString();
          notifyListeners();
        }
      }
    } catch (e) {
      print('从本地加载账号信息失败: $e');
    }
  }

  /// 获取当前微信账号ID
  String? get currentWeChatId => _currentWeChatId;

  /// 更新账号信息（用于从服务器获取后更新）
  void updateMyInfo(Map<String, dynamic> myInfoData) {
    try {
      // 如果传入的是空字典，清除账号信息
      if (myInfoData.isEmpty) {
        _myInfo = null;
        _currentWeChatId = null;
        notifyListeners();
        print('账号信息已清除');
        return;
      }
      
      // ========== 二次验证：验证账号信息的手机号是否匹配当前登录的手机号 ==========
      // 注意：服务器端已经做了主要验证，这里是二次防护，确保数据安全
      final accountPhone = myInfoData['phone']?.toString() ?? '';
      if (_loggedInPhone != null && _loggedInPhone!.isNotEmpty) {
        if (accountPhone != _loggedInPhone) {
          print('警告: [二次验证] 账号信息手机号($accountPhone)与登录手机号($_loggedInPhone)不匹配，忽略此更新');
          print('提示: 服务器端应该已经过滤了不匹配的数据，如果收到此警告，请检查服务器端验证逻辑');
          return;
        }
      }
      
      _myInfo = myInfoData;
      // 更新当前微信账号ID
      _currentWeChatId = myInfoData['wxid']?.toString() ?? myInfoData['account']?.toString();
      // 如果还没有登录手机号，使用账号信息中的手机号
      if (_loggedInPhone == null && accountPhone.isNotEmpty) {
        _loggedInPhone = accountPhone;
      }
      // 保存账号信息到本地
      _saveMyInfoToLocal(myInfoData);
      notifyListeners();
      print('账号信息已更新: $_myInfo');
    } catch (e) {
      print('更新账号信息失败: $e');
    }
  }

  /// 更新联系人数据（用于从服务器获取后更新）
  void updateContacts(List<ContactModel> contacts) {
    try {
      _contacts = contacts;
      notifyListeners();
      print('联系人数据已更新: ${contacts.length} 条');
    } catch (e) {
      print('更新联系人数据失败: $e');
    }
  }

  /// 更新朋友圈数据（用于从服务器获取后更新）
  void updateMoments(List<MomentsModel> moments) {
    try {
      _moments = moments;
      notifyListeners();
      print('朋友圈数据已更新: ${moments.length} 条');
    } catch (e) {
      print('更新朋友圈数据失败: $e');
    }
  }

  /// 处理朋友圈同步
  void _handleMomentsSync(List<dynamic> momentsData) {
    try {
      // 检查数据是否属于当前登录的账号
      if (momentsData.isNotEmpty) {
        final firstItem = momentsData[0] as Map<String, dynamic>;
        final weChatId = firstItem['we_chat_id'] ?? firstItem['weChatId'];
        if (weChatId != null && weChatId.toString() != _currentWeChatId) {
          print('跳过不属于当前账号的朋友圈数据: weChatId=$weChatId, currentWeChatId=$_currentWeChatId');
          return;
        }
      }
      
      _moments = momentsData
          .map((json) => MomentsModel.fromJson(json as Map<String, dynamic>))
          .toList();
      notifyListeners();
    } catch (e) {
      print('处理朋友圈同步失败: $e');
    }
  }

  /// 处理公众号消息同步
  void _handleOfficialAccountSync(Map<String, dynamic> officialAccountData) {
    try {
      // 检查数据是否属于当前登录的账号
      final weChatId = officialAccountData['wechat_id']?.toString() ?? '';
      if (weChatId.isNotEmpty && weChatId != _currentWeChatId) {
        print('跳过不属于当前账号的公众号消息: weChatId=$weChatId, currentWeChatId=$_currentWeChatId');
        return;
      }
      
      // 创建公众号消息对象
      final message = OfficialAccountMessage.fromJson(officialAccountData);
      
      // 添加到列表（避免重复）
      bool exists = _officialAccountMessages.any((m) => 
        m.msgid == message.msgid && m.fromWxid == message.fromWxid
      );
      
      if (!exists) {
        _officialAccountMessages.insert(0, message); // 最新的在前面
        // 限制数量，保留最近100条
        if (_officialAccountMessages.length > 100) {
          _officialAccountMessages = _officialAccountMessages.take(100).toList();
        }
        notifyListeners();
        print('公众号消息已添加: ${message.accountName}, 文章数: ${message.articles.length}');
      }
    } catch (e) {
      print('处理公众号消息同步失败: $e');
    }
  }

  /// 处理命令执行结果
  void _handleCommandResult(Map<String, dynamic> data) {
    print('命令执行结果: $data');
    final commandId = data['command_id'] as String?;
    if (commandId != null && _commandCompleters.containsKey(commandId)) {
      final completer = _commandCompleters[commandId];
      if (completer != null && !completer.isCompleted) {
        completer.complete(data);
        _commandCompleters.remove(commandId);
      }
    }
    notifyListeners();
  }

  /// 发送消息
  void _sendMessage(Map<String, dynamic> message) {
    if (_channel != null) {
      try {
        // 如果会话密钥已设置，使用会话密钥加密；否则发送明文（用于密钥交换阶段）
        if (_encryptionService.hasSessionKey()) {
          // 加密消息内容（使用会话密钥）
          final messageJson = jsonEncode(message);
          final encryptedMessage = _encryptionService.encryptStringForCommunication(messageJson);
          if (encryptedMessage != null) {
            // 包装为JSON格式，包含加密标识
            final messageWrapper = {
              'encrypted': true,
              'data': encryptedMessage,
            };
            _channel!.sink.add(jsonEncode(messageWrapper));
            print('已发送消息（已加密）: ${message['type']}');
          } else {
            print('加密消息失败，使用明文发送: ${message['type']}');
            _channel!.sink.add(jsonEncode(message));
          }
        } else {
          // 会话密钥未设置，发送明文（用于密钥交换阶段）
          _channel!.sink.add(jsonEncode(message));
          print('已发送消息（明文，密钥交换阶段）: ${message['type']}');
        }
      } catch (e) {
        print('发送消息失败: $e');
        _isConnected = false;
        notifyListeners();
      }
    } else {
      print('WebSocket通道未建立，无法发送消息: ${message['type']}');
    }
  }

  /// 发送命令（返回Future，等待命令结果）
  Future<Map<String, dynamic>?> sendCommandAsync(
    String commandType,
    Map<String, dynamic> commandData,
    String targetWeChatId,
  ) async {
    // 生成命令ID
    final commandId = DateTime.now().millisecondsSinceEpoch.toString() + 
                     '_${commandType}_${targetWeChatId}';
    
    // 创建Completer
    final completer = Completer<Map<String, dynamic>>();
    _commandCompleters[commandId] = completer;
    
    // 发送命令
    _sendMessage({
      'type': 'command',
      'command_id': commandId,
      'command_type': commandType,
      'command_data': commandData,
      'target_we_chat_id': targetWeChatId,
    });
    
    // 等待命令结果（30秒超时）
    try {
      return await completer.future.timeout(
        const Duration(seconds: 30),
        onTimeout: () {
          _commandCompleters.remove(commandId);
          return {'status': 'error', 'result': '命令执行超时'};
        },
      );
    } catch (e) {
      _commandCompleters.remove(commandId);
      print('等待命令结果失败: $e');
      return {'status': 'error', 'result': '等待命令结果失败: $e'};
    }
  }

  /// 发送命令（不等待结果，用于向后兼容）
  void sendCommand(String commandType, Map<String, dynamic> commandData, String targetWeChatId) {
    _sendMessage({
      'type': 'command',
      'command_type': commandType,
      'command_data': commandData,
      'target_we_chat_id': targetWeChatId,
    });
  }

  /// 发送消息命令
  void sendMessageCommand(String toWeChatId, String content) {
    sendCommand('send_message', {
      'to_wechat_id': toWeChatId,
      'content': content,
    }, toWeChatId);
  }

  /// 发送文件命令
  void sendFileCommand(String toWeChatId, String filePath) {
    sendCommand('send_file', {
      'to_wechat_id': toWeChatId,
      'file_path': filePath,
    }, toWeChatId);
  }

  /// 点赞朋友圈命令
  void likeMomentsCommand(String momentId) {
    sendCommand('moments_like', {
      'moment_id': momentId,
    }, '');
  }

  /// 评论朋友圈命令
  void commentMomentsCommand(String momentId, String content) {
    sendCommand('moments_comment', {
      'moment_id': momentId,
      'content': content,
    }, '');
  }

  /// 发布朋友圈命令
  void sendMomentsCommand(String content, List<Map<String, dynamic>> items) {
    sendCommand('send_moments', {
      'content': content,
      'items': items,
    }, '');
  }

  /// 请求同步好友列表
  void requestSyncContacts([String? targetWeChatId]) {
    final weChatId = targetWeChatId ?? _currentWeChatId ?? '';
    if (weChatId.isEmpty) {
      print('警告: 无法请求同步好友列表，微信账号ID为空');
      return;
    }
    print('请求同步好友列表: $weChatId');
    sendCommand('sync_contacts', {}, weChatId);
  }

  /// 请求同步朋友圈
  void requestSyncMoments([String? targetWeChatId, String maxId = '0']) {
    final weChatId = targetWeChatId ?? _currentWeChatId ?? '';
    if (weChatId.isEmpty) {
      print('警告: 无法请求同步朋友圈，微信账号ID为空');
      return;
    }
    print('请求同步朋友圈: $weChatId, maxId: $maxId');
    sendCommand('sync_moments', {
      'max_id': maxId,
    }, weChatId);
  }

  /// 请求同步标签列表
  void requestSyncTags([String? targetWeChatId]) {
    final weChatId = targetWeChatId ?? _currentWeChatId ?? '';
    if (weChatId.isEmpty) {
      print('警告: 无法请求同步标签列表，微信账号ID为空');
      return;
    }
    print('请求同步标签列表: $weChatId');
    sendCommand('sync_tags', {}, weChatId);
  }
}

