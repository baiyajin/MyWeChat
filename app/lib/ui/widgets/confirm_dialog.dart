import 'package:flutter/material.dart';

/// 全局确认对话框组件
/// 统一整个应用的确认对话框样式
class ConfirmDialog extends StatelessWidget {
  /// 对话框标题
  final String title;
  
  /// 对话框内容
  final String content;
  
  /// 确认按钮文本（默认：确认）
  final String confirmText;
  
  /// 取消按钮文本（默认：取消）
  final String cancelText;
  
  /// 确认按钮颜色（默认：绿色）
  final Color? confirmColor;
  
  /// 取消按钮颜色（默认：灰色）
  final Color? cancelColor;
  
  /// 是否显示取消按钮（默认：true）
  final bool showCancel;
  
  /// 确认回调
  final VoidCallback? onConfirm;
  
  /// 取消回调
  final VoidCallback? onCancel;

  const ConfirmDialog({
    super.key,
    required this.title,
    required this.content,
    this.confirmText = '确认',
    this.cancelText = '取消',
    this.confirmColor,
    this.cancelColor,
    this.showCancel = true,
    this.onConfirm,
    this.onCancel,
  });

  /// 显示确认对话框（静态方法，方便调用）
  static Future<bool?> show({
    required BuildContext context,
    required String title,
    required String content,
    String confirmText = '确认',
    String cancelText = '取消',
    Color? confirmColor,
    Color? cancelColor,
    bool showCancel = true,
  }) {
    return showDialog<bool>(
      context: context,
      builder: (context) => ConfirmDialog(
        title: title,
        content: content,
        confirmText: confirmText,
        cancelText: cancelText,
        confirmColor: confirmColor,
        cancelColor: cancelColor,
        showCancel: showCancel,
        onConfirm: () => Navigator.of(context).pop(true),
        onCancel: () => Navigator.of(context).pop(false),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(
        title,
        style: const TextStyle(
          fontSize: 18,
          fontWeight: FontWeight.w500,
          color: Colors.black87,
        ),
      ),
      content: Text(
        content,
        style: const TextStyle(
          fontSize: 16,
          color: Colors.black87,
          height: 1.5,
        ),
      ),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(8),
      ),
      actions: [
        if (showCancel)
          TextButton(
            onPressed: () {
              onCancel?.call();
              Navigator.of(context).pop(false);
            },
            style: TextButton.styleFrom(
              foregroundColor: cancelColor ?? Colors.grey[600],
            ),
            child: Text(cancelText),
          ),
        TextButton(
          onPressed: () {
            onConfirm?.call();
            Navigator.of(context).pop(true);
          },
          style: TextButton.styleFrom(
            foregroundColor: confirmColor ?? const Color(0xFF07C160),
          ),
          child: Text(confirmText),
        ),
      ],
    );
  }
}

