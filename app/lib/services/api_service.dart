import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import '../models/contact_model.dart';
import '../models/moments_model.dart';
import '../models/chat_message_model.dart';
import '../models/license_model.dart';
import '../utils/encryption_service.dart';

/// API服务
/// 负责与服务器进行HTTP通信
/// 支持HTTP密钥交换协议
class ApiService extends ChangeNotifier {
  String _serverUrl = 'http://localhost:8000';
  final EncryptionService _encryptionService = EncryptionService();

  String get serverUrl => _serverUrl;

  /// 设置服务器地址
  void setServerUrl(String url) {
    _serverUrl = url;
    notifyListeners();
  }

  /// 确保HTTP会话密钥已交换（如果未交换则进行密钥交换）
  Future<bool> _ensureHttpSessionKey() async {
    // 如果已有会话密钥，直接返回
    if (_encryptionService.hasHttpSessionKey()) {
      return true;
    }

    try {
      // 步骤1：获取RSA公钥
      final publicKeyUrl = '$_serverUrl/api/key-exchange/public-key';
      final publicKeyResponse = await http.get(Uri.parse(publicKeyUrl));

      if (publicKeyResponse.statusCode != 200) {
        print('获取RSA公钥失败: ${publicKeyResponse.statusCode}');
        return false;
      }

      final publicKeyJson = jsonDecode(publicKeyResponse.body);
      final publicKeyPem = publicKeyJson['public_key'] as String?;

      if (publicKeyPem == null || publicKeyPem.isEmpty) {
        print('RSA公钥为空');
        return false;
      }

      // 设置服务器公钥
      if (!_encryptionService.setServerPublicKey(publicKeyPem)) {
        print('设置服务器RSA公钥失败');
        return false;
      }

      print('已获取服务器RSA公钥');

      // 步骤2：生成随机会话密钥
      final sessionKey = _encryptionService.generateSessionKey();

      // 步骤3：使用RSA公钥加密会话密钥
      final encryptedSessionKey = _encryptionService.encryptSessionKey(sessionKey);
      if (encryptedSessionKey == null) {
        print('加密会话密钥失败');
        return false;
      }

      // 步骤4：发送加密的会话密钥给服务器
      final sessionKeyUrl = '$_serverUrl/api/key-exchange/session-key';
      final sessionKeyRequest = jsonEncode({
        'encrypted_key': encryptedSessionKey,
      });

      final sessionKeyResponse = await http.post(
        Uri.parse(sessionKeyUrl),
        headers: {'Content-Type': 'application/json'},
        body: sessionKeyRequest,
      );

      if (sessionKeyResponse.statusCode != 200) {
        print('交换会话密钥失败: ${sessionKeyResponse.statusCode}');
        return false;
      }

      final sessionKeyResponseJson = jsonDecode(sessionKeyResponse.body);
      final sessionId = sessionKeyResponseJson['session_id'] as String?;

      if (sessionId == null || sessionId.isEmpty) {
        print('服务器返回的会话ID为空');
        return false;
      }

      // 保存会话ID和会话密钥
      _encryptionService.setHttpSessionKey(sessionId, sessionKey);

      print('HTTP密钥交换成功');
      return true;
    } catch (e) {
      print('HTTP密钥交换失败: $e');
      return false;
    }
  }

  /// 执行HTTP请求（自动处理密钥交换和加密/解密）
  Future<http.Response> _executeRequest(
    String method,
    String url, {
    Map<String, String>? headers,
    Object? body,
  }) async {
    // 确保HTTP会话密钥已交换
    if (!await _ensureHttpSessionKey()) {
      throw Exception('HTTP密钥交换失败，无法发送请求');
    }

    // 创建请求头
    final requestHeaders = <String, String>{
      if (headers != null) ...headers,
    };

    // 添加会话ID
    final sessionId = _encryptionService.getHttpSessionId();
    if (sessionId != null) {
      requestHeaders['X-Session-ID'] = sessionId;
    }

    // 加密请求体（POST/PUT请求）
    String? encryptedBody;
    if (body != null && (method == 'POST' || method == 'PUT')) {
      // 如果body是字符串，直接加密；否则先转换为JSON字符串
      String bodyString = body is String ? body : jsonEncode(body);
      
      // 使用HTTP会话密钥加密请求体
      final encrypted = _encryptionService.encryptStringForHttp(bodyString);
      if (encrypted != null) {
        // 包装为JSON格式，包含加密标识
        encryptedBody = jsonEncode({
          'encrypted': true,
          'data': encrypted,
        });
        requestHeaders['Content-Type'] = 'application/json';
      } else {
        print('加密请求体失败，使用明文');
        encryptedBody = bodyString;
      }
    }

    // 发送请求
    http.Response response;
    if (method == 'GET') {
      response = await http.get(Uri.parse(url), headers: requestHeaders);
    } else if (method == 'POST') {
      response = await http.post(
        Uri.parse(url),
        headers: requestHeaders,
        body: encryptedBody ?? body,
      );
    } else if (method == 'PUT') {
      response = await http.put(
        Uri.parse(url),
        headers: requestHeaders,
        body: encryptedBody ?? body,
      );
    } else if (method == 'DELETE') {
      response = await http.delete(Uri.parse(url), headers: requestHeaders);
    } else {
      throw Exception('不支持的HTTP方法: $method');
    }

    // 如果会话过期（401），重新进行密钥交换
    if (response.statusCode == 401) {
      print('HTTP会话已过期，重新进行密钥交换');
      _encryptionService.clearHttpSessionKey();

      // 重新交换密钥并重试
      if (await _ensureHttpSessionKey()) {
        final newSessionId = _encryptionService.getHttpSessionId();
        if (newSessionId != null) {
          requestHeaders['X-Session-ID'] = newSessionId;
        }

        // 重新加密请求体（如果之前加密过）
        String? retryEncryptedBody;
        if (body != null && (method == 'POST' || method == 'PUT')) {
          String bodyString = body is String ? body : jsonEncode(body);
          final encrypted = _encryptionService.encryptStringForHttp(bodyString);
          if (encrypted != null) {
            retryEncryptedBody = jsonEncode({
              'encrypted': true,
              'data': encrypted,
            });
          } else {
            retryEncryptedBody = bodyString;
          }
        }

        // 重试请求
        if (method == 'GET') {
          response = await http.get(Uri.parse(url), headers: requestHeaders);
        } else if (method == 'POST') {
          response = await http.post(
            Uri.parse(url),
            headers: requestHeaders,
            body: retryEncryptedBody ?? body,
          );
        } else if (method == 'PUT') {
          response = await http.put(
            Uri.parse(url),
            headers: requestHeaders,
            body: retryEncryptedBody ?? body,
          );
        } else if (method == 'DELETE') {
          response = await http.delete(Uri.parse(url), headers: requestHeaders);
        }
      }
    }

    return response;
  }

  /// 解密HTTP响应
  String _decryptResponse(String responseBody) {
    try {
      final responseObj = jsonDecode(responseBody);
      if (responseObj is Map &&
          responseObj['encrypted'] == true &&
          responseObj['data'] != null) {
        // 加密响应，需要解密
        final encryptedData = responseObj['data'] as String;
        final decrypted = _encryptionService.decryptStringForHttp(encryptedData);
        if (decrypted != null) {
          return decrypted;
        }
      }
      // 非加密响应或解密失败，返回原始响应
      return responseBody;
    } catch (e) {
      print('解密响应失败: $e');
      // 解密失败，返回原始响应
      return responseBody;
    }
  }

  /// 获取联系人列表
  Future<List<ContactModel>> getContacts({String? weChatId, int limit = 100, int offset = 0}) async {
    try {
      String url = '$_serverUrl/api/contacts?limit=$limit&offset=$offset';
      if (weChatId != null) {
        url += '&we_chat_id=$weChatId';
      }

      final response = await _executeRequest('GET', url);
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        List<dynamic> jsonList = jsonDecode(decryptedBody);
        return jsonList.map((json) => ContactModel.fromJson(json as Map<String, dynamic>)).toList();
      } else {
        throw Exception('获取联系人列表失败: ${response.statusCode}');
      }
    } catch (e) {
      print('获取联系人列表失败: $e');
      return [];
    }
  }

  /// 获取朋友圈列表
  Future<List<MomentsModel>> getMoments({String? weChatId, int limit = 50, int offset = 0}) async {
    try {
      String url = '$_serverUrl/api/moments?limit=$limit&offset=$offset';
      if (weChatId != null) {
        url += '&we_chat_id=$weChatId';
      }

      final response = await _executeRequest('GET', url);
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        List<dynamic> jsonList = jsonDecode(decryptedBody);
        return jsonList.map((json) => MomentsModel.fromJson(json as Map<String, dynamic>)).toList();
      } else {
        throw Exception('获取朋友圈列表失败: ${response.statusCode}');
      }
    } catch (e) {
      print('获取朋友圈列表失败: $e');
      return [];
    }
  }

  /// 获取标签列表
  Future<List<Map<String, dynamic>>> getTags({String? weChatId}) async {
    try {
      String url = '$_serverUrl/api/tags';
      if (weChatId != null) {
        url += '?we_chat_id=$weChatId';
      }

      final response = await _executeRequest('GET', url);
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        return List<Map<String, dynamic>>.from(jsonDecode(decryptedBody));
      } else {
        throw Exception('获取标签列表失败: ${response.statusCode}');
      }
    } catch (e) {
      print('获取标签列表失败: $e');
      return [];
    }
  }

  /// 获取聊天记录
  Future<List<ChatMessageModel>> getChatMessages(String fromWeChatId, String toWeChatId, {int limit = 100}) async {
    try {
      String url = '$_serverUrl/api/chat/messages?from_we_chat_id=$fromWeChatId&to_we_chat_id=$toWeChatId&limit=$limit';

      final response = await _executeRequest('GET', url);
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        List<dynamic> jsonList = jsonDecode(decryptedBody);
        return jsonList.map((json) => ChatMessageModel.fromJson(json as Map<String, dynamic>)).toList();
      } else {
        throw Exception('获取聊天记录失败: ${response.statusCode}');
      }
    } catch (e) {
      print('获取聊天记录失败: $e');
      return [];
    }
  }

  /// 获取系统状态
  Future<Map<String, dynamic>?> getStatus() async {
    try {
      final response = await _executeRequest('GET', '$_serverUrl/api/status');
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        return jsonDecode(decryptedBody) as Map<String, dynamic>;
      } else {
        return null;
      }
    } catch (e) {
      print('获取系统状态失败: $e');
      return null;
    }
  }

  /// 获取账号信息
  Future<Map<String, dynamic>?> getAccountInfo({String? wxid, String? phone}) async {
    try {
      String url = '$_serverUrl/api/account';
      if (wxid != null) {
        url += '?wxid=$wxid';
      } else if (phone != null) {
        url += '?phone=$phone';
      }

      final response = await _executeRequest('GET', url);
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        final data = jsonDecode(decryptedBody);
        if (data == null) {
          return null;
        }
        // 转换为与WebSocket消息格式一致的格式
        return {
          'wxid': data['wxid'] ?? '',
          'nickname': data['nickname'] ?? '',
          'avatar': data['avatar'] ?? '',
          'account': data['account'] ?? '',
          'device_id': data['device_id'] ?? '',
          'phone': data['phone'] ?? '',
          'wx_user_dir': data['wx_user_dir'] ?? '',
          'unread_msg_count': data['unread_msg_count'] ?? 0,
          'is_fake_device_id': data['is_fake_device_id'] ?? 0,
          'pid': data['pid'] ?? 0,
        };
      } else {
        return null;
      }
    } catch (e) {
      print('获取账号信息失败: $e');
      return null;
    }
  }

  /// 获取所有账号列表
  Future<List<Map<String, dynamic>>> getAllAccounts({int limit = 100, int offset = 0}) async {
    try {
      final url = '$_serverUrl/api/accounts?limit=$limit&offset=$offset';
      final response = await _executeRequest('GET', url);
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        final List<dynamic> jsonList = jsonDecode(decryptedBody);
        return jsonList.map((json) {
          final account = json as Map<String, dynamic>;
          return {
            'wxid': account['wxid'] ?? '',
            'nickname': account['nickname'] ?? '',
            'avatar': account['avatar'] ?? '',
            'account': account['account'] ?? '',
            'device_id': account['device_id'] ?? '',
            'phone': account['phone'] ?? '',
            'wx_user_dir': account['wx_user_dir'] ?? '',
            'unread_msg_count': account['unread_msg_count'] ?? 0,
            'is_fake_device_id': account['is_fake_device_id'] ?? 0,
            'pid': account['pid'] ?? 0,
          };
        }).toList();
      } else {
        throw Exception('获取账号列表失败: ${response.statusCode}');
      }
    } catch (e) {
      print('获取账号列表失败: $e');
      return [];
    }
  }

  // ========== 授权码管理 API ==========

  /// 获取所有授权用户列表
  Future<List<LicenseModel>> getLicenses({
    int limit = 100,
    int offset = 0,
    String? status,
    String? phone,
  }) async {
    try {
      String url = '$_serverUrl/api/licenses?limit=$limit&offset=$offset';
      if (status != null) {
        url += '&status=$status';
      }
      if (phone != null && phone.isNotEmpty) {
        url += '&phone=$phone';
      }

      final response = await _executeRequest('GET', url);
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        final List<dynamic> jsonList = jsonDecode(decryptedBody);
        return jsonList.map((json) => LicenseModel.fromJson(json as Map<String, dynamic>)).toList();
      } else {
        throw Exception('获取授权列表失败: ${response.statusCode}');
      }
    } catch (e) {
      print('获取授权列表失败: $e');
      return [];
    }
  }

  /// 获取单个授权用户信息
  Future<LicenseModel?> getLicense(int licenseId) async {
    try {
      final response = await _executeRequest('GET', '$_serverUrl/api/licenses/$licenseId');
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        return LicenseModel.fromJson(jsonDecode(decryptedBody) as Map<String, dynamic>);
      } else {
        return null;
      }
    } catch (e) {
      print('获取授权信息失败: $e');
      return null;
    }
  }

  /// 创建授权用户
  Future<LicenseModel?> createLicense({
    required String phone,
    String? licenseKey,
    String? boundWechatPhone,
    bool hasManagePermission = false,
    required DateTime expireDate,
  }) async {
    try {
      final body = jsonEncode({
        'phone': phone,
        if (licenseKey != null) 'license_key': licenseKey,
        if (boundWechatPhone != null) 'bound_wechat_phone': boundWechatPhone,
        'has_manage_permission': hasManagePermission,
        'expire_date': expireDate.toIso8601String(),
      });
      final response = await _executeRequest(
        'POST',
        '$_serverUrl/api/licenses',
        headers: {'Content-Type': 'application/json'},
        body: body,
      );
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        return LicenseModel.fromJson(jsonDecode(decryptedBody) as Map<String, dynamic>);
      } else {
        final error = jsonDecode(response.body);
        throw Exception(error['detail'] ?? '创建失败');
      }
    } catch (e) {
      print('创建授权失败: $e');
      rethrow;
    }
  }

  /// 更新授权用户
  Future<LicenseModel?> updateLicense({
    required int licenseId,
    String? licenseKey,
    String? boundWechatPhone,
    bool? hasManagePermission,
    String? status,
    DateTime? expireDate,
  }) async {
    try {
      final body = <String, dynamic>{};
      if (licenseKey != null) body['license_key'] = licenseKey;
      if (boundWechatPhone != null) body['bound_wechat_phone'] = boundWechatPhone;
      if (hasManagePermission != null) body['has_manage_permission'] = hasManagePermission;
      if (status != null) body['status'] = status;
      if (expireDate != null) body['expire_date'] = expireDate.toIso8601String();

      final response = await _executeRequest(
        'PUT',
        '$_serverUrl/api/licenses/$licenseId',
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(body),
      );
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        return LicenseModel.fromJson(jsonDecode(decryptedBody) as Map<String, dynamic>);
      } else {
        final error = jsonDecode(response.body);
        throw Exception(error['detail'] ?? '更新失败');
      }
    } catch (e) {
      print('更新授权失败: $e');
      rethrow;
    }
  }

  /// 删除授权用户（软删除）
  Future<bool> deleteLicense(int licenseId) async {
    try {
      final response = await _executeRequest('DELETE', '$_serverUrl/api/licenses/$licenseId');
      return response.statusCode == 200;
    } catch (e) {
      print('删除授权失败: $e');
      return false;
    }
  }

  /// 延期授权
  Future<LicenseModel?> extendLicense({
    required int licenseId,
    int? days,
    int? months,
    int? years,
  }) async {
    try {
      final body = <String, dynamic>{};
      if (days != null) body['days'] = days;
      if (months != null) body['months'] = months;
      if (years != null) body['years'] = years;

      final response = await _executeRequest(
        'POST',
        '$_serverUrl/api/licenses/$licenseId/extend',
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(body),
      );
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        return LicenseModel.fromJson(jsonDecode(decryptedBody) as Map<String, dynamic>);
      } else {
        final error = jsonDecode(response.body);
        throw Exception(error['detail'] ?? '延期失败');
      }
    } catch (e) {
      print('延期授权失败: $e');
      rethrow;
    }
  }

  /// 生成新授权码
  Future<LicenseModel?> generateNewKey(int licenseId) async {
    try {
      final response = await _executeRequest('POST', '$_serverUrl/api/licenses/$licenseId/generate-key');
      if (response.statusCode == 200) {
        final decryptedBody = _decryptResponse(response.body);
        return LicenseModel.fromJson(jsonDecode(decryptedBody) as Map<String, dynamic>);
      } else {
        final error = jsonDecode(response.body);
        throw Exception(error['detail'] ?? '生成授权码失败');
      }
    } catch (e) {
      print('生成授权码失败: $e');
      rethrow;
    }
  }
}


