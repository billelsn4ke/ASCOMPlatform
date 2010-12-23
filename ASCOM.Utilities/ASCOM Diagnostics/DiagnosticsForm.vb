﻿' Initial release
' Version 1.0.1.0 - Released

' Fixed issue where all setup log files were not recorded
' Added Conform logs to list of retrieved setup logs
' Added drive scan, reporting available space
' Version 1.0.2.0 - Released 15/10/09 Peter Simpson

Imports ASCOM.Internal
Imports ASCOM.Utilities
Imports ASCOM.Utilities.Exceptions
Imports Microsoft.Win32
Imports System.IO
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Reflection
Imports System.Environment
Imports System.Security.AccessControl
Imports System.Security.Principal
Imports System.Threading

Public Class DiagnosticsForm

    Private Const COMPONENT_CATEGORIES = "Component Categories"
    Private Const ASCOM_ROOT_KEY As String = " (ASCOM Root Key)"
    Const TestTelescopeDescription As String = "This is a test telescope"
    Const RevisedTestTelescopeDescription As String = "Updated description for test telescope!!!"
    Const NewTestTelescopeDescription As String = "New description for test telescope!!!"
#Region "XML  test String"
    Const XMLTestString As String = "<?xml version=""1.0""?>" & vbCrLf & _
                                    "<ASCOMProfile>" & vbCrLf & _
                                    "  <SubKey>" & vbCrLf & _
                                    "    <SubKeyName />" & vbCrLf & _
                                    "    <DefaultValue>" & TestTelescopeDescription & "</DefaultValue>" & vbCrLf & _
                                    "    <Values>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>Results 1</Name>" & vbCrLf & _
                                    "        <Data />" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>Root Test Name</Name>" & vbCrLf & _
                                    "        <Data>Test Value in Root key</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>Test Name</Name>" & vbCrLf & _
                                    "        <Data>Test Value</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>Test Name Default</Name>" & vbCrLf & _
                                    "        <Data>123456</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "    </Values>" & vbCrLf & _
                                    "  </SubKey>" & vbCrLf & _
                                    "  <SubKey>" & vbCrLf & _
                                    "    <SubKeyName>SubKey1</SubKeyName>" & vbCrLf & _
                                    "    <DefaultValue />" & vbCrLf & _
                                    "    <Values />" & vbCrLf & _
                                    "  </SubKey>" & vbCrLf & _
                                    "  <SubKey>" & vbCrLf & _
                                    "    <SubKeyName>SubKey1\SubKey2</SubKeyName>" & vbCrLf & _
                                    "    <DefaultValue>Null Key in SubKey2</DefaultValue>" & vbCrLf & _
                                    "    <Values>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>SubKey2 Test Name</Name>" & vbCrLf & _
                                    "        <Data>Test Value in SubKey 2</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>SubKey2 Test Name1</Name>" & vbCrLf & _
                                    "        <Data>Test Value in SubKey 2</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "    </Values>" & vbCrLf & _
                                    "  </SubKey>" & vbCrLf & _
                                    "  <SubKey>" & vbCrLf & _
                                    "    <SubKeyName>SubKey1\SubKey2\SubKey2a</SubKeyName>" & vbCrLf & _
                                    "    <DefaultValue />" & vbCrLf & _
                                    "    <Values>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>SubKey2a Test Name2a</Name>" & vbCrLf & _
                                    "        <Data>Test Value in SubKey 2a</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "    </Values>" & vbCrLf & _
                                    "  </SubKey>" & vbCrLf & _
                                    "  <SubKey>" & vbCrLf & _
                                    "    <SubKeyName>SubKey1\SubKey2\SubKey2a\SubKey2b</SubKeyName>" & vbCrLf & _
                                    "    <DefaultValue />" & vbCrLf & _
                                    "    <Values>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>SubKey2b Test Name2b</Name>" & vbCrLf & _
                                    "        <Data>Test Value in SubKey 2b</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "    </Values>" & vbCrLf & _
                                    "  </SubKey>" & vbCrLf & _
                                    "  <SubKey>" & vbCrLf & _
                                    "    <SubKeyName>SubKey1\SubKey2\SubKey2c</SubKeyName>" & vbCrLf & _
                                    "    <DefaultValue />" & vbCrLf & _
                                    "    <Values>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>SubKey2c Test Name2c</Name>" & vbCrLf & _
                                    "        <Data>Test Value in SubKey 2c</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "    </Values>" & vbCrLf & _
                                    "  </SubKey>" & vbCrLf & _
                                    "  <SubKey>" & vbCrLf & _
                                    "    <SubKeyName>SubKey3</SubKeyName>" & vbCrLf & _
                                    "    <DefaultValue />" & vbCrLf & _
                                    "    <Values>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>SubKey3 Test Name</Name>" & vbCrLf & _
                                    "        <Data>Test Value SubKey 3</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "    </Values>" & vbCrLf & _
                                    "  </SubKey>" & vbCrLf & _
                                    "  <SubKey>" & vbCrLf & _
                                    "    <SubKeyName>SubKey4</SubKeyName>" & vbCrLf & _
                                    "    <DefaultValue />" & vbCrLf & _
                                    "    <Values>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>SubKey4 Test Name</Name>" & vbCrLf & _
                                    "        <Data>Test Value SubKey 4</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "    </Values>" & vbCrLf & _
                                    "  </SubKey>" & vbCrLf & _
                                    "  <SubKey>" & vbCrLf & _
                                    "    <SubKeyName>SubKeyDefault</SubKeyName>" & vbCrLf & _
                                    "    <DefaultValue />" & vbCrLf & _
                                    "    <Values>" & vbCrLf & _
                                    "      <Value>" & vbCrLf & _
                                    "        <Name>Test Name Default</Name>" & vbCrLf & _
                                    "        <Data>123456</Data>" & vbCrLf & _
                                    "      </Value>" & vbCrLf & _
                                    "    </Values>" & vbCrLf & _
                                    "  </SubKey>" & vbCrLf & _
                                    "</ASCOMProfile>"
#End Region
    Private Const Indent As Integer = 3 ' Display indent for recursive loop output

    Private Const CSIDL_PROGRAM_FILES As Integer = 38 '0x0026
    Private Const CSIDL_PROGRAM_FILESX86 As Integer = 42 '0x002a,
    Private Const CSIDL_WINDOWS As Integer = 36 ' 0x0024,
    Private Const CSIDL_PROGRAM_FILES_COMMONX86 As Integer = 44 ' 0x002c,

    Private NMatches, NNonMatches, NExceptions As Integer
    Private TL As TraceLogger
    Private ASCOMRegistryAccess As ASCOM.Utilities.RegistryAccess
    Private WithEvents ASCOMTimer As ASCOM.Utilities.Timer
    Private RecursionLevel As Integer
    Private g_CountWarning, g_CountIssue, g_CountError As Integer
    Private sw, s1, s2 As Stopwatch
    Private DrvHlpUtil As Object
    Private AscomUtil As ASCOM.Utilities.Util
    Private g_Util2 As Object

    Private LastLogFile As String ' Name of last diagnostics log file

    'DLL to provide the path to Program Files(x86)\Common Files folder location that is not avialable through the .NET framework
    <DllImport("shell32.dll")> _
    Shared Function SHGetSpecialFolderPath(ByVal hwndOwner As IntPtr, _
        <Out()> ByVal lpszPath As System.Text.StringBuilder, _
        ByVal nFolder As Integer, _
        ByVal fCreate As Boolean) As Boolean
    End Function

    Private Sub DiagnosticsForm_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        'Initialise form
        Dim MyVersion As Version
        MyVersion = Assembly.GetExecutingAssembly.GetName.Version
        lblTitle.Text = InstallerName() & " - Diagnostics " & " " & MyVersion.ToString
        lblResult.Text = ""
        lblAction.Text = ""

        lblMessage.Text = "Your diagnostic log will be created in:" & vbCrLf & vbCrLf & _
        System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) & "\ASCOM\Logs " & Format(Now, "yyyy-MM-dd")

        btnLastLog.Enabled = False 'Disable last log button
        sw = New Stopwatch
        Me.BringToFront()
    End Sub

    Sub Status(ByVal Msg As String)
        lblResult.Text = Msg
        Application.DoEvents()
    End Sub

    Sub Action(ByVal Msg As String)
        lblAction.Text = Msg
        Application.DoEvents()
    End Sub

    Private Sub btnCOM_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnCOM.Click
        Dim ASCOMPath As String
        Dim PathShell As New System.Text.StringBuilder(260)
        Dim MyVersion As Version
        Dim SuccessMessage As String

        Try
            Status("Diagnostics running...")

            TL = New TraceLogger("", "Diagnostics")
            TL.Enabled = True

            btnExit.Enabled = False ' Disable buttons during run
            btnLastLog.Enabled = False
            btnCOM.Enabled = False

            'Log Diagnostics version information
            MyVersion = Assembly.GetExecutingAssembly.GetName.Version
            TL.LogMessage("Diagnostics", "Version " & MyVersion.ToString & ", " & Application.ProductVersion)
            TL.BlankLine()
            TL.LogMessage("Diagnostics", "Starting diagnostic run")
            TL.BlankLine()

            LastLogFile = TL.LogFileName
            Try
                Try 'Try and create a registryaccess object
                    ASCOMRegistryAccess = New ASCOM.Utilities.RegistryAccess
                Catch ex As Exception
                    TL.LogMessage("Diagnostics", "ERROR - Unexpected exception creating New RegistryAccess object, later steps will show errors")
                    TL.LogMessageCrLf("Diagnostics", ex.ToString)
                    NExceptions += 1
                End Try

                ScanInstalledPlatform()

                RunningVersions(TL) 'Log diagnostic information

                ScanDrives() 'Scan PC drives and report information

                ScanFrameworks() 'Report on installed .NET Framework versions

                ScanSerial() 'Report serial port information

                ScanASCOMDrivers() : Action("") 'Report installed driver versions

                'ScanProgramFiles() 'Search for copies of Helper and Helper2.DLL in the wrong places

                ScanProfile() : Action("") 'Report profile information

                ScanRegistry() 'Scan Old ASCOM Registry Profile

                ScanProfile55Files() : Action("") 'List contents of Profile 5.5 XML files

                ScanCOMRegistration() 'Report Com Registration

                'Scan files on 32 and 64bit systems
                TL.LogMessage("Files", "")
                ASCOMPath = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles) & "\ASCOM\"
                Call ScanFiles(ASCOMPath) : Action("") 'Scan 32bit files on 32bit OS and 64bit files on 64bit OS

                If System.IntPtr.Size = 8 Then 'We are on a 64bit OS so look in the 32bit locations for files as well
                    SHGetSpecialFolderPath(IntPtr.Zero, PathShell, CSIDL_PROGRAM_FILES_COMMONX86, False)
                    ASCOMPath = PathShell.ToString & "\ASCOM\"
                    Call ScanFiles(ASCOMPath) : Action("")
                End If

                'List GAC contents
                ScanGac()

                'List setup files
                ScanLogs()

                'Scan registry security rights
                ScanRegistrySecurity()

                'Scan event log messages
                ScanEventLog()

                TL.BlankLine()
                TL.LogMessage("Diagnostics", "Completed diagnostic run, starting function testing run")
                TL.BlankLine()
                TL.BlankLine()

                'Functional tests
                UtilTests() : Action("")
                ProfileTests() : Action("")
                TimerTests() : Action("")

                If (NNonMatches = 0) And (NExceptions = 0) Then
                    SuccessMessage = "Congratualtions, all " & NMatches & " function tests passed!"
                Else
                    SuccessMessage = "Completed function testing run: " & NMatches & " matches, " & NNonMatches & " fail(s), " & NExceptions & " exception(s)."
                End If
                TL.LogMessage("Diagnostics", SuccessMessage)
                TL.Enabled = False
                TL.Dispose()
                TL = Nothing
                Status("Diagnostic log created OK")
                Action(SuccessMessage)
            Catch ex As Exception
                Status("Diagnostics exception, please see log")
                TL.LogMessageCrLf("DiagException", ex.ToString)
                TL.Enabled = False
                TL.Dispose()
                Action("")
                TL = Nothing
            Finally
                Try : ASCOMRegistryAccess.Dispose() : Catch : End Try 'Clean up registryaccess object
                ASCOMRegistryAccess = Nothing
            End Try
            btnLastLog.Enabled = True

        Catch ex1 As Exception
            lblResult.Text = "Can't create log: " & ex1.Message
        End Try
        btnExit.Enabled = True ' Enable buttons during run
        btnCOM.Enabled = True
    End Sub

    Private Sub TimerTests()
        Dim start As Date
        TL.LogMessage("TimerTests", "Started")
        Status("Timer tests")
        Try
            ASCOMTimer = New ASCOM.Utilities.Timer
            ASCOMTimer.Interval = 3000
            ASCOMTimer.Enabled = True
            start = Now
            Do
                Thread.Sleep(100)
                Application.DoEvents()
                TL.LogMessage("TimerTests", "Seconds - " & Now.Subtract(start).TotalSeconds)
                Action("Seconds - " & Now.Subtract(start).Seconds)
                Application.DoEvents()
            Loop Until Now.Subtract(start).TotalSeconds > 10.0

        Catch ex As Exception
            TL.LogMessage("TimerTests Exception", ex.ToString)
        Finally
            ASCOMTimer.Enabled = False
        End Try

        TL.LogMessage("TimerTests", "Finished")
        TL.BlankLine()
    End Sub

    Private Sub cnt_TickNet() Handles ASCOMTimer.Tick
        TL.LogMessage("TimerTests", "Fired Net")
        Application.DoEvents()
    End Sub

    Sub ProfileTests()
        Dim RetVal As String = "", RetValProfileKey As New ASCOMProfile

        'Dim DrvHlpProf As Object
        Dim AscomUtlProf As ASCOM.Utilities.Profile
        Const TestScope As String = "Test Telescope"

        Dim keys, values As ArrayList
        Try
            Status("Profile tests")
            TL.LogMessage("ProfileTest", "Creating ASCOM.Utilities.Profile")
            sw.Reset() : sw.Start()
            AscomUtlProf = New ASCOM.Utilities.Profile
            sw.Stop()
            TL.LogMessage("ProfileTest", "ASCOM.Utilities.Profile Created OK in " & sw.ElapsedMilliseconds & " milliseconds")
            AscomUtlProf.DeviceType = "Telescope"

            Compare("ProfileTest", "DeviceType", AscomUtlProf.DeviceType, "Telescope")

            Try : AscomUtlProf.Unregister(TestScope) : Catch : End Try 'Esnure the test scope is not registered
            Compare("ProfileTest", "IsRegistered when not registered should be False", AscomUtlProf.IsRegistered(TestScope).ToString, "False")

            AscomUtlProf.Register(TestScope, "This is a test telescope")
            TL.LogMessage("ProfileTest", TestScope & " registered OK")
            Compare("ProfileTest", "IsRegistered when registered should be True", AscomUtlProf.IsRegistered(TestScope).ToString, "True")

            Compare("ProfileTest", "Get Default Value", "123456", AscomUtlProf.GetValue(TestScope, "Test Name Default", "", "123456"))
            Compare("ProfileTest", "Get Defaulted Value", "123456", AscomUtlProf.GetValue(TestScope, "Test Name Default"))

            Compare("ProfileTest", "Get Default Value SubKey", "123456", AscomUtlProf.GetValue(TestScope, "Test Name Default", "SubKeyDefault", "123456"))
            Compare("ProfileTest", "Get Defaulted Value SubKey", "123456", AscomUtlProf.GetValue(TestScope, "Test Name Default", "SubKeyDefault"))

            AscomUtlProf.WriteValue(TestScope, "Test Name", "Test Value")
            AscomUtlProf.WriteValue(TestScope, "Root Test Name", "Test Value in Root key")

            AscomUtlProf.WriteValue(TestScope, "Test Name", "Test Value SubKey 2", "SubKey1\SubKey2")
            AscomUtlProf.WriteValue(TestScope, "SubKey2 Test Name", "Test Value in SubKey 2", "SubKey1\SubKey2")
            AscomUtlProf.WriteValue(TestScope, "SubKey2 Test Name1", "Test Value in SubKey 2", "SubKey1\SubKey2")
            AscomUtlProf.WriteValue(TestScope, "SubKey2a Test Name2a", "Test Value in SubKey 2a", "SubKey1\SubKey2\SubKey2a")
            AscomUtlProf.WriteValue(TestScope, "SubKey2b Test Name2b", "Test Value in SubKey 2b", "SubKey1\SubKey2\SubKey2a\SubKey2b")
            AscomUtlProf.WriteValue(TestScope, "SubKey2c Test Name2c", "Test Value in SubKey 2c", "SubKey1\SubKey2\SubKey2c")
            AscomUtlProf.WriteValue(TestScope, "", "Null Key in SubKey2", "SubKey1\SubKey2")
            AscomUtlProf.CreateSubKey(TestScope, "SubKey2")
            AscomUtlProf.WriteValue(TestScope, "SubKey3 Test Name", "Test Value SubKey 3", "SubKey3")
            AscomUtlProf.WriteValue(TestScope, "SubKey4 Test Name", "Test Value SubKey 4", "SubKey4")
            Compare("ProfileTest", "GetValue", AscomUtlProf.GetValue(TestScope, "Test Name"), "Test Value")
            Compare("ProfileTest", "GetValue SubKey", AscomUtlProf.GetValue(TestScope, "Test Name", "SubKey1\SubKey2"), "Test Value SubKey 2")

            'Null value write test
            Try
                AscomUtlProf.WriteValue(TestScope, "Results 1", Nothing)
                Compare("ProfileTest", "Null value write test", """" & AscomUtlProf.GetValue(TestScope, "Results 1") & """", """""")
            Catch ex As Exception
                TL.LogMessageCrLf("Null Value Write Test 1 Exception: ", ex.ToString)
                NExceptions += 1
            End Try
            TL.BlankLine()

            TL.LogMessage("ProfileTest", "Testing Profile.SubKeys")
            keys = AscomUtlProf.SubKeys(TestScope, "")
            Compare("ProfileTest", "Create SubKey1", keys(0).Key.ToString & keys(0).Value.ToString, "SubKey1")
            Compare("ProfileTest", "Create SubKey2", keys(1).Key.ToString & keys(1).Value.ToString, "SubKey2")
            Compare("ProfileTest", "Create SubKey3", keys(2).Key.ToString & keys(2).Value.ToString, "SubKey3")
            Compare("ProfileTest", "Create SubKey4", keys(3).Key.ToString & keys(3).Value.ToString, "SubKey4")
            Compare("ProfileTest", "Create SubKeyDefault", keys(4).Key.ToString & keys(4).Value.ToString, "SubKeyDefault")
            Compare("ProfileTest", "SubKey Count", keys.Count.ToString, "5")
            TL.BlankLine()

            TL.LogMessage("ProfileTest", "Testing Profile.Values")
            values = AscomUtlProf.Values(TestScope, "SubKey1\SubKey2")
            Compare("ProfileTest", "SubKey1\SubKey2 Value 0", values(0).Key.ToString & " " & values(0).Value.ToString, " Null Key in SubKey2")
            Compare("ProfileTest", "SubKey1\SubKey2 Value 1", values(1).Key.ToString & " " & values(1).Value.ToString, "SubKey2 Test Name Test Value in SubKey 2")
            Compare("ProfileTest", "SubKey1\SubKey2 Value 2", values(2).Key.ToString & " " & values(2).Value.ToString, "SubKey2 Test Name1 Test Value in SubKey 2")
            Compare("ProfileTest", "SubKey1\SubKey2 Value 3", values(3).Key.ToString & " " & values(3).Value.ToString, "Test Name Test Value SubKey 2")
            Compare("ProfileTest", "SubKey1\SubKey2 Count", values.Count.ToString, "4")
            TL.BlankLine()

            TL.LogMessage("ProfileTest", "Testing Profile.DeleteSubKey - SubKey2")
            AscomUtlProf.DeleteSubKey(TestScope, "Subkey2")
            keys = AscomUtlProf.SubKeys(TestScope, "")
            Compare("ProfileTest", "Create SubKey1", keys(0).Key.ToString & keys(0).Value.ToString, "SubKey1")
            Compare("ProfileTest", "Create SubKey3", keys(1).Key.ToString & keys(1).Value.ToString, "SubKey3")
            Compare("ProfileTest", "Create SubKey4", keys(2).Key.ToString & keys(2).Value.ToString, "SubKey4")
            Compare("ProfileTest", "Create SubKeyDefault", keys(3).Key.ToString & keys(3).Value.ToString, "SubKeyDefault")
            Compare("ProfileTest", "SubKey Count", keys.Count.ToString, "4")
            TL.BlankLine()

            TL.LogMessage("ProfileTest", "Testing Profile.DeleteValue - SubKey1\SubKey2\Test Name")
            AscomUtlProf.DeleteValue(TestScope, "Test Name", "SubKey1\SubKey2")
            values = AscomUtlProf.Values(TestScope, "SubKey1\SubKey2")
            Compare("ProfileTest", "SubKey1\SubKey2 Value 0", values(0).Key.ToString & " " & values(0).Value.ToString, " Null Key in SubKey2")
            Compare("ProfileTest", "SubKey1\SubKey2 Value 1", values(1).Key.ToString & " " & values(1).Value.ToString, "SubKey2 Test Name Test Value in SubKey 2")
            Compare("ProfileTest", "SubKey1\SubKey2 Value 2", values(2).Key.ToString & " " & values(2).Value.ToString, "SubKey2 Test Name1 Test Value in SubKey 2")
            Compare("ProfileTest", "SubKey1\SubKey2 Count", values.Count.ToString, "3")
            TL.BlankLine()

            TL.LogMessage("ProfileTest", "Bulk Profile operation tests")
            Try
                Compare("ProfileTest", "XML Read", AscomUtlProf.GetProfileXML(TestScope), XMLTestString)
            Catch ex As Exception
                TL.LogMessage("GetProfileXML", ex.ToString)
                NExceptions += 1
            End Try

            Try
                RetVal = AscomUtlProf.GetProfileXML(TestScope)
                RetVal = RetVal.Replace(TestTelescopeDescription, RevisedTestTelescopeDescription)
                AscomUtlProf.SetProfileXML(TestScope, RetVal)
                Compare("ProfileTest", "XML Write", AscomUtlProf.GetValue(TestScope, ""), RevisedTestTelescopeDescription)
            Catch ex As Exception
                TL.LogMessageCrLf("SetProfileXML", ex.ToString)
                NExceptions += 1
            End Try

            Try
                RetValProfileKey = AscomUtlProf.GetProfile(TestScope)
                For Each subkey As String In RetValProfileKey.ProfileValues.Keys
                    'TL.LogMessage("SetProfileXML", "Found: " & subkey)
                    For Each valuename As String In RetValProfileKey.ProfileValues.Item(subkey).Keys
                        'TL.LogMessage("SetProfileXML", "Found Value: " & valuename & " = " & RetValProfileKey.ProfileValues.Item(subkey).Item(valuename))
                    Next
                Next
                Compare("ProfileTest", "ASCOMProfile Read", RetValProfileKey.ProfileValues.Item("SubKey1\SubKey2\SubKey2c").Item("SubKey2c Test Name2c"), "Test Value in SubKey 2c")
                RetValProfileKey.SetValue("", NewTestTelescopeDescription)
                RetValProfileKey.SetValue("NewName", "New value")

                RetValProfileKey.SetValue("NewName 2", "New value 2", "\New Subkey 2")
                RetValProfileKey.SetValue("Newname 3", "New value 3", "New Subkey 3")
                AscomUtlProf.SetProfile(TestScope, RetValProfileKey)

                Compare("ProfileTest", "ASCOMProfile Write", AscomUtlProf.GetValue(TestScope, ""), NewTestTelescopeDescription)
                Compare("ProfileTest", "ASCOMProfile Write", AscomUtlProf.GetValue(TestScope, "NewName"), "New value")
                Compare("ProfileTest", "ASCOMProfile Write", AscomUtlProf.GetValue(TestScope, "NewName 2", "\New Subkey 2"), "New value 2")
                Compare("ProfileTest", "ASCOMProfile Write", AscomUtlProf.GetValue(TestScope, "NewName 3", "New Subkey 3"), "New value 3")

                TL.BlankLine()
            Catch ex As Exception
                TL.LogMessageCrLf("SetProfile", ex.ToString)
                NExceptions += 1
            End Try

            'Registered device types test
            Dim DevTypes As String()
            Try
                DevTypes = AscomUtlProf.RegisteredDeviceTypes
                TL.LogMessage("ProfileTest", "DeviceTypes - found " & DevTypes.Length & " device types")
                Compare("ProfileTest", "DeviceTypes", DevTypes(0), "Camera")
                Compare("ProfileTest", "DeviceTypes", DevTypes(1), "Dome")
                Compare("ProfileTest", "DeviceTypes", DevTypes(2), "FilterWheel")
                Compare("ProfileTest", "DeviceTypes", DevTypes(3), "Focuser")
                Compare("ProfileTest", "DeviceTypes", DevTypes(4), "Rotator")
                Compare("ProfileTest", "DeviceTypes", DevTypes(5), "SafetyMonitor")
                Compare("ProfileTest", "DeviceTypes", DevTypes(6), "Switch")
                Compare("ProfileTest", "DeviceTypes", DevTypes(7), "Telescope")
                TL.BlankLine()
            Catch ex As Exception
                TL.LogMessage("RegisteredDeviceTypes", ex.ToString)
                NExceptions += 1
            End Try

            'Registered devices tests
            Try
                TL.LogMessage("ProfileTest", "Installed Simulator Devices")
                keys = AscomUtlProf.RegisteredDevices("Camera")
                CheckSimulator(keys, "Camera", "ASCOM.Simulator.Camera")
                CheckSimulator(keys, "Camera", "CCDSimulator.Camera")
                keys = AscomUtlProf.RegisteredDevices("Dome")
                CheckSimulator(keys, "Dome", "DomeSim.Dome")
                CheckSimulator(keys, "Dome", "Hub.Dome")
                CheckSimulator(keys, "Dome", "Pipe.Dome")
                CheckSimulator(keys, "Dome", "POTH.Dome")
                keys = AscomUtlProf.RegisteredDevices("FilterWheel")
                CheckSimulator(keys, "FilterWheel", "ASCOM.FilterWheelSim.FilterWheel")
                CheckSimulator(keys, "FilterWheel", "FilterWheelSim.FilterWheel")
                keys = AscomUtlProf.RegisteredDevices("Focuser")
                CheckSimulator(keys, "Focuser", "FocusSim.Focuser")
                CheckSimulator(keys, "Focuser", "Hub.Focuser")
                CheckSimulator(keys, "Focuser", "Pipe.Focuser")
                CheckSimulator(keys, "Focuser", "POTH.Focuser")
                keys = AscomUtlProf.RegisteredDevices("Rotator")
                CheckSimulator(keys, "Rotator", "ASCOM.Simulator.Rotator")
                keys = AscomUtlProf.RegisteredDevices("SafetyMonitor")
                CheckSimulator(keys, "SafetyMonitor", "ASCOM.Simulator.SafetyMonitor")
                keys = AscomUtlProf.RegisteredDevices("Switch")
                CheckSimulator(keys, "Switch", "ASCOM.Simulator.Switch")
                CheckSimulator(keys, "Switch", "SwitchSim.Switch")
                keys = AscomUtlProf.RegisteredDevices("Telescope")
                CheckSimulator(keys, "Telescope", "ASCOM.Simulator.Telescope")
                CheckSimulator(keys, "Telescope", "ASCOMDome.Telescope")
                CheckSimulator(keys, "Telescope", "Hub.Telescope")
                CheckSimulator(keys, "Telescope", "Pipe.Telescope")
                CheckSimulator(keys, "Telescope", "POTH.Telescope")
                CheckSimulator(keys, "Telescope", "ScopeSim.Telescope")

                DevTypes = AscomUtlProf.RegisteredDeviceTypes
                For Each DevType As String In DevTypes
                    'TL.LogMessage("RegisteredDevices", "Found " & DevType)
                    keys = AscomUtlProf.RegisteredDevices(DevType)
                    For Each kvp As KeyValuePair In keys
                        'TL.LogMessage("RegisteredDevices", "  " & kvp.Key & " - " & kvp.Value)
                    Next
                Next
            Catch ex As Exception
                TL.LogMessageCrLf("RegisteredDevices", ex.ToString)
                NExceptions += 1
            End Try

            'Empty string
            Try
                keys = AscomUtlProf.RegisteredDevices("")
                TL.LogMessage("RegisteredDevices EmptyString", "Found " & keys.Count & " devices")
                For Each kvp As KeyValuePair(Of String, String) In keys
                    TL.LogMessage("RegisteredDevices EmptyString", "  " & kvp.Key & " - " & kvp.Value)
                Next
            Catch ex As ASCOM.Utilities.Exceptions.InvalidValueException
                Compare("ProfileTest", "RegisteredDevices with an empty string", "InvalidValueException", "InvalidValueException")
            Catch ex As Exception
                TL.LogMessageCrLf("RegisteredDevices EmptyString", ex.ToString)
                NExceptions += 1
            End Try

            'Nothing
            Try
                keys = AscomUtlProf.RegisteredDevices(Nothing)
                TL.LogMessage("RegisteredDevices Nothing", "Found " & keys.Count & " devices")
                For Each kvp As KeyValuePair(Of String, String) In keys
                    TL.LogMessage("RegisteredDevices Nothing", "  " & kvp.Key & " - " & kvp.Value)
                Next
            Catch ex As ASCOM.Utilities.Exceptions.InvalidValueException
                Compare("ProfileTest", "RegisteredDevices with a null value", "InvalidValueException", "InvalidValueException")
            Catch ex As Exception
                TL.LogMessageCrLf("RegisteredDevices Nothing", ex.ToString)
                NExceptions += 1
            End Try

            'Bad value
            Try
                keys = AscomUtlProf.RegisteredDevices("asdwer vbn tyu")
                Compare("ProfileTest", "RegisteredDevices with an Unknown DeviceType", keys.Count.ToString, "0")
                For Each kvp As KeyValuePair(Of String, String) In keys
                    TL.LogMessage("RegisteredDevices Bad", "  " & kvp.Key & " - " & kvp.Value)
                Next
            Catch ex As ASCOM.Utilities.Exceptions.InvalidValueException
                TL.LogMessage("ProfileTest", "RegisteredDevices Unknown DeviceType incorrectly generated an InvalidValueException")
            Catch ex As Exception
                TL.LogMessage("RegisteredDevices Bad", ex.ToString)
                NExceptions += 1
            End Try
            TL.BlankLine()

            Status("Profile performance tests")
            'Timing tests
            sw.Reset() : sw.Start()
            For i = 1 To 100
                AscomUtlProf.WriteValue(TestScope, "Test Name " & i.ToString, "Test Value")
            Next
            sw.Stop() : TL.LogMessage("ProfilePerformance", "Writevalue : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            sw.Reset() : sw.Start()
            For i = 1 To 100
                keys = AscomUtlProf.SubKeys(TestScope, "")
            Next
            sw.Stop() : TL.LogMessage("ProfilePerformance", "SubKeys : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            sw.Reset() : sw.Start()
            For i = 1 To 100
                keys = AscomUtlProf.Values(TestScope, "SubKey1\SubKey2")
            Next
            sw.Stop() : TL.LogMessage("ProfilePerformance", "Values : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            sw.Reset() : sw.Start()
            For i = 1 To 100
                RetVal = AscomUtlProf.GetValue(TestScope, "Test Name " & i.ToString)
            Next
            sw.Stop() : TL.LogMessage("ProfilePerformance", "GetValue : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            sw.Reset()
            For i = 1 To 100
                AscomUtlProf.WriteValue(TestScope, "Test Name", "Test Value SubKey 2", "SubKey1\SubKey2")
                sw.Start()
                AscomUtlProf.DeleteValue(TestScope, "Test Name", "SubKey1\SubKey2")
                sw.Stop()
            Next
            TL.LogMessage("ProfilePerformance", "Delete : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            sw.Reset() : sw.Start()
            For i = 1 To 100
                RetVal = AscomUtlProf.GetProfileXML(TestScope)
            Next
            sw.Stop() : TL.LogMessage("ProfilePerformance", "GetProfileXML : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            sw.Reset() : sw.Start()
            For i = 1 To 100
                RetVal = AscomUtlProf.GetProfileXML("ScopeSim.Telescope")
            Next
            sw.Stop() : TL.LogMessage("ProfilePerformance", "GetProfileXML : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            sw.Reset() : sw.Start()
            For i = 1 To 100
                RetValProfileKey = AscomUtlProf.GetProfile(TestScope)
            Next
            sw.Stop() : TL.LogMessage("ProfilePerformance", "GetProfile : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            sw.Reset() : sw.Start()
            For i = 1 To 100
                RetValProfileKey = AscomUtlProf.GetProfile("ScopeSim.Telescope")
            Next
            sw.Stop() : TL.LogMessage("ProfilePerformance", "GetProfile : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            sw.Reset() : sw.Start()
            For i = 1 To 100
                AscomUtlProf.SetProfile("ScopeSim.Telescope", RetValProfileKey)
            Next
            sw.Stop() : TL.LogMessage("ProfilePerformance", "SetProfile : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            sw.Reset() : sw.Start()
            For i = 1 To 100
                AscomUtlProf.SetProfileXML("ScopeSim.Telescope", RetVal)
            Next
            sw.Stop() : TL.LogMessage("ProfilePerformance", "SetProfileXML : " & sw.ElapsedMilliseconds / 100 & " milliseconds")

            TL.BlankLine()

            AscomUtlProf.Unregister(TestScope)
            Compare("ProfileTest", "Test telescope registered after unregister", AscomUtlProf.IsRegistered(TestScope), "False")

            AscomUtlProf.Dispose()
            TL.LogMessage("ProfileTest", "Profile Disposed OK")
            AscomUtlProf = Nothing
            TL.BlankLine()

            Status("Profile multi-tasking tests")

            Dim P1, P2 As New Profile, R1, R2 As String

            P1.Register(TestScope, "Multi access tester")

            P1.WriteValue(TestScope, TestScope, "1")
            R1 = P1.GetValue(TestScope, TestScope)
            R2 = P2.GetValue(TestScope, TestScope)
            Compare("ProfileMultiAccess", "MultiAccess", R1, R2)

            P1.WriteValue(TestScope, TestScope, "2")
            R1 = P1.GetValue(TestScope, TestScope)
            R2 = P2.GetValue(TestScope, TestScope)
            Compare("ProfileMultiAccess", "MultiAccess", R1, R2)

            P2.Dispose()
            P1.Dispose()

            'Multiple writes to the same value - single threaded
            TL.LogMessage("ProfileMultiAccess", "MultiWrite - SingleThread Started")
            Action("MultiWrite - SingleThread Started")
            Try
                Dim P(100) As Profile
                For i = 1 To 100
                    P(i) = New Profile
                    P(i).WriteValue(TestScope, TestScope, "27")
                Next
            Catch ex As Exception
                TL.LogMessage("MultiWrite - SingleThread", ex.ToString)
                NExceptions += 1
            End Try

            TL.LogMessage("ProfileMultiAccess", "MultiWrite - SingleThread Finished")

            TL.LogMessage("ProfileMultiAccess", "MultiWrite - MultiThread Started")
            Action("MultiWrite - MultiThread Started")
            'Multiple writes -multi-threaded
            Const NThreads As Integer = 3

            Dim ProfileThreads(NThreads) As Thread
            For i = 0 To NThreads
                ProfileThreads(i) = New Thread(AddressOf ProfileThread)
                ProfileThreads(i).Start(i)
            Next
            For i = 0 To NThreads
                ProfileThreads(i).Join()
            Next

            TL.LogMessage("ProfileMultiAccess", "MultiWrite - MultiThread Finished")
            TL.BlankLine()
            TL.BlankLine()

        Catch ex As Exception
            TL.LogMessageCrLf("Exception", ex.ToString)
            NExceptions += 1
        End Try

    End Sub

    Sub CheckSimulator(ByVal Devices As ArrayList, ByVal DeviceType As String, ByVal DeviceName As String)
        Dim Found As Boolean = False
        For Each Device In Devices
            If Device.Key = DeviceName Then Found = True
        Next

        If Found Then
            Compare("ProfileTest", DeviceType, DeviceName, DeviceName)
        Else
            Compare("ProfileTest", DeviceType, DeviceName, "")
        End If
    End Sub

    Sub ProfileThread(ByVal inst As Integer)
        Dim TL As New TraceLogger("", "ProfileTrace " & inst.ToString)
        Dim ts As String = "Test Telescope"
        TL.Enabled = True
        'TL.LogMessage("MultiWrite - MultiThread", "ThreadStart")
        TL.LogMessage("Started", "")
        Try
            Dim P(100) As Profile
            For i = 1 To 100
                P(i) = New Profile
                P(i).WriteValue(ts, ts, i.ToString)
                TL.LogMessage("Written", i.ToString)
                P(i).Dispose()
            Next
        Catch ex As Exception
            TL.LogMessage("MultiWrite - MultiThread", ex.ToString)
            'Throw New ASCOM.Utilities.Exceptions.RestrictedAccessException("Multi-write issue", ex)
        End Try
        'TL.LogMessage("MultiWrite - MultiThread", "ThreadEnd")
        TL.Enabled = False
        TL.Dispose()
        TL = Nothing

    End Sub

    Private Sub Compare(ByVal p_Section As String, ByVal p_Name As String, ByVal p_New As String, ByVal p_Orig As String)
        If p_New = p_Orig Then
            If p_New.Length > 200 Then p_New = p_New.Substring(1, 200) & "..."
            TL.LogMessage(p_Section, "Matched " & p_Name & " = " & p_New)
            NMatches += 1
        Else
            TL.LogMessageCrLf(p_Section, "***** NOT Matched " & p_Name & " #" & p_New & "#" & p_Orig & "#")
            NNonMatches += 1
        End If
    End Sub

    Private Sub CompareDouble(ByVal p_Section As String, ByVal p_Name As String, ByVal p_New As Double, ByVal p_Orig As Double, ByVal p_Tolerance As Double)
        If System.Math.Abs(p_New - p_Orig) < p_Tolerance Then
            TL.LogMessage(p_Section, "Matched " & p_Name & " = " & p_New)
            NMatches += 1
        Else
            TL.LogMessage(p_Section, "NOT Matched " & p_Name & " #" & p_New.ToString & "#" & p_Orig.ToString & "#")
            NNonMatches += 1
        End If
    End Sub

    Sub UtilTests()
        Dim t As Double
        Dim ts As String
        Const TestDate As Date = #6/1/2010 4:37:00 PM#
        Const TestJulianDate As Double = 2455551.0
        Dim i As Integer, Is64Bit As Boolean

        Try
            Is64Bit = (IntPtr.Size = 8) 'Create a simple variable to record whether or not we are 64bit
            Status("Running Utilities funcitonal tests")
            TL.LogMessage("UtilTests", "Creating ASCOM.Utilities.Util")
            sw.Reset() : sw.Start()
            AscomUtil = New ASCOM.Utilities.Util
            TL.LogMessage("UtilTests", "ASCOM.Utilities.Util Created OK in " & sw.ElapsedMilliseconds & " milliseconds")
            If Not Is64Bit Then
                TL.LogMessage("UtilTests", "Creating DriverHelper.Util")
                DrvHlpUtil = CreateObject("DriverHelper.Util")
                TL.LogMessage("UtilTests", "DriverHelper.Util Created OK")

                TL.LogMessage("UtilTests", "Creating DriverHelper2.Util")
                g_Util2 = CreateObject("DriverHelper2.Util")
                TL.LogMessage("UtilTests", "DriverHelper2.Util Created OK")
            Else
                TL.LogMessage("UtilTests", "Running 64bit so avoiding use of 32bit DriverHelper components")
            End If
            TL.BlankLine()

            Compare("UtilTests", "IsMinimumRequiredVersion 5.0", AscomUtil.IsMinimumRequiredVersion(5, 0).ToString, "True")
            Compare("UtilTests", "IsMinimumRequiredVersion 5.4", AscomUtil.IsMinimumRequiredVersion(5, 4).ToString, "True")
            Compare("UtilTests", "IsMinimumRequiredVersion 5.5", AscomUtil.IsMinimumRequiredVersion(5, 5).ToString, "True")
            Compare("UtilTests", "IsMinimumRequiredVersion 5.6", AscomUtil.IsMinimumRequiredVersion(5, 6).ToString, "True")
            Compare("UtilTests", "IsMinimumRequiredVersion 6.0", AscomUtil.IsMinimumRequiredVersion(6, 0).ToString, "True")
            Compare("UtilTests", "IsMinimumRequiredVersion 6.3", AscomUtil.IsMinimumRequiredVersion(6, 3).ToString, "False")
            TL.BlankLine()
            If Is64Bit Then ' Run tests just on the new 64bit component
                t = 30.123456789 : Compare("UtilTests", "DegreesToDM", AscomUtil.DegreesToDM(t, ":").ToString, "30:07'")
                t = 60.987654321 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t, ":", ":", "", 4).ToString, "60:59:15.5556")
                t = 50.123453456 : Compare("UtilTests", "DegreesToHM", AscomUtil.DegreesToHM(t).ToString, "03:20")
                t = 70.763245689 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t).ToString, "04:43:03")
                ts = "43:56:78.2567" : Compare("UtilTests", "DMSToDegrees", AscomUtil.DMSToDegrees(ts).ToString, "43.9550713055555")
                ts = "14:39:23" : Compare("UtilTests", "HMSToDegrees", AscomUtil.HMSToDegrees(ts).ToString, "219.845833333333")
                ts = "14:37:23" : Compare("UtilTests", "HMSToHours", AscomUtil.HMSToHours(ts).ToString, "14.6230555555556")
                t = 15.567234086 : Compare("UtilTests", "HoursToHM", AscomUtil.HoursToHM(t), "15:34")
                t = 9.4367290317 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t), "09:26:12")
                TL.BlankLine()

                Compare("UtilTests", "Platform Version", AscomUtil.PlatformVersion.ToString, ASCOMRegistryAccess.GetProfile("", "PlatformVersion"))
                Compare("UtilTests", "SerialTrace", AscomUtil.SerialTrace, (ASCOMRegistryAccess.GetProfile("", "SerTraceFile", "") <> ""))
                Compare("UtilTests", "Trace File", AscomUtil.SerialTraceFile, IIf(ASCOMRegistryAccess.GetProfile("", "SerTraceFile") = "", "C:\SerialTrace.txt", ASCOMRegistryAccess.GetProfile("", "SerTraceFile")))
                TL.BlankLine()

                Compare("UtilTests", "TimeZoneName", AscomUtil.TimeZoneName.ToString, GetTimeZoneName)
                CompareDouble("UtilTests", "TimeZoneOffset", AscomUtil.TimeZoneOffset, -CDbl(TimeZone.CurrentTimeZone.GetUtcOffset(Now).Hours), 0.017) '1 minute tolerance
                Compare("UtilTests", "UTCDate", AscomUtil.UTCDate.ToString, Date.UtcNow)
                CompareDouble("UtilTests", "Julian date", AscomUtil.JulianDate, Date.UtcNow.ToOADate() + 2415018.5, 0.00002) '1 second tolerance
                TL.BlankLine()

                Compare("UtilTests", "DateJulianToLocal", Format(AscomUtil.DateJulianToLocal(TestJulianDate), "dd MMM yyyy hh:mm:ss.ffff"), "20 Dec 2010 12:00:00.0000")
                Compare("UtilTests", "DateJulianToUTC", Format(AscomUtil.DateJulianToUTC(TestJulianDate), "dd MMM yyyy hh:mm:ss.ffff"), "20 Dec 2010 12:00:00.0000")
                Compare("UtilTests", "DateLocalToJulian", AscomUtil.DateLocalToJulian(TestDate), "2455349.19236111")
                Compare("UtilTests", "DateLocalToUTC", Format(AscomUtil.DateLocalToUTC(TestDate), "dd MMM yyyy hh:mm:ss.ffff"), "01 Jun 2010 04:37:00.0000")
                Compare("UtilTests", "DateUTCToJulian", AscomUtil.DateUTCToJulian(TestDate).ToString, "2455349.19236111")
                Compare("UtilTests", "DateUTCToLocal", Format(AscomUtil.DateUTCToLocal(TestDate), "dd MMM yyyy hh:mm:ss.ffff"), "01 Jun 2010 04:37:00.0000")
                TL.BlankLine()

                t = 43.123894628 : Compare("UtilTests", "DegreesToDM", AscomUtil.DegreesToDM(t), "43" & Chr(&HB0) & " 07'")
                t = 43.123894628 : Compare("UtilTests", "DegreesToDM", AscomUtil.DegreesToDM(t, "-"), "43-07'")
                t = 43.123894628 : Compare("UtilTests", "DegreesToDM", AscomUtil.DegreesToDM(t, "-", ";"), "43-07;")
                t = 43.123894628 : Compare("UtilTests", "DegreesToDM", AscomUtil.DegreesToDM(t, "-", ";", 3), "43-07.434;")
                TL.BlankLine()

                t = 43.123894628 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t), "43" & Chr(&HB0) & " 07' 26""")
                t = 43.123894628 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t, "-"), "43-07' 26""")
                t = 43.123894628 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t, "-", ";"), "43-07;26""")
                t = 43.123894628 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t, "-", ";", "#"), "43-07;26#")
                t = 43.123894628 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t, "-", ";", "#", 3), "43-07;26.021#")
                TL.BlankLine()

                t = 43.123894628 : Compare("UtilTests", "DegreesToHM", AscomUtil.DegreesToHM(t), "02:52")
                t = 43.123894628 : Compare("UtilTests", "DegreesToHM", AscomUtil.DegreesToHM(t, "-"), "02-52")
                t = 43.123894628 : Compare("UtilTests", "DegreesToHM", AscomUtil.DegreesToHM(t, "-", ";"), "02-52;")
                t = 43.123894628 : Compare("UtilTests", "DegreesToHM", AscomUtil.DegreesToHM(t, "-", ";", 3), "02-52.496;")
                TL.BlankLine()

                t = 43.123894628 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t), "02:52:30")
                t = 43.123894628 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t, "-"), "02-52:30")
                t = 43.123894628 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t, "-", ";"), "02-52;30")
                t = 43.123894628 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t, "-", ";", "#"), "02-52;30#")
                t = 43.123894628 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t, "-", ";", "#", 3), "02-52;29.735#")
                TL.BlankLine()

                t = 3.123894628 : Compare("UtilTests", "HoursToHM", AscomUtil.HoursToHM(t), "03:07")
                t = 3.123894628 : Compare("UtilTests", "HoursToHM", AscomUtil.HoursToHM(t, "-"), "03-07")
                t = 3.123894628 : Compare("UtilTests", "HoursToHM", AscomUtil.HoursToHM(t, "-", ";"), "03-07;")
                t = 3.123894628 : Compare("UtilTests", "HoursToHM", AscomUtil.HoursToHM(t, "-", ";", 3), "03-07.434;")
                TL.BlankLine()

                t = 3.123894628 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t), "03:07:26")
                t = 3.123894628 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t, "-"), "03-07:26")
                t = 3.123894628 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t, "-", ";"), "03-07;26")
                t = 3.123894628 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t, "-", ";", "#"), "03-07;26#")
                t = 3.123894628 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t, "-", ";", "#", 3), "03-07;26.021#")
            Else 'Run teststo compare original 32bit only and new 32/64bit capabale components
                t = 30.123456789 : Compare("UtilTests", "DegreesToDM", AscomUtil.DegreesToDM(t, ":").ToString, DrvHlpUtil.DegreesToDM(t, ":").ToString)
                t = 60.987654321 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t, ":", ":", "", 4).ToString, DrvHlpUtil.DegreesToDMS(t, ":", ":", "", 4).ToString)
                t = 50.123453456 : Compare("UtilTests", "DegreesToHM", AscomUtil.DegreesToHM(t).ToString, DrvHlpUtil.DegreesToHM(t).ToString)
                t = 70.763245689 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t).ToString, DrvHlpUtil.DegreesToHMS(t).ToString)
                ts = "43:56:78.2567" : Compare("UtilTests", "DMSToDegrees", AscomUtil.DMSToDegrees(ts).ToString, DrvHlpUtil.DMSToDegrees(ts).ToString)
                ts = "14:39:23" : Compare("UtilTests", "HMSToDegrees", AscomUtil.HMSToDegrees(ts).ToString, DrvHlpUtil.HMSToDegrees(ts))
                ts = "14:37:23" : Compare("UtilTests", "HMSToHours", AscomUtil.HMSToHours(ts).ToString, DrvHlpUtil.HMSToHours(ts))
                t = 15.567234086 : Compare("UtilTests", "HoursToHM", AscomUtil.HoursToHM(t), DrvHlpUtil.HoursToHM(t))
                t = 9.4367290317 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t), DrvHlpUtil.HoursToHMS(t))
                TL.BlankLine()

                Compare("UtilTests", "Platform Version", AscomUtil.PlatformVersion.ToString, g_Util2.PlatformVersion.ToString)
                Compare("UtilTests", "SerialTrace", AscomUtil.SerialTrace, g_Util2.SerialTrace)
                Compare("UtilTests", "Trace File", AscomUtil.SerialTraceFile, g_Util2.SerialTraceFile)
                TL.BlankLine()

                Compare("UtilTests", "TimeZoneName", AscomUtil.TimeZoneName.ToString, g_Util2.TimeZoneName.ToString)
                CompareDouble("UtilTests", "TimeZoneOffset", AscomUtil.TimeZoneOffset, g_Util2.TimeZoneOffset, 0.017) '1 minute tolerance
                Compare("UtilTests", "UTCDate", AscomUtil.UTCDate.ToString, g_Util2.UTCDate.ToString)
                CompareDouble("UtilTests", "Julian date", AscomUtil.JulianDate, g_Util2.JulianDate, 0.00002) '1 second tolerance
                TL.BlankLine()

                Compare("UtilTests", "DateJulianToLocal", Format(AscomUtil.DateJulianToLocal(TestJulianDate), "dd MMM yyyy hh:mm:ss.ffff"), Format(g_Util2.DateJulianToLocal(TestJulianDate), "dd MMM yyyy hh:mm:ss.ffff"))
                Compare("UtilTests", "DateJulianToUTC", Format(AscomUtil.DateJulianToUTC(TestJulianDate), "dd MMM yyyy hh:mm:ss.ffff"), Format(g_Util2.DateJulianToUTC(TestJulianDate), "dd MMM yyyy hh:mm:ss.ffff"))
                Compare("UtilTests", "DateLocalToJulian", AscomUtil.DateLocalToJulian(TestDate), g_Util2.DateLocalToJulian(TestDate))
                Compare("UtilTests", "DateLocalToUTC", Format(AscomUtil.DateLocalToUTC(TestDate), "dd MMM yyyy hh:mm:ss.ffff"), Format(g_Util2.DateLocalToUTC(TestDate), "dd MMM yyyy hh:mm:ss.ffff"))
                Compare("UtilTests", "DateUTCToJulian", AscomUtil.DateUTCToJulian(TestDate).ToString, g_Util2.DateUTCToJulian(TestDate).ToString)
                Compare("UtilTests", "DateUTCToLocal", Format(AscomUtil.DateUTCToLocal(TestDate), "dd MMM yyyy hh:mm:ss.ffff"), Format(g_Util2.DateUTCToLocal(TestDate), "dd MMM yyyy hh:mm:ss.ffff"))
                TL.BlankLine()

                t = 43.123894628 : Compare("UtilTests", "DegreesToDM", AscomUtil.DegreesToDM(t), DrvHlpUtil.DegreesToDM(t))
                t = 43.123894628 : Compare("UtilTests", "DegreesToDM", AscomUtil.DegreesToDM(t, "-"), DrvHlpUtil.DegreesToDM(t, "-"))
                t = 43.123894628 : Compare("UtilTests", "DegreesToDM", AscomUtil.DegreesToDM(t, "-", ";"), DrvHlpUtil.DegreesToDM(t, "-", ";"))
                t = 43.123894628 : Compare("UtilTests", "DegreesToDM", AscomUtil.DegreesToDM(t, "-", ";", 3), DrvHlpUtil.DegreesToDM(t, "-", ";", 3))
                TL.BlankLine()

                t = 43.123894628 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t), DrvHlpUtil.DegreesToDMS(t))
                t = 43.123894628 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t, "-"), DrvHlpUtil.DegreesToDMS(t, "-"))
                t = 43.123894628 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t, "-", ";"), DrvHlpUtil.DegreesToDMS(t, "-", ";"))
                t = 43.123894628 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t, "-", ";", "#"), DrvHlpUtil.DegreesToDMS(t, "-", ";", "#"))
                t = 43.123894628 : Compare("UtilTests", "DegreesToDMS", AscomUtil.DegreesToDMS(t, "-", ";", "#", 3), DrvHlpUtil.DegreesToDMS(t, "-", ";", "#", 3))
                TL.BlankLine()

                t = 43.123894628 : Compare("UtilTests", "DegreesToHM", AscomUtil.DegreesToHM(t), DrvHlpUtil.DegreesToHM(t))
                t = 43.123894628 : Compare("UtilTests", "DegreesToHM", AscomUtil.DegreesToHM(t, "-"), DrvHlpUtil.DegreesToHM(t, "-"))
                t = 43.123894628 : Compare("UtilTests", "DegreesToHM", AscomUtil.DegreesToHM(t, "-", ";"), DrvHlpUtil.DegreesToHM(t, "-", ";"))
                t = 43.123894628 : Compare("UtilTests", "DegreesToHM", AscomUtil.DegreesToHM(t, "-", ";", 3), DrvHlpUtil.DegreesToHM(t, "-", ";", 3))
                TL.BlankLine()

                t = 43.123894628 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t), DrvHlpUtil.DegreesToHMS(t))
                t = 43.123894628 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t, "-"), DrvHlpUtil.DegreesToHMS(t, "-"))
                t = 43.123894628 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t, "-", ";"), DrvHlpUtil.DegreesToHMS(t, "-", ";"))
                t = 43.123894628 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t, "-", ";", "#"), DrvHlpUtil.DegreesToHMS(t, "-", ";", "#"))
                t = 43.123894628 : Compare("UtilTests", "DegreesToHMS", AscomUtil.DegreesToHMS(t, "-", ";", "#", 3), DrvHlpUtil.DegreesToHMS(t, "-", ";", "#", 3))
                TL.BlankLine()

                t = 3.123894628 : Compare("UtilTests", "HoursToHM", AscomUtil.HoursToHM(t), DrvHlpUtil.HoursToHM(t))
                t = 3.123894628 : Compare("UtilTests", "HoursToHM", AscomUtil.HoursToHM(t, "-"), DrvHlpUtil.HoursToHM(t, "-"))
                t = 3.123894628 : Compare("UtilTests", "HoursToHM", AscomUtil.HoursToHM(t, "-", ";"), DrvHlpUtil.HoursToHM(t, "-", ";"))
                t = 3.123894628 : Compare("UtilTests", "HoursToHM", AscomUtil.HoursToHM(t, "-", ";", 3), DrvHlpUtil.HoursToHM(t, "-", ";", 3))
                TL.BlankLine()

                t = 3.123894628 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t), DrvHlpUtil.HoursToHMS(t))
                t = 3.123894628 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t, "-"), DrvHlpUtil.HoursToHMS(t, "-"))
                t = 3.123894628 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t, "-", ";"), DrvHlpUtil.HoursToHMS(t, "-", ";"))
                t = 3.123894628 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t, "-", ";", "#"), DrvHlpUtil.HoursToHMS(t, "-", ";", "#"))
                t = 3.123894628 : Compare("UtilTests", "HoursToHMS", AscomUtil.HoursToHMS(t, "-", ";", "#", 3), DrvHlpUtil.HoursToHMS(t, "-", ";", "#", 3))
            End If
            TL.BlankLine()
            Status("Running Utilities timing tests")
            TL.LogMessage("UtilTests", "Timing tests")
            For i = 0 To 5
                TimingTest(i, Is64Bit)
            Next

            For i = 10 To 50 Step 10
                TimingTest(i, Is64Bit)
            Next

            TimingTest(100, Is64Bit)
            TimingTest(500, Is64Bit)
            TimingTest(1000, Is64Bit)
            TimingTest(2000, Is64Bit)
            TL.BlankLine()

            Try
                AscomUtil.Dispose()
                TL.LogMessage("UtilTests", "ASCOM.Utilities.Dispose, Disposed OK")
            Catch ex As Exception
                TL.LogMessage("UtilTests", "ASCOM.Utilities.Dispose Exception: ", ex.ToString)
                NExceptions += 1
            End Try
            If Not Is64Bit Then
                Try
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(DrvHlpUtil)
                    TL.LogMessage("UtilTests", "Helper Util.Release OK")
                Catch ex As Exception
                    TL.LogMessage("UtilTests", "Helper Util.Release Exception: ", ex.ToString)
                    NExceptions += 1
                End Try
            End If
            AscomUtil = Nothing
            DrvHlpUtil = Nothing
            TL.LogMessage("UtilTests", "Finished")
            TL.BlankLine()

        Catch ex As Exception
            TL.LogMessageCrLf("UtilTests", "Exception: " & ex.ToString)
            NExceptions += 1
        End Try

    End Sub

    Private Function GetTimeZoneName() As String
        If TimeZone.CurrentTimeZone.IsDaylightSavingTime(Now) Then
            Return TimeZone.CurrentTimeZone.DaylightName
        Else
            Return TimeZone.CurrentTimeZone.StandardName
        End If
    End Function

    Sub TimingTest(ByVal p_NumberOfMilliSeconds As Integer, ByVal Is64Bit As Boolean)
        Action("TimingTest " & p_NumberOfMilliSeconds & "ms")
        s1 = Stopwatch.StartNew 'Test time using new ASCOM component
        AscomUtil.WaitForMilliseconds(p_NumberOfMilliSeconds)
        s1.Stop()
        Application.DoEvents()

        If Is64Bit Then
            TL.LogMessage("UtilTests - Timing", "Timer test (64bit): " & p_NumberOfMilliSeconds.ToString & " milliseconds - ASCOM.Utillities.WaitForMilliSeconds: " & Format(s1.ElapsedTicks * 1000.0 / Stopwatch.Frequency, "0.00") & "ms")
        Else
            System.Threading.Thread.Sleep(100)
            s2 = Stopwatch.StartNew 'Test time using original Platform 5 component
            DrvHlpUtil.WaitForMilliseconds(p_NumberOfMilliSeconds)
            s2.Stop()
            TL.LogMessage("UtilTests - Timing", "Timer test: " & p_NumberOfMilliSeconds.ToString & " milliseconds - ASCOM.Utillities.WaitForMilliSeconds: " & Format(s1.ElapsedTicks * 1000.0 / Stopwatch.Frequency, "0.00") & "ms DriverHelper.Util.WaitForMilliSeconds: " & Format(s2.ElapsedTicks * 1000.0 / Stopwatch.Frequency, "0.00") & "ms")
        End If
        Application.DoEvents()
    End Sub

    Sub ScanEventLog()
        Dim ELog As EventLog
        Dim Entries As EventLogEntryCollection
        Dim EventLogs() As EventLog
        Try
            TL.LogMessage("ScanEventLog", "Start")
            EventLogs = EventLog.GetEventLogs()
            For Each EventLog As EventLog In EventLogs
                Try : TL.LogMessage("ScanEventLog", "Found log: " & EventLog.LogDisplayName) : Catch : End Try
            Next
            TL.BlankLine()

            TL.LogMessage("ScanEventLog", "ASCOM Log entries")
            ELog = New EventLog(EVENTLOG_NAME, ".", EVENT_SOURCE)
            Entries = ELog.Entries
            For Each Entry As EventLogEntry In Entries
                TL.LogMessageCrLf("ScanEventLog", Entry.TimeGenerated & " " & Entry.EntryType.ToString & " " & Entry.UserName & " " & Entry.Source & " " & Entry.Message)
            Next
            TL.LogMessage("ScanEventLog", "ASCOM Log entries complete")
            TL.BlankLine()
        Catch ex As Exception
            TL.LogMessageCrLf("ScanEventLog", "Exception: " & ex.ToString)
        End Try
    End Sub

    Sub ScanRegistrySecurity()
        Try
            Status("Scanning Registry Security")
            TL.LogMessage("RegistrySecurity", "Start")

            'Dim RA As New ASCOM.Utilities.RegistryAccess
            'ReadRegistryRights(RA.OpenSubKey(Registry.LocalMachine, REGISTRY_ROOT_KEY_NAME, True, ASCOM.Utilities.RegistryAccess.RegWow64Options.KEY_WOW64_32KEY), ASCOM_ROOT_KEY)

            ReadRegistryRights(Registry.CurrentUser, "")
            ReadRegistryRights(Registry.CurrentUser, "SOFTWARE\ASCOM")
            ReadRegistryRights(Registry.ClassesRoot, "")
            ReadRegistryRights(Registry.ClassesRoot, "DriverHelper.Util")

            ReadRegistryRights(Registry.LocalMachine, "")
            ReadRegistryRights(Registry.LocalMachine, "SOFTWARE")

            If IntPtr.Size = 8 Then '64bit OS so look in Wow64node
                ReadRegistryRights(Registry.LocalMachine, "SOFTWARE\Wow6432Node\ASCOM")
            Else '32 bit OS
                ReadRegistryRights(Registry.LocalMachine, "SOFTWARE\ASCOM")
            End If

            TL.BlankLine()
        Catch ex As Exception
            TL.LogMessage("RegistrySecurity", "Exception: " & ex.ToString)
        End Try
    End Sub

    Private Sub ReadRegistryRights(ByVal key As RegistryKey, ByVal SubKey As String)
        Dim sec As System.Security.AccessControl.RegistrySecurity
        Dim SKey As RegistryKey

        Try
            TL.LogMessage("RegistrySecurity", IIf(SubKey = "", key.Name.ToString, key.Name.ToString & "\" & SubKey))
            If (SubKey = "") Or (SubKey = ASCOM_ROOT_KEY) Then
                SKey = key
            Else
                SKey = key.OpenSubKey(SubKey)
            End If

            sec = SKey.GetAccessControl() 'System.Security.AccessControl.AccessControlSections.All)

            For Each RegRule As RegistryAccessRule In sec.GetAccessRules(True, True, GetType(NTAccount)) 'Iterate over the rule set and list them
                TL.LogMessage("RegistrySecurity", RegRule.AccessControlType.ToString() & " " & _
                                                  RegRule.IdentityReference.ToString() & " " & _
                                                  RegRule.RegistryRights.ToString() & " / " & _
                                                  IIf(RegRule.IsInherited.ToString(), "Inherited", "NotInherited") & " / " & _
                                                  RegRule.InheritanceFlags.ToString() & " / " & _
                                                  RegRule.PropagationFlags.ToString())
            Next
        Catch ex As Exception
            TL.LogMessage("ReadRegistryRights", ex.ToString)
        End Try
        TL.BlankLine()
    End Sub

    Sub ScanRegistry()
        Dim Key As RegistryKey
        Status("Scanning Registry")
        'TL.LogMessage("ScanRegistry", "Start")
        If OSBits() = Bitness.Bits64 Then
            Try
                'List the 32bit registry
                TL.LogMessage("ScanRegistry", "Machine Profile Root (64bit OS - 32bit Registry)")
                Key = ASCOMRegistryAccess.OpenSubKey(Registry.LocalMachine, REGISTRY_ROOT_KEY_NAME, False, RegistryAccess.RegWow64Options.KEY_WOW64_32KEY)
                RecursionLevel = -1
                RecurseRegistry(Key)
            Catch ex As Exception
                TL.LogMessageCrLf("ScanRegistry", "Exception: " & ex.ToString)
            End Try
            TL.BlankLine()

            Try
                'List the 64bit registry
                TL.LogMessage("ScanRegistry", "Machine Profile Root (64bit OS - 64bit Registry)")
                Key = ASCOMRegistryAccess.OpenSubKey(Registry.LocalMachine, REGISTRY_ROOT_KEY_NAME, False, RegistryAccess.RegWow64Options.KEY_WOW64_64KEY)
                RecursionLevel = -1
                RecurseRegistry(Key)
            Catch ex As ProfilePersistenceException
                If InStr(ex.Message, "0x2") > 0 Then
                    TL.LogMessage("ScanRegistry", "Key not found")
                Else
                    TL.LogMessageCrLf("ScanRegistry", "ProfilePersistenceException: " & ex.ToString)
                End If
            Catch ex As Exception
                TL.LogMessageCrLf("ScanRegistry", "Exception: " & ex.ToString)
            End Try
        Else '32 bit OS
            Try
                'List the registry (only one view on a 32bit machine)
                TL.LogMessage("ScanRegistry", "Machine Profile Root (32bit OS)")
                Key = Registry.LocalMachine.OpenSubKey(REGISTRY_ROOT_KEY_NAME)
                RecursionLevel = -1
                RecurseRegistry(Key)
            Catch ex As Exception
                TL.LogMessageCrLf("ScanRegistry", "Exception: " & ex.ToString)
            End Try
        End If
        TL.BlankLine()
        TL.BlankLine()

        Try
            'List the user registry
            TL.LogMessage("ScanRegistry", "User Profile Root")
            Key = Registry.CurrentUser.OpenSubKey(REGISTRY_ROOT_KEY_NAME)
            RecursionLevel = -1
            RecurseRegistry(Key)
        Catch ex As Exception
            TL.LogMessageCrLf("ScanRegistry", "Exception: " & ex.ToString)
        End Try
        TL.BlankLine()
        TL.BlankLine()
    End Sub

    Sub RecurseRegistry(ByVal Key As RegistryKey)
        Dim ValueNames(), SubKeys(), DisplayName As String
        Try
            RecursionLevel += 1
            ValueNames = Key.GetValueNames
            For Each ValueName As String In ValueNames
                If ValueName = "" Then
                    DisplayName = "*** Default Value ***"
                Else
                    DisplayName = ValueName
                End If
                TL.LogMessage("Registry Profile", Space(RecursionLevel * 2) & "   " & DisplayName & " = " & Key.GetValue(ValueName))
            Next
        Catch ex As Exception
            TL.LogMessageCrLf("RecurseRegistry 1", "Exception: " & ex.ToString)
        End Try
        Try
            SubKeys = Key.GetSubKeyNames
            For Each SubKey As String In SubKeys
                TL.BlankLine()
                TL.LogMessage("Registry Profile Key", Space(RecursionLevel * 2) & SubKey)
                RecurseRegistry(Key.OpenSubKey(SubKey))
            Next
        Catch ex As Exception
            TL.LogMessageCrLf("RecurseRegistry 2", "Exception: " & ex.ToString)
        End Try
        RecursionLevel -= 1
    End Sub

    Sub ScanDrives()
        Dim Drives() As String, Drive As DriveInfo
        Try
            Status("Scanning drives")
            Drives = Directory.GetLogicalDrives
            For Each DriveName As String In Drives
                Drive = New DriveInfo(DriveName)
                If Drive.IsReady Then
                    TL.LogMessage("Drives", "Drive " & DriveName & " available space: " & Format(Drive.AvailableFreeSpace, "#,0.") & " bytes, capacity: " & Format(Drive.TotalSize, "#,0.") & " bytes, format: " & Drive.DriveFormat)
                Else
                    TL.LogMessage("Drives", "Skipping drive " & DriveName & " because it is not ready")
                End If
            Next
            TL.LogMessage("", "")
        Catch ex As Exception
            TL.LogMessageCrLf("ScanDrives", "Exception: " & ex.ToString)
        End Try
    End Sub

    Sub ScanProgramFiles()
        Dim BaseDir As String
        Dim PathShell As New System.Text.StringBuilder(260)
        Try
            BaseDir = System.Environment.GetFolderPath(SpecialFolder.ProgramFiles)

            Status("Scanning ProgramFiles Directory for Helper DLLs")
            TL.LogMessage("ProgramFiles Scan", "Searching for Helper.DLL etc.")

            RecurseProgramFiles(BaseDir) ' This is the 32bit path on a 32bit OS and 64bit path on a 64bit OS

            TL.BlankLine()

            'If on a 64bit OS, now scan the 32bit path

            If IntPtr.Size = 8 Then 'We are on a 64bit OS
                BaseDir = System.Environment.GetFolderPath(SpecialFolder.ProgramFiles)
                BaseDir = SHGetSpecialFolderPath(IntPtr.Zero, PathShell, CSIDL_PROGRAM_FILESX86, False)

                Status("Scanning ProgramFiles(x86) Directory for Helper DLLs")
                TL.LogMessage("ProgramFiles(x86) Scan", "Searching for Helper.DLL etc. on 32bit path")

                RecurseProgramFiles(PathShell.ToString) ' This is the 32bit path on a 32bit OS and 64bit path on a 64bit OS

                TL.BlankLine()
            End If
        Catch ex As Exception
            TL.LogMessageCrLf("ScanProgramFiles", "Exception: " & ex.ToString)
        End Try
    End Sub

    Sub RecurseProgramFiles(ByVal Folder As String)
        Dim Files(), Directories() As String

        'TL.LogMessage("Folder", Folder)
        'Process files in this directory
        Try
            Action(Microsoft.VisualBasic.Left(Folder, 70))
            Files = Directory.GetFiles(Folder)
            For Each MyFile As String In Files
                If MyFile.ToUpper.Contains("\HELPER.DLL") Then
                    'TL.LogMessage("Helper.DLL", MyFile)
                    FileDetails(Folder & "\", "Helper.dll")
                End If
                If MyFile.ToUpper.Contains("\HELPER2.DLL") Then
                    'TL.LogMessage("Helper2.DLL", MyFile)
                    FileDetails(Folder & "\", "Helper2.dll")
                End If
            Next
        Catch ex As Exception
            TL.LogMessageCrLf("RecurseProgramFiles 1", "Exception: " & ex.ToString)
        End Try

        Try
            Directories = Directory.GetDirectories(Folder)
            For Each Directory As String In Directories
                'TL.LogMessage("Directory", Directory)
                RecurseProgramFiles(Directory)
            Next
            Action("")
        Catch ex As Exception
            TL.LogMessageCrLf("RecurseProgramFiles 2", "Exception: " & ex.ToString)
        End Try
    End Sub

    Sub ScanProfile55Files()
        Dim ProfileStore As AllUsersFileSystemProvider, Files() As String
        Try
            Status("Scanning Profile 5.5 Files")
            TL.LogMessage("Scanning Profile 5.5", "")

            ProfileStore = New AllUsersFileSystemProvider
            Files = Directory.GetFiles(ProfileStore.BasePath) 'Check that directory exists

            RecurseProfile55Files(ProfileStore.BasePath)

            TL.BlankLine()
        Catch ex As DirectoryNotFoundException
            TL.LogMessage("ScanProfileFiles", "Profile 5.5 filestore not present")
            TL.BlankLine()
        Catch ex As Exception
            TL.LogMessageCrLf("ScanProfileFiles", "Exception: " & ex.ToString)
        End Try
    End Sub

    Sub RecurseProfile55Files(ByVal Folder As String)
        Dim Files(), Directories() As String

        Try
            'TL.LogMessage("Folder", Folder)
            'Process files in this directory
            Files = Directory.GetFiles(Folder)
            For Each MyFile As String In Files
                TL.LogMessage("File", MyFile)
                Using sr As StreamReader = File.OpenText(MyFile)
                    Dim input As String
                    input = sr.ReadLine()
                    While Not input Is Nothing
                        TL.LogMessage("", "  " & input)
                        input = sr.ReadLine()
                    End While
                    Console.WriteLine("The end of the stream has been reached.")
                End Using

            Next
        Catch ex As Exception
            TL.LogMessageCrLf("RecurseProfileFiles 1", "Exception: " & ex.ToString)
        End Try

        Try
            Directories = Directory.GetDirectories(Folder)
            For Each Directory As String In Directories
                TL.LogMessage("Directory", Directory)
                RecurseProfile55Files(Directory)
            Next
        Catch ex As Exception
            TL.LogMessageCrLf("RecurseProfileFiles 2", "Exception: " & ex.ToString)
        End Try

    End Sub

    Sub ScanFrameworks()
        Dim FrameworkPath, FrameworkFile, FrameworkDirectories() As String
        Dim PathShell As New System.Text.StringBuilder(260)

        Try
            Status("Scanning Frameworks")

            SHGetSpecialFolderPath(IntPtr.Zero, PathShell, CSIDL_WINDOWS, False)
            FrameworkPath = PathShell.ToString & "\Microsoft.NET\Framework"

            FrameworkDirectories = Directory.GetDirectories(FrameworkPath)
            For Each Directory As String In FrameworkDirectories
                FrameworkFile = Directory & "\mscorlib.dll"
                Dim FVInfo As FileVersionInfo, FInfo As FileInfo
                If File.Exists(FrameworkFile) Then

                    FVInfo = FileVersionInfo.GetVersionInfo(FrameworkFile)
                    FInfo = Microsoft.VisualBasic.FileIO.FileSystem.GetFileInfo(FrameworkFile)

                    TL.LogMessage("Frameworks", Directory.ToString & " - Version: " & FVInfo.FileMajorPart & "." & FVInfo.FileMinorPart & " " & FVInfo.FileBuildPart & " " & FVInfo.FilePrivatePart)

                Else
                    TL.LogMessage("Frameworks", Directory.ToString)
                End If
            Next
            TL.BlankLine()
        Catch ex As Exception
            TL.LogMessageCrLf("Frameworks", "Exception: " & ex.ToString)
        End Try
    End Sub

    Sub ScanLogs()
        Const NumLine As Integer = 30 'Number of lines to read from file to see if it is an ASCOM log

        Dim TempFiles(NumLine + 1) As String
        Dim SR As StreamReader = Nothing
        Dim Lines(30) As String, LineCount As Integer = 0
        Dim ASCOMFile As Boolean

        Try
            Status("Scanning setup logs")
            TL.LogMessage("SetupFile", "Starting scan")
            'Get an array of setup filenames from the Temp directory
            TempFiles = Directory.GetFiles(Path.GetFullPath(GetEnvironmentVariable("Temp")), "Setup Log*.txt", SearchOption.TopDirectoryOnly)
            For Each TempFile As String In TempFiles 'Iterate over results
                Try
                    TL.LogMessage("SetupFile", TempFile)
                    SR = File.OpenText(TempFile)

                    'Search for word ASCOM in first part of file
                    ASCOMFile = False 'Initialise found flag
                    LineCount = 0
                    Array.Clear(Lines, 1, NumLine) 'Clear out the array ready for next run
                    Do Until (LineCount = NumLine) Or SR.EndOfStream
                        LineCount += 1
                        Lines(LineCount) = SR.ReadLine
                        If InStr(Lines(LineCount).ToUpper, "ASCOM") > 0 Then ASCOMFile = True
                        If InStr(Lines(LineCount).ToUpper, "CONFORM") > 0 Then ASCOMFile = True
                    Loop

                    If ASCOMFile Then 'This is an ASCOM setup so list it

                        For i = 1 To NumLine 'Include the lines read earlier
                            TL.LogMessage("SetupFile", Lines(i))
                        Next

                        Do Until SR.EndOfStream 'include the rest of the file
                            TL.LogMessage("SetupFile", SR.ReadLine())
                        Loop
                    End If
                    TL.LogMessage("", "")
                    SR.Close()
                    SR.Dispose()
                    SR = Nothing
                Catch ex1 As Exception
                    TL.LogMessageCrLf("SetupFile", "Exception 1: " & ex1.ToString)
                    If Not (SR Is Nothing) Then 'Clean up streamreader
                        SR.Close()
                        SR.Dispose()
                        SR = Nothing
                    End If
                End Try
            Next
            TL.BlankLine()
            TL.LogMessage("SetupFile", "Completed scan")
            TL.BlankLine()
        Catch ex2 As Exception
            TL.LogMessageCrLf("SetupFile", "Exception 2: " & ex2.ToString)
        End Try
    End Sub

    Sub ScanCOMRegistration()
        Try
            Status("Scanning Registry")
            TL.LogMessage("COMRegistration", "") 'Report COM registation

            'Original Platform 5 helpers
            GetCOMRegistration("DriverHelper.Chooser")
            GetCOMRegistration("DriverHelper.Profile")
            GetCOMRegistration("DriverHelper.Serial")
            GetCOMRegistration("DriverHelper.Timer")
            GetCOMRegistration("DriverHelper.Util")
            GetCOMRegistration("DriverHelper2.Util")

            'Platform 5 helper support components
            GetCOMRegistration("DriverHelper.ChooserSupport")
            GetCOMRegistration("DriverHelper.ProfileAccess")
            GetCOMRegistration("DriverHelper.SerialSupport")

            'Utlities
            GetCOMRegistration("ASCOM.Utilities.ASCOMProfile")
            GetCOMRegistration("ASCOM.Utilities.Chooser")
            GetCOMRegistration("ASCOM.Utilities.KeyValuePair")
            GetCOMRegistration("ASCOM.Utilities.Profile")
            GetCOMRegistration("ASCOM.Utilities.Serial")
            GetCOMRegistration("ASCOM.Utilities.Timer")
            GetCOMRegistration("ASCOM.Utilities.TraceLogger")
            GetCOMRegistration("ASCOM.Utilities.Util")

            'Astrometry
            GetCOMRegistration("ASCOM.Astrometry.Kepler.Ephemeris")
            GetCOMRegistration("ASCOM.Astrometry.NOVAS.NOVAS2COM")
            GetCOMRegistration("ASCOM.Astrometry.NOVAS.NOVAS3")
            GetCOMRegistration("ASCOM.Astrometry.NOVASCOM.Earth")
            GetCOMRegistration("ASCOM.Astrometry.NOVASCOM.Planet")
            GetCOMRegistration("ASCOM.Astrometry.NOVASCOM.PositionVector")
            GetCOMRegistration("ASCOM.Astrometry.NOVASCOM.Site")
            GetCOMRegistration("ASCOM.Astrometry.NOVASCOM.Star")
            GetCOMRegistration("ASCOM.Astrometry.NOVASCOM.VelocityVector")
            GetCOMRegistration("ASCOM.Astrometry.Transform.Transform")

            'New Platform 6 Simulators 
            GetCOMRegistration("ASCOM.Simulator.Camera") 'If it exists
            GetCOMRegistration("ASCOM.FilterWheelSim.FilterWheel")
            GetCOMRegistration("ASCOM.Simulator.Focuser")
            GetCOMRegistration("ASCOM.Simulator.Rotator")
            GetCOMRegistration("ASCOM.Simulator.SafetyMonitor")
            GetCOMRegistration("ASCOM.Simulator.SetupDialogForm")
            GetCOMRegistration("ASCOM.Simulator.Switch")
            GetCOMRegistration("ASCOM.Simulator.Telescope")

            'Original Platform 5 simulators if present
            GetCOMRegistration("ScopeSim.Telescope")
            GetCOMRegistration("FocusSim.Focuser")
            GetCOMRegistration("CCDSimulator.Camera")
            GetCOMRegistration("DomeSim.Dome")
            GetCOMRegistration("ASCOMDome.Dome")
            GetCOMRegistration("ASCOMDome.Rate")
            GetCOMRegistration("ASCOMDome.Telescope")
            GetCOMRegistration("POTH.Telescope")
            GetCOMRegistration("POTH.Dome")
            GetCOMRegistration("POTH.Focuser")
            GetCOMRegistration("Pipe.Telescope")
            GetCOMRegistration("Pipe.Dome")
            GetCOMRegistration("Pipe.Focuser")
            GetCOMRegistration("Hub.Telescope")
            GetCOMRegistration("Hub.Dome")
            GetCOMRegistration("Hub.Focuser")

            'Exceptions
            GetCOMRegistration("ASCOM.DriverException")
            GetCOMRegistration("ASCOM.InvalidOperationException")
            GetCOMRegistration("ASCOM.InvalidValueException")
            GetCOMRegistration("ASCOM.MethodNotImplementedException")
            GetCOMRegistration("ASCOM.NotConnectedException")
            GetCOMRegistration("ASCOM.NotImplementedException")
            GetCOMRegistration("ASCOM.ParkedException")
            GetCOMRegistration("ASCOM.PropertyNotImplementedException")
            GetCOMRegistration("ASCOM.SlavedException")
            GetCOMRegistration("ASCOM.ValueNotSetException")

            TL.LogMessage("", "")
        Catch ex As Exception
            TL.LogMessageCrLf("ScanCOMRegistration", "Exception: " & ex.ToString)
        End Try
    End Sub

    Sub ScanGac()
        Dim ae As IAssemblyEnum
        Dim an As IAssemblyName = Nothing
        Dim name As AssemblyName
        Dim ass As Assembly
        Try
            Status("Scanning Assemblies")

            TL.LogMessage("Assemblies", "Assemblies registered in the GAC")
            ae = AssemblyCache.CreateGACEnum ' Get an enumerator for the GAC assemblies

            Do While (AssemblyCache.GetNextAssembly(ae, an) = 0) 'Enumerate the assemblies
                Try
                    name = GetAssemblyName(an) 'Convert the fusion representation to a standard AssemblyName
                    If InStr(name.FullName, "ASCOM") > 0 Then 'Extra information for ASCOM files
                        TL.LogMessage("Assemblies", name.Name)
                        ass = Assembly.Load(name.FullName)
                        AssemblyInfo(TL, name.Name, ass) ' Get file version and other information
                    Else
                        TL.LogMessage("Assemblies", name.FullName)
                    End If
                Catch ex As Exception
                    TL.LogMessageCrLf("Assemblies", "Exception: " & ex.ToString)
                End Try
            Loop
            TL.LogMessage("", "")
        Catch ex As Exception
            TL.LogMessageCrLf("ScanGac", "Exception: " & ex.ToString)
        End Try
    End Sub

    Private Function GetAssemblyName(ByVal nameRef As IAssemblyName) As AssemblyName
        Dim AssName As New AssemblyName()
        Try
            AssName.Name = AssemblyCache.GetName(nameRef)
            AssName.Version = AssemblyCache.GetVersion(nameRef)
            AssName.CultureInfo = AssemblyCache.GetCulture(nameRef)
            AssName.SetPublicKeyToken(AssemblyCache.GetPublicKeyToken(nameRef))
        Catch ex As Exception
            TL.LogMessageCrLf("GetAssemblyName", "Exception: " & ex.ToString)
        End Try
        Return AssName
    End Function

    Sub ScanFiles(ByVal ASCOMPath As String)
        Dim ASCOMPathPlatformV6, ASCOMPathInt, ASCOMPathPlatformInternal, ASCOMPathPlatformV55, ASCOMPathAstrometry As String

        Try
            Status("Scanning Files")

            ASCOMPathInt = ASCOMPath & "Interface\" 'Create folder paths
            ASCOMPathPlatformV6 = ASCOMPath & "Platform\V6\"
            ASCOMPathPlatformInternal = ASCOMPath & "Platform\Internal\"
            ASCOMPathPlatformV55 = ASCOMPath & "Platform\v5.5\"
            ASCOMPathAstrometry = ASCOMPath & "Astrometry\"

            'ASCOM Root files
            FileDetails(ASCOMPath, "Helper.dll") 'Report on files
            FileDetails(ASCOMPath, "Helper2.dll")

            'ASCOM\Astrometry
            FileDetails(ASCOMPathAstrometry, "NOVAS3.dll")
            FileDetails(ASCOMPathAstrometry, "NOVAS3.pdb")
            FileDetails(ASCOMPathAstrometry, "NOVAS3-64.dll")
            FileDetails(ASCOMPathAstrometry, "NOVAS3-64.pdb")
            FileDetails(ASCOMPathAstrometry, "NOVAS-C.dll")
            FileDetails(ASCOMPathAstrometry, "NOVAS-C.pdb")
            FileDetails(ASCOMPathAstrometry, "NOVAS-C64.dll")
            FileDetails(ASCOMPathAstrometry, "NOVAS-C64.pdb")

            'ASCOM\Interfaces
            FileDetails(ASCOMPathInt, "Helper.tlb")
            FileDetails(ASCOMPathInt, "Helper2.tlb")
            FileDetails(ASCOMPathInt, "ASCOMMasterInterfaces.tlb")
            FileDetails(ASCOMPathInt, "IObjectSafety.tlb")

            'ASCOM\Platform\Internal
            FileDetails(ASCOMPathPlatformInternal, "ASCOM.Internal.GACInstall.exe")
            FileDetails(ASCOMPathPlatformInternal, "MigrateProfile.exe")
            FileDetails(ASCOMPathPlatformInternal, "RegTlb.exe")

            'ASCOM\Platform\v5.5
            FileDetails(ASCOMPathPlatformV55, "policy.1.0.ASCOM.DriverAccess.dll")
            FileDetails(ASCOMPathPlatformV55, "policy.5.5.ASCOM.Astrometry.dll")
            FileDetails(ASCOMPathPlatformV55, "policy.5.5.ASCOM.Utilities.dll")
            FileDetails(ASCOMPathPlatformV55, "ASCOM.Astrometry.pdb")
            FileDetails(ASCOMPathPlatformV55, "ASCOM.Attributes.pdb")
            FileDetails(ASCOMPathPlatformV55, "ASCOM.DriverAccess.pdb")
            FileDetails(ASCOMPathPlatformV55, "ASCOM.Utilities.pdb")

            'ASCOM\Platform\v6
            FileDetails(ASCOMPathPlatformV6, "ASCOM.Astrometry.dll")
            FileDetails(ASCOMPathPlatformV6, "ASCOM.Exceptions.dll")
            FileDetails(ASCOMPathPlatformV6, "ASCOM.Utilities.dll")

        Catch ex As Exception
            TL.LogMessageCrLf("ScanFiles", "Exception: " & ex.ToString)
        End Try
        TL.BlankLine()

    End Sub

    Sub FileDetails(ByVal FPath As String, ByVal FName As String)
        Dim FullPath As String
        Dim Att As FileAttributes, FVInfo As FileVersionInfo, FInfo As FileInfo
        Dim Ass As Assembly, AssVer As String

        Try
            FullPath = FPath & FName 'Create full filename from path and simple filename
            If File.Exists(FullPath) Then
                TL.LogMessage("FileDetails", FullPath)
                'Try to get assembly version info if present
                Try
                    Ass = Assembly.ReflectionOnlyLoadFrom(FullPath)
                    AssVer = Ass.FullName
                Catch ex As Exception
                    AssVer = "Not an assembly"
                End Try

                TL.LogMessage("FileDetails", "   Assembly Version: " & AssVer)

                FVInfo = FileVersionInfo.GetVersionInfo(FullPath)
                FInfo = Microsoft.VisualBasic.FileIO.FileSystem.GetFileInfo(FullPath)

                TL.LogMessage("FileDetails", "   File Version:     " & FVInfo.FileMajorPart & "." & FVInfo.FileMinorPart & "." & FVInfo.FileBuildPart & "." & FVInfo.FilePrivatePart)
                TL.LogMessage("FileDetails", "   Product Version:  " & FVInfo.ProductMajorPart & "." & FVInfo.ProductMinorPart & "." & FVInfo.ProductBuildPart & "." & FVInfo.ProductPrivatePart)

                TL.LogMessage("FileDetails", "   Description:      " & FVInfo.FileDescription)
                TL.LogMessage("FileDetails", "   Company Name:     " & FVInfo.CompanyName)

                TL.LogMessage("FileDetails", "   Last Write Time:  " & File.GetLastWriteTime(FullPath))
                TL.LogMessage("FileDetails", "   Creation Time:    " & File.GetCreationTime(FullPath))

                TL.LogMessage("FileDetails", "   File Length:      " & Format(FInfo.Length, "#,0."))

                Att = File.GetAttributes(FullPath)
                TL.LogMessage("FileDetails", "   Attributes:       " & Att.ToString())

            Else
                TL.LogMessage("FileDetails", "   ### Unable to find file: " & FullPath)
            End If
        Catch ex As Exception
            TL.LogMessageCrLf("FileDetails", "### Exception: " & ex.ToString)
        End Try

        TL.LogMessage("", "")
    End Sub

    Sub GetCOMRegistration(ByVal ProgID As String)
        Dim RKey As RegistryKey
        Try
            TL.LogMessage("ProgID", ProgID)
            RKey = Registry.ClassesRoot.OpenSubKey(ProgID)
            If Not (RKey Is Nothing) Then ' Registry key exists so process it
                ProcessSubKey(RKey, 1, "None")
                RKey.Close()
                TL.LogMessage("Finished", "")
            Else
                TL.LogMessage("Finished", "*** ProgID " & ProgID & " not found")
            End If
        Catch ex As Exception
            TL.LogMessageCrLf("Exception", ex.ToString)
        End Try
        TL.LogMessage("", "")
    End Sub

    Sub ProcessSubKey(ByVal p_Key As RegistryKey, ByVal p_Depth As Integer, ByVal p_Container As String)
        Dim ValueNames(), SubKeys() As String
        Dim RKey As RegistryKey
        Dim Container As String
        'TL.LogMessage("Start of ProcessSubKey", p_Container & " " & p_Depth)

        If p_Depth > 12 Then
            TL.LogMessage("RecursionTrap", "Recursion depth has exceeded 12 so terminating at this point as we may be in an infinite loop")
        Else
            Try
                ValueNames = p_Key.GetValueNames
                'TL.LogMessage("Start of ProcessSubKey", "Found " & ValueNames.Length & " values")
                For Each ValueName As String In ValueNames
                    Select Case ValueName.ToUpper
                        Case ""
                            TL.LogMessage("KeyValue", Space(p_Depth * Indent) & "*** Default *** = " & p_Key.GetValue(ValueName))
                        Case "APPID"
                            p_Container = "AppId"
                            TL.LogMessage("KeyValue", Space(p_Depth * Indent) & ValueName.ToString & " = " & p_Key.GetValue(ValueName))
                        Case Else
                            TL.LogMessage("KeyValue", Space(p_Depth * Indent) & ValueName.ToString & " = " & p_Key.GetValue(ValueName))
                    End Select
                    If Microsoft.VisualBasic.Left(p_Key.GetValue(ValueName), 1) = "{" Then
                        'TL.LogMessage("ClassExpand", "Expanding " & p_Key.GetValue(ValueName))
                        Select Case p_Container.ToUpper
                            Case "CLSID"
                                RKey = Registry.ClassesRoot.OpenSubKey("CLSID").OpenSubKey(p_Key.GetValue(ValueName))
                                If RKey Is Nothing Then 'Check in 32 bit registry on a 64bit system
                                    RKey = Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID").OpenSubKey(p_Key.GetValue(ValueName))
                                    If Not (RKey Is Nothing) Then TL.LogMessage("NewSubKey", Space(p_Depth * Indent) & "Found under Wow6432Node")
                                End If
                            Case "TYPELIB"
                                RKey = Registry.ClassesRoot.OpenSubKey("TypeLib").OpenSubKey(p_Key.GetValue(ValueName))
                                If RKey Is Nothing Then
                                    RKey = Registry.ClassesRoot.OpenSubKey("Wow6432Node\TypeLib").OpenSubKey(p_Key.GetValue(ValueName))
                                End If
                            Case "APPID"
                                RKey = Registry.ClassesRoot.OpenSubKey("AppId").OpenSubKey(p_Key.GetValue(ValueName))
                                If RKey Is Nothing Then
                                    RKey = Registry.ClassesRoot.OpenSubKey("Wow6432Node\AppId").OpenSubKey(p_Key.GetValue(ValueName))
                                End If
                            Case Else
                                RKey = p_Key.OpenSubKey(p_Key.GetValue(ValueName))
                        End Select

                        If Not RKey Is Nothing Then
                            If RKey.Name <> p_Key.Name Then 'We are in an infinite loop so kill it by settig rkey = Nothing
                                TL.LogMessage("NewSubKey", Space((p_Depth + 1) * Indent) & p_Container & "\" & p_Key.GetValue(ValueName))
                                ProcessSubKey(RKey, p_Depth + 1, "None")
                                RKey.Close()
                            Else
                                TL.LogMessage("IgnoreKey", Space((p_Depth + 1) * Indent) & p_Container & "\" & p_Key.GetValue(ValueName))
                            End If
                        Else
                            TL.LogMessage("KeyValue", "### Unable to open subkey: " & ValueName & "\" & p_Key.GetValue(ValueName) & " in container: " & p_Container)
                        End If
                    End If
                Next
            Catch ex As Exception
                TL.LogMessageCrLf("ProcessSubKey Exception 1", ex.ToString)
            End Try
            Try
                SubKeys = p_Key.GetSubKeyNames
                For Each SubKey In SubKeys
                    TL.LogMessage("ProcessSubKey", Space(p_Depth * Indent) & SubKey)
                    RKey = p_Key.OpenSubKey(SubKey)
                    Select Case SubKey.ToUpper
                        Case "TYPELIB"
                            'TL.LogMessage("Container", "TypeLib...")
                            Container = "TypeLib"
                        Case "CLSID"
                            'TL.LogMessage("Container", "CLSID...")
                            Container = "CLSID"
                        Case "IMPLEMENTED CATEGORIES"
                            'TL.LogMessage("Container", "Component Categories...")
                            Container = COMPONENT_CATEGORIES
                        Case Else
                            'TL.LogMessage("Container", "Other...")
                            Container = "None"
                    End Select
                    If Microsoft.VisualBasic.Left(SubKey, 1) = "{" Then
                        Select Case p_Container
                            Case COMPONENT_CATEGORIES
                                'TL.LogMessage("ImpCat", "ImpCat")
                                RKey = Registry.ClassesRoot.OpenSubKey(COMPONENT_CATEGORIES).OpenSubKey(SubKey)
                                Container = "None"
                            Case Else
                                'Do nothing
                        End Select
                    End If
                    ProcessSubKey(RKey, p_Depth + 1, Container)
                    RKey.Close()
                Next
            Catch ex As Exception
                TL.LogMessageCrLf("ProcessSubKey Exception 2", ex.ToString)
            End Try
            ' TL.LogMessage("End of ProcessSubKey", p_Container & " " & p_Depth)
        End If

    End Sub

    Private Sub btnExit_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnExit.Click
        End 'Close the program
    End Sub

    Private Sub ChooserToolStripMenuItem1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ChooserToolStripMenuItem1.Click
        Dim Chooser As Object, Chosen As String


        If ApplicationBits() = Bitness.Bits32 Then
            Chooser = CreateObject("DriverHelper.Chooser")
            Chooser.DeviceType = "Telescope"
            Chosen = Chooser.Choose("ScopeSim.Telescope")
        Else
            MsgBox("This component is 32bit only and cannot run on a 64bit system")
        End If
    End Sub

    Sub ScanSerial()
        Dim SerialRegKey As RegistryKey, SerialDevices() As String
        Try
            'First list out the ports we can see through .NET
            Status("Scanning Serial Ports")
            For Each Port As String In System.IO.Ports.SerialPort.GetPortNames
                TL.LogMessage("Serial Ports (.NET)", Port)
            Next
            TL.BlankLine()

            SerialRegKey = Registry.LocalMachine.OpenSubKey("HARDWARE\DEVICEMAP\SERIALCOMM")
            SerialDevices = SerialRegKey.GetValueNames
            For Each SerialDevice As String In SerialDevices
                TL.LogMessage("Serial Ports (Registry)", SerialRegKey.GetValue(SerialDevice).ToString & " - " & SerialDevice)
            Next
            TL.BlankLine()

            For i As Integer = 1 To 30
                Call SerialPortDetails(i)
            Next

            TL.BlankLine()
            TL.BlankLine()
        Catch ex As Exception
            TL.LogMessageCrLf("ScanSerial", ex.ToString)
        End Try

    End Sub

    Sub SerialPortDetails(ByVal PortNumber As Integer)
        'List specific details of a particular serial port
        Dim PortName As String, SerPort As New System.IO.Ports.SerialPort

        Try
            PortName = "COM" & PortNumber.ToString 'String version of the port name
            SerPort.PortName = PortName
            SerPort.BaudRate = 9600
            SerPort.Open()
            SerPort.Close()
            TL.LogMessage("Serial Port Test ", PortName & " opened OK")
        Catch ex As Exception
            TL.LogMessageCrLf("Serial Port Test ", ex.Message)
        End Try

        SerPort.Dispose()
        SerPort = Nothing
    End Sub

    Sub ScanProfile()

        Dim ASCOMProfile As Utilities.Profile, DeviceTypes() As String, Devices As ArrayList

        Try
            ASCOMProfile = New Utilities.Profile
            RecursionLevel = -1 'Initialise recursion level so the first increment makes this zero

            Status("Scanning Profile")

            DeviceTypes = ASCOMProfile.RegisteredDeviceTypes
            For Each DeviceType As String In DeviceTypes
                Devices = ASCOMProfile.RegisteredDevices(DeviceType)
                TL.LogMessage("Registered Device Type", DeviceType)
                For Each Device As KeyValuePair In Devices
                    TL.LogMessage("Registered Devices", "   " & Device.Key & " - " & Device.Value)
                Next
            Next
            TL.BlankLine()
            TL.BlankLine()
        Catch ex As Exception
            TL.LogMessageCrLf("RegisteredDevices", "Exception: " & ex.ToString)
        End Try

        Try
            TL.LogMessage("Profile", "Recusrsing Profile")
            RecurseProfile("\") 'Scan recurively over the profile
        Catch ex As Exception
            TL.LogMessageCrLf("ScanProfile", ex.Message)
        End Try

        TL.BlankLine()
        TL.BlankLine()
    End Sub

    Sub RecurseProfile(ByVal ASCOMKey As String)
        Dim SubKeys, Values As New Generic.SortedList(Of String, String)
        Dim NextKey, DisplayName, DisplayValue As String

        'List values in this key
        Try
            'TL.LogMessage("RecurseProfile", Space(3 * (If(RecursionLevel < 0, 0, RecursionLevel))) & ASCOMKey)
            Values = ASCOMRegistryAccess.EnumProfile(ASCOMKey)
            For Each kvp As KeyValuePair(Of String, String) In Values
                If String.IsNullOrEmpty(kvp.Key) Then
                    DisplayName = "*** Default Value ***"
                Else
                    DisplayName = kvp.Key
                End If
                If String.IsNullOrEmpty(kvp.Value) Then
                    DisplayValue = "*** Not Set ***"
                Else
                    DisplayValue = kvp.Value
                End If
                TL.LogMessage("Profile Value", Space(3 * (RecursionLevel + 1)) & DisplayName & " = " & DisplayValue)
            Next
        Catch ex As Exception
            TL.LogMessageCrLf("Profile 1", "Exception: " & ex.ToString)
        End Try

        'Now recurse through all subkeys of this key
        Try
            RecursionLevel += 1 'Increment recursion level
            SubKeys = ASCOMRegistryAccess.EnumKeys(ASCOMKey)

            For Each kvp As KeyValuePair(Of String, String) In SubKeys
                If ASCOMKey = "\" Then
                    NextKey = ""
                Else
                    NextKey = ASCOMKey
                End If
                If String.IsNullOrEmpty(kvp.Value) Then
                    DisplayValue = "*** Not Set ***"
                Else
                    DisplayValue = kvp.Value
                End If
                TL.BlankLine()
                TL.LogMessage("Profile Key", Space(3 * RecursionLevel) & NextKey & "\" & kvp.Key & " - " & DisplayValue)
                RecurseProfile(NextKey & "\" & kvp.Key)
            Next

        Catch ex As Exception
            TL.LogMessageCrLf("Profile 2", "Exception: " & ex.ToString)
        Finally
            RecursionLevel -= 1
        End Try

    End Sub

    Private Sub ChooserNETToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ChooserNETToolStripMenuItem.Click
        Dim Chooser As ASCOM.Utilities.Chooser, Chosen As String

        Chooser = New ASCOM.Utilities.Chooser
        Chooser.DeviceType = "Telescope"
        Chosen = Chooser.Choose("ScopeSim.Telescope")
        Chooser.Dispose()

    End Sub

    Private Sub ConnectToDeviceToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ConnectToDeviceToolStripMenuItem.Click
        ConnectForm.Visible = True
    End Sub

    Private Sub ListAvailableCOMPortsToolStripMenuItem_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ListAvailableCOMPortsToolStripMenuItem.Click
        SerialForm.Visible = True
    End Sub

    Private Sub ScanInstalledPlatform()
        Dim RegKey As RegistryKey

        Try ' Platform 5.5 Inno installer setup, should always be absent in Platform 6!
            RegKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\microsoft\Windows\Currentversion\uninstall\ASCOM.platform.NET.Components_is1", False)

            TL.LogMessage("Installed Platform", RegKey.GetValue("DisplayName"))
            TL.LogMessage("Installed Platform", "Inno Setup App Path - " & RegKey.GetValue("Inno Setup: App Path"))
            TL.LogMessage("Installed Platform", "Inno Setup Version - " & RegKey.GetValue("Inno Setup: Setup Version"))
            TL.LogMessage("Installed Platform", "Install Date - " & RegKey.GetValue("InstallDate"))
            TL.LogMessage("Installed Platform", "Install Location - " & RegKey.GetValue("InstallLocation"))
            RegKey.Close()
        Catch ex As Exception
            TL.LogMessageCrLf("Installed Platform", "OK - no Inno installer path found")
        End Try

        Try ' Platform 6 installer GUID, should always be present in Platform 6
            RegKey = ASCOMRegistryAccess.OpenSubKey(Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" & INSTALLER_PROPDUCT_CODE, False, RegistryAccess.RegWow64Options.KEY_WOW64_32KEY)

            TL.LogMessage("Installed Platform", RegKey.GetValue("DisplayName"))
            TL.LogMessage("Installed Platform", "Version - " & RegKey.GetValue("DisplayVersion"))
            TL.LogMessage("Installed Platform", "Install Date - " & RegKey.GetValue("InstallDate"))
            TL.LogMessage("Installed Platform", "Install Location - " & RegKey.GetValue("InstallLocation"))
            TL.LogMessage("Installed Platform", "Install Source - " & RegKey.GetValue("InstallSource"))
            RegKey.Close()
        Catch ex As Exception
            TL.LogMessageCrLf("Installed Platform", "Exception: " & ex.ToString)
            NExceptions += 1
        End Try
        TL.BlankLine()
    End Sub

    Private Function InstallerName() As String
        Dim RegKey As RegistryKey, DisplayName As String

        Try ' Platform 6 installer GUID, should always be present in Platform 6
            If ApplicationBits() = Bitness.Bits32 Then '32bit OS
                RegKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" & INSTALLER_PROPDUCT_CODE, False)
            Else '64bit OS
                RegKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\" & INSTALLER_PROPDUCT_CODE, False)
            End If

            DisplayName = RegKey.GetValue("DisplayName")
            RegKey.Close()
        Catch ex As Exception
            DisplayName = ""
        End Try

        Return DisplayName
    End Function

    Sub ScanASCOMDrivers()
        Dim BaseDir As String
        Dim PathShell As New System.Text.StringBuilder(260)
        Try

            Status("Scanning for ASCOM Drivers")
            TL.LogMessage("ASCOM Drivers Scan", "Searching for installed drivers")

            If System.IntPtr.Size = 8 Then 'We are on a 64bit OS so look in the 64bit locations for files as well
                BaseDir = SHGetSpecialFolderPath(IntPtr.Zero, PathShell, CSIDL_PROGRAM_FILES_COMMONX86, False)
                BaseDir = PathShell.ToString & "\ASCOM"

                RecurseASCOMDrivers(BaseDir & "\Telescope") 'Check telescope drivers
                RecurseASCOMDrivers(BaseDir & "\Camera") 'Check camera drivers
                RecurseASCOMDrivers(BaseDir & "\Dome") 'Check dome drivers
                RecurseASCOMDrivers(BaseDir & "\FilterWheel") 'Check filterWheel drivers
                RecurseASCOMDrivers(BaseDir & "\Focuser") 'Check focuser drivers
                RecurseASCOMDrivers(BaseDir & "\Rotator") 'Check rotator drivers
                RecurseASCOMDrivers(BaseDir & "\SafetyMonitor") 'Check safetymonitor drivers
                RecurseASCOMDrivers(BaseDir & "\Switch") 'Check switch drivers

                BaseDir = Environment.GetFolderPath(SpecialFolder.CommonProgramFiles) & "\ASCOM"

                RecurseASCOMDrivers(BaseDir & "\Telescope") 'Check telescope drivers
                RecurseASCOMDrivers(BaseDir & "\Camera") 'Check camera drivers
                RecurseASCOMDrivers(BaseDir & "\Dome") 'Check dome drivers
                RecurseASCOMDrivers(BaseDir & "\FilterWheel") 'Check filterWheel drivers
                RecurseASCOMDrivers(BaseDir & "\Focuser") 'Check focuser drivers
                RecurseASCOMDrivers(BaseDir & "\Rotator") 'Check rotator drivers
                RecurseASCOMDrivers(BaseDir & "\SafetyMonitor") 'Check safetymonitor drivers
                RecurseASCOMDrivers(BaseDir & "\Switch") 'Check switch drivers
            Else '32 bit OS
                BaseDir = Environment.GetFolderPath(SpecialFolder.CommonProgramFiles) & "\ASCOM"

                RecurseASCOMDrivers(BaseDir & "\Telescope") 'Check telescope drivers
                RecurseASCOMDrivers(BaseDir & "\Camera") 'Check camera drivers
                RecurseASCOMDrivers(BaseDir & "\Dome") 'Check dome drivers
                RecurseASCOMDrivers(BaseDir & "\FilterWheel") 'Check filterWheel drivers
                RecurseASCOMDrivers(BaseDir & "\Focuser") 'Check focuser drivers
                RecurseASCOMDrivers(BaseDir & "\Rotator") 'Check rotator drivers
                RecurseASCOMDrivers(BaseDir & "\SafetyMonitor") 'Check safetymonitor drivers
                RecurseASCOMDrivers(BaseDir & "\Switch") 'Check switch drivers
            End If

            TL.BlankLine()

        Catch ex As Exception
            TL.LogMessageCrLf("ScanProgramFiles", "Exception: " & ex.ToString)
        End Try
    End Sub

    Sub RecurseASCOMDrivers(ByVal Folder As String)
        Dim Files(), Directories() As String

        Try
            Action(Microsoft.VisualBasic.Left(Folder, 70))
            Files = Directory.GetFiles(Folder)
            For Each MyFile As String In Files
                If MyFile.ToUpper.Contains(".EXE") Or MyFile.ToUpper.Contains(".DLL") Then
                    'TL.LogMessage("Driver", MyFile)
                    'FileDetails(Folder & "\", MyFile)
                    FileDetails("", MyFile)
                End If
            Next
        Catch ex As DirectoryNotFoundException
            TL.LogMessageCrLf("Driver", "Directory not present: " & Folder)
            Exit Sub
        Catch ex As Exception
            TL.LogMessageCrLf("RecurseASCOMDrivers 1", "Exception: " & ex.ToString)
        End Try

        Try
            Directories = Directory.GetDirectories(Folder)
            For Each Directory As String In Directories
                'TL.LogMessage("Directory", Directory)
                RecurseASCOMDrivers(Directory)
            Next
            Action("")
        Catch ex As DirectoryNotFoundException
            TL.LogMessage("Driver", "Directory not present: " & Folder)
        Catch ex As Exception
            TL.LogMessageCrLf("RecurseASCOMDrivers 2", "Exception: " & ex.ToString)
        End Try
    End Sub

    Private Sub btnLastLog_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnLastLog.Click
        Shell("notepad " & LastLogFile, AppWinStyle.NormalFocus)
    End Sub
End Class
