; Instalador do Carreta Joy 3D (Windows) - gerado no GitHub Actions com NSIS
Unicode true
!include "MUI2.nsh"
!ifndef VERSION
  !define VERSION "0.0.0"
!endif
!ifndef SRC
  !define SRC "..\..\build\StandaloneWindows64"
!endif
Name "Carreta Joy 3D"
OutFile "CarretaJoy3D-Setup-v${VERSION}.exe"
InstallDir "$PROGRAMFILES64\Carreta Joy 3D"
InstallDirRegKey HKLM "Software\CarretaJoy3D" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "Carreta Joy 3D"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "FileDescription" "Instalador do Carreta Joy 3D"
VIAddVersionKey "LegalCopyright" "Família Carreta Joy"

!define MUI_ICON "icon.ico"
!define MUI_UNICON "icon.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "side.bmp"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "Carreta Joy 3D"
!define MUI_WELCOMEPAGE_TEXT "Arthur, Afonso e a turma na carreta pelas ruas de Ponta Grossa!$\r$\n$\r$\nClique em Avançar para instalar."
!define MUI_FINISHPAGE_RUN "$INSTDIR\CarretaJoy3D.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Jogar agora"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"

Section "Jogo" SecMain
  SetOutPath "$INSTDIR"
  File /r "${SRC}\*.*"
  File "icon.ico"
  WriteUninstaller "$INSTDIR\Desinstalar.exe"
  CreateDirectory "$SMPROGRAMS\Carreta Joy 3D"
  CreateShortcut "$SMPROGRAMS\Carreta Joy 3D\Carreta Joy 3D.lnk" "$INSTDIR\CarretaJoy3D.exe" "" "$INSTDIR\icon.ico"
  CreateShortcut "$SMPROGRAMS\Carreta Joy 3D\Desinstalar.lnk" "$INSTDIR\Desinstalar.exe"
  CreateShortcut "$DESKTOP\Carreta Joy 3D.lnk" "$INSTDIR\CarretaJoy3D.exe" "" "$INSTDIR\icon.ico"
  WriteRegStr HKLM "Software\CarretaJoy3D" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CarretaJoy3D" "DisplayName" "Carreta Joy 3D"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CarretaJoy3D" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CarretaJoy3D" "Publisher" "Família Carreta Joy"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CarretaJoy3D" "DisplayIcon" "$INSTDIR\icon.ico"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CarretaJoy3D" "UninstallString" '"$INSTDIR\Desinstalar.exe"'
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CarretaJoy3D" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CarretaJoy3D" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\Carreta Joy 3D.lnk"
  RMDir /r "$SMPROGRAMS\Carreta Joy 3D"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CarretaJoy3D"
  DeleteRegKey HKLM "Software\CarretaJoy3D"
SectionEnd
