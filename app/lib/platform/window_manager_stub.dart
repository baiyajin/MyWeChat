// 非 Windows 平台实现（Web、Android、iOS 等）
// 提供空的 window_manager 接口，避免在非 Windows 平台导入 window_manager

import 'package:flutter/material.dart';

enum TitleBarStyle {
  normal,
  hidden,
  hiddenInset,
}

class WindowOptions {
  final Size? size;
  final Size? minimumSize;
  final Size? maximumSize;
  final bool? center;
  final Color? backgroundColor;
  final bool? skipTaskbar;
  final TitleBarStyle? titleBarStyle;
  
  const WindowOptions({
    this.size,
    this.minimumSize,
    this.maximumSize,
    this.center,
    this.backgroundColor,
    this.skipTaskbar,
    this.titleBarStyle,
  });
}

class WindowManagerStub {
  Future<void> ensureInitialized() async {
    // 非 Windows 平台不需要初始化
  }
  
  void waitUntilReadyToShow(WindowOptions options, void Function() callback) {
    // 非 Windows 平台直接执行回调
    callback();
  }
  
  Future<void> show() async {
    // 非 Windows 平台不需要显示窗口
  }
  
  Future<void> focus() async {
    // 非 Windows 平台不需要聚焦窗口
  }
}

final windowManager = WindowManagerStub();

