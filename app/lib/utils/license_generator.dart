import 'dart:math';

/// 授权码生成工具
class LicenseGenerator {
  static const String _uppercase = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
  static const String _lowercase = 'abcdefghijklmnopqrstuvwxyz';
  static const String _digits = '0123456789';
  static const String _special = '!@#\$%^&*';

  /// 生成20位随机授权码
  /// 包含：大写字母、小写字母、数字、特殊符号，随机排列
  static String generate() {
    final random = Random();
    final List<String> chars = [];
    
    // 确保至少包含每种类型的字符
    chars.add(_uppercase[random.nextInt(_uppercase.length)]);
    chars.add(_uppercase[random.nextInt(_uppercase.length)]);
    chars.add(_uppercase[random.nextInt(_uppercase.length)]);
    chars.add(_uppercase[random.nextInt(_uppercase.length)]);
    
    chars.add(_lowercase[random.nextInt(_lowercase.length)]);
    chars.add(_lowercase[random.nextInt(_lowercase.length)]);
    chars.add(_lowercase[random.nextInt(_lowercase.length)]);
    chars.add(_lowercase[random.nextInt(_lowercase.length)]);
    
    chars.add(_digits[random.nextInt(_digits.length)]);
    chars.add(_digits[random.nextInt(_digits.length)]);
    chars.add(_digits[random.nextInt(_digits.length)]);
    chars.add(_digits[random.nextInt(_digits.length)]);
    chars.add(_digits[random.nextInt(_digits.length)]);
    chars.add(_digits[random.nextInt(_digits.length)]);
    
    chars.add(_special[random.nextInt(_special.length)]);
    chars.add(_special[random.nextInt(_special.length)]);
    chars.add(_special[random.nextInt(_special.length)]);
    chars.add(_special[random.nextInt(_special.length)]);
    chars.add(_special[random.nextInt(_special.length)]);
    chars.add(_special[random.nextInt(_special.length)]);
    
    // 随机打乱顺序
    chars.shuffle(random);
    
    return chars.join();
  }
}

