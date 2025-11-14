import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';
import 'package:encrypt/encrypt.dart';

/// 加密服务
/// 提供RSA和AES-256-GCM加密/解密功能
class EncryptionService {
  static final EncryptionService _instance = EncryptionService._internal();
  factory EncryptionService() => _instance;
  EncryptionService._internal();

  dynamic _serverPublicKey; // RSA公钥（使用dynamic避免类型问题）
  Uint8List? _sessionKey; // 32字节会话密钥
  Uint8List? _httpSessionKey; // HTTP会话密钥
  String? _httpSessionId; // HTTP会话ID

  /// 设置服务器RSA公钥（PEM格式）
  bool setServerPublicKey(String publicKeyPem) {
    try {
      // 使用encrypt包的RSA功能解析PEM格式公钥
      final parser = RSAKeyParser();
      _serverPublicKey = parser.parse(publicKeyPem);
      print('服务器RSA公钥已设置');
      return true;
    } catch (e) {
      print('设置服务器RSA公钥失败: $e');
      return false;
    }
  }

  /// 检查服务器公钥是否已设置
  bool hasServerPublicKey() {
    return _serverPublicKey != null;
  }

  /// 使用RSA公钥加密会话密钥
  String? encryptSessionKey(Uint8List sessionKey) {
    if (_serverPublicKey == null) {
      print('服务器RSA公钥未设置，无法加密会话密钥');
      return null;
    }

    if (sessionKey.length != 32) {
      print('会话密钥必须是32字节');
      return null;
    }

    try {
      // 使用OAEP填充方式加密
      final encrypter = Encrypter(RSA(
        publicKey: _serverPublicKey,
        encoding: RSAEncoding.OAEP,
      ));
      
      final encrypted = encrypter.encryptBytes(sessionKey);
      return encrypted.base64;
    } catch (e) {
      print('加密会话密钥失败: $e');
      return null;
    }
  }

  /// 生成随机会话密钥（32字节）
  Uint8List generateSessionKey() {
    final random = Random.secure();
    return Uint8List.fromList(
      List<int>.generate(32, (i) => random.nextInt(256)),
    );
  }

  /// 设置WebSocket会话密钥
  void setSessionKey(Uint8List sessionKey) {
    if (sessionKey.length != 32) {
      throw ArgumentError('会话密钥必须是32字节');
    }
    _sessionKey = Uint8List.fromList(sessionKey);
    print('WebSocket会话密钥已设置');
  }

  /// 设置HTTP会话密钥和会话ID
  void setHttpSessionKey(String sessionId, Uint8List sessionKey) {
    if (sessionKey.length != 32) {
      throw ArgumentError('会话密钥必须是32字节');
    }
    _httpSessionId = sessionId;
    _httpSessionKey = Uint8List.fromList(sessionKey);
    print('HTTP会话密钥已设置（session_id: ${sessionId.substring(0, 8)}...）');
  }

  /// 获取HTTP会话ID
  String? getHttpSessionId() {
    return _httpSessionId;
  }

  /// 检查WebSocket会话密钥是否已设置
  bool hasSessionKey() {
    return _sessionKey != null;
  }

  /// 检查HTTP会话密钥是否已设置
  bool hasHttpSessionKey() {
    return _httpSessionKey != null && _httpSessionId != null;
  }

  /// 清除WebSocket会话密钥
  void clearSessionKey() {
    _sessionKey = null;
  }

  /// 清除HTTP会话密钥
  void clearHttpSessionKey() {
    _httpSessionId = null;
    _httpSessionKey = null;
  }

  /// 使用AES-256-GCM加密字符串（WebSocket）
  String? encryptStringForCommunication(String plainText) {
    if (_sessionKey == null) {
      print('WebSocket会话密钥未设置，无法加密');
      return null;
    }

    try {
      final key = Key(_sessionKey!);
      final iv = IV.fromSecureRandom(12); // GCM nonce size is 12 bytes
      
      final encrypter = Encrypter(AES(key, mode: AESMode.gcm));
      final encrypted = encrypter.encrypt(plainText, iv: iv);
      
      // 组合：nonce(12字节) + ciphertext + tag(16字节)
      final result = Uint8List(12 + encrypted.bytes.length);
      result.setRange(0, 12, iv.bytes);
      result.setRange(12, result.length, encrypted.bytes);
      
      return base64Encode(result);
    } catch (e) {
      print('加密字符串失败: $e');
      return null;
    }
  }

  /// 使用AES-256-GCM解密字符串（WebSocket）
  String? decryptStringForCommunication(String cipherText) {
    if (_sessionKey == null) {
      print('WebSocket会话密钥未设置，无法解密');
      return null;
    }

    try {
      final encryptedBytes = base64Decode(cipherText);
      
      if (encryptedBytes.length < 28) {
        // 至少需要 12(nonce) + 0(ciphertext) + 16(tag)
        print('密文长度不足');
        return null;
      }

      // 提取 nonce 和 ciphertext+tag
      final nonce = encryptedBytes.sublist(0, 12);
      final ciphertextWithTag = encryptedBytes.sublist(12);

      final key = Key(_sessionKey!);
      final iv = IV(nonce);

      final encrypter = Encrypter(AES(key, mode: AESMode.gcm));
      final decrypted = encrypter.decrypt(
        Encrypted(ciphertextWithTag),
        iv: iv,
      );

      return decrypted;
    } catch (e) {
      print('解密字符串失败: $e');
      return null;
    }
  }

  /// 使用AES-256-GCM加密字符串（HTTP）
  String? encryptStringForHttp(String plainText) {
    if (_httpSessionKey == null) {
      print('HTTP会话密钥未设置，无法加密');
      return null;
    }

    try {
      final key = Key(_httpSessionKey!);
      final iv = IV.fromSecureRandom(12); // GCM nonce size is 12 bytes
      
      final encrypter = Encrypter(AES(key, mode: AESMode.gcm));
      final encrypted = encrypter.encrypt(plainText, iv: iv);
      
      // 组合：nonce(12字节) + ciphertext + tag(16字节)
      final result = Uint8List(12 + encrypted.bytes.length);
      result.setRange(0, 12, iv.bytes);
      result.setRange(12, result.length, encrypted.bytes);
      
      return base64Encode(result);
    } catch (e) {
      print('加密HTTP字符串失败: $e');
      return null;
    }
  }

  /// 使用AES-256-GCM解密字符串（HTTP）
  String? decryptStringForHttp(String cipherText) {
    if (_httpSessionKey == null) {
      print('HTTP会话密钥未设置，无法解密');
      return null;
    }

    try {
      final encryptedBytes = base64Decode(cipherText);
      
      if (encryptedBytes.length < 28) {
        // 至少需要 12(nonce) + 0(ciphertext) + 16(tag)
        print('密文长度不足');
        return null;
      }

      // 提取 nonce 和 ciphertext+tag
      final nonce = encryptedBytes.sublist(0, 12);
      final ciphertextWithTag = encryptedBytes.sublist(12);

      final key = Key(_httpSessionKey!);
      final iv = IV(nonce);

      final encrypter = Encrypter(AES(key, mode: AESMode.gcm));
      final decrypted = encrypter.decrypt(
        Encrypted(ciphertextWithTag),
        iv: iv,
      );

      return decrypted;
    } catch (e) {
      print('解密HTTP字符串失败: $e');
      return null;
    }
  }
}

