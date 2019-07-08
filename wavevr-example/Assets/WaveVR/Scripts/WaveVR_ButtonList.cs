using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using wvr;
using WaveVR_Log;

public class WaveVR_ButtonList : MonoBehaviour {
    private static string LOG_TAG = "WaveVR_ButtonList";
    private void PrintInfoLog(string msg) { Log.i (LOG_TAG, msg, true); }
    private void PrintDebugLog(string msg) { Log.d (LOG_TAG, msg, true); }

    public enum EButtons
    {
        Menu = WVR_InputId.WVR_InputId_Alias1_Menu,
        Grip = WVR_InputId.WVR_InputId_Alias1_Grip,
        DPadUp = WVR_InputId.WVR_InputId_Alias1_DPad_Up,
        DPadRight = WVR_InputId.WVR_InputId_Alias1_DPad_Right,
        DPadDown = WVR_InputId.WVR_InputId_Alias1_DPad_Down,
        DPadLeft = WVR_InputId.WVR_InputId_Alias1_DPad_Left,
        VolumeUp = WVR_InputId.WVR_InputId_Alias1_Volume_Up,
        VolumeDown = WVR_InputId.WVR_InputId_Alias1_Volume_Down,
        //DigitalTrigger = WVR_InputId.WVR_InputId_Alias1_Digital_Trigger,
        HMDEnter = WVR_InputId.WVR_InputId_Alias1_Enter,
        Touchpad = WVR_InputId.WVR_InputId_Alias1_Touchpad,
        Trigger = WVR_InputId.WVR_InputId_Alias1_Trigger,
        Thumbstick = WVR_InputId.WVR_InputId_Alias1_Thumbstick
    }

    public List<EButtons> HmdButtons;
    private WVR_InputAttribute_t[] inputAttribtues_hmd;
    private List<WVR_InputId> usableButtons_hmd = new List<WVR_InputId> ();
    private bool hmd_connected = false;

    public List<EButtons> DominantButtons;
    private WVR_InputAttribute_t[] inputAttributes_Dominant;
    private List<WVR_InputId> usableButtons_dominant = new List<WVR_InputId> ();
    private bool dominant_connected = false;

    public List<EButtons> NonDominantButtons;
    private WVR_InputAttribute_t[] inputAttributes_NonDominant;
    private List<WVR_InputId> usableButtons_nonDominant = new List<WVR_InputId> ();
    private bool nodomint_connected = false;

    private const uint inputTableSize = (uint)WVR_InputId.WVR_InputId_Max;
    private WVR_InputMappingPair_t[] inputTable = new WVR_InputMappingPair_t[inputTableSize];
    private WVR_InputMappingPair_t inputPair;

    private static WaveVR_ButtonList instance = null;
    public static WaveVR_ButtonList Instance {
        get
        {
            return instance;
        }
    }

    #region MonoBehaviour overrides
    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    void Start ()
    {
        PrintInfoLog ("Start()");
        ResetAllInputRequest ();
	}

	void Update ()
    {
        bool _hmd_connected = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.Head).connected;
        bool _dominant_connected = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.Dominant).connected;
        bool _nodomint_connected = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.NonDominant).connected;

        #if UNITY_EDITOR
        if (Application.isEditor)
        {
            this.hmd_connected = _hmd_connected;
            this.dominant_connected = _dominant_connected;
            this.nodomint_connected = _nodomint_connected;
        } else
        #endif
        {
            /***
             * Consider a situation:
             * Only 1 controller connected but trying to SetInputRequest of both controllers.
             * There is only 1 controller SetInputRequest succeeds because another controller is disconnected.
             * In order to apply new input attribute, it needs to SetInputRequest when controller is connected.
             ***/
            if (this.hmd_connected != _hmd_connected)
            {
                this.hmd_connected = _hmd_connected;
                if (this.hmd_connected)
                {
                    PrintDebugLog ("Update() HMD is connected.");
                    //ResetInputRequest (WaveVR_Controller.EDeviceType.Head);
                }
            }
            if (this.dominant_connected != _dominant_connected)
            {
                this.dominant_connected = _dominant_connected;
                if (this.dominant_connected)
                {
                    PrintDebugLog ("Update() Dominant is connected.");
                    //ResetInputRequest (WaveVR_Controller.EDeviceType.Dominant);
                }
            }
            if (this.nodomint_connected != _nodomint_connected)
            {
                this.nodomint_connected = _nodomint_connected;
                if (this.nodomint_connected)
                {
                    PrintDebugLog ("Update() NonDominant is connected.");
                    //ResetInputRequest (WaveVR_Controller.EDeviceType.NonDominant);
                }
            }
        }
	}
    #endregion

    public bool GetInputMappingPair(WaveVR_Controller.EDeviceType device, ref WVR_InputId destination)
    {
        // Default true in editor mode, destination will be equivallent to source.
        bool _result = true;

        if (!Application.isEditor && WaveVR.Instance != null)
        {
            WVR_DeviceType _type = WaveVR_Controller.Input (device).DeviceType;
            /*
            uint _ret = Interop.WVR_GetInputMappingTable (_type, this.inputTable, WaveVR_ButtonList.inputTableSize);
            for (int _i = 0; _i < (int)_ret; _i++)
            {
                PrintDebugLog ("GetInputMappingPair " + device + " table: " + _type + " " + this.inputTable [_i].source.id + " to " + inputTable [_i].destination.id);
                if (this.inputTable [_i].destination.id == destination)
                {
                    destination = this.inputTable [_i].source.id;
                    _result = true;
                    break;
                }
            }
            */
            _result = Interop.WVR_GetInputMappingPair (_type, destination, ref this.inputPair);
            if (_result)
                destination = this.inputPair.source.id;
        }

        return _result;
    }

    private void setupButtonAttributes(WaveVR_Controller.EDeviceType device, List<EButtons> buttons, WVR_InputAttribute_t[] inputAttributes, int count)
    {
        WVR_DeviceType _type = WaveVR_Controller.Input (device).DeviceType;

        for (int _i = 0; _i < count; _i++)
        {
            switch (buttons [_i])
            {
            case EButtons.Menu:
            case EButtons.Grip:
            case EButtons.DPadLeft:
            case EButtons.DPadUp:
            case EButtons.DPadRight:
            case EButtons.DPadDown:
            case EButtons.VolumeUp:
            case EButtons.VolumeDown:
            case EButtons.HMDEnter:
                inputAttributes [_i].id = (WVR_InputId)buttons [_i];
                inputAttributes [_i].capability = (uint)WVR_InputType.WVR_InputType_Button;
                inputAttributes [_i].axis_type = WVR_AnalogType.WVR_AnalogType_None;
                break;
            case EButtons.Touchpad:
            case EButtons.Thumbstick:
                inputAttributes [_i].id = (WVR_InputId)buttons [_i];
                inputAttributes [_i].capability = (uint)(WVR_InputType.WVR_InputType_Button | WVR_InputType.WVR_InputType_Touch | WVR_InputType.WVR_InputType_Analog);
                inputAttributes [_i].axis_type = WVR_AnalogType.WVR_AnalogType_2D;
                break;
            case EButtons.Trigger:
                inputAttributes [_i].id = (WVR_InputId)buttons [_i];
                inputAttributes [_i].capability = (uint)(WVR_InputType.WVR_InputType_Button | WVR_InputType.WVR_InputType_Touch | WVR_InputType.WVR_InputType_Analog);
                inputAttributes [_i].axis_type = WVR_AnalogType.WVR_AnalogType_1D;
                break;
            default:
                break;
            }

            PrintDebugLog ("setupButtonAttributes() " + device + " (" + _type + ") " + buttons [_i] + " analog type: " + inputAttributes [_i].axis_type);
        }
    }

    private void createHmdRequestAttributes()
    {
        PrintInfoLog ("createHmdRequestAttributes()");

        if (!this.HmdButtons.Contains (EButtons.HMDEnter))
            this.HmdButtons.Add (EButtons.HMDEnter);

        int _count = this.HmdButtons.Count;
        this.inputAttribtues_hmd = new WVR_InputAttribute_t[_count];
        setupButtonAttributes (WaveVR_Controller.EDeviceType.Head, this.HmdButtons, this.inputAttribtues_hmd, _count);
    }

    private void createDominantRequestAttributes()
    {
        PrintInfoLog ("createDominantRequestAttributes()");
        int _count = this.DominantButtons.Count;
        this.inputAttributes_Dominant = new WVR_InputAttribute_t[_count];
        setupButtonAttributes (WaveVR_Controller.EDeviceType.Dominant, this.DominantButtons, this.inputAttributes_Dominant, _count);
    }

    private void createNonDominantRequestAttributes()
    {
        PrintInfoLog ("createNonDominantRequestAttributes()");
        int _count = this.NonDominantButtons.Count;
        this.inputAttributes_NonDominant = new WVR_InputAttribute_t[_count];
        setupButtonAttributes (WaveVR_Controller.EDeviceType.NonDominant, this.NonDominantButtons, this.inputAttributes_NonDominant, _count);
    }

    public bool IsButtonAvailable(WaveVR_Controller.EDeviceType device, WVR_InputId button)
    {
        if (device == WaveVR_Controller.EDeviceType.Head)
            return this.usableButtons_hmd.Contains (button);
        if (device == WaveVR_Controller.EDeviceType.Dominant)
            return this.usableButtons_dominant.Contains (button);
        if (device == WaveVR_Controller.EDeviceType.NonDominant)
            return this.usableButtons_nonDominant.Contains (button);

        return false;
    }

    private void SetHmdInputRequest()
    {
        this.usableButtons_hmd.Clear ();
        #if UNITY_EDITOR
        if (Application.isEditor)
        {
            for (int _i = 0; _i < this.inputAttribtues_hmd.Length; _i++)
            {
                PrintDebugLog ("SetHmdInputRequest() " + this.inputAttribtues_hmd [_i].id);
                // Set all request buttons usable in editor mode.
                this.usableButtons_hmd.Add (this.inputAttribtues_hmd [_i].id);
            }
        } else
        #endif
        {
            if (WaveVR.Instance == null)
                return;

            WVR_DeviceType _type = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.Head).DeviceType;
            bool _ret = Interop.WVR_SetInputRequest (_type, this.inputAttribtues_hmd, (uint)this.inputAttribtues_hmd.Length);
            if (_ret)
            {
                uint _size = Interop.WVR_GetInputMappingTable (_type, this.inputTable, WaveVR_ButtonList.inputTableSize);
                if (_size > 0)
                {
                    for (int _i = 0; _i < (int)_size; _i++)
                    {
                        this.usableButtons_hmd.Add (this.inputTable [_i].destination.id);
                        PrintDebugLog ("SetHmdInputRequest() " + _type
                        + " button: " + this.inputTable [_i].source.id
                        + " is mapping to HMD input ID: " + this.inputTable [_i].destination.id);
                    }
                }
            }
        }
    }

    private void SetDominantInputRequest()
    {
        this.usableButtons_dominant.Clear();
        #if UNITY_EDITOR
        if (Application.isEditor)
        {
            for (int _i = 0; _i < this.inputAttributes_Dominant.Length; _i++)
            {
                PrintDebugLog ("SetDominantInputRequest() " + this.inputAttributes_Dominant [_i].id);
                // Set all request buttons usable in editor mode.
                #if UNITY_EDITOR
                this.usableButtons_dominant.Add (this.inputAttributes_Dominant [_i].id);
                #endif
            }
        } else
        #endif
        {
            if (WaveVR.Instance == null)
                return;

            WVR_DeviceType _type = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.Dominant).DeviceType;
            bool _ret = Interop.WVR_SetInputRequest (_type, this.inputAttributes_Dominant, (uint)this.inputAttributes_Dominant.Length);
            if (_ret)
            {
                uint _size = Interop.WVR_GetInputMappingTable (_type, this.inputTable, WaveVR_ButtonList.inputTableSize);
                if (_size > 0)
                {
                    for (int _i = 0; _i < (int)_size; _i++)
                    {
                        this.usableButtons_dominant.Add (this.inputTable [_i].destination.id);
                        PrintDebugLog ("SetDominantInputRequest() " + _type
                        + " button: " + this.inputTable [_i].source.id
                        + " is mapping to Dominant input ID: " + this.inputTable [_i].destination.id);
                    }
                }
            }
        }
    }

    private void SetNonDominantInputRequest()
    {
        this.usableButtons_nonDominant.Clear ();
        #if UNITY_EDITOR
        if (Application.isEditor)
        {
            for (int _i = 0; _i < this.inputAttributes_NonDominant.Length; _i++)
            {
                PrintDebugLog ("SetNonDominantInputRequest() " + this.inputAttributes_NonDominant [_i].id);
                // Set all request buttons usable in editor mode.
                #if UNITY_EDITOR
                this.usableButtons_nonDominant.Add (this.inputAttributes_NonDominant [_i].id);
                #endif
            }
        } else
        #endif
        {
            if (WaveVR.Instance == null)
                return;

            WVR_DeviceType _type = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.NonDominant).DeviceType;
            bool _ret = Interop.WVR_SetInputRequest (_type, this.inputAttributes_NonDominant, (uint)this.inputAttributes_NonDominant.Length);
            if (_ret)
            {
                uint _size = Interop.WVR_GetInputMappingTable (_type, this.inputTable, WaveVR_ButtonList.inputTableSize);
                if (_size > 0)
                {
                    for (int _i = 0; _i < (int)_size; _i++)
                    {
                        this.usableButtons_nonDominant.Add (this.inputTable [_i].destination.id);
                        PrintDebugLog ("SetNonDominantInputRequest() " + _type
                        + " button: " + this.inputTable [_i].source.id
                        + " is mapping to NonDominant input ID: " + this.inputTable [_i].destination.id);
                    }
                }
            }
        }
    }

    private void ResetInputRequest(WaveVR_Controller.EDeviceType device)
    {
        PrintDebugLog ("ResetInputRequest() " + device);
        switch (device)
        {
        case WaveVR_Controller.EDeviceType.Head:
            createHmdRequestAttributes ();
            SetHmdInputRequest ();
            break;
        case WaveVR_Controller.EDeviceType.Dominant:
            createDominantRequestAttributes ();
            SetDominantInputRequest ();
            break;
        case WaveVR_Controller.EDeviceType.NonDominant:
            createNonDominantRequestAttributes ();
            SetNonDominantInputRequest ();
            break;
        default:
            break;
        }
    }

    public void SetupButtonList(WaveVR_Controller.EDeviceType device, List<WaveVR_ButtonList.EButtons> list)
    {
        PrintDebugLog ("SetupButtonList() " + device);
        switch (device)
        {
        case WaveVR_Controller.EDeviceType.Head:
            this.HmdButtons = list;
            ResetInputRequest (WaveVR_Controller.EDeviceType.Head);
            break;
        case WaveVR_Controller.EDeviceType.Dominant:
            this.DominantButtons = list;
            ResetInputRequest (WaveVR_Controller.EDeviceType.Dominant);
            break;
        case WaveVR_Controller.EDeviceType.NonDominant:
            this.NonDominantButtons = list;
            ResetInputRequest (WaveVR_Controller.EDeviceType.NonDominant);
            break;
        default:
            break;
        }
    }

    public void ResetAllInputRequest()
    {
        PrintDebugLog ("ResetAllInputRequest()");
        ResetInputRequest (WaveVR_Controller.EDeviceType.Head);
        ResetInputRequest (WaveVR_Controller.EDeviceType.Dominant);
        ResetInputRequest (WaveVR_Controller.EDeviceType.NonDominant);
    }
}
