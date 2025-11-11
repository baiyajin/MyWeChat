/// 授权用户数据模型
class LicenseModel {
  final int id;
  final String phone;
  final String licenseKey;
  final String? boundWechatPhone;
  final bool hasManagePermission;
  final String status;
  final DateTime expireDate;
  final DateTime createdAt;
  final DateTime updatedAt;

  LicenseModel({
    required this.id,
    required this.phone,
    required this.licenseKey,
    this.boundWechatPhone,
    required this.hasManagePermission,
    required this.status,
    required this.expireDate,
    required this.createdAt,
    required this.updatedAt,
  });

  factory LicenseModel.fromJson(Map<String, dynamic> json) {
    return LicenseModel(
      id: json['id'] as int,
      phone: json['phone'] as String,
      licenseKey: json['license_key'] as String,
      boundWechatPhone: json['bound_wechat_phone'] as String?,
      hasManagePermission: json['has_manage_permission'] as bool,
      status: json['status'] as String,
      expireDate: DateTime.parse(json['expire_date'] as String),
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'phone': phone,
      'license_key': licenseKey,
      'bound_wechat_phone': boundWechatPhone,
      'has_manage_permission': hasManagePermission,
      'status': status,
      'expire_date': expireDate.toIso8601String(),
      'created_at': createdAt.toIso8601String(),
      'updated_at': updatedAt.toIso8601String(),
    };
  }

  bool get isExpired => expireDate.isBefore(DateTime.now());
  bool get isActive => status == 'active' && !isExpired;
}

