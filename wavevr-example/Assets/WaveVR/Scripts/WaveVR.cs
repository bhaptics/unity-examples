// "WaveVR SDK 
// © 2017 HTC Corporation. All Rights Reserved.
//
// Unless otherwise required by copyright law and practice,
// upon the execution of HTC SDK license agreement,
// HTC grants you access to and use of the WaveVR SDK(s).
// You shall fully comply with all of HTC’s SDK license agreement terms and
// conditions signed by you and all SDK and API requirements,
// specifications, and documentation provided by HTC to You."

using UnityEngine;
using UnityEngine.Assertions;
using wvr;
using WaveVR_Log;
using System;
using System.Runtime.InteropServices;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WaveVR : System.IDisposable
{
    [SerializeField]
    public bool editor = false;
#if UNITY_EDITOR
    public bool EnableSimulator = false;
    public bool isSimulatorOn = false;
    private const string MENU_NAME = "WaveVR/Simulator/Enable Simulator";
#endif

    private static string LOG_TAG = "WVR_WaveVR";
    private void PrintDebugLog(string msg)
    {
        Log.d (LOG_TAG, msg, true);
    }

    private void PrintInfoLog(string msg)
    {
        Log.i (LOG_TAG, msg, true);
    }

    private void PrintErrorLog(string msg)
    {
        Log.e (LOG_TAG, msg, true);
    }

    public static WaveVR Instance
    {
        get
        {
            if (instance == null)
            {
                instance = /*Application.isEditor ? null : */new WaveVR ();
                #if UNITY_EDITOR
                if (Application.isEditor)
                {
                    if (instance.EnableSimulator && !instance.Initialized)
                        instance = null;
                } else
                #endif
                {
                    if (!instance.Initialized)
                        instance = null;
                }
            }
            return instance;
        }
    }
    private static WaveVR instance = null;
    public bool Initialized = false;

    public bool FocusCapturedBySystem = false;

    public class Device
    {
        public Device(WVR_DeviceType type)
        {
            this.type = type;
            for (int i = 0; i < DeviceTypes.Length; i++)
            {
                if (DeviceTypes[i] == type)
                {
                    index = i;
                    break;
                }
            }
        }
        public WVR_DeviceType type { get; private set; }
        public int index { get; private set; }
        public bool connected { get { return instance.connected[index]; } }
        public WVR_DevicePosePair_t pose { get { return instance.poses[instance.deviceIndexMap[index]]; } }
        public WaveVR_Utils.RigidTransform rigidTransform { get { return instance.rtPoses[instance.deviceIndexMap[index]]; } }
    }

    public Device hmd { get; private set; }
    public Device controllerLeft { get; private set; }
    public Device controllerRight { get; private set; }

    public Device getDeviceByType(WVR_DeviceType type)
    {
        switch (type)
        {
        case WVR_DeviceType.WVR_DeviceType_HMD:
            return hmd;
        case WVR_DeviceType.WVR_DeviceType_Controller_Right:
            return WaveVR_Controller.IsLeftHanded ? controllerLeft : controllerRight;
        case WVR_DeviceType.WVR_DeviceType_Controller_Left:
            return WaveVR_Controller.IsLeftHanded ? controllerRight : controllerLeft;
        default:
            Assert.raiseExceptions = true;
            return hmd;  // Should not happen
        }
    }

    public Device getDeviceByType(WaveVR_Controller.EDeviceType type)
    {
        switch (type)
        {
        case WaveVR_Controller.EDeviceType.Head:
            return hmd;
        case WaveVR_Controller.EDeviceType.Dominant:
            return WaveVR_Controller.IsLeftHanded ? controllerLeft : controllerRight;
        case WaveVR_Controller.EDeviceType.NonDominant:
            return WaveVR_Controller.IsLeftHanded ? controllerRight : controllerLeft;
        default:
            Assert.raiseExceptions = true;
            return hmd;  // Should not happen
        }
    }

    private static void ReportError(WVR_InitError error)
    {
        switch (error)
        {
            case WVR_InitError.WVR_InitError_None:
                break;
            case WVR_InitError.WVR_InitError_NotInitialized:
                Log.e(LOG_TAG, "WaveVR: Not initialized");
                Application.Quit();
                break;
            case WVR_InitError.WVR_InitError_Unknown:
                Log.e(LOG_TAG, "WaveVR: Unknown error during initializing");
                break;
            default:
                //TODO Log.e(LOG_TAG, Interop.WVR_GetErrorString(error));
                break;
        }
    }

    [System.Obsolete("Please check WaveVR.Instance directly")]
    public static bool Hmd
    {
        get
        {
            return Instance != null;
        }
    }

    public static WVR_DeviceType[] DeviceTypes = new WVR_DeviceType[]{
        WVR_DeviceType.WVR_DeviceType_HMD,
        WVR_DeviceType.WVR_DeviceType_Controller_Right,
        WVR_DeviceType.WVR_DeviceType_Controller_Left
    };

    public bool[] connected = new bool[DeviceTypes.Length];
    public uint[] deviceIndexMap = new uint[DeviceTypes.Length];  // Mapping from DeviceTypes's index to poses's index

    private WVR_DevicePosePair_t[] poses = new WVR_DevicePosePair_t[DeviceTypes.Length];  // HMD, R, L controllers.
    private WaveVR_Utils.RigidTransform[] rtPoses = new WaveVR_Utils.RigidTransform[DeviceTypes.Length];

    private WaveVR()
    {
        PrintInfoLog ("WaveVR()+ commit: " + WaveVR_COMMITINFO.wavevr_version);
#if UNITY_EDITOR
        if (Application.isEditor)
        {
            this.EnableSimulator = EditorPrefs.GetBool(MENU_NAME, false);
            if (this.EnableSimulator)
            {
                try
                {
                    string ipaddr = "";
                    //WaveVR_Utils.SIM_ConnectType type = WaveVR_Utils.SIM_ConnectType.SIM_ConnectType_USB;
                    System.IntPtr ptrIPaddr = Marshal.StringToHGlobalAnsi (ipaddr);
                    WaveVR_Utils.WVR_SetPrintCallback_S (WaveVR_Utils.PrintLog);
                    WaveVR_Utils.SIM_InitError error = WaveVR_Utils.WVR_Init_S (0, ptrIPaddr);
                    PrintInfoLog ("WaveVR() WVR_Init_S = " + error);

                    if (error != 0)
                    {
                        WaveVR_Utils.WVR_Quit_S ();
                        PrintErrorLog ("WaveVR() initialize simulator failed, WVR_Quit_S()");
                        return;
                    }
                    isSimulatorOn = true;
                } catch (Exception e)
                {
                    PrintErrorLog ("WaveVR() initialize simulator failed, exception: " + e);
                    return;
                }
            }
        }
        else 
#endif
        {
            WVR_InitError error = Interop.WVR_Init(WVR_AppType.WVR_AppType_VRContent);
            if (error != WVR_InitError.WVR_InitError_None)
            {
                ReportError(error);
                Interop.WVR_Quit();
                PrintErrorLog ("WaveVR() initialize simulator failed, WVR_Quit()");
                return;
            }
            WaveVR_Utils.notifyActivityUnityStarted();
        }

        this.Initialized = true;
        PrintInfoLog ("WaveVR() initialization succeeded.");

        for (int i = 0; i < 3; i++)
        {
            poses[i] = new WVR_DevicePosePair_t();
            connected[i] = false; // force update connection status to all listener.
            deviceIndexMap[i] = 0;  // use hmd's id as default.
        }

        hmd = new Device(WVR_DeviceType.WVR_DeviceType_HMD);
        controllerLeft = new Device(WVR_DeviceType.WVR_DeviceType_Controller_Left);
        controllerRight = new Device(WVR_DeviceType.WVR_DeviceType_Controller_Right);

        // Check left-handed mode first, then set connection status according to left-handed mode.
        SetLeftHandedMode ();
        SetConnectionStatus ();
        SetDefaultButtons ();

        PrintInfoLog ("WaveVR()-");
    }

    ~WaveVR()
    {
        Dispose();
    }

    public void onLoadLevel()
    {
        Log.i (LOG_TAG, "onLoadLevel() reset all connection");
        for (int i = 0; i < DeviceTypes.Length; i++)
        {
            poses[i] = new WVR_DevicePosePair_t();
            connected[i] = false; // force update connection status to all listener.
        }
    }

    private void UpdateConnection(WVR_DeviceType dt, bool conn)
    {
        for (int i = 0; i < DeviceTypes.Length; i++)
        {
            if (DeviceTypes [i] == dt)
            {
                connected [i] = conn;
                Log.d (LOG_TAG, "UpdateConnection() set " + dt + " pose to " + (conn ? "valid." : "invalid."), true);
                break;
            }
        }
    }

#if UNITY_EDITOR
    public static void EndSimulator()
    {
        if (WaveVR.Instance != null && WaveVR.Instance.isSimulatorOn)
        {
            WaveVR_Utils.WVR_Quit_S();
            WaveVR.Instance.isSimulatorOn = false;
        }
    }
#endif

    public void Dispose()
    {
#if UNITY_EDITOR
        EndSimulator();
#else
        Interop.WVR_Quit();
#endif
        Debug.Log("WVR_Quit");
        instance = null;
        System.GC.SuppressFinalize(this);
    }

    // Use this interface to avoid accidentally creating the instance 
    // in the process of attempting to dispose of it.
    public static void SafeDispose()
    {
        if (instance != null)
            instance.Dispose();
    }

    // Use this interface to check what kind of dof is running
    public int is6DoFTracking()
    {
        if (!this.Initialized)
            return 0;

        WVR_NumDoF dof = Interop.WVR_GetDegreeOfFreedom(WVR_DeviceType.WVR_DeviceType_HMD);

        if (dof == WVR_NumDoF.WVR_NumDoF_6DoF)
            return 6;  // 6 DoF
        else if (dof == WVR_NumDoF.WVR_NumDoF_3DoF)
            return 3;  // 3 DoF
        else
            return 0;  // abnormal case
    }

    public void UpdatePoses(WVR_PoseOriginModel origin)
    {
        UpdatePoses(origin, false);
    }

    public void UpdatePoses(WVR_PoseOriginModel origin, bool isSimulator)
    {
        if (!this.Initialized)
            return;

        Log.gpl.d(LOG_TAG, "UpdatePoses");

#if UNITY_EDITOR
        if (Application.isEditor)
        {
            if (isSimulator)
            {
                WaveVR_Utils.WVR_GetSyncPose_S((int)origin, poses, poses.Length);
            }
        }
        else
#endif
        {
            bool _focusCapturedBySystem = Interop.WVR_IsInputFocusCapturedBySystem ();
            if (this.FocusCapturedBySystem != _focusCapturedBySystem)
            {
                this.FocusCapturedBySystem = _focusCapturedBySystem;
                WaveVR_Utils.Event.Send (WaveVR_Utils.Event.SYSTEMFOCUS_CHANGED, this.FocusCapturedBySystem);

                // When getting system focus again, reset button events.
                if (!this.FocusCapturedBySystem)
                {
                    Log.i (LOG_TAG, "UpdatePoses() get system focus, reset button events.");
                    ResetButtonEvents ();
                } else
                {
                    Log.i (LOG_TAG, "UpdatePoses() lost system focus.");
                }
            }
            Interop.WVR_GetSyncPose(origin, poses, (uint)poses.Length);
        }

        for (uint i = 0; i < DeviceTypes.Length; i++)
        {
            bool _hasType = false;

            for (uint j = 0; j < poses.Length; j++)
            {
                WVR_DevicePosePair_t _pose = poses[j];

                if (_pose.type == DeviceTypes [i])
                {
                    _hasType = true;
                    deviceIndexMap[i] = j;

                    if (connected [i] != _pose.pose.IsValidPose)
                    {
                        connected [i] = _pose.pose.IsValidPose;
                        Log.i (LOG_TAG, "device " + DeviceTypes [i] + " is " + (connected [i] ? "connected" : "disconnected"));
                        WaveVR_Utils.Event.Send(WaveVR_Utils.Event.DEVICE_CONNECTED, DeviceTypes [i], connected[i]);
                    }

                    if (connected [i])
                    {
                        rtPoses[j].update(_pose.pose.PoseMatrix);
                    }

                    break;
                }
            }

            // no such type
            if (!_hasType)
            {
                if (connected [i] == true)
                {
                    connected [i] = false;
                    Log.i (LOG_TAG, "device " + DeviceTypes [i] + " is disconnected.");
                    WaveVR_Utils.Event.Send(WaveVR_Utils.Event.DEVICE_CONNECTED, DeviceTypes [i], connected[i]);
                }
            }
        }

        for (int i = 0; i < poses.Length; i++)
        {
            WVR_DeviceType _type = poses [i].type;
            bool _connected = false;
#if UNITY_EDITOR
            if (isSimulator)
            {
                _connected = WaveVR_Utils.WVR_IsDeviceConnected_S((int)_type);
            }
            else
#endif
            {
                _connected = Interop.WVR_IsDeviceConnected(_type);
            }
            
            bool _posevalid = poses [i].pose.IsValidPose;

            Log.gpl.d (LOG_TAG, "Device " + _type + " is " + (_connected ? "connected" : "disconnected")
                + ", pose is " + (_posevalid ? "valid" : "invalid")
                + ", pos: {" + rtPoses [i].pos.x + ", " + rtPoses [i].pos.y + ", " + rtPoses [i].pos.z + "}"
                + ", rot: {" + rtPoses [i].rot.x + ", " + rtPoses [i].rot.y + ", " + rtPoses [i].rot.z + ", " + rtPoses [i].rot.w + "}");
        }

        try
        {
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.NEW_POSES, poses, rtPoses);
        }
        catch (Exception ex)
        {
            Log.e(LOG_TAG, "Send NEW_POSES Event Exception : " + ex);
        }
        Log.gpl.d(LOG_TAG, "after new poses");
        try
        {
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.AFTER_NEW_POSES);
        }
        catch (Exception ex)
        {
            Log.e(LOG_TAG, "Send AFTER_NEW_POSES Event Exception : " + ex);
        }
    }

    public int SetQualityLevel(int level, bool applyExpensiveChanges = true)
    {
        return WaveVR_Render.Instance.SetQualityLevel(level, applyExpensiveChanges);
    }

    #region WaveVR_Controller status
    public bool SetLeftHandedMode(bool leftHandedInEditor = false)
    {
        if (!this.Initialized)
            return false;

        bool _changed = false, _lefthanded = false;
        #if UNITY_EDITOR
        if (Application.isEditor)
        {
            _lefthanded = leftHandedInEditor;
        } else
        #endif
        {
            _lefthanded = Interop.WVR_GetDefaultControllerRole () == WVR_DeviceType.WVR_DeviceType_Controller_Left ? true : false;
        }

        if (WaveVR_Controller.IsLeftHanded != _lefthanded)
        {
            _changed = true;

            PrintInfoLog ("SetLeftHandedMode() Set left-handed mode to " + _lefthanded);
            WaveVR_Controller.SetLeftHandedMode (_lefthanded);
        } else
        {
            PrintInfoLog ("SetLeftHandedMode() not change default role: " + (_lefthanded ? "LEFT." : "RIGHT."));
        }

        return _changed;
    }

    public void SetConnectionStatus(WVR_DeviceType type, bool conn)
    {
        if (type == WVR_DeviceType.WVR_DeviceType_Invalid)
            return;

        WVR_DeviceType _type = WaveVR_Controller.Input (type).DeviceType;
        WaveVR_Controller.Input (_type).connected = conn;
    }

    public void SetConnectionStatus()
    {
        if (!this.Initialized)
            return;

        bool _connH = false, _connR = false, _connL = false;
        #if UNITY_EDITOR
        if (Application.isEditor)
        {
            _connH = true;
            _connR = true;
            _connL = true;
        } else
        #endif
        {
            _connH = Interop.WVR_IsDeviceConnected (WVR_DeviceType.WVR_DeviceType_HMD);
            if (!_connH)
                UpdateConnection (WVR_DeviceType.WVR_DeviceType_HMD, false);
            _connR = Interop.WVR_IsDeviceConnected (WVR_DeviceType.WVR_DeviceType_Controller_Right);
            if (!_connR)
                UpdateConnection (WVR_DeviceType.WVR_DeviceType_Controller_Right, false);
            _connL = Interop.WVR_IsDeviceConnected (WVR_DeviceType.WVR_DeviceType_Controller_Left);
            if (!_connL)
                UpdateConnection (WVR_DeviceType.WVR_DeviceType_Controller_Left, false);
        }

        Log.i (LOG_TAG, "SetConnectionStatus() Set connection HEAD: " + _connH + ", RIGHT: " + _connR + ", LEFT: " + _connL, true);
        SetConnectionStatus (WVR_DeviceType.WVR_DeviceType_HMD, _connH);
        SetConnectionStatus (WVR_DeviceType.WVR_DeviceType_Controller_Right, _connR);
        SetConnectionStatus (WVR_DeviceType.WVR_DeviceType_Controller_Left, _connL);
    }

    public void ResetButtonEvents()
    {
        if (!this.Initialized)
            return;

        Log.i (LOG_TAG, "ResetButtonEvents() Reset button events.");
        foreach (WVR_DeviceType _type in WaveVR.DeviceTypes)
        {
            WaveVR_Controller.Input (_type).ResetButtonEvents ();
        }
    }

    public void ResetAllButtonStates()
    {
        Log.i (LOG_TAG, "ResetAllButtonStates() Reset button states.");
        foreach (WVR_DeviceType _type in WaveVR.DeviceTypes)
        {
            WaveVR_Controller.Input (_type).ResetAllButtonStates ();
        }
    }

    public void SetDefaultButtons()
    {
        if (!this.Initialized)
            return;

        #if UNITY_EDITOR
        if (Application.isEditor)
            return;
        #endif

        Log.i (LOG_TAG, "SetDefaultButtons()");

        WVR_InputAttribute_t[] inputAttribtues_hmd = new WVR_InputAttribute_t[1];
        inputAttribtues_hmd [0].id = WVR_InputId.WVR_InputId_Alias1_Enter;
        inputAttribtues_hmd [0].capability = (uint)WVR_InputType.WVR_InputType_Button;
        inputAttribtues_hmd [0].axis_type = WVR_AnalogType.WVR_AnalogType_None;

        WVR_DeviceType _type = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.Head).DeviceType;
        bool _ret = Interop.WVR_SetInputRequest (_type, inputAttribtues_hmd, (uint)inputAttribtues_hmd.Length);
        if (_ret)
        {
            uint inputTableSize = (uint)WVR_InputId.WVR_InputId_Max;
            WVR_InputMappingPair_t[] inputTable = new WVR_InputMappingPair_t[inputTableSize];
            uint _size = Interop.WVR_GetInputMappingTable (_type, inputTable, inputTableSize);
            if (_size > 0)
            {
                for (int _i = 0; _i < (int)_size; _i++)
                {
                    PrintDebugLog ("SetDefaultButtons() " + _type
                        + " button: " + inputTable [_i].source.id
                        + " is mapping to HMD input ID: " + inputTable [_i].destination.id);
                }
            }
        }
    }
    #endregion
}

