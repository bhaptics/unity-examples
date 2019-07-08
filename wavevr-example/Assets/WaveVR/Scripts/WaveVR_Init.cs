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
using wvr;
using WaveVR_Log;

public class WaveVR_Init : MonoBehaviour
{
    private const string LOG_TAG = "WaveVR_Init";

    /// <summary>
    /// The singleton instance of the <see cref="WaveVR_Init"/> class, there only be one instance in a scene.
    /// </summary>
    public static WaveVR_Init Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<WaveVR_Init>();
                if (_instance == null)
                {
                    Log.d(LOG_TAG, "WaveVR_Init create an instance");
                    _instance = new GameObject("[WaveVR]").AddComponent<WaveVR_Init>();
                }
            }
            return _instance;
        }
    }
    private static WaveVR_Init _instance;

    void signalSurfaceState(string msg) {
        WaveVR_Render.signalSurfaceState(msg);
    }

    void Start()
    {
        if (WaveVR.Instance != null)
        {
            Log.i (LOG_TAG, "Start()", true);
            WaveVR.Instance.onLoadLevel ();
            WaveVR.Instance.SetConnectionStatus ();
            WaveVR.Instance.SetLeftHandedMode ();
        }
    }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        if (_instance != this)
        {
            Log.w(LOG_TAG, "Has another [WaveVR] object in a scene. Destory this.");
            Destroy(this);
            return;
        }

#if UNITY_EDITOR
        if (Application.isEditor) return;
#endif
        if (WaveVR.Instance != null)
        {
            Log.d(LOG_TAG, "Initialized");
        }
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Application.isEditor && !WaveVR.Instance.isSimulatorOn) return;
#endif

        bool ret = false;
        do
        {
            WVR_Event_t vrevent = new WVR_Event_t();
#if UNITY_EDITOR
            if (Application.isEditor) 
            {
                ret = WaveVR_Utils.WVR_PollEventQueue_S(ref vrevent);
            }
            else
#endif
            {
                ret = Interop.WVR_PollEventQueue(ref vrevent);
            }
            if (ret)
                processVREvent(vrevent);
        } while (ret);
    }

    void OnApplicationPause(bool pauseStatus)
    {
        Log.i (LOG_TAG, "OnApplicationPause() pauseStatus: " + pauseStatus);
        if (!pauseStatus)
        {
            // Application resume.
            if (WaveVR.Instance != null)
            {
                WaveVR.Instance.SetConnectionStatus ();
                // If system resumes and role changes, reset all button state.
                if (WaveVR.Instance.SetLeftHandedMode ())
                {
                    WaveVR_Utils.Event.Send (WaveVR_Utils.Event.DEVICE_ROLE_CHANGED);
                    WaveVR.Instance.ResetAllButtonStates ();
                } else
                {
                    WaveVR.Instance.ResetButtonEvents ();
                }
            }
        }
    }

    private void processVREvent(WVR_Event_t vrEvent)
    {
        WVR_DeviceType _type = vrEvent.device.type;
        WVR_InputId _btn = vrEvent.input.inputId;
        // Process events used by plugin
        switch ((WVR_EventType)vrEvent.common.type)
        {
            case WVR_EventType.WVR_EventType_IpdChanged:
                {
                    WaveVR_Utils.Event.Send("IpdChanged");
                    if (WaveVR_Render.Instance != null)
                        WaveVR_Render.Expand(WaveVR_Render.Instance);
                }
                break;
            case WVR_EventType.WVR_EventType_DeviceStatusUpdate:
               {
                    WaveVR_Utils.Event.Send("TrackedDeviceUpdated", vrEvent.device.common.type);
               }
               break;
            case WVR_EventType.WVR_EventType_BatteryStatusUpdate:
                {
                    WaveVR_Utils.Event.Send("BatteryStatusUpdate");
                }
                break;
        case WVR_EventType.WVR_EventType_LeftToRightSwipe:
        case WVR_EventType.WVR_EventType_RightToLeftSwipe:
        case WVR_EventType.WVR_EventType_DownToUpSwipe:
        case WVR_EventType.WVR_EventType_UpToDownSwipe:
            Log.i (LOG_TAG, "Swipe event: " + (WVR_EventType)vrEvent.common.type);
            WaveVR_Utils.Event.Send (WaveVR_Utils.Event.SWIPE_EVENT, vrEvent.common.type, _type);
            break;
        case WVR_EventType.WVR_EventType_DeviceRoleChanged:
            Log.i (LOG_TAG, "WVR_EventType_DeviceRoleChanged() " + _type + ", " + _btn + ", Resend connection notification after switching hand.");
            WaveVR.Instance.SetConnectionStatus ();
            if (WaveVR.Instance.SetLeftHandedMode ())
            {
                WaveVR_Utils.Event.Send (WaveVR_Utils.Event.DEVICE_ROLE_CHANGED);
                WaveVR.Instance.ResetAllButtonStates ();
            }
            break;
        case WVR_EventType.WVR_EventType_ButtonPressed:
            Log.d (LOG_TAG, "WVR_EventType_ButtonPressed() " + _type + ", " + _btn + ", left-handed? " + WaveVR_Controller.IsLeftHanded);
            if (_type != WVR_DeviceType.WVR_DeviceType_Invalid && WaveVR.Instance != null)
                _type = WaveVR.Instance.getDeviceByType (_type).type;
            WaveVR_Controller.Input (_type).SetEventState_Press (_btn, true);
            break;
        case WVR_EventType.WVR_EventType_ButtonUnpressed:
            Log.d (LOG_TAG, "WVR_EventType_ButtonUnpressed() " + _type + ", " + _btn + ", left-handed? " + WaveVR_Controller.IsLeftHanded);
            if (_type != WVR_DeviceType.WVR_DeviceType_Invalid && WaveVR.Instance != null)
                _type = WaveVR.Instance.getDeviceByType (_type).type;
            WaveVR_Controller.Input (_type).SetEventState_Press (_btn, false);
            break;
        case WVR_EventType.WVR_EventType_TouchTapped:
            Log.d (LOG_TAG, "WVR_EventType_TouchTapped() " + _type + ", " + _btn + ", left-handed? " + WaveVR_Controller.IsLeftHanded);
            if (_type != WVR_DeviceType.WVR_DeviceType_Invalid && WaveVR.Instance != null)
                _type = WaveVR.Instance.getDeviceByType (_type).type;
            WaveVR_Controller.Input (_type).SetEventState_Touch (_btn, true);
            break;
        case WVR_EventType.WVR_EventType_TouchUntapped:
            Log.d (LOG_TAG, "WVR_EventType_TouchUntapped() " + _type + ", " + _btn + ", left-handed? " + WaveVR_Controller.IsLeftHanded);
            if (_type != WVR_DeviceType.WVR_DeviceType_Invalid && WaveVR.Instance != null)
                _type = WaveVR.Instance.getDeviceByType (_type).type;
            WaveVR_Controller.Input (_type).SetEventState_Touch (_btn, false);
            break;
        case WVR_EventType.WVR_EventType_DeviceConnected:
            Log.d (LOG_TAG, "WVR_EventType_DeviceConnected() " + _type + ", left-handed? " + WaveVR_Controller.IsLeftHanded);
            WaveVR.Instance.SetConnectionStatus (_type, true);
            break;
        case WVR_EventType.WVR_EventType_DeviceDisconnected:
            Log.d (LOG_TAG, "WVR_EventType_DeviceDisconnected() " + _type + ", left-handed? " + WaveVR_Controller.IsLeftHanded);
            WaveVR.Instance.SetConnectionStatus (_type, false);
            break;
        default:
            break;
        }

        // Send event to developer for all kind of event if developer don't want to add callbacks for every event.
        WaveVR_Utils.Event.Send(WaveVR_Utils.Event.ALL_VREVENT, vrEvent);

        // Send event to developer by name.
        WaveVR_Utils.Event.Send(vrEvent.common.type.ToString(), vrEvent);
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}

