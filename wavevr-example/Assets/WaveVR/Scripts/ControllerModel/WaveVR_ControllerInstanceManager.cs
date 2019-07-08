using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using wvr;
using WaveVR_Log;
using UnityEngine.EventSystems;

public class WaveVR_ControllerInstanceManager : MonoBehaviour {
    private static string LOG_TAG = "WaveVR_ControllerInstanceManager";

    [System.Serializable]
    private class ControllerInstance
    {
        public WVR_DeviceType type;
        public GameObject instance;
        public int index;
        public bool eventEnabled;
        public bool ShowBeam;
        public bool showPointer;
    }

    private enum CComponent
    {
        Beam,
        ControllerPointer
    };

    private static WaveVR_ControllerInstanceManager instance = null;
    public static WaveVR_ControllerInstanceManager Instance
    {
        get
        {
            if (instance == null)
            {
                Log.i(LOG_TAG, "Instance, create WaveVR_ControllerInstanceManager GameObject");
                var gameObject = new GameObject("WaveVR_ControllerInstanceManager");
                instance = gameObject.AddComponent<WaveVR_ControllerInstanceManager>();
                // This object should survive all scene transitions.
                GameObject.DontDestroyOnLoad(instance);
            }
            return instance;
        }
    }

    private int ControllerIdx = 0;
    private GameObject eventSystem = null;
    private List<ControllerInstance> ctrInstanceList = new List<ControllerInstance>();
    private WVR_DeviceType systemEventType = WVR_DeviceType.WVR_DeviceType_Controller_Right;
    private WVR_DeviceType lastEventType = WVR_DeviceType.WVR_DeviceType_Invalid;
    private bool isFocusCapturedBySystemLastFrame = false;
    private bool isCtrInstanceUpdated = false;

    /// <summary>
    /// Variables to check if set emitter.
    /// </summary>
    private bool toSetActiveOfEmitter = false;
    private bool connectionUpdated = false;
    private bool rConnected = false, lConnected = false;

    private void PrintDebugLog(string msg)
    {
        Log.d(LOG_TAG, msg, true);
    }

    private void PrintIntervalLog(Log.PeriodLog.StringProcessDelegate del)
    {
        Log.gpl.d(LOG_TAG, del, true);
    }

    private bool getEventSystemParameter(WVR_DeviceType type)
    {
        bool ret = false;
        if (EventSystem.current == null)
        {
            EventSystem _es = FindObjectOfType<EventSystem>();
            if (_es != null)
            {
                eventSystem = _es.gameObject;
            }
        }
        else
        {
            eventSystem = EventSystem.current.gameObject;
        }

        if (eventSystem != null)
        {
            WaveVR_ControllerInputModule wcim = eventSystem.GetComponent<WaveVR_ControllerInputModule>();

            if (wcim != null)
            {
                if (type == WVR_DeviceType.WVR_DeviceType_Controller_Right)
                {
                    ret = wcim.DomintEventEnabled;
                    PrintDebugLog("getEventSystemParameter() DomintEventEnabled is " + ret);
                }
                else if (type == WVR_DeviceType.WVR_DeviceType_Controller_Left)
                {
                    ret = wcim.NoDomtEventEnabled;
                    PrintDebugLog("getEventSystemParameter() NoDomtEventEnabled is " + ret);
                }

            }
        }

        return ret;
    }

    private bool getComponentParameter(GameObject controller, CComponent comp)
    {
        bool ret = false;

        var ch = controller.transform.childCount;

        for (int i = 0; i < ch; i++)
        {
            GameObject child = controller.transform.GetChild(i).gameObject;

            if (comp == CComponent.Beam)
            {
                WaveVR_Beam wb = child.GetComponentInChildren<WaveVR_Beam>();
                if (wb != null)
                {
                    ret = wb.ShowBeam;
                    PrintDebugLog("getComponentParameter() wb.ShowBeam is " + ret);
                    break;
                }
            }
            else if (comp == CComponent.ControllerPointer)
            {
                WaveVR_ControllerPointer wcp = child.GetComponentInChildren<WaveVR_ControllerPointer>();
                if (wcp != null)
                {
                    ret = wcp.ShowPointer;
                    PrintDebugLog("getComponentParameter() wcp.ShowPointer is " + ret);
                    break;
                }
            }
        }

        return ret;
    }

    public int registerControllerInstance(WVR_DeviceType type, GameObject controller)
    {
        PrintDebugLog ("registerControllerInstance() " + type + ", controller: " + (controller != null ? controller.name : "null"));
        if (type != WVR_DeviceType.WVR_DeviceType_Controller_Left && type != WVR_DeviceType.WVR_DeviceType_Controller_Right)
        {
            PrintDebugLog("registerControllerInstance, type is not allowed");
            return 0;
        }

        if (controller == null)
        {
            PrintDebugLog("registerControllerInstance, controller is null");
            return 0;
        }

        ControllerIdx++;

        ControllerInstance t = new ControllerInstance();
        t.type = type;
        t.instance = controller;
        t.index = ControllerIdx;

        t.eventEnabled = getEventSystemParameter(type);
        t.ShowBeam = getComponentParameter(controller, CComponent.Beam);
        t.showPointer = getComponentParameter(controller, CComponent.ControllerPointer);

        ctrInstanceList.Add(t);
        this.isCtrInstanceUpdated = true;
        PrintDebugLog("registerControllerInstance, add controller index: " + t.index + ", type: " + t.type + ", name: " + t.instance.name
            + ", event able: " + t.eventEnabled + ", ShowBeam: " + t.ShowBeam + ", showPointer: " + t.showPointer);

        return ControllerIdx;
    }

    public void removeControllerInstance(int index)
    {
        ControllerInstance waitforRemove = null;
        foreach (ControllerInstance t in ctrInstanceList)
        {
            if (t.index == index)
            {
                PrintDebugLog("removeControllerInstance, remove controller index: " + t.index + ", type: " + t.type);
                waitforRemove = t;
            }
        }

        if (waitforRemove != null)
        {
            ctrInstanceList.Remove (waitforRemove);
            this.isCtrInstanceUpdated = true;
        }
    }

    private void onDeviceConnected(params object[] args)
    {
        WVR_DeviceType _type = (WVR_DeviceType)args [0];
        bool _connected = (bool)args [1];
        PrintDebugLog ("onDeviceConnected() device " + _type + " is " + (_connected ? "connected." : "disconnected.") + ", left-handed? " + WaveVR_Controller.IsLeftHanded);

        WaveVR.Device _rdev = WaveVR.Instance.getDeviceByType (WVR_DeviceType.WVR_DeviceType_Controller_Right);
        WaveVR.Device _ldev = WaveVR.Instance.getDeviceByType (WVR_DeviceType.WVR_DeviceType_Controller_Left);

        if (_type == _rdev.type)
        {
            this.rConnected = _rdev.connected;
            this.connectionUpdated = true;
            PrintDebugLog ("onDeviceConnected() rConnected: " + this.rConnected);
        }
        if (_type == _ldev.type)
        {
            this.lConnected = _ldev.connected;
            this.connectionUpdated = true;
            PrintDebugLog ("onDeviceConnected() lConnected: " + this.lConnected);
        }
    }

    private void onDeviceRoleChanged(params object[] args)
    {
        if (WaveVR.Instance != null)
        {
            WaveVR.Device _rdev = WaveVR.Instance.getDeviceByType (WVR_DeviceType.WVR_DeviceType_Controller_Right);
            if (_rdev != null)
                this.rConnected = _rdev.connected;
            WaveVR.Device _ldev = WaveVR.Instance.getDeviceByType (WVR_DeviceType.WVR_DeviceType_Controller_Left);
            if (_ldev != null)
                this.lConnected = _ldev.connected;

            this.connectionUpdated = true;
        }
        PrintDebugLog ("onDeviceRoleChanged() rConnected: " + this.rConnected + ", lConnected: " + this.lConnected);
    }

    #region Monobehaviour overrides
    void OnEnable()
    {
        WaveVR_Utils.Event.Listen (WaveVR_Utils.Event.DEVICE_CONNECTED, onDeviceConnected);
        WaveVR_Utils.Event.Listen (WaveVR_Utils.Event.DEVICE_ROLE_CHANGED, onDeviceRoleChanged);
        checkControllerConnected ();
    }

    void OnDisable()
    {
        WaveVR_Utils.Event.Remove (WaveVR_Utils.Event.DEVICE_CONNECTED, onDeviceConnected);
        WaveVR_Utils.Event.Remove (WaveVR_Utils.Event.DEVICE_ROLE_CHANGED, onDeviceRoleChanged);
    }

    // Use this for initialization
    void Start()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
#endif
        // should get current system focus device type
        systemEventType = WaveVR_Utils.WVR_GetFocusedController ();
        PrintDebugLog ("Start() Focus controller: " + systemEventType);
    }

    void OnDestroy()
    {
        PrintDebugLog("OnDestroy");
    }

    void OnApplicationPause(bool pauseStatus)
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
#endif
        if (!pauseStatus) // resume
        {
            systemEventType = WaveVR_Utils.WVR_GetFocusedController();
            PrintDebugLog("Application resume, Focus controller: " + systemEventType);
            isFocusCapturedBySystemLastFrame = false;
            this.toSetActiveOfEmitter = true;
        }
    }

    // Update is called once per frame
    void Update () {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
#endif
        if (Interop.WVR_IsInputFocusCapturedBySystem())
        {
            if (!isFocusCapturedBySystemLastFrame)
            {
                foreach (ControllerInstance t in ctrInstanceList)
                {
                    bool ret = ForceSetActiveOfEmitter(t, false);
                }
            }
            isFocusCapturedBySystemLastFrame = true;

            return;
        }

        if (isFocusCapturedBySystemLastFrame)
        {
            systemEventType = WaveVR_Utils.WVR_GetFocusedController();
            PrintDebugLog("back from overlay() Focus controller: " + systemEventType);
            isFocusCapturedBySystemLastFrame = false;
            this.toSetActiveOfEmitter = true;
        }

        if (this.ctrInstanceList.Count < 1)
        {
            this.lastEventType = WVR_DeviceType.WVR_DeviceType_Invalid;
            return;
        }

        PrintIntervalLog(() => "Controller instance: " + ctrInstanceList.Count + ", Focus controller: " + systemEventType);

        if (this.ctrInstanceList.Count == 1)
        {
            ControllerInstance t = ctrInstanceList [0];
            this.systemEventType = t.type;
            if (this.isCtrInstanceUpdated)
            {
                this.isCtrInstanceUpdated = !this.isCtrInstanceUpdated;
                PrintDebugLog ("Update() Controller focus changes to " + this.systemEventType);
            }
        } else // count > 1
        {
            if (this.systemEventType == WVR_DeviceType.WVR_DeviceType_Controller_Right)
            {
                if (WaveVR_Controller.Input (WVR_DeviceType.WVR_DeviceType_Controller_Left).GetPressUp (WVR_InputId.WVR_InputId_Alias1_Digital_Trigger) ||
                    WaveVR_Controller.Input (WVR_DeviceType.WVR_DeviceType_Controller_Left).GetPressUp (WVR_InputId.WVR_InputId_Alias1_Trigger))
                {
                    this.systemEventType = WVR_DeviceType.WVR_DeviceType_Controller_Left;
                    PrintDebugLog ("Update() Controller focus changes from Right to Left, set to runtime.");
                }
            }
            if (this.systemEventType == WVR_DeviceType.WVR_DeviceType_Controller_Left)
            {
                // Listen to right
                if (WaveVR_Controller.Input (WVR_DeviceType.WVR_DeviceType_Controller_Right).GetPressUp (WVR_InputId.WVR_InputId_Alias1_Digital_Trigger) ||
                    WaveVR_Controller.Input (WVR_DeviceType.WVR_DeviceType_Controller_Right).GetPressUp (WVR_InputId.WVR_InputId_Alias1_Trigger))
                {
                    this.systemEventType = WVR_DeviceType.WVR_DeviceType_Controller_Right;
                    PrintDebugLog ("Update() Controller focus changes from Left to Right, set to runtime.");
                }
            }
        }

        if (this.lastEventType != this.systemEventType || this.connectionUpdated)
        {
            this.lastEventType = this.systemEventType;
            PrintDebugLog ("Update() current focus: " + this.systemEventType);
            this.connectionUpdated = false;

            if (this.systemEventType == WVR_DeviceType.WVR_DeviceType_Controller_Right)
            {
                activateEventSystem (WVR_DeviceType.WVR_DeviceType_Controller_Right, true);
                activateEventSystem (WVR_DeviceType.WVR_DeviceType_Controller_Left, false);
            }

            if (this.systemEventType == WVR_DeviceType.WVR_DeviceType_Controller_Left)
            {
                activateEventSystem (WVR_DeviceType.WVR_DeviceType_Controller_Right, false);
                activateEventSystem (WVR_DeviceType.WVR_DeviceType_Controller_Left, true);
            }

            WaveVR_Utils.WVR_SetFocusedController (this.systemEventType);
            this.toSetActiveOfEmitter = true;
        }

        setActiveOfEmitter ();
    }
    #endregion

    private void activateEventSystem(WVR_DeviceType type, bool enabled)
    {
        if (EventSystem.current == null)
        {
            EventSystem _es = FindObjectOfType<EventSystem>();
            if (_es != null)
            {
                eventSystem = _es.gameObject;
            }
        }
        else
        {
            eventSystem = EventSystem.current.gameObject;
        }

        if (eventSystem != null)
        {
            WaveVR_ControllerInputModule wcim = eventSystem.GetComponent<WaveVR_ControllerInputModule>();

            if (wcim != null)
            {
                if (type == WVR_DeviceType.WVR_DeviceType_Controller_Right)
                {
                    wcim.DomintEventEnabled = enabled;
                    PrintDebugLog("Forced set DomintEventEnabled to " + wcim.DomintEventEnabled);
                }
                else if (type == WVR_DeviceType.WVR_DeviceType_Controller_Left)
                {
                    wcim.NoDomtEventEnabled = enabled;
                    PrintDebugLog("Forced set NoDomtEventEnabled to " + wcim.NoDomtEventEnabled);
                }
            }
        }
    }

    private void printAllChildren(GameObject go)
    {
        var ch = go.transform.childCount;

        for (int i = 0; i < ch; i++)
        {
            GameObject child = go.transform.GetChild(i).gameObject;
            PrintDebugLog("-- " + child.name + " " + child.activeInHierarchy);

            printAllChildren(child);
        }
    }

    private void checkControllerConnected()
    {
        #if UNITY_EDITOR
        if (Application.isEditor)
        {
            this.rConnected = true;
            this.lConnected = true;
        } else
        #endif
        {
            this.rConnected = WaveVR.Instance.getDeviceByType (WVR_DeviceType.WVR_DeviceType_Controller_Right).connected;
            this.lConnected = WaveVR.Instance.getDeviceByType (WVR_DeviceType.WVR_DeviceType_Controller_Left).connected;
            PrintDebugLog ("checkControllerConnected() rConnected: " + this.rConnected + ", lConnected: " + this.lConnected);
        }
    }

    private void setActiveOfEmitter()
    {
        //printAllChildren(ci.instance);
        foreach (ControllerInstance t in ctrInstanceList)
        {
            // Now allow to setActiveOfEmitter
            if (!this.toSetActiveOfEmitter)
                return;
            // Controller is disconnected
            if (t.type == WVR_DeviceType.WVR_DeviceType_Controller_Right && !this.rConnected)
            {
                PrintIntervalLog (() => "setActiveOfEmitter() right instance is disconnected. left-handed? " + WaveVR_Controller.IsLeftHanded);
                return;
            }
            if (t.type == WVR_DeviceType.WVR_DeviceType_Controller_Left && !this.lConnected)
            {
                PrintIntervalLog (() => "setActiveOfEmitter() left instance is disconnected. left-handed? " + WaveVR_Controller.IsLeftHanded);
                return;
            }

            bool _ret = ForceSetActiveOfEmitter (t, (t.type == this.systemEventType));
            // Set emitter failed.
            if (!_ret)
                return;
        }
        this.toSetActiveOfEmitter = false;
    }

    private bool ForceSetActiveOfEmitter(ControllerInstance ci, bool enabled)
    {
        bool _ret = false;

        GameObject _controller = ci.instance;
        if (_controller != null)
        {
            WaveVR_Beam _beam = _controller.GetComponentInChildren<WaveVR_Beam> ();
            WaveVR_ControllerPointer _pointer = _controller.GetComponentInChildren<WaveVR_ControllerPointer> ();

            if (_beam != null && _pointer != null)
            {
                _beam.ShowBeam = enabled & ci.ShowBeam;
                _pointer.ShowPointer = enabled & ci.showPointer;

                PrintDebugLog ("ForceSetActiveOfEmitter() Set " + ci.type + " controller " + _controller.name
                    + ", index: " + ci.index
                    + ", beam: " + _beam.ShowBeam
                    + ", pointer: " + _pointer.ShowPointer);

                _ret = true;
            } else
            {
                PrintIntervalLog (() => "ForceSetActiveOfEmitter() " + ci.type + " controller " + _controller.name
                    + ", beam is " + (_beam == null ? "disabled" : "enabled")
                    + ", pointer is " + (_pointer == null ? "disabled" : "enabled"));
            }
        } else
        {
            PrintDebugLog("ForceSetActiveOfEmitter() controller " + ci.type + " , index: " + ci.index + " controller is null, remove it from list.");
            removeControllerInstance(ci.index);
        }

        return _ret;
    }
}
