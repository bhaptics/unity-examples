using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using wvr;
using WVR_Log;

public class WaveVR_ButtonList : MonoBehaviour {
	private static string LOG_TAG = "WaveVR_ButtonList";
	private void PrintInfoLog(string msg) { Log.i (LOG_TAG, msg, true); }
	private void PrintDebugLog(string msg) { Log.d (LOG_TAG, msg, true); }

	public enum EButtons
	{
		Unavailable = WVR_InputId.WVR_InputId_Alias1_System,
		Menu = WVR_InputId.WVR_InputId_Alias1_Menu,
		Grip = WVR_InputId.WVR_InputId_Alias1_Grip,
		DPadUp = WVR_InputId.WVR_InputId_Alias1_DPad_Up,
		DPadRight = WVR_InputId.WVR_InputId_Alias1_DPad_Right,
		DPadDown = WVR_InputId.WVR_InputId_Alias1_DPad_Down,
		DPadLeft = WVR_InputId.WVR_InputId_Alias1_DPad_Left,
		VolumeUp = WVR_InputId.WVR_InputId_Alias1_Volume_Up,
		VolumeDown = WVR_InputId.WVR_InputId_Alias1_Volume_Down,
		//DigitalTrigger = WVR_InputId.WVR_InputId_Alias1_Digital_Trigger,
		Back = WVR_InputId.WVR_InputId_Alias1_Back,
		Enter = WVR_InputId.WVR_InputId_Alias1_Enter,
		Touchpad = WVR_InputId.WVR_InputId_Alias1_Touchpad,
		Trigger = WVR_InputId.WVR_InputId_Alias1_Trigger,
		Thumbstick = WVR_InputId.WVR_InputId_Alias1_Thumbstick
	}

	public enum EHmdButtons
	{
		Menu = EButtons.Menu,
		DPadUp = EButtons.DPadUp,
		DPadRight = EButtons.DPadRight,
		DPadDown = EButtons.DPadDown,
		DPadLeft = EButtons.DPadLeft,
		VolumeUp = EButtons.VolumeUp,
		VolumeDown = EButtons.VolumeDown,
		Enter = EButtons.Enter,
		Touchpad = EButtons.Touchpad
	}

	private List<EButtons> ToEButtons(List<EHmdButtons> eList)
	{
		List<EButtons> _list = new List<EButtons> ();
		for (int i = 0; i < eList.Count; i++)
		{
			if (!_list.Contains ((EButtons)eList [i]))
				_list.Add ((EButtons)eList [i]);
		}

		return _list;
	}

	public enum EControllerButtons
	{
		Menu = EButtons.Menu,
		Grip = EButtons.Grip,
		DPadUp = EButtons.DPadUp,
		DPadRight = EButtons.DPadRight,
		DPadDown = EButtons.DPadDown,
		DPadLeft = EButtons.DPadLeft,
		VolumeUp = EButtons.VolumeUp,
		VolumeDown = EButtons.VolumeDown,
		Touchpad = EButtons.Touchpad,
		Trigger = EButtons.Trigger,
		Thumbstick = EButtons.Thumbstick
	}

	private List<EButtons> ToEButtons(List<EControllerButtons> eList)
	{
		List<EButtons> _list = new List<EButtons> ();
		for (int i = 0; i < eList.Count; i++)
		{
			if (!_list.Contains ((EButtons)eList [i]))
				_list.Add ((EButtons)eList [i]);
		}

		return _list;
	}

	public List<EHmdButtons> HmdButtons;
	private WVR_InputAttribute_t[] inputAttributes_hmd;
	private List<WVR_InputId> usableButtons_hmd = new List<WVR_InputId> ();
	private bool hmd_connected = false;

	public List<EControllerButtons> DominantButtons;
	private WVR_InputAttribute_t[] inputAttributes_Dominant;
	private List<WVR_InputId> usableButtons_dominant = new List<WVR_InputId> ();
	private bool dominant_connected = false;

	public List<EControllerButtons> NonDominantButtons;
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
	#endregion

	public bool GetInputMappingPair(WaveVR_Controller.EDeviceType device, ref WVR_InputId destination)
	{
		if (!WaveVR.Instance.Initialized)
			return false;

		// Default true in editor mode, destination will be equivallent to source.
		bool _result = true;

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
			case EButtons.Back:
			case EButtons.Enter:
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

			PrintDebugLog ("setupButtonAttributes() " + device + " (" + _type + ") " + buttons [_i]
				+ ", capability: " + inputAttributes [_i].capability
				+ ", analog type: " + inputAttributes [_i].axis_type);
		}
	}

	private void createHmdRequestAttributes()
	{
		PrintInfoLog ("createHmdRequestAttributes()");

		List<EButtons> _list = ToEButtons (this.HmdButtons);
		if (!_list.Contains (EButtons.Enter))
			_list.Add (EButtons.Enter);

		int _count = _list.Count;
		inputAttributes_hmd = new WVR_InputAttribute_t[_count];
		setupButtonAttributes (WaveVR_Controller.EDeviceType.Head, _list, inputAttributes_hmd, _count);
	}

	private void createDominantRequestAttributes()
	{
		PrintInfoLog ("createDominantRequestAttributes()");

		List<EButtons> _list = ToEButtons (this.DominantButtons);

		int _count = _list.Count;
		inputAttributes_Dominant = new WVR_InputAttribute_t[_count];
		setupButtonAttributes (WaveVR_Controller.EDeviceType.Dominant, _list, inputAttributes_Dominant, _count);
	}

	private void createNonDominantRequestAttributes()
	{
		PrintInfoLog ("createNonDominantRequestAttributes()");

		List<EButtons> _list = ToEButtons (this.NonDominantButtons);

		int _count = _list.Count;
		inputAttributes_NonDominant = new WVR_InputAttribute_t[_count];
		setupButtonAttributes (WaveVR_Controller.EDeviceType.NonDominant, _list, inputAttributes_NonDominant, _count);
	}

	public bool IsButtonAvailable(WaveVR_Controller.EDeviceType device, EButtons button)
	{
		return IsButtonAvailable (device, (WVR_InputId)button);
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
		if (!WaveVR.Instance.Initialized)
			return;

		WVR_DeviceType _type = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.Head).DeviceType;
		bool _ret = Interop.WVR_SetInputRequest (_type, this.inputAttributes_hmd, (uint)this.inputAttributes_hmd.Length);
		if (_ret)
		{
			uint _size = Interop.WVR_GetInputMappingTable (_type, this.inputTable, WaveVR_ButtonList.inputTableSize);
			if (_size > 0)
			{
				for (int _i = 0; _i < (int)_size; _i++)
				{
					if (this.inputTable [_i].source.capability != 0)
					{
						this.usableButtons_hmd.Add (this.inputTable [_i].destination.id);
						PrintDebugLog ("SetHmdInputRequest() " + _type
							+ " button: " + this.inputTable [_i].source.id + "(capability: " + this.inputTable [_i].source.capability + ")"
							+ " is mapping to HMD input ID: " + this.inputTable [_i].destination.id);
					} else
					{
						PrintDebugLog ("SetHmdInputRequest() " + _type
							+ " source button " + this.inputTable [_i].source.id + " has invalid capability.");
					}
				}
			}
		}
	}

	private void SetDominantInputRequest()
	{
		this.usableButtons_dominant.Clear ();
		if (!WaveVR.Instance.Initialized)
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
					if (this.inputTable [_i].source.capability != 0)
					{
						this.usableButtons_dominant.Add (this.inputTable [_i].destination.id);
						PrintDebugLog ("SetDominantInputRequest() " + _type
							+ " button: " + this.inputTable [_i].source.id + "(capability: " + this.inputTable [_i].source.capability + ")"
							+ " is mapping to Dominant input ID: " + this.inputTable [_i].destination.id);
					} else
					{
						PrintDebugLog ("SetDominantInputRequest() " + _type
							+ " source button " + this.inputTable [_i].source.id + " has invalid capability.");
					}
				}
			}
		}
	}

	private void SetNonDominantInputRequest()
	{
		this.usableButtons_nonDominant.Clear ();
		if (!WaveVR.Instance.Initialized)
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
					if (this.inputTable [_i].source.capability != 0)
					{
						this.usableButtons_nonDominant.Add (this.inputTable [_i].destination.id);
						PrintDebugLog ("SetNonDominantInputRequest() " + _type
							+ " button: " + this.inputTable [_i].source.id + "(capability: " + this.inputTable [_i].source.capability + ")"
							+ " is mapping to NonDominant input ID: " + this.inputTable [_i].destination.id);
					} else
					{
						PrintDebugLog ("SetNonDominantInputRequest() " + _type
							+ " source button " + this.inputTable [_i].source.id + " has invalid capability.");
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

	public void SetupHmdButtonList(List<EHmdButtons> list)
	{
		PrintDebugLog ("SetupHmdButtonList()");

		this.HmdButtons = list;
		ResetInputRequest (WaveVR_Controller.EDeviceType.Head);
	}

	public void SetupControllerButtonList(WaveVR_Controller.EDeviceType device, List<WaveVR_ButtonList.EControllerButtons> list)
	{
		PrintDebugLog ("SetupControllerButtonList() " + device);
		switch (device)
		{
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
