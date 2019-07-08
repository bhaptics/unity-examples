using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using wvr;
using WaveVR_Log;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(WaveVR_InputModuleManager))]
public class WaveVR_InputModuleManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        WaveVR_InputModuleManager myScript = target as WaveVR_InputModuleManager;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("EnableInputModule"), true);
        EditorGUILayout.HelpBox("Select to enable input module.", MessageType.Info);
        serializedObject.ApplyModifiedProperties();

        if (myScript.EnableInputModule)
        {
            EditorGUILayout.PropertyField (serializedObject.FindProperty ("OverrideSystemSettings"), true);
            EditorGUILayout.HelpBox ("If this checkbox is not selected, it will use system settings, otherwise inspector will popup items which you can define your preferred settings", MessageType.Warning);
            serializedObject.ApplyModifiedProperties ();

            //EditorGUILayout.PropertyField (serializedObject.FindProperty ("AutoGaze"), true);
            //EditorGUILayout.HelpBox ("Select to use Gaze automatically when no connected controller.", MessageType.Info);
            //serializedObject.ApplyModifiedProperties ();

            //EditorGUILayout.PropertyField (serializedObject.FindProperty ("AlwaysShowController"), true);
            //EditorGUILayout.HelpBox ("Whether to show controller when using Gaze input module.", MessageType.Info);
            //serializedObject.ApplyModifiedProperties ();

            if (myScript.OverrideSystemSettings)
            {
                EditorGUILayout.PropertyField (serializedObject.FindProperty ("CustomInputModule"), true);
                EditorGUILayout.HelpBox ("Choose input module.", MessageType.Info);
                serializedObject.ApplyModifiedProperties ();

                if (myScript.CustomInputModule == WaveVR_EInputModule.Gaze)
                {
                    EditorGUILayout.PropertyField (serializedObject.FindProperty ("Gaze"), true);
                    serializedObject.ApplyModifiedProperties ();

                    if (myScript != null && myScript.Gaze != null)
                    {
                        myScript.Gaze.BtnControl = EditorGUILayout.Toggle ("    BtnControl", myScript.Gaze.BtnControl);
                        if (myScript.Gaze.BtnControl)
                        {
                            myScript.Gaze.GazeDevice = (EGazeTriggerDevice)EditorGUILayout.EnumPopup ("        Gaze Trigger Device", myScript.Gaze.GazeDevice);
                            myScript.Gaze.ButtonToTrigger = (EGazeTriggerButton)EditorGUILayout.EnumPopup ("        Button To Trigger", myScript.Gaze.ButtonToTrigger);
                            myScript.Gaze.WithTimeGaze = EditorGUILayout.Toggle ("        With Time Gaze", myScript.Gaze.WithTimeGaze);
                        }

                        if (EventSystem.current != null)
                        {
                            GazeInputModule gim = EventSystem.current.GetComponent<GazeInputModule> ();
                            if (gim != null)
                            {
                                gim.BtnControl = myScript.Gaze.BtnControl;
                                gim.GazeDevice = myScript.Gaze.GazeDevice;
                                gim.ButtonToTrigger = myScript.Gaze.ButtonToTrigger;
                                gim.SetWithTimeGaze (myScript.Gaze.WithTimeGaze);
                            }
                        }
                    }
                    serializedObject.Update ();
                } else
                {
                    EditorGUILayout.PropertyField (serializedObject.FindProperty ("Controller"), true);
                    serializedObject.ApplyModifiedProperties ();

                    if (myScript != null && myScript.Controller != null)
                    {
                        if (myScript.Controller.RaycastMode == ERaycastMode.Fixed)
                        {
                            myScript.FixedBeamLength = (float)EditorGUILayout.FloatField ("Beam Length", myScript.FixedBeamLength);
                        }
                    }
                    serializedObject.Update ();
                }
            }
        }

        if (GUI.changed)
            EditorUtility.SetDirty ((WaveVR_InputModuleManager)target);
    }
}
#endif

public enum WaveVR_EInputModule {
    Controller,
    Gaze
}

public class WaveVR_InputModuleManager : MonoBehaviour
{
    private const string LOG_TAG = "WaveVR_InputModuleManager";

    private void PrintDebugLog(string msg)
    {
        #if UNITY_EDITOR
        Debug.Log(LOG_TAG + " " + msg);
        #endif
        Log.d (LOG_TAG, msg);
    }

    public bool EnableInputModule = true;
    public bool OverrideSystemSettings = false;
    public bool AutoGaze = false;
    public bool AlwaysShowController = false;
    public WaveVR_EInputModule CustomInputModule = WaveVR_EInputModule.Controller;

    #region Gaze parameters
    [System.Serializable]
    public class CGazeInputModule
    {
        public bool progressRate = true;  // The switch to show how many percent to click by TimeToGaze
        public float RateTextZPosition = 0.5f;
        public bool progressCounter = true;  // The switch to show how long to click by TimeToGaze
        public float CounterTextZPosition = 0.5f;
        public float TimeToGaze = 2.0f;
        public EGazeInputEvent InputEvent = EGazeInputEvent.PointerSubmit;
        public GameObject Head = null;
        [HideInInspector]
        public bool BtnControl = false;
        [HideInInspector]
        public EGazeTriggerDevice GazeDevice = EGazeTriggerDevice.HMD;
        [HideInInspector]
        public EGazeTriggerButton ButtonToTrigger = EGazeTriggerButton.Trigger;
        [HideInInspector]
        public bool WithTimeGaze = false;
    }

    public CGazeInputModule Gaze;
    #endregion

    #region Controller Input Module parameters
    [System.Serializable]
    public class CControllerInputModule
    {
        public bool DominantEventEnabled = true;
        public GameObject DominantController;
        public LayerMask DominantRaycastMask = ~0;
        public bool NonDominantEventEnabled = true;
        public GameObject NonDominantController;
        public LayerMask NonDominantRaycastMask = ~0;
        public EControllerButtons ButtonToTrigger = EControllerButtons.Touchpad;
        public List<EControllerButtons> OtherButtonToTrigger;
        public ERaycastMode RaycastMode = ERaycastMode.Mouse;
        public ERaycastStartPoint RaycastStartPoint = ERaycastStartPoint.CenterOfEyes;
        public GameObject Head = null;
        [Tooltip("Will be obsoleted soon!")]
        public string CanvasTag = "EventCanvas";
    }

    public CControllerInputModule Controller;
    #endregion
    public float FixedBeamLength = 9.5f;

    private static WaveVR_InputModuleManager instance = null;
    public static WaveVR_InputModuleManager Instance {
        get
        {
            return instance;
        }
    }

    #region Interaction Mode and Gaze Trigger Type
    private bool preOverrideSystemSettings = false;
    private WaveVR_EInputModule InteractionMode_User = WaveVR_EInputModule.Controller;
    private WVR_InteractionMode InteractionMode_System = WVR_InteractionMode.WVR_InteractionMode_Controller;
    private WVR_InteractionMode InteractionMode_Current = WVR_InteractionMode.WVR_InteractionMode_Controller;
    private WVR_GazeTriggerType gazeTriggerType_User = WVR_GazeTriggerType.WVR_GazeTriggerType_Timeout;
    private WVR_GazeTriggerType gazeTriggerType_System = WVR_GazeTriggerType.WVR_GazeTriggerType_Timeout;
    private WVR_GazeTriggerType gazeTriggerType_User_pre = WVR_GazeTriggerType.WVR_GazeTriggerType_Timeout;

    private void initInteractionModeAndGazeTriggerType()
    {
        this.preOverrideSystemSettings = this.OverrideSystemSettings;
        this.InteractionMode_User = this.CustomInputModule;
        updateGazeTriggerType_User ();  // set gazeTriggerType_User

        if (this.OverrideSystemSettings)
        {
            if (!Application.isEditor)
            {
                // Get default system settings.
                this.InteractionMode_System = Interop.WVR_GetInteractionMode ();
                this.gazeTriggerType_System = Interop.WVR_GetGazeTriggerType ();

                WVR_InteractionMode _mode = (this.InteractionMode_User == WaveVR_EInputModule.Controller) ?
                    WVR_InteractionMode.WVR_InteractionMode_Controller : WVR_InteractionMode.WVR_InteractionMode_Gaze;

                // Change system settings to user settings.
                Interop.WVR_SetInteractionMode (_mode);
                Interop.WVR_SetGazeTriggerType (this.gazeTriggerType_User);

                this.gazeTriggerType_User_pre = this.gazeTriggerType_User;
            }
            // Initialize by user settings.
            initializeInputModuleByCustomSettings ();
        } else
        {
            if (!Application.isEditor)
            {
                // Reset runtime settings to system default if no override.
                Interop.WVR_SetInteractionMode (WVR_InteractionMode.WVR_InteractionMode_SystemDefault);

                // Get default system settings.
                this.InteractionMode_System = Interop.WVR_GetInteractionMode ();
                this.gazeTriggerType_System = Interop.WVR_GetGazeTriggerType ();
            }
            // Initialize by system settings.
            initializeInputModuleBySystemSetting ();
        }
        this.InteractionMode_Current = GetInteractionMode ();

        PrintDebugLog ("initInteractionModeAndGazeTriggerType() OverrideSystemSettings: " + OverrideSystemSettings);
        PrintDebugLog ("initInteractionModeAndGazeTriggerType() Interaction Mode - System: " + this.InteractionMode_System + ", User: " + this.InteractionMode_User + ", Current: " + this.InteractionMode_Current);
        PrintDebugLog ("initInteractionModeAndGazeTriggerType() Gaze Trigger - System: " + this.gazeTriggerType_System + ", User: " + this.gazeTriggerType_User);
    }

    private void updateInteractionModeAndGazeTriggerType()
    {
        if (this.OverrideSystemSettings)
        {
            if ((this.InteractionMode_User != this.CustomInputModule) || (this.preOverrideSystemSettings != this.OverrideSystemSettings))
            {
                if (this.InteractionMode_User != this.CustomInputModule)
                {
                    this.InteractionMode_User = this.CustomInputModule;
                    PrintDebugLog ("updateInteractionModeAndGazeTriggerType() Set interaction mode " + this.InteractionMode_User + " due to CustomInputModule changed.");
                }
                if (this.preOverrideSystemSettings != this.OverrideSystemSettings)
                {
                    this.preOverrideSystemSettings = this.OverrideSystemSettings;
                    PrintDebugLog ("updateInteractionModeAndGazeTriggerType() Set interaction mode " + this.InteractionMode_User + " due to OverrideSystemSettings changed.");
                }
                if (!Application.isEditor)
                {
                    WVR_InteractionMode _mode = (this.InteractionMode_User == WaveVR_EInputModule.Controller) ?
                        WVR_InteractionMode.WVR_InteractionMode_Controller : WVR_InteractionMode.WVR_InteractionMode_Gaze;
                    Interop.WVR_SetInteractionMode (_mode);
                }
            }

            updateGazeTriggerType_User ();
            if (this.gazeTriggerType_User_pre != this.gazeTriggerType_User)
            {
                this.gazeTriggerType_User_pre = this.gazeTriggerType_User;
                if (!Application.isEditor)
                {
                    Interop.WVR_SetGazeTriggerType (this.gazeTriggerType_User);
                }
                PrintDebugLog ("updateInteractionModeAndGazeTriggerType() Gaze Trigger - User: " + this.gazeTriggerType_User_pre);
            }

            updateInputModuleByCustomSettings ();
        } else
        {
            if (this.preOverrideSystemSettings != this.OverrideSystemSettings)
            {
                this.preOverrideSystemSettings = this.OverrideSystemSettings;
                // Restore runtime mirror system setting.
                if (!Application.isEditor)
                {
                    Interop.WVR_SetInteractionMode (WVR_InteractionMode.WVR_InteractionMode_SystemDefault);
                }
            }

            if (!Application.isEditor)
            {
                this.InteractionMode_System = Interop.WVR_GetInteractionMode ();
                this.gazeTriggerType_System = Interop.WVR_GetGazeTriggerType ();
            }

            updateInputModuleBySystemSetting ();
        }
    }

    private void updateGazeTriggerType_User()
    {
        // Sync user settings of gaze trigger type
        if (Gaze.BtnControl)
        {
            if (Gaze.WithTimeGaze)
            {
                gazeTriggerType_User = WVR_GazeTriggerType.WVR_GazeTriggerType_TimeoutButton;
                // PrintDebugLog("[User Settings] Sync gaze trigger type to WVR_GazeTriggerType_TimeoutButton!");
            } else
            {
                gazeTriggerType_User = WVR_GazeTriggerType.WVR_GazeTriggerType_Button;
                // PrintDebugLog("[User Settings] Sync gaze trigger type to WVR_GazeTriggerType_Button!");
            }
        } else
        {
            gazeTriggerType_User = WVR_GazeTriggerType.WVR_GazeTriggerType_Timeout;
            // PrintDebugLog("[User Settings] Sync gaze trigger type to WVR_GazeTriggerType_Timeout!");
        }
    }
    #endregion

    private GameObject Head = null;
    private GameObject eventSystem = null;
    private GazeInputModule gazeInputModule = null;
    private WaveVR_Reticle gazePointer = null;
    private WaveVR_ControllerInputModule controllerInputModule = null;

    #region MonoBehaviour overrides.
    void Awake()
    {
        if (instance == null)
            instance = this;
        ActivateGazePointer (false);
    }

    void Start()
    {
        if (EventSystem.current == null)
        {
            EventSystem _es = FindObjectOfType<EventSystem> ();
            if (_es != null)
            {
                eventSystem = _es.gameObject;
                PrintDebugLog ("Start() find current EventSystem: " + eventSystem.name);
            }

            if (eventSystem == null)
            {
                PrintDebugLog ("Start() could not find EventSystem, create new one.");
                eventSystem = new GameObject ("EventSystem", typeof(EventSystem));
                eventSystem.AddComponent<GazeInputModule> ();
            }
        } else
        {
            eventSystem = EventSystem.current.gameObject;
        }

        // Standalone Input Module
        StandaloneInputModule _sim = eventSystem.GetComponent<StandaloneInputModule> ();
        if (_sim != null)
            _sim.enabled = false;

        // Gaze Input Module
        this.gazeInputModule = eventSystem.GetComponent<GazeInputModule> ();

        // Controller Input Module
        this.controllerInputModule = eventSystem.GetComponent<WaveVR_ControllerInputModule> ();

        initInteractionModeAndGazeTriggerType ();

        if (!this.EnableInputModule)
        {
            disableAllInputModules ();
        }
    }

    public void Update()
    {
        if (WaveVR_Render.Instance != null)
            this.Head = WaveVR_Render.Instance.gameObject;

        if (this.Head != null)
        {
            gameObject.transform.localPosition = this.Head.transform.localPosition;
            gameObject.transform.localRotation = this.Head.transform.localRotation;
        }

        if (!this.EnableInputModule)
        {
            disableAllInputModules ();
            return;
        }

        updateInteractionModeAndGazeTriggerType ();

        WVR_InteractionMode _cur_mode = this.GetInteractionMode ();
        if (this.InteractionMode_Current != _cur_mode)
        {
            PrintDebugLog ("Update() current interaction mode: " + _cur_mode);
            this.InteractionMode_Current = _cur_mode;
            WaveVR_Utils.Event.Send (WaveVR_Utils.Event.INTERACTION_MODE_CHANGED, this.InteractionMode_Current, this.AlwaysShowController);
        }
    }
    #endregion

    private void ActivateGazePointer(bool active)
    {
        if (Gaze.Head == null)
        {
            gazePointer = null;
            return;
        }
        if (gazePointer == null)
            gazePointer = Gaze.Head.GetComponentInChildren<WaveVR_Reticle> ();
        if (gazePointer != null)
            gazePointer.gameObject.SetActive (active);
    }

    private void CreateGazeInputModule()
    {
        if (this.gazeInputModule == null)
        {
            // Before initializing variables of input modules, disable EventSystem to prevent the OnEnable() of input modules being executed.
            eventSystem.SetActive (false);

            this.gazeInputModule = eventSystem.AddComponent<GazeInputModule> ();
            SetGazeInputModuleParameters ();

            // Enable EventSystem after initializing input modules.
            eventSystem.SetActive (true);
        }
    }

    private void SetGazeInputModuleParameters()
    {
        if (this.gazeInputModule != null)
        {
            ActivateGazePointer (true);

            this.gazeInputModule.enabled = false;
            this.gazeInputModule.progressRate = Gaze.progressRate;
            this.gazeInputModule.RateTextZPosition = Gaze.RateTextZPosition;
            this.gazeInputModule.progressCounter = Gaze.progressCounter;
            this.gazeInputModule.CounterTextZPosition = Gaze.CounterTextZPosition;
            this.gazeInputModule.TimeToGaze = Gaze.TimeToGaze;
            this.gazeInputModule.InputEvent = Gaze.InputEvent;
            this.gazeInputModule.Head = Gaze.Head;
            this.gazeInputModule.BtnControl = Gaze.BtnControl;
            this.gazeInputModule.GazeDevice = Gaze.GazeDevice;
            this.gazeInputModule.ButtonToTrigger = Gaze.ButtonToTrigger;
            this.gazeInputModule.SetWithTimeGaze (Gaze.WithTimeGaze);
            this.gazeInputModule.enabled = true;
        }
    }

    private void CreateControllerInputModule()
    {
        if (controllerInputModule == null)
        {
            // Before initializing variables of input modules, disable EventSystem to prevent the OnEnable() of input modules being executed.
            eventSystem.SetActive (false);

            controllerInputModule = eventSystem.AddComponent<WaveVR_ControllerInputModule> ();
            SetControllerInputModuleParameters ();

            // Enable EventSystem after initializing input modules.
            eventSystem.SetActive (true);
        }
    }

    private void SetControllerInputModuleParameters()
    {
        if (controllerInputModule != null)
        {
            this.controllerInputModule.enabled = false;
            this.controllerInputModule.DomintEventEnabled = Controller.DominantEventEnabled;
            this.controllerInputModule.DominantController = Controller.DominantController;
            this.controllerInputModule.DominantRaycastMask = Controller.DominantRaycastMask;
            this.controllerInputModule.NoDomtEventEnabled = Controller.NonDominantEventEnabled;
            this.controllerInputModule.NonDominantController = Controller.NonDominantController;
            this.controllerInputModule.NonDominantRaycastMask = Controller.NonDominantRaycastMask;
            this.controllerInputModule.ButtonToTrigger = Controller.ButtonToTrigger;
            this.controllerInputModule.OtherButtonToTrigger = Controller.OtherButtonToTrigger;
            this.controllerInputModule.RaycastMode = Controller.RaycastMode;
            this.controllerInputModule.RaycastStartPoint = Controller.RaycastStartPoint;
            this.controllerInputModule.CanvasTag = Controller.CanvasTag;
            this.controllerInputModule.FixedBeamLength = this.FixedBeamLength;
            this.controllerInputModule.Head = Controller.Head;
            this.controllerInputModule.enabled = true;
        }
    }

    private void SetActiveGaze(bool value)
    {
        if (this.gazeInputModule != null)
        {
            this.gazeInputModule.enabled = value;
            ActivateGazePointer (this.gazeInputModule.enabled);
        }
        else
        {
            if (value)
                CreateGazeInputModule ();
        }
    }

    private void SetActiveController(bool value)
    {
        if (controllerInputModule != null)
            controllerInputModule.enabled = value;
        else
        {
            if (value)
                CreateControllerInputModule ();
        }
    }

    private bool IsAnyControllerConnected()
    {
        bool _result = false;

        foreach (WVR_DeviceType _dt in Enum.GetValues(typeof(WVR_DeviceType)))
        {
            if (_dt == WVR_DeviceType.WVR_DeviceType_HMD)
                continue;

            #if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (WaveVR_Controller.Input (_dt).connected)
                {
                    _result = true;
                    break;
                }
            }
            else
            #endif
            {
                if (WaveVR.Instance != null)
                {
                    WaveVR.Device _dev = WaveVR.Instance.getDeviceByType (_dt);
                    if (_dev.connected)
                    {
                        _result = true;
                        break;
                    }
                }
            }
        }

        return _result;
    }

    #region Input Module
    private void initializeInputModuleByCustomSettings()
    {
        switch (this.InteractionMode_User)
        {
        case WaveVR_EInputModule.Controller:
            SetActiveGaze (false);
            ActivateGazePointer (false);

            if (this.controllerInputModule == null)
                CreateControllerInputModule ();
            else
                SetControllerInputModuleParameters ();
            break;
        case WaveVR_EInputModule.Gaze:
            SetActiveController (false);

            if (this.gazeInputModule == null)
                CreateGazeInputModule ();
            else
                SetGazeInputModuleParameters ();
            break;
        default:
            break;
        }
    }

    private void initializeInputModuleBySystemSetting()
    {
        switch (this.InteractionMode_System)
        {
        case WVR_InteractionMode.WVR_InteractionMode_Controller:
            SetActiveGaze (false);
            ActivateGazePointer (false);

            if (this.controllerInputModule == null)
                CreateControllerInputModule ();
            else
                SetControllerInputModuleParameters ();
            break;
        case WVR_InteractionMode.WVR_InteractionMode_Gaze:
            SetActiveController (false);

            if (this.gazeInputModule == null)
                CreateGazeInputModule ();
            else
                SetGazeInputModuleParameters ();
            break;
        default:
            break;
        }
    }

    private void disableAllInputModules()
    {
        SetActiveController (false);
        SetActiveGaze (false);
    }

    private void updateInputModuleByCustomSettings()
    {
        switch (this.InteractionMode_User)
        {
        case WaveVR_EInputModule.Gaze:
            SetActiveGaze (true);
            SetActiveController (false);
            if (this.gazeInputModule != null)
            {
                this.gazeInputModule.BtnControl = this.Gaze.BtnControl;
                this.gazeInputModule.SetWithTimeGaze (this.Gaze.WithTimeGaze);
            }
            break;
        case WaveVR_EInputModule.Controller:
            SetActiveGaze (false);
            if (IsAnyControllerConnected ())
            {
                SetActiveController (true);
            } else
            {
                SetActiveController (false);
                if (this.AutoGaze)
                {
                    // No controller connected, using gaze input module.
                    SetActiveGaze (true);
                    if (this.gazeInputModule != null)
                    {
                        this.gazeInputModule.BtnControl = this.Gaze.BtnControl;
                        this.gazeInputModule.SetWithTimeGaze (this.Gaze.WithTimeGaze);
                    }
                }
            }
            break;
        default:
            break;
        }
    }

    private void updateInputModuleBySystemSetting()
    {
        // Sync system settings of interaction mode
        switch (this.InteractionMode_System)
        {
        case WVR_InteractionMode.WVR_InteractionMode_Controller:
            SetActiveGaze (false);
            if (IsAnyControllerConnected ())
            {
                SetActiveController (true);
            } else
            {
                SetActiveController (false);
                if (this.AutoGaze)
                {
                    SetActiveGaze (true);
                    if (this.gazeInputModule != null)
                    {
                        switch (this.gazeTriggerType_System)
                        {
                        case WVR_GazeTriggerType.WVR_GazeTriggerType_Button:
                            this.gazeInputModule.BtnControl = true;
                            this.gazeInputModule.SetWithTimeGaze (false);
                            break;
                        case WVR_GazeTriggerType.WVR_GazeTriggerType_TimeoutButton:
                            this.gazeInputModule.BtnControl = true;
                            this.gazeInputModule.SetWithTimeGaze (true);
                            break;
                        default:
                            this.gazeInputModule.BtnControl = false;
                            this.gazeInputModule.SetWithTimeGaze (false);
                            break;
                        }
                    }
                }
            }
            break;
        case WVR_InteractionMode.WVR_InteractionMode_Gaze:
            SetActiveGaze (true);
            SetActiveController (false);
            if (this.gazeInputModule != null)
            {
                switch (this.gazeTriggerType_System)
                {
                case WVR_GazeTriggerType.WVR_GazeTriggerType_Button:
                    this.gazeInputModule.BtnControl = true;
                    this.gazeInputModule.SetWithTimeGaze (false);
                    break;
                case WVR_GazeTriggerType.WVR_GazeTriggerType_TimeoutButton:
                    this.gazeInputModule.BtnControl = true;
                    this.gazeInputModule.SetWithTimeGaze (true);
                    break;
                default:
                    this.gazeInputModule.BtnControl = false;
                    this.gazeInputModule.SetWithTimeGaze (false);
                    break;
                }
            }
            break;
        default:
            break;
        }
    }
    #endregion

    public ERaycastMode GetRaycastMode()
    {
        if (controllerInputModule != null)
            return controllerInputModule.RaycastMode;
        else
            return ERaycastMode.Beam;
    }

    public WVR_InteractionMode GetInteractionMode()
    {
        WVR_InteractionMode _custom_mode =
            (this.InteractionMode_User == WaveVR_EInputModule.Controller) ?
            WVR_InteractionMode.WVR_InteractionMode_Controller : WVR_InteractionMode.WVR_InteractionMode_Gaze;
        return (OverrideSystemSettings) ? _custom_mode : this.InteractionMode_System;
    }

    public WVR_GazeTriggerType GetGazeTriggerType()
    {
        return (OverrideSystemSettings)? gazeTriggerType_User : gazeTriggerType_System;
    }

    public WVR_GazeTriggerType GetUserGazeTriggerType()
    {
        return gazeTriggerType_User;
    }

    public void SetControllerBeamLength(WaveVR_Controller.EDeviceType dt, float length)
    {
        if (this.controllerInputModule != null)
            this.controllerInputModule.ChangeBeamLength (dt, length);
    }
}
