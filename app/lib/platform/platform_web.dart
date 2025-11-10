// Web 平台实现
import 'dart:html' as html;

/// 获取当前访问的 URL
String? getCurrentUrl() {
  try {
    final uri = html.window.location;
    String href = uri.href ?? '';
    
    // 修复端口重复问题
    if (href.contains('://')) {
      href = href.replaceAll(RegExp(r':(\d+):\1(?=/|$)'), ':\$1');
      
      final parts = href.split(':');
      if (parts.length > 3) {
        final protocol = parts[0];
        final host = parts[1].replaceAll('//', '');
        final port = parts[2];
        final rest = parts.sublist(3).join(':');
        return '$protocol://$host:$port$rest';
      }
      return href;
    } else {
      String port = '';
      final portNum = uri.port;
      if (portNum != 0 && portNum != 80 && portNum != 443) {
        port = ':${portNum}';
      }
      
      String pathname = uri.pathname ?? '';
      if (pathname.isEmpty || pathname == '/') {
        pathname = '';
      }
      
      return '${uri.protocol}//${uri.host}$port$pathname';
    }
  } catch (e) {
    print('无法获取访问链接: $e');
    return null;
  }
}

