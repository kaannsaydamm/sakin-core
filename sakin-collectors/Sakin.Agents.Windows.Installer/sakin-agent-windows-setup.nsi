; SAKIN Windows Agent Installer
; Created with NSIS (Nullsoft Scriptable Install System)
;
; Build command:
;   makensis sakin-agent-windows-setup.nsi
;
; For silent install:
;   sakin-agent-windows-setup.exe /S /D=C:\Program Files\Sakin\Agent

!addincludedir "."
!addincludedir "include"

; --------------------------
; VERSION AND DEFINITIONS
; --------------------------
!define APPNAME "SAKIN Agent"
; !define COMPANYNAME "SAKIN Security"
!define DESCRIPTION "SAKIN Security Agent for Windows"
!define VERSIONMAJOR 1
!define VERSIONMINOR 0
!define INSTALLSIZE 15000

; Request application privileges
RequestExecutionLevel admin

; Installer name and output
Name "SAKIN Agent"
Caption "SAKIN Security Agent Installation"
OutFile "..\..\artifacts\bin\sakin-agent-windows-setup.exe"
InstallDir "$PROGRAMFILES\SAKIN\Agent"
InstallDirRegKey HKLM "Software\SAKIN\Agent" "InstallDir"

; Set the compression
SetCompressor /SOLID lzma

; Set the MUI (Modern User Interface)
!include "MUI2.nsh"

; --------------------------
; MUI (Modern User Interface)
; --------------------------
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_NOAUTOCLOSE

; Welcome page
!insertmacro MUI_PAGE_WELCOME

; Directory page
!insertmacro MUI_PAGE_DIRECTORY

; Installation page
!insertmacro MUI_PAGE_INSTFILES

; Finish page
!insertmacro MUI_PAGE_FINISH

; Uninstaller pages
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; Language
!insertmacro MUI_LANGUAGE "English"

; --------------------------
; VERSION INFORMATION
; --------------------------
VIProductVersion "${VERSIONMAJOR}.${VERSIONMINOR}.0.0"
VIAddVersionKey /LANG=1033 "ProductName" "SAKIN Agent"
VIAddVersionKey /LANG=1033 "FileDescription" "SAKIN Security Agent for Windows"
VIAddVersionKey /LANG=1033 "FileVersion" "${VERSIONMAJOR}.${VERSIONMINOR}.0.0"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright © 2024 SAKIN Security"
VIAddVersionKey /LANG=1033 "OriginalFilename" "sakin-agent-windows-setup.exe"

; --------------------------
; INSTALL SECTIONS
; --------------------------
Section "Main Application" SecMain
    SectionIn RO

    ; Set output path to the installation directory
    SetOutPath $INSTDIR

    ; --------------------------
    ; COPY FILES
    ; --------------------------
    
    ; Main application files
    File /r "..\..\sakin-collectors\Sakin.Agents.Windows\bin\Release\net8.0-windows\*.*"
    
    ; Configuration template
    File "..\configs\windows\appsettings.json"
    
    ; Custom actions DLL (if built)
    File "..\Sakin.Agents.Windows.Installer\bin\Release\CustomActions.dll" 0

    ; --------------------------
    ; DIRECTORY SETUP
    ; --------------------------
    
    ; Create logs directory
    SetOutPath $INSTDIR\logs
    SetOutPath $INSTDIR

    ; Create data directory
    CreateDirectory "$APPDATA\SAKIN\Agent"

    ; --------------------------
    ; SERVICE REGISTRATION
    ; --------------------------
    
    ; Install the Windows Service
    ; Using sc.exe for service registration
    ExecWait 'sc.exe create "sakin-agent-windows" binPath= "\"$INSTDIR\Sakin.Agents.Windows.exe\"" DisplayName= "SAKIN Agent" start= "auto" obj= "NT AUTHORITY\SYSTEM" password= ""'
    
    ; Set service description
    WriteRegStr HKLM "SYSTEM\CurrentControlSet\Services\sakin-agent-windows" "Description" "SAKIN Security Agent - Collects and forwards security events"

    ; --------------------------
    ; EVENT LOG REGISTRATION
    ; --------------------------
    
    ; Create event log source
    WriteRegStr HKLM "SYSTEM\CurrentControlSet\Services\EventLog\Application\SAKIN Agent" "EventMessageFile" "$INSTDIR\Sakin.Agents.Windows.dll"
    WriteRegDWORD HKLM "SYSTEM\CurrentControlSet\Services\EventLog\Application\SAKIN Agent" "TypesSupported" 7

    ; --------------------------
    ; REGISTRY SETTINGS
    ; --------------------------
    
    ; Store install directory
    WriteRegStr HKLM "Software\SAKIN\Agent" "InstallDir" "$INSTDIR"
    WriteRegStr HKLM "Software\SAKIN\Agent" "Version" "${VERSIONMAJOR}.${VERSIONMINOR}"

    ; Add uninstall information
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent" "DisplayName" "SAKIN Agent"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent" "UninstallString" '"$INSTDIR\uninstall.exe"'
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent" "DisplayIcon" "$INSTDIR\Sakin.Agents.Windows.exe"
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent" "VersionMajor" ${VERSIONMAJOR}
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent" "VersionMinor" ${VERSIONMINOR}
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent" "NoModify" 1
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent" "NoRepair" 1
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent" "EstimatedSize" ${INSTALLSIZE}

    ; --------------------------
    ; ENVIRONMENT VARIABLES
    ; --------------------------
    
    ; Set environment variables for the service
    WriteRegExpandStr HKLM "SYSTEM\CurrentControlSet\Services\sakin-agent-windows\Parameters" "Environment" "ASPNETCORE_ENVIRONMENT=Production"

    ; --------------------------
    ; START SERVICE
    ; --------------------------
    
    ; Start the service
    ExecWait 'sc.exe start "sakin-agent-windows"'

    ; --------------------------
    ; DESKTOP SHORTCUT
    ; --------------------------
    
    CreateDirectory "$DESKTOP"
    CreateShortCut "$DESKTOP\SAKIN Agent - Logs.lnk" "$INSTDIR\logs" "" "$INSTDIR\Sakin.Agents.Windows.exe" 0
    CreateShortCut "$DESKTOP\SAKIN Agent - Uninstall.lnk" "$INSTDIR\uninstall.exe" "" "$INSTDIR\uninstall.exe" 0

    ; --------------------------
    ; POST-INSTALLATION VERIFICATION
    ; --------------------------
    
    ; Verify service was created
    IfFileExists "$INSTDIR\Sakin.Agents.Windows.exe" 0 NoExe
        ; Service should be running
        DetailPrint "SAKIN Agent installed successfully"
    NoExe:
    
SectionEnd

; --------------------------
; UNINSTALL SECTION
; --------------------------
Section "Uninstall"

    ; --------------------------
    ; STOP SERVICE
    ; --------------------------
    
    ; Stop and delete the service
    ExecWait 'sc.exe stop "sakin-agent-windows"' 0
    Sleep 1000
    ExecWait 'sc.exe delete "sakin-agent-windows"' 0

    ; --------------------------
    ; REMOVE FILES
    ; --------------------------
    
    ; Remove application files
    Delete "$INSTDIR\Sakin.Agents.Windows.exe"
    Delete "$INSTDIR\Sakin.Agents.Windows.dll"
    Delete "$INSTDIR\*.json"
    Delete "$INSTDIR\appsettings.json"
    Delete "$INSTDIR\*.deps.json"
    Delete "$INSTDIR\*.runtimeconfig.json"
    Delete "$INSTDIR\*.xml"
    Delete "$INSTDIR\uninstall.exe"
    
    ; Remove log files
    Delete "$INSTDIR\logs\*.*"
    RMDir "$INSTDIR\logs"

    ; Remove data directory
    RMDir "$APPDATA\SAKIN\Agent"

    ; Remove installation directory
    RMDir "$INSTDIR"

    ; --------------------------
    ; REMOVE REGISTRY KEYS
    ; --------------------------
    
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SAKINAgent"
    DeleteRegKey HKLM "Software\SAKIN\Agent"
    DeleteRegKey HKLM "SYSTEM\CurrentControlSet\Services\EventLog\Application\SAKIN Agent"

    ; --------------------------
    ; REMOVE DESKTOP SHORTCUTS
    ; --------------------------
    
    Delete "$DESKTOP\SAKIN Agent - Logs.lnk"
    Delete "$DESKTOP\SAKIN Agent - Uninstall.lnk"

SectionEnd

; --------------------------
; FUNCTIONS
; --------------------------

; Function to check if .NET 8.0 is installed
Function .NETCheck
    Push $R0
    Push $R1
    
    ; Check for .NET 8.0 registry key
    ReadRegStr $R0 HKLM "SOFTWARE\Microsoft\dotnet\InstalledSdk\8.0" ""
    StrCmp $R0 "" NotInstalled 0
    
    ; Check for runtime
    ReadRegStr $R0 HKLM "SOFTWARE\Microsoft\dotnet\Runtime\8.0" "Version"
    StrCmp $R0 "" NotInstalled 0
    
    MessageBox MB_OK "NET 8.0 found: $R0"
    Goto Done
    
    NotInstalled:
        MessageBox MB_OK "NET 8.0 not found. Please install .NET 8.0 Runtime first."
    
    Done:
        Pop $R1
        Pop $R0
FunctionEnd

; --------------------------
; INSTALLER FUNCTIONS
; --------------------------

; .onInit function for initialization
Function .onInit
    ; Check for admin privileges
    UserInfo::GetAccountType
    Pop $R0
    StrCmp $R0 "Admin" 0 NotAdmin
        Goto Done
    
    NotAdmin:
        MessageBox MB_OK|MB_ICONERROR "This installer requires administrator privileges.$\nPlease run as administrator."
        Abort
    
    Done:
FunctionEnd

; .onGUIInit function for GUI initialization
Function .onGUIInit
FunctionEnd

; .onInstSuccess function called after successful install
Function .onInstSuccess
    ; Check if service started successfully
    System::Call 'advapi32::OpenSCManagerA(0,0,0) i.s'
    Pop $R0
    System::Call 'advapi32::OpenServiceA(i $R0, t "sakin-agent-windows", i 0x4) i.s'
    Pop $R1
    System::Call 'advapi32::CloseServiceHandle(i $R1)'
    System::Call 'advapi32::CloseServiceHandle(i $R0)'
    
    ${If} $R1 <> 0
        MessageBox MB_OK|MB_ICONINFORMATION "SAKIN Agent has been installed and started successfully!$\n$\nThe service is configured to start automatically on system boot."
    ${Else}
        MessageBox MB_OK|MB_ICONWARNING "SAKIN Agent has been installed, but the service could not be started.$\nPlease check the Event Viewer for errors."
    ${EndIf}
FunctionEnd

; .onUninstSuccess function called after successful uninstall
Function .onUninstSuccess
    MessageBox MB_OK|MB_ICONINFORMATION "SAKIN Agent has been uninstalled successfully."
FunctionEnd
