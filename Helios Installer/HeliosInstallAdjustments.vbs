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
   
   ' upgrade code for Helios Dev Custom, installed in Program Files\Helios Virtual Cockpit\Helios Dev Custom
   ' (must match UpgradeCode in the Helios installer projects).  Because it differs from regular Helios,
   ' the regular Helios installer never upgrades or removes it; only Helios Dev Custom installers do.
   Dim heliosDevCustomUpgrade:heliosDevCustomUpgrade = "{27158E45-C08C-42E4-AD7A-473D0E2AD3A1}"

   ' upgrade code for Helios Development Prototypes
   Dim heliosDevUpgrade:heliosDevUpgrade = "{BA7FFC56-BDB7-4B02-8A12-089A630CCF96}"

   ' open MSI as database session ourselves, instead of using WiRunSQL wrapper
   Dim installer : Set installer = Wscript.CreateObject("WindowsInstaller.Installer") : CheckError "connect to installer"
   Dim database : Set database = installer.OpenDatabase(msiPackage, 1) : CheckError "open database"
   Dim session : Set session = installer.OpenPackage(database,1) : If Err <> 0 Then Fail "Installer: '" & msiPackage & "' has invalid installer package format"

   ' check product (this script is shared)
   Dim isDevCustom:isDevCustom = (session.Property("ProductName") = "Helios Dev Custom")
   if isDevCustom then
        Wscript.Echo "configuring Helios Dev Custom"
        upgradeCode = heliosDevCustomUpgrade
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

   if session.Property("ProductName") = "Helios" or isDevCustom then
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

   ' Helios Dev Custom is built from the same installer project as regular Helios, so its components would
   ' otherwise carry the same component IDs.  Windows Installer tracks installed files by component ID, so
   ' sharing them would let installing or removing regular Helios affect this installation.  Give every
   ' component its own ID (the same ones on every build, so Helios Dev Custom upgrades itself cleanly).
   if isDevCustom then
      MakeComponentIdsUnique database
   end if

   ' allow any version to upgrade
   Execute database, "DELETE FROM Upgrade"
   ' Windows Installer only compares the first three version numbers, so for example 1.6.6200.75 and 1.6.6200.7
   ' count as the same version.  "build.cmd auto" sets HELIOS_REPLACE_SAME_VERSION so its installer replaces an
   ' installed Helios with the same first three numbers, and is only blocked by a strictly newer one.
   Dim replaceSameVersion
   replaceSameVersion = (CreateObject("WScript.Shell").Environment("PROCESS")("HELIOS_REPLACE_SAME_VERSION") = "1")
   if replaceSameVersion then
      Wscript.Echo "Installer will replace an installed version with the same first three numbers"
      ' 2 = detect only, minimum exclusive: only strictly newer installs block this one
      Execute database, "INSERT INTO Upgrade(UpgradeCode, VersionMin, Attributes, ActionProperty) VALUES ('" & upgradeCode & "', '" & version & "', '2', 'NEWERPRODUCTFOUND')"
      ' 512 = maximum inclusive: remove everything up to and including this version
      Execute database, "INSERT INTO Upgrade(UpgradeCode, VersionMax, Attributes, ActionProperty) VALUES ('" & upgradeCode & "', '" & version & "', '512', 'PREVIOUSVERSIONINSTALLED')"
   else
      Execute database, "INSERT INTO Upgrade(UpgradeCode, VersionMin, Attributes, ActionProperty) VALUES ('" & upgradeCode & "', '" & version & "', '258', 'NEWERPRODUCTFOUND')"
      Execute database, "INSERT INTO Upgrade(UpgradeCode, VersionMax, Attributes, ActionProperty) VALUES ('" & upgradeCode & "', '" & version & "', '0', 'PREVIOUSVERSIONINSTALLED')"
   end if

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

' replace every ComponentId with a different, stable GUID: the hex digits of the original in reverse order
Sub MakeComponentIdsUnique(database)
   Dim view, record, original, digits, reversed, count
   Set view = database.OpenView("SELECT `Component`, `ComponentId` FROM `Component`") : CheckError "open Component table"
   view.Execute : CheckError "read Component table"
   count = 0
   Do
      Set record = view.Fetch : CheckError "fetch component"
      If record Is Nothing Then Exit Do
      original = record.StringData(2)
      If Len(original) = 38 Then
         digits = Replace(Mid(original, 2, 36), "-", "")
         reversed = StrReverse(digits)
         record.StringData(2) = "{" & Mid(reversed, 1, 8) & "-" & Mid(reversed, 9, 4) & "-" & Mid(reversed, 13, 4) & "-" & _
                                Mid(reversed, 17, 4) & "-" & Mid(reversed, 21, 12) & "}"
         view.Modify 2, record : CheckError "update component " & record.StringData(1)
         count = count + 1
      End If
   Loop
   view.Close
   Wscript.Echo "Gave " & count & " components their own IDs for Helios Dev Custom"
End Sub

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

 
