﻿//tabs=4
// --------------------------------------------------------------------------------
//
// ASCOM Gemini Telescope Hardware
//
// Description:	This implements a simulated Telescope Hardware
//
// Implements:	ASCOM Telescope interface version: 2.0
// Author:		(rbt) Robert Turner <robert@robertturnerastro.com>
//              (pk) Paul Kanevsky <paul@pk.darkhorizons.org>
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// 07-JUL-2009	rbt	1.0.0	Initial edit, from ASCOM Telescope Driver template
// 28-JUL-2009  pk  1.0.1   Initial implementation of Gemini hardware layer and command processor
// 29-JUL-2009  pk  1.0.1   Added DoCommandAsync asynchronous call-back version of the command processor
//                          Added array versions of DoCommand functions for multiple command execution
// 31-JUL-2009  rbt 1.0.1   Added Focuser Implementations and Settings
// --------------------------------------------------------------------------------
//

using System;
using System.Collections;
using System.Text;
using System.ComponentModel;
using System.Timers;
using System.IO.Ports;
using System.Windows.Forms;
using System.Drawing;

namespace ASCOM.GeminiTelescope
{

    /// <summary>
    /// Async delegate callback for DoCommandAsync
    /// </summary>
    /// <param name="cmd">original command string passed to DoCommandAsync</param>
    /// <param name="result">return result from Gemini, or null if timeout exceeded</param>
    public delegate void HardwareAsyncDelegate(string cmd, string result);

    public delegate void ConnectDelegate(bool Connected, int Clients);

    public delegate void ErrorDelegate(string from, string msg);

    public delegate void SafetyDelegate();

    
    /// <summary>
    /// Single serial command to be delivered to Gemini Hardware through worker thread queue
    /// </summary>
    internal class CommandItem
    {

        internal string m_Command;  //actual serial command to be sent, not including ending '#' or the native checksum
        int m_ThreadID;             //this will record thread id of the calling thread
        internal int m_Timeout;     //timeout value for this command in msec, -1 if no timeout wanted

        private System.Threading.ManualResetEvent m_WaitForResultHandle = null; // wait handle set by worker thread when result is received
        internal HardwareAsyncDelegate m_AsyncDelegate = null;  // call-back delegate for asynchronous operation
        /// <summary>
        /// result produced by Gemini, or null if no result. Ending '#' is always stripped off
        /// </summary>
        internal string m_Result { get; set; }
        internal bool m_Raw = false;

        internal bool m_UpdateRequired { get; set; } //true if this command updates a polled status variable, and an update is needed ASAP
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="command">actual serial command to be sent, not including ending '#' or the native checksum</param>
        /// <param name="timeout">timeout value for this command in msec, -1 if no timeout wanted</param>
        /// <param name="wantResult">does the caller want the result returned by Gemini?</param>
        /// <param name="bRaw">command is a raw string to be passed to the device unmodified</param>
        internal CommandItem(string command, int timeout, bool wantResult, bool bRaw)
        {
            m_Command = command;
            m_ThreadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            m_Timeout = timeout;

            // create a wait handle if result is desired
            if (wantResult) 
                m_WaitForResultHandle = new System.Threading.ManualResetEvent(false);
            m_Result = null;
            m_Raw = bRaw;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command">actual serial command to be sent, not including ending '#' or the native checksum</param>
        /// <param name="timeout">timeout value for this command in msec, -1 if no timeout wanted</param>
        /// <param name="wantResult">does the caller want the result returned by Gemini?</param>
        internal CommandItem(string command, int timeout, bool wantResult) : this(command,timeout,wantResult, false)
        {
        }

        /// <summary>
        ///  Initialize with an asynchrounous call-back delegate and a timeout
        /// </summary>
        /// <param name="command">actual serial command to be sent, not including ending '#' or the native checksum</param>
        /// <param name="timeout">timeout value for this command in msec, -1 if no timeout wanted</param>
        /// <param name="callback">asynchronous callback delegate to call on completion
        ///        public delegate void HardwareAsyncDelegate(string cmd, string result);
        /// </param>
        /// <param name="bRaw">command is a raw string to be passed to the device unmodified</param>
        internal CommandItem(string command, int timeout, HardwareAsyncDelegate callback, bool bRaw) 
            : this(command, timeout, true, bRaw)
        {
            m_AsyncDelegate = callback;
        }

        /// <summary>
        /// Return WaitHandle object to be set on receipt of the result for this command
        /// </summary>
        internal System.Threading.ManualResetEvent WaitObject
        {
            get { return m_WaitForResultHandle; }
        }

        /// <summary>
        ///     Wait on the synchronization wait handle to signal that the result is now available
        ///     result is placed into m_sResult by the worker thread and the event is then signaled
        /// </summary>
        /// <returns>result produced by Gemini as after executing this command or null if timeout expired</returns>
        internal string WaitForResult()
        {
            if (m_WaitForResultHandle != null)
            {
                if (m_Timeout > 0)
                {
                    if (m_WaitForResultHandle.WaitOne(m_Timeout))
                        return m_Result;
                    else
                    {
                        GeminiError.LogSerialError(SharedResources.TELESCOPE_DRIVER_NAME, "Time out occurred after " + m_Timeout.ToString() + "msec processing command '" + m_Command + "'");
                        return null;
                    }
                }
                else
                    m_WaitForResultHandle.WaitOne();  // no timeout specified, wait indefinitely
            }
            return null;
        }
    }


    /// <summary>
    /// Class encapsulating all serial communications with Gemini
    /// </summary>
    public static class GeminiHardware
    {
#region Member Variables

        public static ASCOM.Utilities.Profile m_Profile;
        public static ASCOM.Utilities.Util m_Util;
        public static ASCOM.Astrometry.Transform.Transform  m_Transform;

        private static Queue m_CommandQueue; //Queue used for messages to the gemini
        private static System.Threading.Thread m_BackgroundWorker; // Thread to run for communications

        private static bool m_CancelAsync = false; // when to stop the background thread
 

        //Telescope Implementation
        
        private static double m_Latitude;
        private static double m_Longitude;
        private static double m_Elevation;

        private static int m_UTCOffset;

        private static double m_RightAscension;
        private static double m_Declination;
        private static double m_Altitude;
        private static double m_Azimuth;
        private static double m_TargetRightAscension = SharedResources.INVALID_DOUBLE;
        private static double m_TargetDeclination = SharedResources.INVALID_DOUBLE;
        private static double m_SiderealTime;
        private static string m_Velocity;
        private static string m_SideOfPier;
        private static double m_TargetAltitude = SharedResources.INVALID_DOUBLE;
        private static double m_TargetAzimuth= SharedResources.INVALID_DOUBLE;

        private static bool m_AdditionalAlign;

        public static bool SwapSyncAdditionalAlign
        {
            get { return GeminiHardware.m_AdditionalAlign; }
            set { GeminiHardware.m_AdditionalAlign = value; }
        }

        private static bool m_Precession;
        private static bool m_Refraction;
        private static bool m_ShowHandbox;
        private static bool m_UseDriverSite;
        private static bool m_UseDriverTime;


        private static bool m_SendAdvancedSettings;

        public static bool SendAdvancedSettings
        {
            get { return GeminiHardware.m_SendAdvancedSettings; }
            set { 
                GeminiHardware.m_SendAdvancedSettings = value;
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "SendAdvancedSettings", value.ToString());
            }
        }


        private static bool m_Tracking;

        private static bool m_AtPark;
        private static bool m_AtHome;
        private static string m_ParkState = "";
     
        private static bool m_SouthernHemisphere = false;

        private static string m_GeminiVersion = "";

        private static TimeSpan m_GPSTimeDifference = TimeSpan.Zero;    // GPS UTC time - PC clock UTC time

        private static int m_SlewSettleTime = 0;

        public static int SlewSettleTime
        {
            get { return GeminiHardware.m_SlewSettleTime; }
            set { GeminiHardware.m_SlewSettleTime = value;
            m_Profile.DeviceType = "Telescope";
            m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "SlewSettleTime", value.ToString());
            }
        }


        private static double m_FocalLength;

        public static double FocalLength
        {
            get { return GeminiHardware.m_FocalLength; }
            set {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "FocalLength", value.ToString());
                GeminiHardware.m_FocalLength = value; 
            }
        }

        private static double m_ApertureArea;

        public static double ApertureArea
        {
            get { return GeminiHardware.m_ApertureArea; }
            set {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "ApertureArea", value.ToString());
                GeminiHardware.m_ApertureArea = value;
            }
        }


        private static double m_ApertureDiameter;

        public static double ApertureDiameter
        {
            get { return GeminiHardware.m_ApertureDiameter; }
            set {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "ApertureDiameter", value.ToString());
                GeminiHardware.m_ApertureDiameter = value;
            }
        }
        

        private static string m_ComPort;
        private static int m_BaudRate;
        private static ASCOM.Utilities.SerialParity m_Parity;
        private static int m_DataBits;
        private static ASCOM.Utilities.SerialStopBits m_StopBits;

        private static string m_GpsComPort;
        private static int m_GpsBaudRate;
        private static bool m_GpsUpdateClock;

        private static string m_PassThroughComPort;

        public static string PassThroughComPort
        {
            get { return GeminiHardware.m_PassThroughComPort; }
            set { 
                GeminiHardware.m_PassThroughComPort = value;
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "PassThroughComPort", value);
            }
        }

        private static int m_PassThroughBaudRate;

        public static int PassThroughBaudRate
        {
            get { return GeminiHardware.m_PassThroughBaudRate; }
            set {
                GeminiHardware.m_PassThroughBaudRate = value;
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "PassThroughBaudRate", value.ToString());
            }
        }
        private static bool m_PassThroughPortEnabled;

        public static bool PassThroughPortEnabled
        {
            get { return m_PassThroughPortEnabled; }
            set { 
                m_PassThroughPortEnabled = value;
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "PassThroughPortEnabled", value.ToString());
            }
        }

        private static System.Threading.AutoResetEvent m_WaitForCommand;

        private static string m_PolledVariablesString = ":GR#:GD#:GA#:GZ#:Gv#:GS#:Gm#:h?#<99:F#";
        private static string m_ShortPolledVariablesString1 = ":GR#:GD#:GA#:GZ#:Gv#";
        private static string m_ShortPolledVariablesString2 = ":GS#:Gm#:h?#<99:F#";

        public static int MAX_TIMEOUT = 10000; //max default timeout for all commands

        static System.Timers.Timer tmrReadTimeout = new System.Timers.Timer();
        static System.Threading.AutoResetEvent m_SerialTimeoutExpired = new System.Threading.AutoResetEvent(false);
        static System.Threading.AutoResetEvent m_SerialErrorOccurred = new System.Threading.AutoResetEvent(false);

        static private int m_TraceLevel = -1;

        /// <summary>
        /// Trace level, if set at or above zero, will create a new tracer object
        /// </summary>
        static public int TraceLevel
        {
            get { return m_TraceLevel; }    
            set {
                if (value >= 0)
                    m_Trace.Start(SharedResources.TELESCOPE_DRIVER_NAME, value);
                else
                    m_Trace.Stop();
            }
        }

        static private Tracer m_Trace = new Tracer();

        /// <summary>
        /// Tracer object for all tracing needs
        /// </summary>
        static public Tracer Trace
        {
            get { return m_Trace; }
        }


        private static bool m_UseJoystick = false;

        public static bool UseJoystick
        {
            get { return GeminiHardware.m_UseJoystick; }
            set {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "UseJoystick", value.ToString());
                GeminiHardware.m_UseJoystick = value; 
            }
        }

        private static string m_JoystickName = null;

        public static string JoystickName
        {
            get { return GeminiHardware.m_JoystickName; }
            set {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "JoystickName", value.ToString());
                GeminiHardware.m_JoystickName = value; 
            }
        }


        private static UserFunction[] m_JoystickButtonMap;

        public static UserFunction[] JoystickButtonMap
        {
            get { return m_JoystickButtonMap; }
            set { 
                m_JoystickButtonMap = value;
                GeminiHardware.m_Profile.DeviceType = "Telescope";
                for (int i = 0; i < value.Length; ++i)
                {
                    int v = (int)value[i];
                    GeminiHardware.m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Button " + (i+1).ToString(), v.ToString());
                }
            }
        }

        private static double m_JoystickAccelerator = 0;

        public static double JoystickAccelerator
        {
            get { return GeminiHardware.m_JoystickAccelerator; }
            set { GeminiHardware.m_JoystickAccelerator = value; }
        }

        private static bool m_JoystickIsAnalog = true;

        public static bool JoystickIsAnalog
        {
            get { return GeminiHardware.m_JoystickIsAnalog; }
            set { 
                GeminiHardware.m_JoystickIsAnalog = value;
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "JoystickIsAnalog", value.ToString());
                m_JoystickFixedSpeed = 0;   //reset this to guiding speed
            }
        }

        private static int m_JoystickFixedSpeed = 0; // 0 = guide, 1=center, 2 = slew

        public static int JoystickFixedSpeed
        {
            get { return GeminiHardware.m_JoystickFixedSpeed; }
            set { GeminiHardware.m_JoystickFixedSpeed = value; }
        }


        public enum GeminiBootMode
        {
            Prompt = 0,
            ColdStart = 1,
            WarmStart = 2,
            WarmRestart = 3,
        }
        ;
        private static GeminiBootMode m_BootMode = GeminiBootMode.Prompt; 

        private static SerialPort m_SerialPort; // main physical port

        private static PassThroughPort m_PassThroughPort = null; // a secondary port (virtual) for connecting non-ASCOM compliant Gemini applications

        private static bool m_Connected = false; //Keep track of the connection status of the hardware

        private static int m_Clients;

        private static DateTime m_LastUpdate;
        private static object m_ConnectLock = new object();

        private static int m_QueryInterval = SharedResources.GEMINI_POLLING_INTERVAL;     // query mount for status this often, in msecs.

        private static int m_TotalErrors = 0;              //total number of errors encountered since m_FirstErrorTick count
        private static int m_FirstErrorTick = 0;           //

        private static DateTime m_LastDataTick;              // tick of when the last successful data was received from the mount

        private static int m_GeminiStatusByte;              // result of <99: native command, polled on an interval
        private static bool m_SafetyNotified;               // true if safety limit notification was already sent


        private static frmStatus m_StatusForm = null;

        //Focuser Private Data
        private static int m_MaxIncrement = 0;
        private static int m_MaxStep = 0;
        private static int m_StepSize = 0;
        private static bool m_ReverseDirection = false;
        private static int m_BacklashDirection = 0;
        private static int m_BacklashSize = 0;
        private static int m_BrakeSize = 0;
        private static int m_Speed = 1;
#endregion

#region Public Events
        /// <summary>
        /// OnConnect is fired when a new client is connected or disconnected
        ///   OnConnect(Connected, Clients) - Connected is true if a client conects, false if disconnects
        ///   Clients is the total number of remaining attached clients after the connect/disconnect
        ///   if Connected is false, and Clients is 0, then the driver is disconnected from the serial port/gemini
        /// </summary>
        public static ConnectDelegate OnConnect;

        /// <summary>
        /// OnError is fired when a serious serial error occurs, such as a command timeout
        ///  checksum error, or other failure to complete a request
        /// </summary>
        public static ErrorDelegate OnError;


        /// <summary>
        /// OnInfo is fired when a UI notification to the user is needed
        /// </summary>
        public static ErrorDelegate OnInfo;

        private static bool m_AllowErrorNotify = true;

        /// <summary>
        /// Fired when safety limit is reached
        /// </summary>
        public static SafetyDelegate OnSafetyLimit;

#endregion

        #region Initializers
        /// <summary>
        ///  TelescopeHadrware constructor
        ///     create serial port
        /// </summary>
        static GeminiHardware()
        {
            TraceLevel = 4;    // 
            Trace.Enter("GeminiHardware", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version, DateTime.Now.ToString());

            m_Profile = new ASCOM.Utilities.Profile();
            m_Util = new ASCOM.Utilities.Util();
            m_Transform = new ASCOM.Astrometry.Transform.Transform();

            m_SerialPort = new SerialPort();
            m_CommandQueue = new Queue();
            m_Clients = 0;

            m_WaitForCommand = new System.Threading.AutoResetEvent(false);

            tmrReadTimeout.AutoReset = false;            
            tmrReadTimeout.Elapsed += new ElapsedEventHandler(tmrReadTimeout_Elapsed);

            GetProfileSettings();
            Trace.Exit("GeminiHardware");
        }


        /// <summary>
        ///  reloads all the variables from the profile
        /// </summary>
        private static void GetProfileSettings() 
        {
            Trace.Enter("GetProfileSettings");

            //Telescope Settings
            m_Profile.DeviceType = "Telescope";
            if (m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "RegVer", "") != SharedResources.REGISTRATION_VERSION)
            {
                Trace.Info(2, "New Profile version");

                //Main Driver Settings
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "RegVer", SharedResources.REGISTRATION_VERSION);

                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "ComPort", "COM1");
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "BaudRate", "9600");

                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "GpsComPort", "COM1");
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "GpsBaudRate", "4800");
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "GpsUpdateClock", true.ToString());

                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "AdditionalAlign", false.ToString());
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Precession", false.ToString());
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Refraction", false.ToString());
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Show Handbox", false.ToString());
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Use Driver Site", true.ToString());
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Use Driver Time", true.ToString());

                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "PassThroughPortEnabled", false.ToString());
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "PassThroughComPort", "COM10");
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "PassThroughBaudRate", "9600");

                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "BootMode", "0");

            }

            //Load up the values from saved
            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "AdditionalAlign", ""), out m_AdditionalAlign))
                m_AdditionalAlign = false;

            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "Precession", ""), out m_Precession))
                m_Precession = false;
            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "Refraction", ""), out m_Refraction))
                m_Refraction = false;
            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "Show Handbox", ""), out m_ShowHandbox))
                m_ShowHandbox = false;
            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "Use Driver Site", ""), out m_UseDriverSite))
                m_UseDriverSite= false;
            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "Use Driver Time", ""), out m_UseDriverTime))
                m_UseDriverTime = false;

            if (!int.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "TraceLevel", ""), out m_TraceLevel))
                m_TraceLevel = 4;

            TraceLevel = m_TraceLevel;

            Trace.Info(2, "User Settings", m_AdditionalAlign, m_Precession, m_Refraction, m_ShowHandbox, m_UseDriverSite, m_UseDriverTime);

            if (!int.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "SlewSettleTime", ""), out m_SlewSettleTime))
                m_SlewSettleTime = 2;

            m_ComPort = m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "ComPort", "");
            if (!int.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "BaudRate", ""), out m_BaudRate))
                m_BaudRate = 9600;


            m_GpsComPort = m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "GpsComPort", "");
            if (!int.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "GpsBaudRate", ""), out m_GpsBaudRate))
                m_GpsBaudRate = 4800;
            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "GpsUpdateClock", ""), out m_GpsUpdateClock))
                m_GpsUpdateClock = false;

            if (!int.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "DataBits", ""), out m_DataBits))
                m_DataBits = 8;

            int _parity = 0;
            if (!int.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "Parity", ""), out _parity))
                _parity = 0;

            m_Parity = (ASCOM.Utilities.SerialParity)_parity;

            int _stopbits = 8;
            if (!int.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "StopBits", ""), out _stopbits))
                _stopbits = 1;

            m_StopBits = (ASCOM.Utilities.SerialStopBits)_stopbits;

            Trace.Info(2, "Comm Settings", m_ComPort, m_BaudRate, m_DataBits, m_Parity, m_StopBits);

            if (!double.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "Latitude", ""), out m_Latitude))
                m_Latitude = 0.0;

            if (!double.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "Longitude", ""), out m_Longitude))
                m_Longitude = 0.0;

            if (!double.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "Elevation", ""), out m_Elevation))
                m_Elevation = 0.0;


            if (!double.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "ApertureArea", ""), out m_ApertureArea))
                m_ApertureArea = 0.0;

            if (!double.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "ApertureDiameter", ""), out m_ApertureDiameter))
                m_ApertureDiameter = 0.0;

            if (!double.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "FocalLength", ""), out m_FocalLength))
                m_FocalLength = 0.0;


            Trace.Info(2, "Geo Settings", m_Latitude, m_Longitude, m_Elevation);
            
            m_PassThroughComPort = m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "PassThroughComPort", "");
            if (!int.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "PassThroughBaudRate", ""), out m_PassThroughBaudRate))
                m_PassThroughBaudRate = 9600;

            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "PassThroughPortEnabled", ""), out m_PassThroughPortEnabled))
                m_PassThroughPortEnabled = false;

            if (m_PassThroughPortEnabled && m_PassThroughComPort.Equals(m_ComPort, StringComparison.CurrentCultureIgnoreCase))
            {
                Trace.Error("Pass-through port is invalid", m_PassThroughComPort);
                if (OnError != null) OnError("Pass-through port will be disabled", "Gemini Pass-Through port # is invalid: " + m_PassThroughComPort);
                PassThroughPortEnabled = false;
            }

            if (m_ComPort != "")
            {
                m_SerialPort.PortName = m_ComPort;
            }

            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "SendAdvancedSettings", ""), out m_SendAdvancedSettings))
                m_SendAdvancedSettings = false;

            Trace.Info(2, "Pass Through Port", m_PassThroughComPort, m_PassThroughBaudRate, m_PassThroughPortEnabled);


            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "UseJoystick", ""), out m_UseJoystick))
                m_UseJoystick = false;

            m_JoystickName = m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "JoystickName", "");

            m_JoystickButtonMap = JoystickButtonMapFromProfile();

            if (!bool.TryParse(m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "JoystickIsAnalog", ""), out m_JoystickIsAnalog))
                m_JoystickIsAnalog = true;

            //Get the Boot Mode from settings
            try
            {
                switch (m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "BootMode", ""))
                {
                    case "0":
                        m_BootMode = GeminiBootMode.Prompt;
                        break;
                    case "1":
                        m_BootMode = GeminiBootMode.ColdStart;
                        break;
                    case "2":
                        m_BootMode = GeminiBootMode.WarmStart;
                        break;
                    case "3":
                        m_BootMode = GeminiBootMode.WarmRestart;
                        break;
                    default:
                        m_BootMode = GeminiBootMode.Prompt;
                        break;
                }
            }
            catch
            {
                m_BootMode = GeminiBootMode.Prompt;
            }

            Trace.Info(2, "Boot Settings", m_BootMode);


            //Focuser Settings
            m_Profile.DeviceType = "Focuser";
            
            if (m_Profile.GetValue(SharedResources.FOCUSER_PROGRAM_ID, "RegVer", "") != SharedResources.REGISTRATION_VERSION)
            {
                Trace.Info(2, "New Focuser version");

                //Main Driver Settings
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "RegVer", SharedResources.REGISTRATION_VERSION);
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "MaxIncrement", "5000");
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "StepSize", "100");
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "ReverseDirection", false.ToString());
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "BacklashDirection", "0");
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "BacklashSize", "50");
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "BrakeSize", "0");
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "Speed", "1");
            }

            string s = m_Profile.GetValue(SharedResources.FOCUSER_PROGRAM_ID, "MaxIncrement");
            if (!int.TryParse(s, out m_MaxIncrement) || m_MaxIncrement <= 0)
                m_MaxIncrement = 5000;

            s = m_Profile.GetValue(SharedResources.FOCUSER_PROGRAM_ID, "MaxStep");
            //if (!int.TryParse(s, out m_MaxStep) || m_MaxStep <= 0)
            m_MaxStep = 0x7fffffff;

            s = m_Profile.GetValue(SharedResources.FOCUSER_PROGRAM_ID, "StepSize");
            if (!int.TryParse(s, out m_StepSize) || m_StepSize <= 0)
                m_StepSize = 100;

            s = m_Profile.GetValue(SharedResources.FOCUSER_PROGRAM_ID, "ReverseDirection");
            if (!bool.TryParse(s, out m_ReverseDirection))
                m_ReverseDirection = false;

            s = m_Profile.GetValue(SharedResources.FOCUSER_PROGRAM_ID, "BacklashDirection");
            if (!int.TryParse(s, out m_BacklashDirection) || m_BacklashDirection < -1 || m_BacklashDirection > 1)
                m_BacklashDirection = 0;

            s = m_Profile.GetValue(SharedResources.FOCUSER_PROGRAM_ID, "BacklashSize");
            if (!int.TryParse(s, out m_BacklashSize) || m_BacklashSize < 0)
                m_BacklashSize = 0;

            s = m_Profile.GetValue(SharedResources.FOCUSER_PROGRAM_ID, "BrakeSize");
            if (!int.TryParse(s, out m_BrakeSize) || m_BrakeSize < 0)
                m_BrakeSize = 0;

            s = m_Profile.GetValue(SharedResources.FOCUSER_PROGRAM_ID, "Speed");
            if (!int.TryParse(s, out m_Speed) || m_Speed < 1 || m_Speed > 3)
                m_Speed = 1;

            Trace.Info(2, "Focuser Settings", m_MaxIncrement, m_MaxStep, m_StepSize, m_ReverseDirection, m_BacklashDirection, m_BacklashSize, m_BrakeSize, m_Speed);

            m_Profile.DeviceType = "Telescope";

            Trace.Exit("GetProfileSettings");

        }

        private static UserFunction[] JoystickButtonMapFromProfile()
        {
            UserFunction [] funcs = new UserFunction[32];
            
            for (int i = 0; i < 32; ++i)
            {
                string value = GeminiHardware.m_Profile.GetValue(SharedResources.TELESCOPE_PROGRAM_ID, "Button " + (i + 1).ToString(), "");
                int val = 0;
                int.TryParse(value, out val);
                funcs[i] = (UserFunction)val;
            }
            return funcs;
        }

#endregion


#region Properties For Settings
        /// <summary>
        /// Get/Set Boot Mode 
        /// </summary>
        public static GeminiBootMode BootMode
        {
            get { return m_BootMode; }
            set 
            { 
                m_BootMode = value;
                string bootMode = "0";
                switch (value)
                {
                    case GeminiBootMode.Prompt:
                        bootMode = "0";
                        break;
                    case GeminiBootMode.ColdStart:
                        bootMode = "1";
                        break;
                    case GeminiBootMode.WarmStart:
                        bootMode = "2";
                        break;
                    case GeminiBootMode.WarmRestart:
                        bootMode = "3";
                        break;
                }
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "BootMode", bootMode);
            }

        }

        /// <summary>
        /// Returns Gemini version, level number digit, followed by two version digits
        /// </summary>
        public static string Version
        {
            get { return m_GeminiVersion; }

        }

        /// <summary>
        /// Gemini-defined UTC offset (timezone)
        /// </summary>
        public static int UTCOffset
        {
            get { return GeminiHardware.m_UTCOffset; }
            set { GeminiHardware.m_UTCOffset = value; }
        }

        /// <summary>
        /// Get/Set Hanbox Form Setting 
        /// </summary>
        public static bool ShowHandbox
        {
            get { return m_ShowHandbox; }
            set
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Show Handbox", value.ToString());
                m_ShowHandbox = value;
            }
        }

        /// <summary>
        /// Get/Set Use Gemini Site 
        /// </summary>
        public static bool UseDriverSite
        {
            get { return m_UseDriverSite; }
            set
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Use Driver Site", value.ToString());
                m_UseDriverSite = value;
            }
        }
        /// <summary>
        /// Get/Set Use Gemini Time 
        /// </summary>
        public static bool UseDriverTime
        {
            get { return m_UseDriverTime; }
            set
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Use Driver Time", value.ToString());
                m_UseDriverTime = value;
            }
        }
        /// <summary>
        /// Get/Set serial comm port 
        /// </summary>
        public static string ComPort
        {
            get { return m_ComPort; }
            set 
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "ComPort", value.ToString());
                m_ComPort = value; 
            }
        }
        /// <summary>
        /// Get/Set baud rate
        /// </summary>
        public static int BaudRate
        {
            get { return m_BaudRate; }
            set
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "BaudRate", value.ToString());
                m_BaudRate = value;
            }
        }
        /// <summary>
        /// Get/Set serial comm port 
        /// </summary>
        public static string GpsComPort
        {
            get { return m_GpsComPort; }
            set
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "GpsComPort", value.ToString());
                m_GpsComPort = value;
            }
        }
        /// <summary>
        /// Get/Set baud rate
        /// </summary>
        public static int GpsBaudRate
        {
            get { return m_GpsBaudRate; }
            set
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "GpsBaudRate", value.ToString());
                m_GpsBaudRate = value;
            }
        }
        /// <summary>
        /// Get/Set Gps Updates Clock
        /// </summary>
        public static bool GpsUpdateClock
        {
            get { return m_GpsUpdateClock; }
            set 
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "GpsUpdateClock", value.ToString());
                m_GpsUpdateClock = value; 
            }
        }
        /// <summary>
        /// Get/Set parity
        /// </summary>
        public static ASCOM.Utilities.SerialParity Parity
        {
            get { return m_Parity; }
            set
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Parity", value.ToString());
                m_Parity = value;
            }
        }

        /// <summary>
        /// Get/Set # of stop bits
        /// </summary>
        public static ASCOM.Utilities.SerialStopBits StopBits
        {
            get { return m_StopBits; }
            set
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "StopBits", value.ToString());
                m_StopBits = value;
            }
        }

        /// <summary>
        /// Get/Set # of data bits
        /// </summary>
        public static int DataBits
        {
            get { return m_DataBits; }
            set
            {
                m_Profile.DeviceType = "Telescope";
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "DataBits", value.ToString());
                m_DataBits = value;
            }
        }

        /// <summary>
        /// Get/Set Elevation
        /// </summary>
        public static double Elevation
        {
            get { return m_Elevation; }
            set
            {
                m_Profile.DeviceType = "Telescope";
                m_Elevation = value;
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Elevation", value.ToString());
            }
        }
        /// <summary>
        /// Get/Set Latitude
        /// </summary>
        public static double Latitude
        {
            get { return m_Latitude; }
            set
            {
                
                Trace.Enter("Latitude", value);
                if (value < -90 || value > 90) throw new ASCOM.InvalidValueException("Latitude", value.ToString(), "-90..90");
                m_Profile.DeviceType = "Telescope";
                m_Latitude = value;
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Latitude", value.ToString());
                m_SouthernHemisphere = (m_Latitude < 0);
                m_Transform.SiteLatitude = m_Latitude;
                Trace.Exit("Latitude", value);
            }
        }

        /// <summary>
        /// Get/Set Longitude
        /// </summary>
        public static double Longitude
        {
            get { return m_Longitude; }
            set
            {
                Trace.Enter("Longitude", value);
                if (value < -180 || value > 180) throw new ASCOM.InvalidValueException("Longitude", value.ToString(), "-180..180");
                m_Profile.DeviceType = "Telescope";
                m_Longitude = value;
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Longitude", value.ToString());
                m_Transform.SiteLongitude = m_Longitude;
                Trace.Enter("Longitude", value);

            }
        }

        /// <summary>
        /// Get/Set Equatorial system type: JNOW = 1, or J2000  = 2
        ///   current Refraction setting is also updated to the mount, as that's the only way Gemini takes
        ///   these settings: together.
        /// </summary>
        public static bool Precession
        {
            get
            {
                Trace.Enter("Pecession.Get", (m_GeminiStatusByte & 32) == 0 ? false : true);
                return (m_GeminiStatusByte & 32) == 0 ?  false : true; 
            }
            set {
                if (value == false) //JNOW 
                {
                    Trace.Enter("Precession.Set", value);
                    if (m_Refraction)
                        DoCommandResult(":p1", MAX_TIMEOUT, false);
                    else
                        DoCommandResult(":p0", MAX_TIMEOUT, false);
                }
                else //J2000
                {
                    if (m_Refraction)
                        DoCommandResult(":p3", MAX_TIMEOUT, false);
                    else
                        DoCommandResult(":p2", MAX_TIMEOUT, false);
                }
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Precession", m_Precession.ToString());
            }
        }

        /// <summary>
        /// Get/Set whether Gemini should apply refraction correction: true = Gemini calculates refraction, false = it doesn't
        ///   current precession setting is also updated to the mount, as that's the only way Gemini takes
        ///   these settings: together.
        /// </summary>
        public static bool Refraction
        {
            get
            {
                Trace.Enter("Refraction.Get", m_Refraction);
                return (m_Refraction);
            }
            set
            {
                Trace.Enter("Refraction.Set", m_Refraction);
                m_Refraction = value;
                Precession = Precession; // this updates the mount with refraction and precession settings
                m_Profile.WriteValue(SharedResources.TELESCOPE_PROGRAM_ID, "Refraction", m_Refraction.ToString());
            }
        }

        public static bool AtSafetyLimit
        {
            get
            {
                Trace.Enter(4, "AtSafetyLimit.Get", (m_GeminiStatusByte & 16) != 0);
                return (m_GeminiStatusByte & 16) != 0;
            }
        }

        /// <summary>
        /// Execute a single serial command, block and wait for the response from the mount, return it 
        /// </summary>
        /// <example>
        /// <code>
        /// // Get Altitude from Gemini with a 1 second timeout:
        /// double dAltitude = 0;
        /// 
        /// string sAlt = GeminiHardware.DoCommandResult(":GA", 1000, false);
        ///
        /// if (!string.IsNullOrEmpty(sAlt))
        ///     dAltitude = NETHelper.DMSToDegrees(sAlt);
        /// </code>
        /// </example>
        /// <param name="cmd">command string to send to Gemini</param>
        /// <param name="timeout">in msecs, -1 if no timeout</param>
        /// <param name="bRaw">command is a raw string to be passed to the device unmodified</param>
        /// <returns>result received from Gemini, or null if no result, timeout, or bad result received</returns>
        public static string DoCommandResult(string cmd, int timeout, bool bRaw)
        {
            return DoCommandResult(new string[] { cmd }, timeout, bRaw);
        }


        /// <summary>
        /// Execute standard command, no result, no blocking
        /// </summary>
        /// <example>
        /// <code>
        /// // Move to Home Position
        /// GeminiHardware.DoCommand(":hP", false);
        /// </code>
        /// </example>
        /// <param name="cmd">command string to send to Gemini</param>
        /// <param name="bRaw">command is a raw string to be passed to the device unmodified</param>
        public static void DoCommand(string cmd, bool bRaw)
        {
            DoCommand(new string[] { cmd }, bRaw);
        }

        /// <summary>
        /// Execute an array of command in sequence, no return expected.  Commands guaranteed to be executed in the sequence specified, with no interruptions from other threads.
        /// </summary>
        /// <param name="cmd">array of commands to execute, element 0 will be executed first</param>
        /// <param name="bRaw">command is a raw string to be passed to the device unmodified</param>
        public static void DoCommand(string [] cmd, bool bRaw)
        {
            if (!m_Connected) return;
            CommandItem[] ci = new CommandItem[cmd.Length];

            for (int i = 0; i < ci.Length; ++i)
                ci[i] = new CommandItem(cmd[i], -1, false, bRaw);

            QueueCommands(ci);
        }


        /// <summary>
        /// Executes a command and returns immediately. The callback function is called 
        /// when Gemini returns a result, or when the timeout value expires.
        /// 
        /// Spawns a background thread that waits for the command execution result.
        /// Callback delegate is defined as:
        /// 
        ///    public delegate void HardwareAsyncDelegate(string cmd, string result);
        /// </summary>
        /// <param name="cmd">command to send to Gemini</param>
        /// <param name="timeout">timeout value in msec, or -1 for no timeout</param>
        /// <param name="callback">callback delegate will be called with the result
        ///     public delegate void HardwareAsyncDelegate(string cmd, string result);
        /// </param>
        /// <param name="bRaw">command is a raw string to be passed to the device unmodified</param>
        public static void DoCommandAsync(string cmd, int timeout, HardwareAsyncDelegate callback, bool bRaw)
        {
            CommandItem ci = new CommandItem(cmd, timeout, callback, bRaw);
            System.Threading.ThreadPool.QueueUserWorkItem(DoCommandAndWaitAsync, ci);
        }

        /// <summary>
        /// Execute a sequence of commands guaranteed not to be interrupted by another ASCOM client or another thread
        ///   
        ///   
        ///   The result in the case of successfull completion is the Gemini generated result for
        ///   the last command
        ///   
        ///   In the case of a timeout, the result passed into the callback is 'null'.        
        /// </summary>
        /// <param name="cmd">an array of commands to execute</param>
        /// <param name="timeout">timeout per command in the whole sequence, in msec, or -1 for no timeout</param>
        /// <returns>the result of the last command in the array if the sequence was successfully completed,
        /// otherwise 'null'.
        /// </returns>
        /// <param name="bRaw">command is a raw string to be passed to the device unmodified</param>
        public static string DoCommandResult(string [] cmd, int timeout, bool bRaw)
        {
            if (!m_Connected) return null;

            int total_timeout = 0;

            CommandItem[] ci = new CommandItem[cmd.Length];

            for (int i = 0; i < ci.Length; ++i)
                ci[i] = new CommandItem(cmd[i], timeout, true, bRaw); //initialize all CommandItem objects

            if (!QueueCommands(ci)) return null;  // queue them all at once

            // construct an array of all the wait handles
            System.Threading.ManualResetEvent[] events = new System.Threading.ManualResetEvent[ci.Length];
            for (int i = 0; i < ci.Length; ++i)
            {
                events[i] = ci[i].WaitObject;
                total_timeout += timeout;
            }

            // success only if all wait handles are signalled by the worker thread. Return result from the last command:
            if (System.Threading.ManualResetEvent.WaitAll(events, timeout < 0? -1 : total_timeout)) 
                return ci[ci.Length-1].m_Result;

            return null;
        }

        /// <summary>
        /// Execute a sequence of commands guaranteed not to be interrupted by another ASCOM client or another thread
        ///   
        ///   
        ///   The result in the case of successfull completion is an array  of Gemini generated results for
        ///   each of the commands. For commands that don't produce a result or timed out, the corresponding result 
        ///   value will be null.
        /// 
        /// </summary>
        /// <param name="cmd">an array of commands to execute</param>
        /// <param name="timeout">total timeout for the whole sequence, in msec, or -1 for no timeout</param>
        /// <param name="bRaw">command is a raw string to be passed to the device unmodified</param>
        /// <param name="result">out parameter, contains array of results for all the commands</param>
        public static void DoCommandResult(string[] cmd, int timeout, bool bRaw, out string [] result)
        {
            result = null;

            if (!m_Connected) return;

            CommandItem[] ci = new CommandItem[cmd.Length];

            for (int i = 0; i < ci.Length; ++i)
                ci[i] = new CommandItem(cmd[i], timeout, true, bRaw); //initialize all CommandItem objects

            if (!QueueCommands(ci)) return;  // queue them all at once

            int total_timeout = 0;
            // construct an array of all the wait handles
            System.Threading.ManualResetEvent[] events = new System.Threading.ManualResetEvent[ci.Length];
            for (int i = 0; i < ci.Length; ++i)
            {
                total_timeout += timeout;
                events[i] = ci[i].WaitObject;
            }

            // success only if all wait handles are signalled by the worker thread. Return result from the last command:
            if (System.Threading.ManualResetEvent.WaitAll(events, timeout<0? -1 :  total_timeout))
            {
                result = new string [ci.Length];
                for (int i = 0; i < ci.Length; ++i)
                    result[i] = ci[i].m_Result;
            }
        }

        /// <summary>
        /// Execute a sequence of commands guaranteed not to be interrupted by another ASCOM client or another thread
        ///   
        ///   the callback is called if the sequence times out, or if all the commands are performed successfully
        ///   the result passed back to the callback in the case of successfull completion is the command string for
        ///   the last command, and the result received from Gemini after executing this command
        ///   
        ///   in the case of a timeout, the result passed into the callback is 'null'.
        /// </summary>
        /// <param name="cmd">an array of commands to execute</param>
        /// <param name="timeout">total timeout for the whole sequence, in msec, or -1 for no timeout</param>
        /// <param name="callback">asynchrnous callback delegate to call when the sequence completes
        ///     public delegate void HardwareAsyncDelegate(string cmd, string result);
        /// </param>
        /// <param name="bRaw">command is a raw string to be passed to the device unmodified</param>
        public static void DoCommandAsync(string[] cmd, int timeout, HardwareAsyncDelegate callback, bool bRaw)
        {
            CommandItem[] ci = new CommandItem[cmd.Length];

            for (int i = 0; i < ci.Length; ++i)
                ci[i] = new CommandItem(cmd[i], timeout, callback, bRaw);

            System.Threading.ThreadPool.QueueUserWorkItem(DoCommandsAndWaitAsync, ci);
        }


        /// <summary>
        /// executed by a worker thread for an asynchronous command call-back
        /// </summary>
        /// <param name="command_item">CommandItem type containing command to execute</param>
        private static void DoCommandAndWaitAsync(object command_item)
        {
            CommandItem ci = (CommandItem)command_item;

            if (!QueueCommand(ci))
            {
                if (ci.m_AsyncDelegate != null)
                        ci.m_AsyncDelegate(ci.m_Command, null);
                return;
            }

            if (ci.m_AsyncDelegate!=null)
                try
                {
                    string result = ci.WaitForResult();
                    ci.m_AsyncDelegate(ci.m_Command, result);
                }
                catch { }
        }

        /// <summary>
        /// Executed by a worker thread for an asynchronous command call-back
        /// Executes and waits for multiple commands to complete, then fires a callback.
        /// 
        /// All commands are executed in sequence.
        /// If timeout is exceeded, the callback receives the last command string, along with 'null' for result
        /// if all commands successfully execute and all return proper values,
        ///   the callback will receive the last command string and the result produced by the last command
        /// </summary>
        /// <param name="command_items">CommandItems array containing commands to execute</param>
        private static void DoCommandsAndWaitAsync(object command_items)
        {
            CommandItem [] ci = (CommandItem[])command_items;

            if (!QueueCommands(ci))
            {
                CommandItem last_ci = ci[ci.Length - 1];
                last_ci.m_AsyncDelegate(last_ci.m_Command, null);
                return;
            }

            System.Threading.ManualResetEvent[] events = new System.Threading.ManualResetEvent[ci.Length];

            for (int i = 0; i < ci.Length; ++i) events[i] = ci[i].WaitObject;

            bool bAllDone = (System.Threading.ManualResetEvent.WaitAll(events, ci[0].m_Timeout));

            try
            {
                CommandItem last_ci = ci[ci.Length - 1];
                if (bAllDone)
                    last_ci.m_AsyncDelegate(last_ci.m_Command, last_ci.m_Result);
                else
                    last_ci.m_AsyncDelegate(last_ci.m_Command, null);
            }
            catch { }
        }

        /// <summary>
        /// True while PulseGuiding command is in progress
        /// </summary>
        public static bool IsPulseGuiding
        {
            get 
            {
                string l_Velocity = DoCommandResult(":Gv", MAX_TIMEOUT, false); //Get current velocity
                if (l_Velocity.ToUpper() == "G") return true; // If G then we are pulse guiding 
                else return false; // Some other rate so we are not pulse guiding
            }
        }

        public static int Clients
        {
            get { return m_Clients; }
        }


        /// <summary>
        /// Wait for completion of a goto home or park at cwd operation
        /// </summary>
        /// <param name="where">'home' or 'park' for logging and exception reporting purposes</param>
        public static void WaitForHomeOrPark(string where)
        {
            Trace.Enter(4, "WaitForHomeOrPark", where);

            int count = 0;

            // wait for parking move to begin, wait for a maximum of 16*250ms = 4 seconds
            while (ParkState != "2" && count < 16) { System.Threading.Thread.Sleep(250); count++; }
            //            if (count == 16) throw new TimeoutException(where + " operation didn't start");

            // now wait for it to end
            while (ParkState == "2") { System.Threading.Thread.Sleep(1000); };

            // 0 => didn't park.
            //if (GeminiHardware.ParkState == "0") throw new DriverException("Failed to " + where, (int)SharedResources.ERROR_BASE);
            Trace.Exit(4, "WaitForHomeOrPark", where);
        }

        /// <summary>
        /// Wait for completion of asynchronous slew operation, at end wait out the slewsettle time
        /// </summary>
        public static void WaitForSlewToEnd()
        {
            Trace.Enter(4, "WaitForSlewToEnd");

            Velocity = "";

            int when = System.Environment.TickCount + 5000;
            while (System.Environment.TickCount < when && !(Velocity == "S" || Velocity == "C"))
                System.Threading.Thread.Sleep(500);

            while (Velocity == "S" || Velocity == "C") System.Threading.Thread.Sleep(500);

            System.Threading.Thread.Sleep((SlewSettleTime + 2) * 1000);

            Trace.Exit(4, "WaitForSlewToEnd");
        }


        /// <summary>
        /// wait for one or more possible velocity states of the mount
        /// </summary>
        /// <param name="p">contains one or more letters representing velocities we are waiting for: N, T, G, C, S</param>
        /// <param name="tmout">how long to wait or -1 to wait indefinitely</param>
        /// <returns></returns>
        public static bool WaitForVelocity(string p, int tmout)
        {
            Trace.Enter(4, "WaitForVelocity", p, tmout);

            int timeout = System.Environment.TickCount + tmout;
            while ((tmout <= 0 || System.Environment.TickCount < timeout) && !p.Contains(Velocity)) System.Threading.Thread.Sleep(500);

            Trace.Exit(4, "WaitForVelocity", p, tmout, Velocity);
            if (p.Contains(Velocity)) return true;
            return false;
        }


        static private void StartStatus(object arg)
        {
            Point pt = (Point)arg;
            Screen scr = Screen.FromPoint(pt);

            m_StatusForm = new frmStatus();
            m_StatusForm.AutoHide = true;

            Point top = (pt);
            top.Y -= m_StatusForm.Bounds.Height + 32;
            top.X -= 32;

            top.Y = Math.Min(top.Y, scr.WorkingArea.Height - m_StatusForm.Bounds.Height - 32);
            top.X = Math.Min(top.X, scr.WorkingArea.Width - m_StatusForm.Bounds.Width - 32);

            m_StatusForm.Location = top;

            m_StatusForm.Visible = true;
            m_StatusForm.Show();
            Win32API.SetForegroundWindow(m_StatusForm.Handle);
            Application.Run(m_StatusForm);
        }

        private static System.Threading.Thread statusThread = null;

        public static void ShowStatus(Point pt, bool autoHide)
        {
            if (statusThread != null)
            {
                if (m_StatusForm != null && m_StatusForm.InvokeRequired)
                    m_StatusForm.BeginInvoke(new EventHandler(m_StatusForm.ShowMe));
                return;
            }
            // Create a new thread from which to start the status screen form
            statusThread = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(StartStatus));
            statusThread.Start(pt);

        }

#endregion

#region Telescope Implementation

        /// <summary>
        /// Establish connection with the mount, open serial port
        /// </summary>
        private static void Connect()
        {
            Trace.Enter("Connect()");
            lock (m_ConnectLock)   // make sure only one connection goes through at a time
            {
                m_Clients += 1;

                Trace.Info(2, "Clients=", m_Clients);

                if (!m_SerialPort.IsOpen)
                {
                    Trace.Info(2, "SerialPort.IsOpen", "false");

                    GetProfileSettings();

                    m_SerialPort.PortName = m_ComPort;
                    m_SerialPort.BaudRate = m_BaudRate;
                    m_SerialPort.Parity = (System.IO.Ports.Parity)m_Parity;
                    m_SerialPort.DataBits = m_DataBits;
                    m_SerialPort.StopBits = (System.IO.Ports.StopBits)m_StopBits;              
                    m_SerialPort.Handshake = System.IO.Ports.Handshake.None;
                    m_SerialPort.ErrorReceived += new SerialErrorReceivedEventHandler(m_SerialPort_ErrorReceived);

                    try
                    {
                        Trace.Info(2, "Before Port.Open");
                        m_SerialPort.Open();
                
                        m_SerialPort.DtrEnable = true;
                        Trace.Info(2, "After Port.Open");
                    }
                    catch (Exception e)
                    {
                        if (!HuntForGemini(null))
                        {
                            m_Clients -= 1;
                            Trace.Except(e);
                            GeminiError.LogSerialError(SharedResources.TELESCOPE_DRIVER_NAME, "Serial comm error connecting to port " + m_ComPort + ":" + e.Message);
                            if (OnError != null) OnError(SharedResources.TELESCOPE_DRIVER_NAME, "Connection Failed: " + e.Message);
                            m_Connected = false;
                            throw e;    //rethrow the exception
                        }
                    }

                    m_TargetRightAscension = SharedResources.INVALID_DOUBLE;
                    m_TargetDeclination = SharedResources.INVALID_DOUBLE;
                    m_TargetAltitude = SharedResources.INVALID_DOUBLE;
                    m_TargetAzimuth = SharedResources.INVALID_DOUBLE;

                    lock(m_CommandQueue) 
                        m_CommandQueue.Clear();

                    // process initial ping to Gemini
                    // and take it through the boot process if it's not booted yet
                    // it all succeeds, set connected state and start worker thread

                    if (StartGemini())
                    {
                        m_Connected = true;
                     
                        UpdateInitialVariables();

                        m_WaitForCommand.Reset();

                        Trace.Info(2, "Creating worker thread");
                        m_CancelAsync = false;

                        m_BackgroundWorker = new System.Threading.Thread(BackgroundWorker_DoWork);
                        m_BackgroundWorker.Start();


                        try
                        {
                            SendStartUpCommands();
                        }
                        catch { }

                        if (m_PassThroughPortEnabled)
                            try {
                                Trace.Info(2, "Open pass-through port");
                                m_PassThroughPort = new PassThroughPort();
                                m_PassThroughPort.Initialize(m_PassThroughComPort, m_PassThroughBaudRate);   
                            } 
                            catch (Exception ptp_e)
                            {
                                Trace.Except(ptp_e);
                                GeminiError.LogSerialError(SharedResources.TELESCOPE_DRIVER_NAME, "Cannot open pass-through port: " +ptp_e.Message);
                                if (OnError != null) OnError(SharedResources.TELESCOPE_DRIVER_NAME, "Cannot open pass-through port: " + ptp_e.Message);
                                m_PassThroughPort = null;                                
                            }
                        System.Threading.Thread.Sleep(1000);

                        if (SendAdvancedSettings)
                        {
                            SetGeminiAdvancedSettings();
                        }

                    }
                    else
                    {
                        Trace.Error("Gemini is not responding. Please check that it's connected");
                        if (OnError != null) OnError(SharedResources.TELESCOPE_DRIVER_NAME, "Gemini is not responding. Please check that it's connected.");

                        Disconnect();

                        throw new ASCOM.DriverException(SharedResources.MSG_NOT_CONNECTED, (int)SharedResources.SCODE_NOT_CONNECTED);
                    }
                }

            }

            if (OnConnect != null && m_Connected) OnConnect(true, m_Clients);

            Trace.Exit("Connect()");
        }

        private static void SetGeminiAdvancedSettings()
        {
            Trace.Enter("SetGeminiAdvancedSettings");
            GeminiProperties props = new GeminiProperties();
            if (props.Serialize(false, null))   //read default profile settings
            {
                Trace.Info(2, "Apply Advanced settings");
                props.SyncWithGemini(true); //send the default profile to Gemini
            }
        }

        private static void SendStartUpCommands()
        {
            Trace.Enter("SendStartUpCommands");
            // check that the precision is set to high, if not, set it:
            string precision = DoCommandResult(":P", MAX_TIMEOUT, false);
            Trace.Info(2, "Current precision", precision);

            if (precision != "HIGH PRECISION")
            {
                Trace.Info(2, "Setting HIGH PRECISION");
                DoCommandResult(":U", MAX_TIMEOUT, false);
            }

            Trace.Info(2, "Updating refraction");
            Refraction = m_Refraction;  // update this setting to the mount 

            //Set the site and time if required
            if (m_UseDriverSite)
            {
                Trace.Info(2, "Set Long/Lat from PC", m_Latitude, m_Longitude);

                SetLatitude(m_Latitude);
                SetLongitude(m_Longitude);
            }

            if (m_UseDriverTime)
            {
                Trace.Info(2, "Set UTC from PC", (DateTime.UtcNow + m_GPSTimeDifference).ToString());

                UTCDate = DateTime.UtcNow + m_GPSTimeDifference;
            }
            Trace.Exit("SendStartUpCommands");
        }


        static void m_SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Trace.Error("Serial Port Error", e.EventType, e);
            m_SerialErrorOccurred.Set();    //signal that an error has occurred
        }

        public static void Transmit(string s)
        {
            Trace.Enter(4, "Transmit", s);

            if (s == String.Empty) return;

            m_SerialErrorOccurred.Reset();

            if (m_SerialPort.IsOpen)
            {
                Trace.Info(0, "Serial Transmit", s);

                m_SerialPort.Write(s);
                m_SerialPort.BaseStream.Flush();
                Trace.Info(4, "Finished Port.Write");
            }
            if (m_SerialErrorOccurred.WaitOne(0))
            {
                Trace.Error("Tramsmit timeout", s);
                throw new TimeoutException("Serial port transmission error: "+s);
            }
            Trace.Exit(4, "Transmit");
        }


        /// <summary>
        /// Check and process the initialization status of Gemini on initial connect:
        ///   return true if already started and initialized,
        ///   otherwise, perform initialization based on configured options
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// param name="sRes" value returned by Gemini to the ^G command /param
        ///</remarks>
        private static bool StartGemini()
        {
            Trace.Enter("StartGemini");
            if (OnInfo!=null) OnInfo("Connecting to Gemini on " + m_SerialPort.PortName + ", " + m_SerialPort.BaudRate.ToString(), "Connecting...");
            Transmit("\x6");
            System.Threading.Thread.Sleep(0);
            CommandItem ci = new CommandItem("\x6", 10, true); // quick timeout, don't want to hang up the user for too long
            string sRes = GetCommandResult(ci);


            Transmit("\x6");
            ci = new CommandItem("\x6", 1000, true);
            sRes = GetCommandResult(ci);

            if (sRes == null)
            {
                if (!HuntForGemini(null)) return false;
                Transmit("\x6");
                ci = new CommandItem("\x6", 10000, true);
                sRes = GetCommandResult(ci);
            }

            Trace.Info(2, "^G result", sRes);


            if (sRes == "B")
            {
                // scrolling message? wait for it to end:
                while (sRes == "B" || sRes==null)
                {
                    Trace.Info(4, "Waiting...");
                    System.Threading.Thread.Sleep(500);
                    Transmit("\x6");
                    ci = new CommandItem("\x6", 1000, true);
                    sRes = GetCommandResult(ci);
                    
                    // if no response, it could be because while we did
                    // HuntForGemini previously, we may have gotten the wrong baud rate.
                    // Gemini allows a connection at 9600 baud while the scrolling message (before the boot menu)
                    // is scrolling, so try one more time to scan the same port to find the correct rate:
                    if (sRes == null) if (!HuntForGemini(m_SerialPort.PortName)) return false;
                }
            }


            if (sRes == "b")    //Gemini waiting for user to select the boot mode
            {

                Trace.Info(2, "Gemini boot menu");

                GeminiBootMode bootMode = m_BootMode;

                //ask the user what mode to boot up in
                if (bootMode == GeminiBootMode.Prompt)
                {
                    Trace.Info(2, "Prompt boot mode");

                    frmBootMode frmBoot = new frmBootMode();
                    System.Windows.Forms.DialogResult res = frmBoot.ShowDialog();
                    if (res == System.Windows.Forms.DialogResult.OK)
                        bootMode = frmBoot.BootMode;
                    else {
                        Trace.Info(2, "User canceled boot");
                        return false;   // not started, the user chose to cancel
                    }
                }

                Trace.Info(2, "Booting Gemini", bootMode);

                switch (bootMode)
                {
                    case GeminiBootMode.ColdStart: Transmit("bC#"); break;
                    case GeminiBootMode.WarmRestart: Transmit("bR#"); break;
                    case GeminiBootMode.WarmStart: Transmit("bW#"); break;
                }
                sRes = "S"; // put it into "starting" mode, so the next loop will wait for full initialization
            }

            // processing Cold start mode -- wait for this to end
            while (sRes == "S" || sRes=="b")
            {
                Trace.Info(4, "Waiting for completion", sRes);
                System.Threading.Thread.Sleep(500);
                Transmit("\x6");
                ci = new CommandItem("\x6", 3000, true);
                sRes = GetCommandResult(ci); ;
            }

            Trace.Exit("Start Gemini", sRes);

            return sRes == "G"; // true if startup completed, otherwise false
        }

        /// <summary>
        /// search through all defined COM ports for Gemini
        /// try various baud rates, 4800,9600,19200
        /// </summary>
        /// 
        /// <returns></returns>
        private static bool HuntForGemini(string one_port)
        {            
            Trace.Enter("HuntForGemini", m_SerialPort.PortName, m_SerialPort.BaudRate);
            
            m_AllowErrorNotify = false;

            try
            {
                if (m_SerialPort.IsOpen) m_SerialPort.Close();

                string[] ports = SerialPort.GetPortNames();

                // if port was specified, just look at that port:
                if (one_port != null) ports = new string[] { one_port };

                int[] rates = { 9600, 4800, 19200, 38400 };

                foreach (string p in ports)
                {

                    if (OnInfo != null) OnInfo("Searching for Gemini", "Checking serial port " + p);

                    for (int i = 0; i < rates.Length; ++i)
                    {
                        m_SerialPort.PortName = p;

                        m_SerialPort.BaudRate = rates[i];
                        try
                        {
                            m_SerialPort.Open();
                            m_SerialPort.DtrEnable = true;

                            if (m_SerialPort.IsOpen)
                            {
                                Transmit("\x6");
                                System.Threading.Thread.Sleep(0);
                                CommandItem ci = new CommandItem("\x6", 100, true); // quick timeout, don't want to hang up the user for too long
                                string sRes = GetCommandResult(ci);

                                Transmit("\x6");
                                System.Threading.Thread.Sleep(0);
                                ci = new CommandItem("\x6", 500, true); // quick timeout, don't want to hang up the user for too long
                                sRes = GetCommandResult(ci);

                                
                                if (sRes == null)
                                {
                                    m_SerialPort.Close();
                                }
                                else
                                {
                                    Trace.Info(1, "Found Gemini!", p, rates[i]);
                                    GeminiHardware.ComPort = p;
                                    GeminiHardware.BaudRate = rates[i];
                                    return true;
                                }
                            }
                        }
                        catch (Exception ex1)
                        {
                            Trace.Except(ex1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.Except(ex);
            }
            finally
            {
                m_AllowErrorNotify = true;
                Trace.Exit("HuntForGemini");
            }

            return false;
        }

        /// <summary>
        /// Disconnect this client. If no more clients, close the port, disconnect from the mount.
        /// </summary>
        private static void Disconnect()
        {
            Trace.Enter("Disconnect()");

            bool bMessage = m_Connected;    // if currently connected, fire the disconnect message at the end

            lock (m_ConnectLock)
            {
                Trace.Info(2, "Current connect state", m_Connected);

                m_Clients -= 1;

                Trace.Info(2, "Remaining clients", m_Clients);

                if (m_Clients <= 0)
                {
                    try
                    {
                        Trace.Info(2, "No more clients, disconnect");
                        m_CancelAsync = true;

                        m_Connected = false;
                        // no new commands will be queued after m_Connected is set to false, clear out
                        // anything remaining:
                        m_CommandQueue.Clear();

                        // wait for the thread to die for 5 seconds,
                        // then kill it -- we don't want to tie up the serial comm
                        if (m_BackgroundWorker != null)
                        {
                            Trace.Info(2, "Stopping bkgd thread");
                            m_WaitForCommand.Set(); // wake up the background thread
                            try
                            {
                                if (!m_BackgroundWorker.Join(2000))
                                    m_BackgroundWorker.Abort();
                            }
                            catch { }
                            Trace.Info(2, "Bkgd thread stopped");
                        }

                        //Transmit(":Q#"); // stop all slews, in case we are in the middle of one

                        Trace.Info(2, "Closing serial port");
                        m_SerialPort.Close();
                        Trace.Info(2, "Serial port closed");

                        m_BackgroundWorker = null;

                        Trace.Info(2, "Closing pass-through port");
                        if (m_PassThroughPort != null) m_PassThroughPort.Stop();
                        Trace.Info(2, "Pass-through port closed");
                    }
                    catch { }
                    CloseStatusForm();
                }
            }

            if (OnConnect != null && bMessage) OnConnect(false, m_Clients);

            Trace.Exit("Disconnect()");
        }

        public static void CloseStatusForm()
        {
            GeminiHardware.Trace.Info(4, "Before closing status form");

            if (m_StatusForm != null && m_StatusForm.InvokeRequired)
            {
                GeminiHardware.Trace.Info(4, "Before BeginInvoke status form");
                m_StatusForm.BeginInvoke(new EventHandler(m_StatusForm.ShutDown));

                GeminiHardware.Trace.Info(4, "After BeginInvoke status form");

                if (statusThread != null)
                {
                    if (!statusThread.Join(2000))
                    {
                        GeminiHardware.Trace.Info(4, "Thread.Abort status form");
                        statusThread.Abort();
                    }
                    statusThread = null;

                }
                m_StatusForm = null;
                GeminiHardware.Trace.Info(4, "After closing status form");
            }
        }

        /// <summary>
        /// Process queued up commands in the sequence queued.
        /// </summary>
        private static void BackgroundWorker_DoWork()
        {

            Trace.Enter("BackgroundWorker thread");

            while (!m_CancelAsync && m_SerialPort.IsOpen)
            {
                object [] commands = null;

                lock (m_CommandQueue)
                {
                    // process multiple commands, if more than one is queued up:
                    if (m_CommandQueue.Count > 0)
                    {
                        Trace.Info(4, "Command queue depth", m_CommandQueue.Count);
                        // gemini doesn't like too many long commands (buffer size problem?)
                        // remove up to x commands at a time
                        int cnt = Math.Min(10, m_CommandQueue.Count);

                        commands = new object[cnt]; // m_CommandQueue.ToArray();
                        for (int i = 0; i < cnt; ++i) commands[i] = m_CommandQueue.Dequeue();
                    }
                }

                try
                {
                    if (commands != null)    // got a new command, send it to the mount
                    {
                        string all_commands = String.Empty;

                        bool bNeedStatusUpdate = false;


                        foreach (CommandItem ci in commands)
                        {
                            ci.m_Result = null;
                            string serial_cmd = ci.m_Command;

                            // raw commands are passed to hardware unmodified:
                            if (!ci.m_Raw)
                            {
                                // native Gemini command?
                                if (ci.m_Command.Length > 0 && (ci.m_Command[0] == '<' || ci.m_Command[0] == '>'))
                                    serial_cmd = CompleteNativeCommand(ci.m_Command);
                                else
                                    serial_cmd = CompleteStandardCommand(ci.m_Command);
                            }
                            all_commands += serial_cmd;
                            if (ci.WaitObject == null) ci.m_Timeout = 2000;    // default timeout for requests where the user doesn't care
                        }

                        DiscardInBuffer();

                        Trace.Info(2, "Transmitting commands", all_commands);

                        int startTime = System.Environment.TickCount;

                        Transmit(all_commands);

                        Trace.Info(2, "Done transmitting");

                        foreach (CommandItem ci in commands)
                        {
                            Trace.Info(4, "Waiting for", ci.m_Command);

                            // wait for the result whether or not the caller wants it
                            // otherwise delayed result from a previous command
                            // can be falsely returned for a later request:

                            string result = GetCommandResult(ci);

                            Trace.Info(4, "Result", result);


                            if (ci.WaitObject != null)    // receive result, if one is expected
                            {
                                ci.m_Result = result;
                                ci.WaitObject.Set();   //release the wait handle so the calling thread can continue
                            }

                            // if this command is one of the status variables to be polled, make a note to update 
                            // status ASAP!
                            if (ci.m_UpdateRequired)
                            {
                                bNeedStatusUpdate = true;
                                Trace.Info(4, "Status Update requested");
                            }
                        }

                        if (bNeedStatusUpdate || (DateTime.Now - m_LastUpdate).TotalMilliseconds > SharedResources.GEMINI_POLLING_INTERVAL)
                        {
                            m_AllowErrorNotify = false; //don't bother the user with timeout errors during polling  -- these are not very important
                            UpdatePolledVariables(bNeedStatusUpdate); //update variables if one of them was altered by a processed command
                            m_AllowErrorNotify = false;
                        }
                    }
                    else
                    {
                        m_AllowErrorNotify = false; //don't bother the user with timeout errors during polling  -- these are not very important
                        UpdatePolledVariables(false);
                        m_AllowErrorNotify = true;
                    }
                }
                catch (Exception ex)
                {
                    Trace.Error("Unexpected exception", ex.ToString());
                }
                finally
                {
                    // release all pending commands
                    if (commands != null)
                        foreach (CommandItem ci in commands)
                            if (ci.WaitObject != null)
                                ci.WaitObject.Set();
                }

                // wait specified interval before querying the mount if no more commands, but
                // wake up immediately if a new command has been posted
                int waitfor =  SharedResources.GEMINI_POLLING_INTERVAL - (int)(DateTime.Now - m_LastUpdate).TotalMilliseconds;
                Trace.Info(4, "Sleep (msec)", waitfor);
                if (waitfor > 0)
                    m_WaitForCommand.WaitOne(waitfor);
            }

            Trace.Exit("BackgroundWorker thread", m_CancelAsync, m_SerialPort.IsOpen);
            m_CancelAsync = false;

        }

        /// <summary>
        /// Discard anything in the in-buffer, but keep all the binary 
        /// data, as it may be needed by the software on the other side of the
        /// pass-through port
        /// </summary>
        /// 
        private static void DiscardInBuffer()
        {
            StringBuilder sb = new StringBuilder();

            if (m_PassThroughPort!=null && m_PassThroughPort.PortActive)
            {
                while (m_SerialPort.BytesToRead > 0)
                {
                    int c = m_SerialPort.ReadByte();
                    if (c>=0x80) sb.Append(Convert.ToChar(c));
                }
                if (sb.Length > 0 && (m_PassThroughPort!=null && m_PassThroughPort.PortActive))
                    m_PassThroughPort.PassStringToPort(sb);
            }
            else m_SerialPort.DiscardInBuffer();
        }

        /// <summary>
        /// update all variable sthat are polled on an interval
        /// </summary>
        private static void UpdateInitialVariables()
        {
            CommandItem command;

            DiscardInBuffer(); //clear all received data
            
            //longitude, latitude, UTC offset
            Transmit(":GV#:Gg#:Gt#:GG#");

            //verify that Gemini is at least Level 4
            command = new CommandItem(":GV", MAX_TIMEOUT, true);
            string ver = GetCommandResult(command);
            if (ver != null)
            {
                if (ver.EndsWith("#")) ver = ver.Substring(0, ver.Length - 1);
                int v;
                if (int.TryParse(ver, out v))
                {
                    if (v / 100 < 4)   //level below 4!
                    {
                        Disconnect();

                        if (OnError != null) OnError(SharedResources.TELESCOPE_DRIVER_NAME, SharedResources.MSG_GEMINI_VERSION);
                        throw new DriverException(SharedResources.MSG_GEMINI_VERSION, (int)SharedResources.SCODE_GEMINI_VERSION);
                    }
                    m_GeminiVersion = ver;
                }
            }


            command = new CommandItem(":Gg", MAX_TIMEOUT, true);
            string longitude = GetCommandResult(command);

            command = new CommandItem(":Gt", MAX_TIMEOUT, true);
            string latitude = GetCommandResult(command);

            command = new CommandItem(":GG", MAX_TIMEOUT, true);
            string UTC_Offset = GetCommandResult(command);


            try
            {
                if (longitude != null && !UseDriverSite)  Longitude = -m_Util.DMSToDegrees(longitude);  // Gemini has the reverse notion of longitude sign: + for West, - for East}
            }
            catch (Exception ex)
            {
                Trace.Except(ex);
            }
            try
            {
                if (latitude != null && !UseDriverSite) Latitude = m_Util.DMSToDegrees(latitude);
            }
            catch (Exception ex)
            {
                Trace.Except(ex);
            }
            try
            {
                if (UTC_Offset != null) int.TryParse(UTC_Offset, out m_UTCOffset);
            }
            catch (Exception ex)
            {
                Trace.Except(ex);
            }

            try
            {
                //Get RA and DEC etc
                UpdatePolledVariables(true);
            }
            catch (Exception ex)
            {
                Trace.Except(ex);
            }
            m_LastUpdate = System.DateTime.Now;
        }


        static private int m_PollUpdateCount = 0;    // keep track of number of updates


        /// <summary>
        /// update all variable sthat are polled on an interval
        /// if UpdateAll is true, all polled variables are queries
        ///  otherwise, some variables are queried less frequently to
        ///  reduce serial port traffic and load on Gemini and PC
        /// </summary>
        /// 
        private static void UpdatePolledVariables(bool UpdateAll)
        {

            Trace.Enter("UpdatePolledVariables", UpdateAll);
            try
            {
                CommandItem command;

                // Gemini gets slow to respond when slewing, so increase timeout if we're in the middle of it:
                int timeout = (m_Velocity == "S" ? MAX_TIMEOUT*2 : MAX_TIMEOUT);

                int level = 0;
                string vars;

                m_PollUpdateCount++;

                if (UpdateAll)
                {
                    level = 3;   // update all
                    vars = m_PolledVariablesString;
                }
                else
                    if ((m_PollUpdateCount & 1) == 0)   // update set #1
                    {
                        level = 1;
                        vars = m_ShortPolledVariablesString1;
                    }
                    else
                    {
                        level = 2;          // update set #2
                        vars = m_ShortPolledVariablesString2;
                    }


                System.Diagnostics.Trace.Write("Poll commands: " + vars + "\r\n");
                //Get RA and DEC etc
                DiscardInBuffer(); //clear all received data
                Transmit(vars);

                string trc = "";
                

                if ((level & 1) != 0)
                {
                    command = new CommandItem(":GR", timeout, true);
                    string RA = GetCommandResult(command);
                    if (RA == null)
                    {
                        Trace.Error("timeout", command.m_Command);
                        Resync();
                        return;
                    }

                    command = new CommandItem(":GD", timeout, true);
                    string DEC = GetCommandResult(command);
                    if (DEC == null)
                    {
                        Trace.Error("timeout", command.m_Command);
                        Resync();
                        return;
                    }

                    command = new CommandItem(":GA", timeout, true);
                    string ALT = GetCommandResult(command);
                    if (ALT == null)
                    {
                        Trace.Error("timeout", command.m_Command);
                        Resync();
                        return;
                    }


                    command = new CommandItem(":GZ", timeout, true);
                    string AZ = GetCommandResult(command);

                    if (AZ == null)
                    {
                        Trace.Error("timeout", command.m_Command);
                        Resync();
                        return;
                    }

                    command = new CommandItem(":Gv", timeout, true);
                    string V = GetCommandResult(command);
                    if (V == null)
                    {
                        Trace.Error("timeout", command.m_Command);
                        Resync();
                        return;
                    }

                    if (RA != null) m_RightAscension = m_Util.HMSToHours(RA);
                    if (DEC != null) m_Declination = m_Util.DMSToDegrees(DEC);
                    if (ALT != null) m_Altitude = m_Util.DMSToDegrees(ALT);
                    if (AZ != null) m_Azimuth = m_Util.DMSToDegrees(AZ);
                    if (V != null) m_Velocity = V;
                    trc = "RA=" + RA + ", DEC=" + DEC + "ALT=" + ALT + " AZ=" + AZ + " Velocity=" + Velocity;

                }

                if ((level & 2) != 0)
                {
                    command = new CommandItem(":GS", timeout, true);
                    string ST = GetCommandResult(command);
                    if (ST == null)
                    {
                        Trace.Error("timeout", command.m_Command);
                        Resync();
                        return;
                    }
                    command = new CommandItem(":Gm", timeout, true);
                    string SOP = GetCommandResult(command);
                    if (SOP == null)
                    {
                        Trace.Error("timeout", command.m_Command);
                        Resync();
                        return;
                    }
                    command = new CommandItem(":h?", timeout, true);
                    string HOME = GetCommandResult(command);
                    if (HOME == null)
                    {
                        Trace.Error("timeout", command.m_Command);
                        Resync();
                        return;
                    }

                    command = new CommandItem("<99:", timeout, true);
                    string STATUS = GetCommandResult(command);
                    if (STATUS == null)
                    {
                        Trace.Error("timeout", command.m_Command);
                        Resync();
                        return;
                    }

                    if (Velocity == "N") m_Tracking = false;
                    else
                        m_Tracking = true;

                    if (ST != null)
                    {
                        m_SiderealTime = m_Util.HMSToHours(ST);
                    }
                    if (SOP != null) m_SideOfPier = SOP;

                    if (HOME != null)
                    {
                        m_ParkState = HOME;
                        if (HOME == "1")
                        {
                            m_AtHome = true;
                            if (Velocity == "N") m_AtPark = true;
                            else
                            {
                                m_AtPark = false;
                            }
                        }
                        else
                        {
                            m_AtHome = false;
                            m_AtPark = false;
                        }
                    }

                    if (STATUS != null)
                    {
                        int.TryParse(STATUS, out m_GeminiStatusByte);

                        // if reached safety limit, send out one notification 
                        if ((m_GeminiStatusByte & 16) != 0 && !m_SafetyNotified)
                        {
                            if (OnSafetyLimit != null) OnSafetyLimit();
                            m_SafetyNotified = true;
                        }
                        else if ((m_GeminiStatusByte & 16) == 0) m_SafetyNotified = false;
                    }

                    trc += " SOP=" + SOP + " HOME=" + HOME + " Status=" + m_GeminiStatusByte.ToString();
                }


                Trace.Info(4, trc);

                System.Diagnostics.Trace.Write("Done polling: " + trc +  "\r\n");
                m_LastUpdate = System.DateTime.Now;
            }
            catch (Exception e)
            {
                Trace.Except(e);
                m_SerialPort.DiscardOutBuffer();
                DiscardInBuffer();
            }
            Trace.Enter("UpdatePolledVariables");
        }

        /// <summary>
        /// After a timeout error, resync with the mount
        ///  by waiting for a proper response to ^G command
        ///  all in/out buffers are discarded to make sure
        ///  commands and their results are synchronized after 
        ///  this
        /// </summary>
        public static void Resync()
        {
            if (!m_Connected) return;

            Trace.Enter("Resync");

            if (m_SerialPort.IsOpen)
            {
                lock (m_CommandQueue)
                {
                    string sRes = null;
                    int count = 3;
                    do
                    {
                        try
                        {
                            m_SerialPort.DiscardOutBuffer();
                            DiscardInBuffer();

                            Transmit("\x6");
                            CommandItem ci = new CommandItem("\x6", 1000, true);
                            sRes = GetCommandResult(ci);
                        }
                        catch (Exception ex)
                        {
                            Trace.Except(ex);
                        }

                    } while (sRes != "G" && --count > 0);

                    if (sRes=="G")
                    {
                        Trace.Info(2, "Got a sync");
                        System.Threading.Thread.Sleep(1000);
                    }
                    else
                        Trace.Info(2, "Didn't get a sync, giving up!");

                    m_SerialPort.DiscardOutBuffer();
                    DiscardInBuffer();
                }
            }
            Trace.Exit("Resync");
        }



        /// <summary>
        /// Wait for a proper response from Gemini for a given command. Command has already been sent.
        /// 
        /// </summary>
        /// <param name="command">actual command sent to Gemini</param>
        /// <returns>result received from Gemini, or null if no result, timeout, or bad result received</returns>
        private static string GetCommandResult(CommandItem command)
        {
            string result = null;

            if (!m_SerialPort.IsOpen) return null;

            Trace.Enter(4, "GetCommandResult", command.m_Command);

            GeminiCommand.ResultType gemini_result = GeminiCommand.ResultType.HashChar;
            int char_count = 0;
            GeminiCommand gmc = FindGeminiCommand(command.m_Command);

            if (gmc != null)
            {
                gemini_result = gmc.Type;
                char_count = gmc.Chars;
                command.m_UpdateRequired = gmc.UpdateStatus;
            }

            // no result expected by this command, just return;
            if (gemini_result == GeminiCommand.ResultType.NoResult) return null;

            m_SerialTimeoutExpired.Reset();
            m_SerialErrorOccurred.Reset();

            if (command.m_Timeout > 0)
            {
                tmrReadTimeout.Interval = command.m_Timeout;
                tmrReadTimeout.Start();
            }

            try
            {
                Trace.Info(0, "Serial wait for repsonse", command.m_Command);

                switch (gemini_result)
                {
                    // a specific number of characters expected as the return value
                    case GeminiCommand.ResultType.NumberofChars:
                        result = ReadNumber(char_count);
                        break;

                    // value '1' or a string terminated by '#'
                    case GeminiCommand.ResultType.OneOrHash:
                        result = ReadNumber(1); ;  // check if first character is 1, and return if it is, no hash expected
                        if (result != "1")
                        {
                            result += ReadTo('#');
                            if (command.m_Raw) //Raw should return the full string including #
                                result += "#";
                        }
                        break;

                    // value '0' or a string terminated by '#'
                    case GeminiCommand.ResultType.ZeroOrHash:
                        result = ReadNumber(1);
                        if (result != "0")
                        {
                            result += ReadTo('#');
                            if (command.m_Raw) //Raw should return the full string including #
                                result += "#";
                        }
                        break;

                    // string terminated by '#'
                    case GeminiCommand.ResultType.HashChar:
                        result = ReadTo('#');
                        if (command.m_Raw) //Raw should return the full string including #
                            result += "#";
                        break;
                }


            }
            catch (Exception ex)
            {
                if (m_AllowErrorNotify)
                {
                    Trace.Except(ex);
                    GeminiError.LogSerialError(SharedResources.TELESCOPE_DRIVER_NAME, "Timeout error occurred after " + command.m_Timeout + "msec while processing command '" + command.m_Command + "'");
                    if (OnError != null && m_Connected) OnError(SharedResources.TELESCOPE_DRIVER_NAME, "Serial port timed out!");
                }
                //else Resync();

                AddOneMoreError();

                return null;
            }
            finally
            {
                tmrReadTimeout.Stop();
            }


            if (m_SerialErrorOccurred.WaitOne(0))
            {
                Trace.Error("Serial port error", command.m_Command);

                GeminiError.LogSerialError(SharedResources.TELESCOPE_DRIVER_NAME, "Serial comm error reported while processing command '" + command.m_Command + "'");
                if (OnError != null && m_Connected && m_AllowErrorNotify) OnError(SharedResources.TELESCOPE_DRIVER_NAME, "Serial port communication error");
                AddOneMoreError();
                return null;  // error occurred!
            }

            if (result!=null)
                m_LastDataTick = DateTime.Now;      // remember when last successfull data was received.

            // return value for native commands has a checksum appended: validate it and remove it from the return string:
            if (!string.IsNullOrEmpty(result) && command.m_Command[0] == '<' && !command.m_Raw)
            {
                char chksum = result[result.Length - 1];
                result = result.Substring(0, result.Length - 1); //remove checksum character

                if (chksum != ComputeChecksum(result))  // bad checksum -- ignore the return value! 
                {
                    Trace.Error("Bad Checksum", command.m_Command, result);

                    GeminiError.LogSerialError(SharedResources.TELESCOPE_DRIVER_NAME, "Serial comm error (bad checksum) while processing command '" + command.m_Command + "'");
                    if (OnError != null && m_Connected && m_AllowErrorNotify) OnError(SharedResources.TELESCOPE_DRIVER_NAME, "Serial port communication error");
                    AddOneMoreError();
                    result = null;
                }
            }

            Trace.Info(0, "Serial received:", result);
            Trace.Exit(4, "GetCommandResult", command.m_Command, result);
            return result;
        }

        /// <summary>
        /// Add one more error to the total error tally
        /// if number of errors in the defined interval (MAXIMUM_ERROR_INTERVAL) exceeds specified number (MAXIMUM_ERRORS)
        ///   assume that Gemini is off-line or some other catastrophic failure has occurred.
        ///   Send a message to the user through OnError event to fix the problem,
        ///   reset pending communication queues, and wait a defined "cool-down" interval of (RECOVER_SLEEP)
        ///   then, resume processing.
        /// </summary>
        private static void AddOneMoreError()
        {
            Trace.Enter("AddOneMoreError", m_TotalErrors);

            if (Connected && DateTime.Now - m_LastDataTick  > TimeSpan.FromMilliseconds(SharedResources.MAXIMUM_ERROR_INTERVAL))
            {
                string msg = "No response from Gemini for " + (SharedResources.MAXIMUM_ERROR_INTERVAL/1000 ).ToString() + " seconds, terminating connection";
                Trace.Error(msg);
                GeminiError.LogSerialError(SharedResources.TELESCOPE_DRIVER_NAME, msg);
                if (OnError != null && m_Connected) OnError(SharedResources.TELESCOPE_DRIVER_NAME, "Gemini is not responding! Terminating connection.");
                while (m_Connected) Disconnect();
                return;
            }

            // if this is the first error, or if it's been longer than maximum interval since the last error, start from scratch
            if (m_TotalErrors == 0)
                m_FirstErrorTick = System.Environment.TickCount;

            if (m_FirstErrorTick + SharedResources.MAXIMUM_ERROR_INTERVAL < System.Environment.TickCount)
            {
                m_FirstErrorTick = System.Environment.TickCount;
                m_TotalErrors = 0;
            }

            if (++m_TotalErrors > SharedResources.MAXIMUM_ERRORS)
            {
                Trace.Error("Too many errors");

                if (OnError != null && m_Connected && m_AllowErrorNotify) OnError(SharedResources.TELESCOPE_DRIVER_NAME, "Too many serial port errors! Please check Gemini.");
                GeminiError.LogSerialError(SharedResources.TELESCOPE_DRIVER_NAME, "Too many serial port errors in the last " + SharedResources.MAXIMUM_ERROR_INTERVAL / 1000 + " seconds. Resetting serial port.");

                lock (m_CommandQueue) // remove all pending commands, keep the queue locked so that the worker thread can't process during port reset
                {
                    Trace.Info(2, "Resetting serial port");

                    m_CommandQueue.Clear();
                    DiscardInBuffer();
                    m_SerialPort.DiscardOutBuffer();
                    try
                    {
                        Trace.Info(2, "Closing port");
                        m_SerialPort.Close();
                        System.Threading.Thread.Sleep(SharedResources.RECOVER_SLEEP);

                        Trace.Info(2, "Opening port");
                        m_SerialPort.Open();
                    }
                    catch (Exception ex)
                    {
                        Trace.Except(ex);
                        GeminiError.LogSerialError(SharedResources.TELESCOPE_DRIVER_NAME, "Cannot reset serial port after errors: " + ex.Message);
                    }
                }
                m_TotalErrors = 0;
                m_FirstErrorTick = 0;
            }
            //else Resync();

            Trace.Exit("AddOneMoreError");
        }



        static void tmrReadTimeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            m_SerialTimeoutExpired.Set();
        }

        /// <summary>
        /// Read serial port until the terminating character is encoutered. Don't include 
        /// terminating character in the result, honor readtimeout specified on the port.
        /// </summary>
        /// <param name="terminate"></param>
        /// <returns></returns>
        private static string ReadTo(char terminate)
        {
            StringBuilder res = new StringBuilder();

            StringBuilder outp = new StringBuilder();

            for (; ; )
            {
                if (m_SerialPort.BytesToRead > 0)
                {
                    char c = Convert.ToChar(m_SerialPort.ReadByte());

                    if (c != terminate)
                    {
                        // 223 = degree character, the only char > 0x80 that's used in normal
                        // response to commands (longitude, latitude, etc.) 
                        // it must occur inside the string to be a legitimate response,
                        // otherwise consider it part of a binary stream meant for the passthrough port
                        if ((int)c >= 0x80 && (c!=223 || res.Length==0)) outp.Append(c);
                        else
                            res.Append(c);
                    }
                    else
                    {
                        if (outp.Length > 0 && (m_PassThroughPort!=null && m_PassThroughPort.PortActive))
                            m_PassThroughPort.PassStringToPort(outp);
                        return res.ToString();
                    }
                }
                else
                {
                    if (m_SerialTimeoutExpired.WaitOne(0))
                    {
                        if (outp.Length > 0 && (m_PassThroughPort!=null && m_PassThroughPort.PortActive))
                            m_PassThroughPort.PassStringToPort(outp);
                        throw new TimeoutException("ReadTo");
                    }
                    System.Threading.Thread.Sleep(0);  //[pk] should instead wait on a waithandle set by serialdatareceived event...
                }
            }
        }

        /// <summary>
        /// Read exact number of characters from the serial port, honoring the read timeout
        /// </summary>
        /// <param name="chars"></param>
        /// <returns></returns>
        private static string ReadNumber(int chars)
        {
            StringBuilder res = new StringBuilder();
            StringBuilder outp = new StringBuilder();

            for (; ; )
            {
                if (m_SerialPort.BytesToRead > 0)
                {
                    byte c = (byte)m_SerialPort.ReadByte();
                    if ((int)c >= 0x80)
                        outp.Append(Convert.ToChar(c));
                    else
                        res.Append(Convert.ToChar(c));

                    if (res.Length == chars)
                    {
                        if (outp.Length > 0 && (m_PassThroughPort!=null && m_PassThroughPort.PortActive))
                            m_PassThroughPort.PassStringToPort(outp);
                        return res.ToString();
                    }
                }
                else
                {
                    if (m_SerialTimeoutExpired.WaitOne(0))
                    {
                        if (outp.Length > 0 && (m_PassThroughPort!=null && m_PassThroughPort.PortActive))
                            m_PassThroughPort.PassStringToPort(outp);
                        throw new TimeoutException("ReadNumber");
                    }
                    System.Threading.Thread.Sleep(0);   //[pk] should instead wait on a waithandle set by serialdatareceived event...
                }
            }
        }

        /// <summary>
        /// Find an entry in GeminiCommands collection for the full command
        /// string. The full command string can include parameters as part of the string
        /// </summary>
        /// <param name="full_cmd">full command, possibly including parameters</param>
        /// <returns>object describing return value for this Gemini command, or null if not found</returns>
        private static GeminiCommand FindGeminiCommand(string full_cmd)
        {

            if (full_cmd.StartsWith("<"))       // native get command is always '#' terminated
                return new GeminiCommand(GeminiCommand.ResultType.HashChar, 0);
            else if (full_cmd.StartsWith(">"))  // native set command always no return value
                return new GeminiCommand(GeminiCommand.ResultType.NoResult, 0);
            else
            {
                if (GeminiCommands.Commands.ContainsKey(full_cmd))
                    return GeminiCommands.Commands[full_cmd];

                // try to match the longest string first. Maximum length
                // command is something like :GVD or four characters,
                // minimum length command is 2 characters:
                for (int i = Math.Min(4, full_cmd.Length); i >= 2; --i)
                {
                    string sub = full_cmd.Substring(0, i);
                    if (GeminiCommands.Commands.ContainsKey(sub))
                        return GeminiCommands.Commands[sub];
                }
            }

            return null;            
        }

        /// <summary>
        /// Get/Set serial port connection state
        /// </summary>
        public static bool Connected
        {
            get
            { 
                // make sure we are fully connected/disconnected before returning a status,
                // so that another worker thread doesn't come in with a request while a connect is 
                // still in progress [pk]
                lock (m_ConnectLock) { return m_Connected; }
            }
            set
            {
                if (value)
                {                    
                    Connect();
                }
                else
                    Disconnect();
            }
        }

        /// <summary>
        /// Get SouthernHemisphere state
        /// </summary>
       public static bool SouthernHemisphere
       { get { return m_SouthernHemisphere; } }


        /// <summary>
        /// Get current RightAscention propery
        /// retrieved from the latest polled value from the mount, no actual command is executed
        /// </summary>
        public static double RightAscension
       { get { return m_RightAscension; } }

        /// <summary>
        /// Get current Declination propery
        /// retrieved from the latest polled value from the mount, no actual command is executed
        /// </summary>
        public static double Declination
        { get { return m_Declination; } }


        /// <summary>
        /// Get current Altitude propery
        /// retrieved from the latest polled value from the mount, no actual command is executed
        /// </summary>
        public static double Altitude
        { get { return m_Altitude; } }

        /// <summary>
        /// Get current Azimuth propery
        /// retrieved from the latest polled value from the mount, no actual command is executed
        /// </summary>
        public static double Azimuth
        { get { return m_Azimuth; } }

        /// <summary>
        /// Get current AtHome propery
        /// returns whether the telescope is at the home position
        /// </summary>
        public static bool AtHome
        { get { return m_AtHome; } }

        /// <summary>
        /// Get current AtPark propery
        /// returns whether the telescope is parked
        /// </summary>
        public static bool AtPark
        { get { return m_AtPark; } }

        /// <summary>
        /// Get Set current TargetRightAscention propery
        /// </summary>
        public static double TargetRightAscension
        { 
            get { return m_TargetRightAscension; }
            set { m_TargetRightAscension = value; }
        }

        /// <summary>
        /// Get Set current TargetDeclination propery
        /// </summary>
        public static double TargetDeclination
        { 
            get { return m_TargetDeclination; }
            set { m_TargetDeclination = value; }
        }

        /// <summary>
        /// Get Set current TargetAltitude propery
        /// </summary>
        public static double TargetAltitude
        {
            get { return m_TargetAltitude; }
            set { m_TargetAltitude = value; }
        }

        /// <summary>
        /// Get Set current TargetAzimuth propery
        /// </summary>
        public static double TargetAzimuth
        {
            get { return m_TargetAzimuth; }
            set { m_TargetAzimuth = value; }
        }

        /// <summary>
        /// Get current SiderealTime propery
        /// retrieved from the latest polled value from the mount, no actual command is executed
        /// </summary>
        public static double SiderealTime
        { get { return m_SiderealTime; } }

        /// <summary>
        /// Get/Set current UTC propery
        /// </summary>
        public static DateTime UTCDate
        { 
            get 
            {
                try
                {
                    DateTime geminiDateTime = DateTime.Now ;
                    string l_Time = GeminiHardware.DoCommandResult(":GL", GeminiHardware.MAX_TIMEOUT, false);
                    string l_Date = GeminiHardware.DoCommandResult(":GC", GeminiHardware.MAX_TIMEOUT, false);
                    double l_TZOffset = double.Parse(GeminiHardware.DoCommandResult(":GG", GeminiHardware.MAX_TIMEOUT, false));
                    geminiDateTime = DateTime.ParseExact(l_Date + " " + l_Time, "MM/dd/yy HH:mm:ss", new System.Globalization.DateTimeFormatInfo()); // Parse to a local datetime using the given format
                    geminiDateTime = geminiDateTime.AddHours(l_TZOffset); // Add this to the local time to get a UTC date time
                    return geminiDateTime;
                }
                catch (Exception ex)
                { throw new ASCOM.DriverException("Error reading UTCDate: " + ex.ToString(), (int)SharedResources.SCODE_INVALID_VALUE); }; 
            }
            set 
            {
                int utc_offset_hours = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now).Hours;

                // set timezone from PC (gemini seems to want a '+' or a '-' in front of the hours, so make sure positive
                // number gets a '+' in front:
                string result = DoCommandResult(":SG" + (utc_offset_hours < 0? "+":"") + (-utc_offset_hours).ToString("0"), MAX_TIMEOUT, false);

                // compute civil time using whole hours only, since Gemini doesn't take fractions:
                DateTime civil = value + TimeSpan.FromHours(utc_offset_hours);

                string localTime = civil.ToString("HH:mm:ss");
                string localDate = civil.ToString("MM/dd/yy");
                result = DoCommandResult(":SC" + localDate, MAX_TIMEOUT, false);
                result = DoCommandResult(":SL" + localTime, MAX_TIMEOUT, false);
            }
        }

        /// <summary>
        /// SetLatitude Method
        /// Stores the Latitude in the Gemini Computer
        /// </summary>
        public static void SetLatitude(double Latitude)
        {         
            m_Latitude = Latitude;
            string latitudedddmm = m_Util.DegreesToDM(Latitude, "*", "");
            string result = DoCommandResult(":St" + latitudedddmm, MAX_TIMEOUT, false);
            if (result == null || result != "1")
                throw new ASCOM.Utilities.Exceptions.InvalidValueException("Latitidue not set");
        }

        /// <summary>
        /// SetLatitude Method
        /// Stores the Latitude in the Gemini Computer
        /// </summary>
        public static void SetLongitude(double Longitude)
        {
            m_Longitude = Longitude;
            string longitudedddmm = m_Util.DegreesToDM(-Longitude, "*", "");  // Gemini has the reverse notion of longitude sign: + for West, - for East
            string result = DoCommandResult(":Sg" + longitudedddmm, MAX_TIMEOUT, false);
            if (result == null || result != "1")
                throw new ASCOM.Utilities.Exceptions.InvalidValueException("Longitude not set");
        }

        /// <summary>
        /// Get current SiderealTime propery
        /// retrieved from the latest polled value from the mount, no actual command is executed
        /// </summary>
        public static string Velocity
        {
            get { return m_Velocity; }
            set { m_Velocity = value; }
        }


        public static bool Slewing
        {
            get
            {
                string res = DoCommandResult("<99:", MAX_TIMEOUT, false);
                int status;
                if (int.TryParse(res, out status))
                {
                    if ((status & 8) != 0) return true;
                }
                else
                    throw new TimeoutException("Slewing property");

                return false;
            }
        }
        /// <summary>
        /// Get current Status of Gemini Park
        /// retrieved from the latest polled value from the mount, no actual command is executed
        /// </summary>
        public static string ParkState
        { get { return m_ParkState; } }

        /// <summary>
        /// Get current SideOfPier propery
        /// retrieved from the latest polled value from the mount, no actual command is executed
        /// </summary>
        public static string SideOfPier
        { get { return m_SideOfPier; } }

        /// <summary>
        /// Get current Tracking propery
        /// returns whether the telescope is tracking
        /// </summary>
        public static bool Tracking
        { 
            get { return m_Tracking; }
        }

        /// <summary>
        /// Syncs the mount using Ra and Dec
        /// </summary>
        public static void SyncEquatorial()
        {
            SyncToEquatorialCoords(m_TargetRightAscension, m_TargetDeclination);
        }


        /// <summary>
        /// Syncs the mount using Ra and Dec coordinates passed in
        /// </summary>
        public static void SyncToEquatorialCoords(double ra, double dec)
        {
            string[] cmd = { ":Sr" + m_Util.HoursToHMS(ra, ":", ":", ""), ":Sd" + m_Util.DegreesToDMS(dec, ":", ":", ""), "" };
            if (m_AdditionalAlign)
            {
                cmd[2] = ":Cm";
            }
            else
            {
                cmd[2] = ":CM";
            }
            string[] result = null;
            DoCommandResult(cmd, MAX_TIMEOUT / 2, false, out result);
            if (result == null || result[0] == null || result[1] == null || result[2] == null)
                throw new TimeoutException((m_AdditionalAlign ? "Align to" : "Sync to ") + "RA/DEC");
            if (result[0] != "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("RA value is invalid");
            if (result[1] != "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("DEC value is invalid");
            if (result[2] == "No object!") throw new ASCOM.Utilities.Exceptions.InvalidValueException((m_AdditionalAlign ? "Align to" : "Sync to ") + "RA/DEC");
            
            m_RightAscension = ra;// Update state machine variables with new RA and DEC.
            m_Declination = dec;
        
        }


        /// <summary>
        /// Slews the mount using Ra and Dec
        /// </summary>
        public static void SlewEquatorial()
        {
            string[] cmd = { ":Sr" + m_Util.HoursToHMS(TargetRightAscension, ":", ":", ""), ":Sd" + m_Util.DegreesToDMS(TargetDeclination, ":", ":", ""), ":MS"};


            string [] result= null;

            DoCommandResult(cmd, MAX_TIMEOUT/2, false, out result);

            if (result == null || result[0] == null || result[1] == null || result[2] == null) throw new TimeoutException("SlewEquatorial");
            if (result[2] == "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew to object below horizon");
            if (result[2] == "4") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Position unreachable");
            if (result[2] == "5") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Mount not aligned");
            if (result[2] == "6") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew to outside of safety limits");
            if (result[0] != "1" || result[1] != "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew RA/DEC: Invalid coordinates");
        }
        /// <summary>
        /// Slews the mount using Ra and Dec
        /// </summary>
        public static void SlewEquatorialAsync()
        {
            string[] cmd = { ":Sr" + m_Util.HoursToHMS(TargetRightAscension, ":", ":", ""), ":Sd" + m_Util.DegreesToDMS(TargetDeclination, ":", ":", ""), ":MS" };

            string[] result = null;

            DoCommandResult(cmd, MAX_TIMEOUT/2, false, out result);

            if (result == null || result[0] == null || result[1] == null || result[2] == null) throw new TimeoutException("SlewEquatorialAsync");
            if (result[2] == "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew to object below horizon");
            if (result[2] == "4") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Position unreachable");
            if (result[2] == "5") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Mount not aligned");
            if (result[2] == "6") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew to outside of safety limits");
            if (result[0] != "1" || result[1] != "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("SlewAsync: Invalid coordinates");

        }


        public static void SyncHorizonCoordinates(double azimuth, double altitude)
        {
            string[] cmd = { ":Sz" + m_Util.DegreesToDMS(azimuth, ":", ":", ""), ":Sa" + m_Util.DegreesToDMS(altitude, ":", ":", ""), "" };
            if (m_AdditionalAlign)
            {
                cmd[2] = ":Cm";
            }
            else
            {
                cmd[2] = ":CM";
            }
            string[] result = null;
            DoCommandResult(cmd, MAX_TIMEOUT / 2, false, out result);
            if (result == null || result[0] == null || result[1] == null || result[2] == null)
                throw new TimeoutException((m_AdditionalAlign ? "Align to" : "Sync to ") + "Alt/Az");
            if (result[0] != "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Alt value is invalid");
            if (result[1] != "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Az value is invalid");
            if (result[2] == "No object!") throw new ASCOM.Utilities.Exceptions.InvalidValueException((m_AdditionalAlign ? "Align to" : "Sync to ") + "Alt/Az");

            m_Azimuth = azimuth; // Update state machine variables
            m_Altitude = altitude;
        }
        
        /// <summary>
        /// Syncs the mount using Alt and Az
        /// </summary>
        public static void SyncHorizon()
        {
            SyncHorizonCoordinates(TargetAzimuth, TargetAltitude);
        }

        /// <summary>
        /// Slews the mount using Alt and Az
        /// </summary>
        public static void SlewHorizon()
        {
            string[] cmd = { ":Sz" + m_Util.DegreesToDMS(TargetAzimuth, ":", ":", ""), ":Sa" + m_Util.DegreesToDMS(TargetAltitude, ":", ":", ""), ":MA" };
            string[] result = null;

            DoCommandResult(cmd, MAX_TIMEOUT / 2, false, out result);
            if (result == null || result[0] == null || result[1] == null || result[2] == null)
                throw new TimeoutException("Slew to Alt/Az");
            if (result[0] != "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Alt value is invalid");
            if (result[1] != "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Az value is invalid");
            if (result[2].StartsWith("1")) throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew to Alt/Az: Object below horizon");
            if (result[2].StartsWith("2")) throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew to Alt/Az: No object selected");
            if (result[2].StartsWith("3")) throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew to Alt/Az: Manual control");
        }

        /// <summary>
        /// Slews the mount using Alt and Az
        /// </summary>
        public static void SlewHorizonAsync()
        {
            string[] cmd = { ":Sz" + m_Util.DegreesToDMS(TargetAzimuth, ":", ":", ""), ":Sa" + m_Util.DegreesToDMS(TargetAltitude, ":", ":", ""), ":MA" };

            string[] result = null;
            DoCommandResult(cmd, MAX_TIMEOUT/2, false, out result);

            if (result==null || result[0] == null || result[1] == null || result[2] == null)
                throw new TimeoutException("Slew to Alt/Az");
            if (result[0] != "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Alt value is invalid");
            if (result[1] != "1") throw new ASCOM.Utilities.Exceptions.InvalidValueException("Az value is invalid");
            if (result[2].StartsWith("1")) throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew to Alt/Az: Object below horizon");
            if (result[2].StartsWith("2")) throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew to Alt/Az: No object selected");
            if (result[2].StartsWith("3")) throw new ASCOM.Utilities.Exceptions.InvalidValueException("Slew to Alt/Az: Manual control");
        }

        /// <summary>
        /// Set/Get PEC Status byte from Gemini
        ///  PEC status. Decimal
        ///     1: PEC active,
        ///     2: freshly trained (not yet altered) PEC data are available as current PEC data,
        ///     4: PEC training in progress,
        ///     8: PEC training was just completed,
        ///     16: PEC training will start soon,
        ///     32: PEC data are available.
        /// </summary>
        public static byte PECStatus
        {
            get
            {
                string s = DoCommandResult("<509:", 2000, false);
                byte res = 0;
                byte.TryParse(s, out res);
                return res;
            }
            set
            {
                DoCommand(">509:" + value.ToString(), false);            
            }
        }

        #endregion

        #region Focuser Implementation
        /// <summary>
        /// Focuser Reverse Directions
        /// </summary>
        public static bool ReverseDirection
        { 
            get { return m_ReverseDirection; }
            set
            {
                m_Profile.DeviceType = "Focuser";
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "ReverseDirection", value.ToString());
                m_ReverseDirection = value;
            }
        }

        /// <summary>
        /// Focuser Step Size
        /// </summary>
        public static int StepSize
        { 
            get { return m_StepSize; }
            set
            {
                m_Profile.DeviceType = "Focuser";
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "StepSize", value.ToString());
                m_StepSize = value;
            }
        }

        /// <summary>
        /// Focuser Brake Size
        /// </summary>
        public static int BrakeSize
        { 
            get { return m_BrakeSize; }
            set
            {
                m_Profile.DeviceType = "Focuser";
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "BrakeSize", value.ToString());
                m_BrakeSize = value;
            }
        }

        /// <summary>
        /// Focuser Speed
        /// </summary>
        public static int Speed
        { 
            get { return m_Speed; }
            set
            {
                m_Profile.DeviceType = "Focuser";
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "Speed", value.ToString());
                m_Speed = value;
            }
        }

        /// <summary>
        /// Focuser Maximum Increment
        /// </summary>
        public static int MaxIncrement
        { 
            get { return m_MaxIncrement; }
            set
            {
                m_Profile.DeviceType = "Focuser";
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "MaxIncrement", value.ToString());
                m_MaxIncrement = value;
            }
        }


        /// <summary>
        /// Focuser Maximum Step Size
        /// </summary>
        public static int MaxStep
        { 
            get { return m_MaxStep; }
            set
            {
                m_Profile.DeviceType = "Focuser";
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "MaxStep", value.ToString());
                m_MaxStep = value;
            }
        }


        /// <summary>
        /// Focuser Backlash Direction
        /// </summary>
        public static int BacklashDirection
        { 
            get { return m_BacklashDirection; }
            set
            {
                m_Profile.DeviceType = "Focuser";
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "BacklashDirection", value.ToString());
                m_BacklashDirection = value;
            }
        }

        /// <summary>
        /// Focuser Backlash Size
        /// </summary>
        public static int BacklashSize
        { 
            get { return m_BacklashSize; }
            set
            {
                m_Profile.DeviceType = "Focuser";
                m_Profile.WriteValue(SharedResources.FOCUSER_PROGRAM_ID, "BacklashSize", value.ToString());
                m_BacklashSize = value;
            }
        }

        #endregion

        /// <summary>
        /// Get the time the RA/DEC values were last updated from the mount
        /// </summary>
        public static DateTime LastUpdate
        { get { return m_LastUpdate; } }

        
        #region Helper Functions

        /// <summary>
        /// Finish off the formatting of a standard (LX200) command to be sent to the mount. Usually means appending '#' at the end.
        /// </summary>
        /// <param name="p">standard command to complete, not including '#' at the end</param>
        /// <returns>completed command to send to the mount</returns>
        private static string CompleteStandardCommand(string p)
        {
            if (p[0]!=':') p = ":"+p;
            return p+"#"; // standard commands end in '#' character, no checksum needed
        }

        /// <summary>
        /// Complete native Gemini command. Involves appending a checksum and a '#' at the end.
        /// </summary>
        /// <param name="p">command to complete</param>
        /// <returns>completed command to send to the mount</returns>
        private static string CompleteNativeCommand(string p)
        {
            return p + ComputeChecksum(p) + "#";
        }

        /// <summary>
        /// Gemini Native command checksum
        /// </summary>
        /// <param name="p">command to compute checksum for</param>
        /// <returns>Gemini checksum character</returns>
        private static char ComputeChecksum(string p)
        {
            int chksum = 0;

            for (int i = 0; i < p.Length; ++i)
                chksum ^= p[i];

            return (char)((chksum & 0x7f) + 0x40);
        }

        /// <summary>
        /// Add command item 'ci' to the queue for execution
        /// </summary>
        /// <param name="ci">actual command to queue</param>
        private static bool QueueCommand(CommandItem ci)
        {
            System.Diagnostics.Trace.WriteLine("Queue command..."+ci.m_Command);
            lock (m_CommandQueue)
            {
                if (!m_Connected) return false;
                m_CommandQueue.Enqueue(ci);
            }
            m_WaitForCommand.Set();     //signal to the background worker that commands are queued up
            return true;
        }

        /// <summary>
        /// Add all the command items in 'ci' to the queue for execution
        /// </summary>
        /// <param name="ci">array of commands to be executed in sequence</param>
        private static bool QueueCommands(CommandItem[] ci)
        {
            System.Diagnostics.Trace.Write("Queue commands...");
            foreach(CommandItem c in ci) System.Diagnostics.Trace.Write(c.m_Command + ", ");
            System.Diagnostics.Trace.WriteLine("");
            lock (m_CommandQueue)
            {
                if (!m_Connected) return false;
                for(int i=0; i<ci.Length; ++i)
                    m_CommandQueue.Enqueue(ci[i]);
            }
            m_WaitForCommand.Set();     //signal to the background worker that commands are queued up
            return true;
        }



        #endregion

    }
}
