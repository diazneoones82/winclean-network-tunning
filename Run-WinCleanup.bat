@echo off
setlocal EnableExtensions EnableDelayedExpansion

net session >nul 2>&1
if not "%errorlevel%"=="0" (
    echo Requesting administrator privileges...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

title Windows Network and Cleanup Reset
set "LOG=%~dp0WinCleanup-%DATE:/=-%-%TIME::=-%.log"
set "LOG=%LOG: =0%"
set "TOTAL=34"
set "DONE=0"
set "WIDTH=30"

cls
echo Windows Network and Cleanup Reset
echo Log: %LOG%
echo.

call :RunStep "Flush DNS resolver cache" "ipconfig /flushdns"
call :RunStep "Purge NetBIOS name cache" "nbtstat -R"
call :RunStep "Release existing IP address leases" "ipconfig /release"
call :RunStep "Renew IP addresses from DHCP" "ipconfig /renew"
call :RunStep "Clear ARP cache" "arp -d *"
call :RunStep "Reset Winsock catalog" "netsh winsock reset"
call :RunStep "Reset TCP/IP stack" "netsh int ip reset"
call :RunStep "Clear persistent static routes" "route -f"
call :RunStep "Reset Windows Defender Firewall" "netsh advfirewall reset"
call :RunStep "Flush local BranchCache data" "netsh branchcache reset"
call :RunStep "Clear user and system temp files" "del /q /f /s ""%TEMP%\*"" & del /q /f /s ""C:\Windows\Temp\*"""
call :RunStep "Clear user temp files" "del /q /f /s ""%TEMP%\*"""
call :RunStep "Clear system temp files" "del /q /f /s ""C:\Windows\Temp\*"""
call :RunStep "Clear Windows Prefetch files" "del /q /f /s ""C:\Windows\Prefetch\*"""
call :RunStep "Cleanup old Windows Update components" "Dism.exe /online /Cleanup-Image /StartComponentCleanup /ResetBase"
call :RunStep "Stop Windows Update service" "net stop wuauserv"
call :RunStep "Stop Background Intelligent Transfer Service" "net stop bits"
call :RunStep "Delete temporary Windows Update installer packages" "del /f /s /q ""C:\Windows\SoftwareDistribution\Download\*"""
call :RunStep "Start Windows Update service" "net start wuauserv"
call :RunStep "Start Background Intelligent Transfer Service" "net start bits"
call :RunStep "Clear Delivery Optimization peer-to-peer cache" "del /f /s /q ""%ProgramData%\Microsoft\Windows\SoftwareDistribution\DeliveryOptimization\*"""
call :RunStep "Delete oldest System Restore shadow copy" "vssadmin delete shadows /for=C: /oldest /quiet"
call :RunStep "Clear Chrome browser cache" "del /f /s /q ""%LocalAppData%\Google\Chrome\User Data\Default\Cache\*"""
call :RunStep "Clear Chrome code cache" "del /f /s /q ""%LocalAppData%\Google\Chrome\User Data\Default\Code Cache\*"""
call :RunStep "Clear Chrome GPU cache" "del /f /s /q ""%LocalAppData%\Google\Chrome\User Data\Default\GPUCache\*"""
call :RunStep "Clear Edge browser cache" "del /f /s /q ""%LocalAppData%\Microsoft\Edge\User Data\Default\Cache\*"""
call :RunStep "Clear Edge code cache" "del /f /s /q ""%LocalAppData%\Microsoft\Edge\User Data\Default\Code Cache\*"""
call :RunStep "Clear Edge GPU cache" "del /f /s /q ""%LocalAppData%\Microsoft\Edge\User Data\Default\GPUCache\*"""
call :RunStep "Clear Firefox cache" "del /f /s /q ""%LocalAppData%\Mozilla\Firefox\Profiles\*\cache2\*"""
call :RunStep "Clear Firefox startup cache" "del /f /s /q ""%LocalAppData%\Mozilla\Firefox\Profiles\*\startupCache\*"""
call :RunStep "Clear Brave browser cache" "del /f /s /q ""%LocalAppData%\BraveSoftware\Brave-Browser\User Data\Default\Cache\*"""
call :RunStep "Clear Brave code cache" "del /f /s /q ""%LocalAppData%\BraveSoftware\Brave-Browser\User Data\Default\Code Cache\*"""
call :RunStep "Flush DNS resolver cache again" "ipconfig /flushdns"
call :RunStep "Clear Delivery Optimization and Windows Store cache" "del /q /f /s ""%ProgramData%\Microsoft\Windows\SoftwareDistribution\Download\*"" & wsreset.exe"

echo.
call :Progress
echo.
echo Complete. Some network reset changes may require a restart.
echo Log saved to: %LOG%
pause
exit /b

:RunStep
set /a CURRENT=DONE+1
set "LABEL=%~1"
set "COMMAND=%~2"
cls
echo Windows Network and Cleanup Reset
echo.
echo Step !CURRENT! of %TOTAL%: !LABEL!
echo Running: !COMMAND!
echo.
call :Progress
echo.
echo [!DATE! !TIME!] Step !CURRENT! of %TOTAL%: !LABEL!>>"%LOG%"
echo Command: !COMMAND!>>"%LOG%"
cmd /c "!COMMAND!" >>"%LOG%" 2>&1
set "RESULT=!errorlevel!"
if not "!RESULT!"=="0" (
    echo.
    echo Warning: step returned exit code !RESULT!. Continuing...
    echo Exit code: !RESULT!>>"%LOG%"
    timeout /t 2 /nobreak >nul
)
echo.>>"%LOG%"
set /a DONE+=1
exit /b 0

:Progress
set /a PERCENT=(DONE*100)/TOTAL
set /a FILLED=(DONE*WIDTH)/TOTAL
set "BAR="
for /l %%I in (1,1,%WIDTH%) do (
    if %%I leq !FILLED! (
        set "BAR=!BAR!#"
    ) else (
        set "BAR=!BAR!-"
    )
)
echo Progress: [!BAR!] !PERCENT!%%  ^(!DONE!/%TOTAL%!^)
exit /b 0
