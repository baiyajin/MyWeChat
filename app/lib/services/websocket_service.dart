import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:web_socket_channel/web_socket_channel.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../models/contact_model.dart';
import '../models/moments_model.dart';
import '../main.dart' as main_app;

/// WebSocket服务
/// 负责与服务器建立WebSocket连接，接收数据并发送命令
class WebSocketService extends ChangeNotifier {
  WebSocketChannel? _channel;
  bool _isConnected = false;
  String _serverUrl = 'ws://localhost:8000/ws';

  List<ContactModel> _contacts = [];
  List<MomentsModel> _moments = [];
  Map<String, dynamic>? _myInfo;
  String? _currentWeChatId; // 当前微信账号ID

  bool get isConnected => _isConnected;
  List<ContactModel> get contacts => _contacts;
  List<MomentsModel> get moments => _moments;
  Map<String, dynamic>? get myInfo => _myInfo;

  /// 连接WebSocket服务器
  Future<bool> connect(String serverUrl) async {
    try {
      // 如果已经连接，先断开
      if (_isConnected) {
        disconnect();
      }

      _serverUrl = serverUrl;
      _channel = WebSocketChannel.connect(Uri.parse(serverUrl));

      // 设置消息监听器
      _channel!.stream.listen(
        (message) {
          _handleMessage(message);
        },
        onError: (error) {
          print('WebSocket错误: $error');
          _isConnected = false;
          notifyListeners();
        },
        onDone: () {
          print('WebSocket连接已关闭');
          _isConnected = false;
          notifyListeners();
        },
      );

      // 等待一小段时间让连接建立
      await Future.delayed(const Duration(milliseconds: 300));
      
      // 连接建立后，设置状态并发送客户端类型
      if (_channel != null) {
        _isConnected = true;
        notifyListeners();
        
        // 设置WebSocket链接到全局变量
        if (kIsWeb) {
          main_app.setWebSocketUrl(_serverUrl);
        }
        
        // 发送客户端类型
        _sendClientType();
        
        // 加载本地保存的账号信息
        await _loadMyInfoFromLocal();
      }

      return true;
    } catch (e) {
      print('WebSocket连接失败: $e');
      _isConnected = false;
      notifyListeners();
      return false;
    }
  }
  

  /// 发送客户端类型
  void _sendClientType() {
    if (_channel != null) {
      try {
        _channel!.sink.add(jsonEncode({
          'type': 'client_type',
          'client_type': 'app',
        }));
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

  /// 处理接收到的消息
  void _handleMessage(dynamic message) {
    try {
      print('========== 收到WebSocket消息 ==========');
      print('原始消息: $message');
      print('消息类型: ${message.runtimeType}');
      
      Map<String, dynamic> data = jsonDecode(message);
      print('解析后的数据: $data');

      String type = data['type'] ?? '';
      print('消息类型: $type');

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

  /// 处理联系人同步
  void _handleContactsSync(List<dynamic> contactsData) {
    try {
      print('========== 开始处理好友列表同步 ==========');
      print('原始数据数量: ${contactsData.length}');
      print('原始数据: $contactsData');
      
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
      print('========== 收到我的信息同步 ==========');
      print('数据: $myInfoData');
      _myInfo = myInfoData;
      // 更新当前微信账号ID
      _currentWeChatId = myInfoData['wxid']?.toString() ?? myInfoData['account']?.toString();
      print('我的信息已更新: $_myInfo');
      print('当前微信账号ID: $_currentWeChatId');
      
      // 保存账号信息到本地
      _saveMyInfoToLocal(myInfoData);
      
      notifyListeners();
      print('已通知监听器更新UI');
      print('========== 我的信息同步完成 ==========');
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
        _myInfo = myInfoData;
        // 更新当前微信账号ID
        _currentWeChatId = myInfoData['wxid']?.toString() ?? myInfoData['account']?.toString();
        print('从本地加载账号信息成功: $_myInfo');
        print('当前微信账号ID: $_currentWeChatId');
        notifyListeners();
      } else {
        print('本地没有保存的账号信息');
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
      _myInfo = myInfoData;
      // 更新当前微信账号ID
      _currentWeChatId = myInfoData['wxid']?.toString() ?? myInfoData['account']?.toString();
      // 保存账号信息到本地
      _saveMyInfoToLocal(myInfoData);
      notifyListeners();
      print('账号信息已更新: $_myInfo');
    } catch (e) {
      print('更新账号信息失败: $e');
    }
  }

  /// 处理朋友圈同步
  void _handleMomentsSync(List<dynamic> momentsData) {
    _moments = momentsData
        .map((json) => MomentsModel.fromJson(json as Map<String, dynamic>))
        .toList();
    notifyListeners();
  }

  /// 处理命令执行结果
  void _handleCommandResult(Map<String, dynamic> data) {
    print('命令执行结果: $data');
    notifyListeners();
  }

  /// 发送消息
  void _sendMessage(Map<String, dynamic> message) {
    if (_channel != null) {
      try {
        _channel!.sink.add(jsonEncode(message));
        print('已发送消息: ${message['type']}');
      } catch (e) {
        print('发送消息失败: $e');
        _isConnected = false;
        notifyListeners();
      }
    } else {
      print('WebSocket通道未建立，无法发送消息: ${message['type']}');
    }
  }

  /// 发送命令
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

