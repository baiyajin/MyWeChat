@echo off
chcp 65001 >nul
echo 正在配置Flutter平台支持...
echo.

echo [1/3] 配置Android平台支持...
flutter create . --platforms=android
if %ERRORLEVEL% EQU 0 (
    echo ✓ Android平台配置完成
) else (
    echo ✗ Android平台配置失败
)

echo.
echo [2/3] 配置Windows平台支持...
flutter create . --platforms=windows
if %ERRORLEVEL% EQU 0 (
    echo ✓ Windows平台配置完成
) else (
    echo ✗ Windows平台配置失败
)

echo.
echo [3/3] 配置Web平台支持...
flutter create . --platforms=web
if %ERRORLEVEL% EQU 0 (
    echo ✓ Web平台配置完成
) else (
    echo ✗ Web平台配置失败
)

echo.
echo 平台配置完成！
echo.
echo 现在可以使用以下命令运行应用：
echo   - Windows: flutter run -d windows
echo   - Chrome: flutter run -d chrome
echo   - Edge: flutter run -d edge
echo   - Android: flutter run -d <device_id>
echo.
pause

