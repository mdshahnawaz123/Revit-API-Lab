; ==============================================================
; B-LAB ENTERPRISE REVIT PLUGIN INSTALLER (V1.0.1)
; Optimized for Revit 2021-2027
; ==============================================================

#define AppName        "BuiltFlow"
#define AppVersion     "1.0.4.0"
#define AppPublisher   "BIM Digital Design"
#define AppURL         "https://www.bimdigitaldesign.com"
#define VendorId       "BIM Digital Design"
#define VendorDesc     "BIM Digital Design"
#define AppGuid        "{A1B2C3D4-1234-5678-ABCD-123456789000}"
#define AppIconFile    "C:\Users\Mohd Shahnawaz\source\repos\Revit-API-Lab\BDD.ico"

; ================= BUILD PATHS =================
#define API_ROOT_24  "C:\Users\Mohd Shahnawaz\source\repos\Revit-API-Lab\B-Lab\bin\x64\Release\net48"
#define API_ROOT_25  "C:\Users\Mohd Shahnawaz\source\repos\Revit-API-Lab\B-Lab\bin\x64\Release\net8.0-windows"
#define API_ROOT_26  "C:\Users\Mohd Shahnawaz\source\repos\Revit-API-Lab\B-Lab\bin\x64\Release\net9.0-windows"
#define API_ROOT_27  "C:\Users\Mohd Shahnawaz\source\repos\Revit-API-Lab\B-Lab\bin\x64\Release\net10.0-windows"

#define TOP_ROOT_24  "C:\Users\Mohd Shahnawaz\source\repos\RevitTopSurfacePoint\bin\Release\net48"
#define TOP_ROOT_25  "C:\Users\Mohd Shahnawaz\source\repos\RevitTopSurfacePoint\bin\Release\net8.0-windows"
#define TOP_ROOT_26  "C:\Users\Mohd Shahnawaz\source\repos\RevitTopSurfacePoint\bin\Release\net9.0-windows"
#define TOP_ROOT_27  "C:\Users\Mohd Shahnawaz\source\repos\RevitTopSurfacePoint\bin\Release\net10.0-windows"

#define COST_ROOT_24 "C:\Users\Mohd Shahnawaz\source\repos\CostAnalysis\bin\Release\net48"
#define COST_ROOT_25 "C:\Users\Mohd Shahnawaz\source\repos\CostAnalysis\bin\Release\net8.0-windows"
#define COST_ROOT_26 "C:\Users\Mohd Shahnawaz\source\repos\CostAnalysis\bin\Release\net9.0-windows"
#define COST_ROOT_27 "C:\Users\Mohd Shahnawaz\source\repos\CostAnalysis\bin\Release\net10.0-windows"

#define ABS_ROOT_24  "C:\Users\Mohd Shahnawaz\source\repos\ABS-WIZZ\bin\Release\net48"
#define ABS_ROOT_25  "C:\Users\Mohd Shahnawaz\source\repos\ABS-WIZZ\bin\Release\net8.0-windows"
#define ABS_ROOT_26  "C:\Users\Mohd Shahnawaz\source\repos\ABS-WIZZ\bin\Release\net9.0-windows"
#define ABS_ROOT_27  "C:\Users\Mohd Shahnawaz\source\repos\ABS-WIZZ\bin\Release\net10.0-windows"

#define JOIN_ROOT_24 "D:\RevitPlugin\ElementJoin\bin\Release\net48"
#define JOIN_ROOT_25 "D:\RevitPlugin\ElementJoin\bin\Release\net8.0-windows"
#define JOIN_ROOT_26 "D:\RevitPlugin\ElementJoin\bin\Release\net9.0-windows"
#define JOIN_ROOT_27 "D:\RevitPlugin\ElementJoin\bin\Release\net10.0-windows"

; ================= PLUGIN METADATA =================
#define API_NAME   "BuiltFlow API"
#define API_DLL    "B-Lab.dll"
#define API_CLASS  "B_Lab.RevitApp.App"
#define API_ID     "{0d9c15ae-c9e1-4120-b490-d46c3fc9b9ef}"

#define TOP_NAME   "Top Surface Point"
#define TOP_DLL    "RevitTopSurfacePoint.dll"
#define TOP_CLASS  "RevitTopSurfacePoint.App.App"
#define TOP_ID     "{E0B53B20-619A-448F-86E4-287E80449DE5}"

#define COST_NAME  "Cost Analysis"
#define COST_DLL   "CostAnalysis.dll"
#define COST_CLASS "CostAnalysis.App"
#define COST_ID    "{E647EBA9-4453-465E-AF84-2223F79938D3}"

#define JOIN_NAME  "Element Join"
#define JOIN_DLL   "ElementJoin.dll"
#define JOIN_CLASS "ElementJoin.App.App"
#define JOIN_ID    "{F2C69631-61AF-48ED-9484-3C6E2F874FA3}"

#define ABS_NAME   "ABS Wizz"
#define ABS_DLL    "ABS-WIZZ.dll"
#define ABS_CLASS  "ABS_WIZZ.App.App"
#define ABS_ID     "{130F4AD8-0514-4937-B849-1881DF3A066C}"

[Setup]
AppId={{#AppGuid}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={code:GetDefaultDirName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=Output
OutputBaseFilename=BuiltFlow
SetupIconFile={#AppIconFile}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Icon
Source: "{#AppIconFile}"; DestDir: "{app}"; Flags: ignoreversion

; 1. API Lab Binaries
Source: "{#API_ROOT_24}\*"; DestDir: "{app}\APILab\R24"; Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#API_ROOT_25}\*"; DestDir: "{app}\APILab\R25"; Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#API_ROOT_26}\*"; DestDir: "{app}\APILab\R26"; Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#API_ROOT_27}\*"; DestDir: "{app}\APILab\R27"; Flags: recursesubdirs ignoreversion createallsubdirs

; 2. Other Tools
Source: "{#TOP_ROOT_24}\*";  DestDir: "{app}\TopSurface\R24";   Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#TOP_ROOT_25}\*";  DestDir: "{app}\TopSurface\R25";   Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#TOP_ROOT_26}\*";  DestDir: "{app}\TopSurface\R26";   Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#TOP_ROOT_27}\*";  DestDir: "{app}\TopSurface\R27";   Flags: recursesubdirs ignoreversion createallsubdirs

Source: "{#COST_ROOT_24}\*"; DestDir: "{app}\CostAnalysis\R24"; Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#COST_ROOT_25}\*"; DestDir: "{app}\CostAnalysis\R25"; Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#COST_ROOT_26}\*"; DestDir: "{app}\CostAnalysis\R26"; Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#COST_ROOT_27}\*"; DestDir: "{app}\CostAnalysis\R27"; Flags: recursesubdirs ignoreversion createallsubdirs

Source: "{#ABS_ROOT_24}\*";  DestDir: "{app}\ABS-WIZZ\R24";     Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#ABS_ROOT_25}\*";  DestDir: "{app}\ABS-WIZZ\R25";     Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#ABS_ROOT_26}\*";  DestDir: "{app}\ABS-WIZZ\R26";     Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#ABS_ROOT_27}\*";  DestDir: "{app}\ABS-WIZZ\R27";     Flags: recursesubdirs ignoreversion createallsubdirs

; 3. Element Join
Source: "{#JOIN_ROOT_24}\*"; DestDir: "{app}\ElementJoin\R24"; Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#JOIN_ROOT_25}\*"; DestDir: "{app}\ElementJoin\R25"; Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#JOIN_ROOT_26}\*"; DestDir: "{app}\ElementJoin\R26"; Flags: recursesubdirs ignoreversion createallsubdirs
Source: "{#JOIN_ROOT_27}\*"; DestDir: "{app}\ElementJoin\R27"; Flags: recursesubdirs ignoreversion createallsubdirs

[UninstallDelete]
; Force-remove the entire app directory (including empty subdirs from createallsubdirs)
Type: filesandordirs; Name: "{app}"
; Force-remove .addin manifests from ALL possible Revit Addins locations (2024-2027)
Type: files; Name: "{commonappdata}\Autodesk\Revit\Addins\2024\BDD-Tool.addin"
Type: files; Name: "{commonappdata}\Autodesk\Revit\Addins\2025\BDD-Tool.addin"
Type: files; Name: "{commonappdata}\Autodesk\Revit\Addins\2026\BDD-Tool.addin"
Type: files; Name: "{commonappdata}\Autodesk\Revit\Addins\2027\BDD-Tool.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\BDD-Tool.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\BDD-Tool.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\BDD-Tool.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2027\BDD-Tool.addin"
Type: files; Name: "{commonpf}\Autodesk\Revit\Addins\2024\BDD-Tool.addin"
Type: files; Name: "{commonpf}\Autodesk\Revit\Addins\2025\BDD-Tool.addin"
Type: files; Name: "{commonpf}\Autodesk\Revit\Addins\2026\BDD-Tool.addin"
Type: files; Name: "{commonpf}\Autodesk\Revit\Addins\2027\BDD-Tool.addin"

[Code]
var
  InstallForAllUsers : Boolean;
  Years              : array of string;

function GetDefaultDirName(Param: String): String;
begin
  if InstallForAllUsers then
    Result := ExpandConstant('{commonpf}\BDD-Tool')
  else
    Result := ExpandConstant('{localappdata}\Programs\BDD-Tool');
end;

function InitializeSetup(): Boolean;
var
  Response: Integer;
begin
  Response := MsgBox('Install for ALL users on this machine?' + #13#10#13#10 +
                     'Yes = All Users' + #13#10 +
                     'No = Current User only', mbConfirmation, MB_YESNO);
  if Response = idYes then
    InstallForAllUsers := True
  else
    InstallForAllUsers := False;
    
  Result := True;
end;

function IsRevitRunning(): Boolean;
var
  WbemLocator, WbemServices, WbemObjectSet: Variant;
begin
  Result := False;
  try
    WbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    WbemServices := WbemLocator.ConnectServer('localhost', 'root\CIMV2');
    WbemObjectSet := WbemServices.ExecQuery('SELECT Name FROM Win32_Process WHERE Name="Revit.exe"');
    Result := (WbemObjectSet.Count > 0);
  except
  end;
end;

procedure InitializeWizard();
begin
  // Force "All Users" installation to ensure Administrator rights are strictly applied
  InstallForAllUsers := True;
  SetArrayLength(Years, 4);
  Years[0] := '2024';
  Years[1] := '2025';
  Years[2] := '2026';
  Years[3] := '2027';

  // // Warn user if Revit is running during installation
  if IsRevitRunning() then
  begin
    MsgBox('Autodesk Revit is currently running.' + #13#10 + #13#10 +
           'Please close all Revit instances before continuing the installation ' +
           'to avoid file-locking issues.', mbInformation, MB_OK);
  end;
end;

function InitializeUninstall(): Boolean;
begin
  SetArrayLength(Years, 4);
  Years[0] := '2024';
  Years[1] := '2025';
  Years[2] := '2026';
  Years[3] := '2027';

  // Block uninstall until Revit is closed for a clean removal
  while IsRevitRunning() do
  begin
    if MsgBox('Autodesk Revit is currently running.' + #13#10 + #13#10 +
              'Please close ALL Revit instances for a clean uninstallation.' + #13#10 +
              'Click OK after closing Revit, or Cancel to abort uninstall.',
              mbError, MB_OKCANCEL) = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;

  Result := True;
end;



function GetAddinDir(Year: string): string;
var
  IntYear: Integer;
begin
  IntYear := StrToIntDef(Year, 0);
  if InstallForAllUsers then
  begin
    if IntYear >= 2027 then
      // Revit 2027+: ProgramData is DEPRECATED and ignored by Revit.
      // Use C:\Program Files\Autodesk\Revit\Addins\2027
      Result := ExpandConstant('{autopf}\Autodesk\Revit\Addins\' + Year)
    else
      // Revit 2024-2026: Standard ProgramData location
      Result := ExpandConstant('{commonappdata}\Autodesk\Revit\Addins\' + Year);
  end
  else
    // Per-user: AppData\Roaming works for all Revit versions
    Result := ExpandConstant('{userappdata}\Autodesk\Revit\Addins\' + Year);
end;

function GetSubfolder(Year: string): string;
var
  IntYear: Integer;
begin
  IntYear := StrToIntDef(Year, 0);
  if IntYear <= 2024 then Result := 'R24'
  else if IntYear = 2025 then Result := 'R25'
  else if IntYear = 2026 then Result := 'R26'
  else Result := 'R27';
end;

function GenerateAddinManifest(Year: string): string;
var
  Sub: string;
  IntYear: Integer;
begin
  Sub := GetSubfolder(Year);
  IntYear := StrToIntDef(Year, 0);
  
  Result := '<?xml version="1.0" encoding="utf-8"?>' + #13#10 +
            '<RevitAddIns>' + #13#10;
            
  // API Lab
  Result := Result + '  <AddIn Type="Application">' + #13#10 +
            '    <Name>{#API_NAME}</Name>' + #13#10 +
            '    <Assembly>' + ExpandConstant('{app}\APILab\' + Sub + '\{#API_DLL}') + '</Assembly>' + #13#10 +
            '    <ClientId>{#API_ID}</ClientId>' + #13#10 +
            '    <FullClassName>{#API_CLASS}</FullClassName>' + #13#10 +
            '    <VendorId>{#VendorId}</VendorId>' + #13#10 +
            '    <VendorDescription>{#VendorDesc}</VendorDescription>' + #13#10 +
            '  </AddIn>' + #13#10;

  // Element Join
  Result := Result + '  <AddIn Type="Application">' + #13#10 +
            '    <Name>{#JOIN_NAME}</Name>' + #13#10 +
            '    <Assembly>' + ExpandConstant('{app}\ElementJoin\' + Sub + '\{#JOIN_DLL}') + '</Assembly>' + #13#10 +
            '    <ClientId>{#JOIN_ID}</ClientId>' + #13#10 +
            '    <FullClassName>{#JOIN_CLASS}</FullClassName>' + #13#10 +
            '    <VendorId>{#VendorId}</VendorId>' + #13#10 +
            '    <VendorDescription>{#VendorDesc}</VendorDescription>' + #13#10 +
            '  </AddIn>' + #13#10;

  // Top Surface
  Result := Result + '  <AddIn Type="Application">' + #13#10 +
            '    <Name>{#TOP_NAME}</Name>' + #13#10 +
            '    <Assembly>' + ExpandConstant('{app}\TopSurface\' + Sub + '\{#TOP_DLL}') + '</Assembly>' + #13#10 +
            '    <ClientId>{#TOP_ID}</ClientId>' + #13#10 +
            '    <FullClassName>{#TOP_CLASS}</FullClassName>' + #13#10 +
            '    <VendorId>{#VendorId}</VendorId>' + #13#10 +
            '    <VendorDescription>{#VendorDesc}</VendorDescription>' + #13#10 +
            '  </AddIn>' + #13#10;
  
  // Cost Analysis
  Result := Result + '  <AddIn Type="Application">' + #13#10 +
            '    <Name>{#COST_NAME}</Name>' + #13#10 +
            '    <Assembly>' + ExpandConstant('{app}\CostAnalysis\' + Sub + '\{#COST_DLL}') + '</Assembly>' + #13#10 +
            '    <ClientId>{#COST_ID}</ClientId>' + #13#10 +
            '    <FullClassName>{#COST_CLASS}</FullClassName>' + #13#10 +
            '    <VendorId>{#VendorId}</VendorId>' + #13#10 +
            '    <VendorDescription>{#VendorDesc}</VendorDescription>' + #13#10 +
            '  </AddIn>' + #13#10;

  // ABS WIZZ
  Result := Result + '  <AddIn Type="Application">' + #13#10 +
            '    <Name>{#ABS_NAME}</Name>' + #13#10 +
            '    <Assembly>' + ExpandConstant('{app}\ABS-WIZZ\' + Sub + '\{#ABS_DLL}') + '</Assembly>' + #13#10 +
            '    <ClientId>{#ABS_ID}</ClientId>' + #13#10 +
            '    <FullClassName>{#ABS_CLASS}</FullClassName>' + #13#10 +
            '    <VendorId>{#VendorId}</VendorId>' + #13#10 +
            '    <VendorDescription>{#VendorDesc}</VendorDescription>' + #13#10 +
            '  </AddIn>' + #13#10;

  Result := Result + '</RevitAddIns>';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  i: Integer;
  Dir: string;
  Year: string;
begin
  if CurStep = ssPostInstall then
  begin
    // Create add-in manifests for each targeted Revit version
    for i := 0 to GetArrayLength(Years) - 1 do
    begin
      Year := Years[i];
      Dir := GetAddinDir(Year);
      
      if ForceDirectories(Dir) then
      begin
        SaveStringToFile(Dir + '\BDD-Tool.addin', GenerateAddinManifest(Year), False);
      end;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  i: Integer;
  AppDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // 1. Force removal of .addin files from ALL possible locations
    for i := 0 to GetArrayLength(Years) - 1 do
    begin
      // Force removal from all possible locations to ensure complete uninstallation
      DeleteFile(ExpandConstant('{commonappdata}\Autodesk\Revit\Addins\' + Years[i] + '\BDD-Tool.addin'));
      DeleteFile(ExpandConstant('{userappdata}\Autodesk\Revit\Addins\' + Years[i] + '\BDD-Tool.addin'));
      DeleteFile(ExpandConstant('{commonpf}\Autodesk\Revit\Addins\' + Years[i] + '\BDD-Tool.addin'));
    end;

    // 2. Force removal of the entire app directory to prevent leftover files/empty dirs
    AppDir := ExpandConstant('{app}');
    if DirExists(AppDir) then
      DelTree(AppDir, True, True, True);
  end;
end;

