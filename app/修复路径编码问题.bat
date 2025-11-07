@echo off
chcp 65001 >nul
echo 修复Flutter路径编码问题...
echo.

REM 设置环境变量，使用UTF-8编码
set JAVA_TOOL_OPTIONS=-Dfile.encoding=UTF-8
set GRADLE_OPTS=-Dfile.encoding=UTF-8

REM 设置Flutter环境变量
set FLUTTER_ROOT=%USERPROFILE%\.flutter
set PATH=%FLUTTER_ROOT%\bin;%PATH%

echo 环境变量已设置：
echo JAVA_TOOL_OPTIONS=%JAVA_TOOL_OPTIONS%
echo GRADLE_OPTS=%GRADLE_OPTS%
echo.

echo 现在可以运行"启动应用.bat"了
echo.
pause

