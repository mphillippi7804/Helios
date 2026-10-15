Option Explicit
On Error Resume Next
Dim argNum, argCount:argCount = Wscript.Arguments.Count
if argCount = 0  then
   Wscript.Echo "Helios Installer Post Build changes requires an argument which is the" &_
   vbLf & " msi file to have the releases changed, and also set certain flags." &_
   vbLf & " No argument was passed into the HeliosInstallAdjustments.vbs file." &_
   vbLf & " SELECT queries will display the rows of the result list specified in the query"
    Wscript.Quit 1
else
   Dim msiPackage:msiPackage = Wscript.Arguments(0)
   Dim oShell 
   Dim TypeLib
   Dim version:version = Wscript.Arguments(1)
   Dim infinity:infinity = "65535" & MID(version, 2)
   Wscript.Echo "Starting Post Build Script to set up " & msiPackage & " for version " & version
   Wscript.Echo "Setting file versions to " & infinity

   ' https://docs.microsoft.com/en-us/windows/win32/msi/database-object
   ' https://docs.microsoft.com/en-us/windows/win32/msi/session-object

   ' upgrade codes for Helios 1.4+ (production)
   Dim heliosUpgrade:heliosUpgrade = "{589D8667-3ED9-478B-8F67-A56E4FADBC63}"
   Dim keypressUpgrade:keypressUpgrade = "{EC3AC978-542C-4062-B6D0-F652A7C3E134}"
   Dim toolsUpgrade:toolsUpgrade = "{1A6DEA70-100A-4C3B-A9D9-64340BEAC520}"
   Dim upgradeCode:upgradeCode = heliosUpgrade
   
   ' upgrade code for Helios Developer Edition, which installs alongside regular Helios
   ' in Program Files\Helios Development (must match UpgradeCode in the Helios installer projects)
   Dim heliosDevEditionUpgrade:heliosDevEditionUpgrade = "{61ABF5C4-2190-4DF7-8D49-7203DFD55DE0}"

   ' upgrade code for Helios Development Prototypes
   Dim heliosDevUpgrade:heliosDevUpgrade = "{BA7FFC56-BDB7-4B02-8A12-089A630CCF96}"

   ' open MSI as database session ourselves, instead of using WiRunSQL wrapper
   Dim installer : Set installer = Wscript.CreateObject("WindowsInstaller.Installer") : CheckError "connect to installer"
   Dim database : Set database = installer.OpenDatabase(msiPackage, 1) : CheckError "open database"
   Dim session : Set session = installer.OpenPackage(database,1) : If Err <> 0 Then Fail "Installer: '" & msiPackage & "' has invalid installer package format"

   ' check product (this script is shared)
   if session.Property("ProductName") = "Helios Developer Edition" then
        Wscript.Echo "configuring Helios Developer Edition"
        upgradeCode = heliosDevEditionUpgrade
   elseif session.Property("ProductName") = "Helios Keypress Receiver" then
        Wscript.Echo "configuring Helios Keypress Receiver Installer"
        upgradeCode = keypressUpgrade
   elseif session.Property("ProductName") = "Helios Developer Tools" then
        Wscript.Echo "configuring Helios Developer Tools"
        upgradeCode = toolsUpgrade
   end if

   ' change product version and file versions
   Execute database, "UPDATE Property SET `Value` = '" & version & "' WHERE `Property` = 'ProductVersion'"
   Execute database, "UPDATE File SET `Version` = '" & infinity & "' WHERE `Version` <> ''"

   ' make all upgrades major upgrades
   Set TypeLib = CreateObject("Scriptlet.TypeLib")
   Dim newGuid:newGuid = TypeLib.Guid
   newGuid = Left(newGuid, Len(newGuid)-2)
   Execute database, "UPDATE Property SET `Value` = '" & newGuid & "' WHERE `Property` = 'ProductCode'"

   ' run custom actions as user instead of system
   Execute database, "UPDATE CustomAction SET `Type` = 1025 WHERE `Type` = 3073"

   if session.Property("ProductName") = "Helios" or session.Property("ProductName") = "Helios Developer Edition" then
		if not RemoveProblemComponent( database, "PATCHING.DLL|patching.dll") then 
            ' if lowercase was not found then try mixed case
            if not RemoveProblemComponent( database, "PATCHING.DLL|Patching.dll") then
                	Wscript.Echo  "* * * Info: Component Patching.dll was not found and not deleted."
            end if
        end if
   End if


   ' special handling for development builds
   Dim devBuild
   Set devBuild = New RegExp
   devBuild.Pattern = "[0-9]+\.[0-9]+\.1...\.[0-9]+"
   if devBuild.Test(version) then
     Execute database, "DELETE FROM Shortcut WHERE `Directory_` = 'DesktopFolder' " 
     Execute database, "UPDATE Shortcut SET `Name` = 'CONTRO~1|Dev Control Center Debug' WHERE `Name` = 'CONTRO~1|Control Center Debug'"
     Execute database, "UPDATE Shortcut SET `Name` = 'PROFIL~1|Dev Profile Editor Debug' WHERE `Name` = 'PROFIL~1|Profile Editor Debug'"
     Execute database, "UPDATE Shortcut SET `Name` = 'HELIOS~2|Dev Helios Profile Editor' WHERE `Name` = 'HELIOS~2|Helios Profile Editor'"
     Execute database, "UPDATE Shortcut SET `Name` = 'HELIOS~4|Dev Helios Control Center' WHERE `Name` = 'HELIOS~4|Helios Control Center'"

     if session.Property("ProductName") = "Helios" then
         Wscript.Echo "Changing product to HeliosDev"
         upgradeCode = heliosDevUpgrade
         Execute database, "UPDATE Property SET `Value` = '" & heliosDevUpgrade & "' WHERE `Property` = 'UpgradeCode'"
         Execute database, "UPDATE Property SET `Value` = 'HeliosDev' WHERE `Property` = 'ProductName'"
     end if

     ' fix up installation paths (DIRCA_TARGETDIR) for dev tools (or similar) that install on top of Helios folder
     Execute database, "UPDATE CustomAction SET `Target` = '[ProgramFiles64Folder][Manufacturer]\HeliosDev' WHERE `Target` = '[ProgramFiles64Folder][Manufacturer]\Helios'"

     ' fix up start menu folder
     Execute database, "UPDATE Directory SET `DefaultDir` = 'HELIOS~1|HeliosDev' WHERE `DefaultDir` = 'HELIOS|Helios'"
   end if

   ' allow any version to upgrade
   Execute database, "DELETE FROM Upgrade"
   Execute database, "INSERT INTO Upgrade(UpgradeCode, VersionMin, Attributes, ActionProperty) VALUES ('" & upgradeCode & "', '" & version & "', '258', 'NEWERPRODUCTFOUND')"
   Execute database, "INSERT INTO Upgrade(UpgradeCode, VersionMax, Attributes, ActionProperty) VALUES ('" & upgradeCode & "', '" & version & "', '0', 'PREVIOUSVERSIONINSTALLED')"

   ' "UPDATE MsiAssemblyName SET Value = '" & version & "' WHERE `Value` = '" & sOldRel & "'"

   ' set disable advertise shortcuts if not already done
   if session.Property("DISABLEADVTSHORTCUTS") < "1" then
      Wscript.Echo "Setting DISABLEADVTSHORTCUTS = 1"
      Execute database, "INSERT INTO Property(Property, Value) VALUES ('DISABLEADVTSHORTCUTS', '1')"
   else
      Wscript.Echo "DISABLEADVTSHORTCUTS " & session.Property("DISABLEADVTSHORTCUTS")
   end if

   ' commit database
   database.Commit() : CheckError "commit"

   ' check results
   ' XXX since we can't correctly close the views, we need to do this in another process
   ' session = nothing
   ' database = nothing
   ' Dim session2 : Set session2 = installer.OpenPackage(msiPackage, 1) : If Err <> 0 ThenFail "Cannot reopen '" & msiPackage & "' to check results"
   ' Wscript.Echo "UpgradeCode " & session2.Property("UpgradeCode")
   ' Wscript.Echo "ProductVersion " & session2.Property("ProductVersion")
   ' Wscript.Echo "ProductCode " & session2.Property("ProductCode")
end if

Wscript.Quit 0

Sub Execute(database, sql)
   Dim view: Set view = database.OpenView(sql) : CheckError sql
   view.Execute : CheckError sql
   ' this appears to be async, so it breaks if we release view, and there is no way
   ' to wait for result because Fetch is not allowed
   ' Dim record: Set record = view.Fetch
   ' view.Close
   ' view = nothing

end Sub

Function RemoveProblemComponent(database, fileName)
    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    ''' REVISIT:  If the Windows Installer starts processing dependencies more reliably, then the following code to '''
    '''           delete a unnecessary and problematic dependencies can probably be removed.                        '''
    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    ' This is to remove a problematic copy of a dll which can end up in TARGETDIR and cause problems.
	RemoveProblemComponent = false
	Dim sql: sql = "SELECT Component.KeyPath, Component.ComponentId, File.FileName FROM Component, File " & _
										   "WHERE File.Component_ = Component.Component AND File.FileName = '" & fileName & "' AND " & _ 
										   "Component.Directory_ = 'TARGETDIR'"
	Dim componentView: Set componentView = database.OpenView(sql)
	componentView.Execute  : CheckError sql
	Dim Record: Set Record = componentView.Fetch() : CheckError sql
    If Not Record Is Nothing then
        Execute database, "DELETE FROM Component WHERE `KeyPath` = '" & Record.StringData(1) & "' " 
    	Wscript.Echo "Delete Component: " & Record.StringData(2) & " for File " & Record.StringData(3)
        RemoveProblemComponent = true
    Else
        RemoveProblemComponent = false
    End If
end Function

Sub CheckError(context)
    Dim message, errRec
    If Err = 0 Then Exit Sub
    message = context & ": " & Err.Source & " " & Hex(Err) & ": " & Err.Description
    If Not installer Is Nothing Then
        Set errRec = installer.LastErrorRecord
        If Not errRec Is Nothing Then message = message & vbNewLine & errRec.FormatText
    End If
    Fail message
End Sub

Sub Fail(message)
    Wscript.Echo message
    Wscript.Quit 2
End Sub

 
