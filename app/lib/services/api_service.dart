import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import '../models/contact_model.dart';
import '../models/moments_model.dart';
import '../models/chat_message_model.dart';
import '../models/license_model.dart';

/// API服务
/// 负责与服务器进行HTTP通信
class ApiService extends ChangeNotifier {
  String _serverUrl = 'http://localhost:8000';

  String get serverUrl => _serverUrl;

  /// 设置服务器地址
  void setServerUrl(String url) {
    _serverUrl = url;
    notifyListeners();
  }

  /// 获取联系人列表
  Future<List<ContactModel>> getContacts({String? weChatId, int limit = 100, int offset = 0}) async {
    try {
      String url = '$_serverUrl/api/contacts?limit=$limit&offset=$offset';
      if (weChatId != null) {
        url += '&we_chat_id=$weChatId';
      }

      final response = await http.get(Uri.parse(url));
      if (response.statusCode == 200) {
        List<dynamic> jsonList = jsonDecode(response.body);
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

      final response = await http.get(Uri.parse(url));
      if (response.statusCode == 200) {
        List<dynamic> jsonList = jsonDecode(response.body);
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

      final response = await http.get(Uri.parse(url));
      if (response.statusCode == 200) {
        return List<Map<String, dynamic>>.from(jsonDecode(response.body));
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

      final response = await http.get(Uri.parse(url));
      if (response.statusCode == 200) {
        List<dynamic> jsonList = jsonDecode(response.body);
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
      final response = await http.get(Uri.parse('$_serverUrl/api/status'));
      if (response.statusCode == 200) {
        return jsonDecode(response.body) as Map<String, dynamic>;
      } else {
        return null;
      }
    } catch (e) {
      print('获取系统状态失败: $e');
      return null;
    }
  }

  /// 获取账号信息
  Future<Map<String, dynamic>?> getAccountInfo({String? wxid}) async {
    try {
      String url = '$_serverUrl/api/account';
      if (wxid != null) {
        url += '?wxid=$wxid';
      }

      final response = await http.get(Uri.parse(url));
      if (response.statusCode == 200) {
        final data = jsonDecode(response.body);
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
      final response = await http.get(Uri.parse(url));
      if (response.statusCode == 200) {
        final List<dynamic> jsonList = jsonDecode(response.body);
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

      final response = await http.get(Uri.parse(url));
      if (response.statusCode == 200) {
        final List<dynamic> jsonList = jsonDecode(response.body);
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
      final response = await http.get(Uri.parse('$_serverUrl/api/licenses/$licenseId'));
      if (response.statusCode == 200) {
        return LicenseModel.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
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
      final response = await http.post(
        Uri.parse('$_serverUrl/api/licenses'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'phone': phone,
          if (licenseKey != null) 'license_key': licenseKey,
          if (boundWechatPhone != null) 'bound_wechat_phone': boundWechatPhone,
          'has_manage_permission': hasManagePermission,
          'expire_date': expireDate.toIso8601String(),
        }),
      );
      if (response.statusCode == 200) {
        return LicenseModel.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
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

      final response = await http.put(
        Uri.parse('$_serverUrl/api/licenses/$licenseId'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(body),
      );
      if (response.statusCode == 200) {
        return LicenseModel.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
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
      final response = await http.delete(Uri.parse('$_serverUrl/api/licenses/$licenseId'));
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

      final response = await http.post(
        Uri.parse('$_serverUrl/api/licenses/$licenseId/extend'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(body),
      );
      if (response.statusCode == 200) {
        return LicenseModel.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
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
      final response = await http.post(Uri.parse('$_serverUrl/api/licenses/$licenseId/generate-key'));
      if (response.statusCode == 200) {
        return LicenseModel.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
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


