using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using wvr;
using WVR_Log;
using System;
using UnityEngine.Profiling;

public enum ERaycastStartPoint
{
	CenterOfEyes,
	LeftEye,
	RightEye
}

public class WaveVR_ControllerInputModule : BaseInputModule
{
	private const string LOG_TAG = "WaveVR_ControllerInputModule";

	private void PrintDebugLog(string msg)
	{
		if (Log.EnableDebugLog)
			Log.d (LOG_TAG, msg, true);
	}

	public enum ERaycastMode
	{
		Beam,
		Fixed,
		Mouse
	}
	public static ERaycastMode[] RaycastModes = new ERaycastMode[] {
		ERaycastMode.Beam,
		ERaycastMode.Fixed,
		ERaycastMode.Mouse
	};

	#region Developer specified parameters
	[HideInInspector]
	public bool UnityMouseMode = false;
	public bool DomintEventEnabled = true;
	public GameObject DominantController = null;
	public LayerMask DominantRaycastMask = ~0;
	public bool NoDomtEventEnabled = true;
	public GameObject NonDominantController = null;
	public LayerMask NonDominantRaycastMask = ~0;
	public List<WaveVR_ButtonList.EButtons> ButtonToTrigger = new List<WaveVR_ButtonList.EButtons> ();
	private GameObject Head = null;
	[HideInInspector]
	public ERaycastMode RaycastMode = ERaycastMode.Mouse;
	[HideInInspector]
	public ERaycastStartPoint RaycastStartPoint = ERaycastStartPoint.CenterOfEyes;
	[Tooltip("Will be obsoleted soon!")]
	public string CanvasTag = "EventCanvas";
	#endregion

	// Do NOT allow event DOWN being sent multiple times during CLICK_TIME.
	// Since UI element of Unity needs time to perform transitions.
	private const float CLICK_TIME = 0.2f;
	private const float DRAG_TIME = 0.2f;

	private const float raycastStartPointOffset = 0.0315f;

	private GameObject pointCameraNoDomt = null;
	private GameObject pointCameraDomint = null;
	public float FixedBeamLength = 9.5f;			// = Beam endOffsetMax, can be specified in editor mode.
	private float lengthFromBeamToPointer = 0.5f;   // = Beam enfOffsetMin
	private Color32 FlexiblePointerColor = Color.blue;
	private ERaycastMode preRaycastMode;
	private bool toChangeBeamPointer = true;   // Should change beam mesh on start.
	private Vector3 DomintIntersectPos_prev = Vector3.zero;
	private Vector3 NoDomtIntersectPos_prev = Vector3.zero;

	#region basic declaration
	[SerializeField]
	private bool mForceModuleActive = true;

	public bool ForceModuleActive
	{
		get { return mForceModuleActive; }
		set { mForceModuleActive = value; }
	}

	public override bool IsModuleSupported()
	{
		return mForceModuleActive;
	}

	public override bool ShouldActivateModule()
	{
		if (!base.ShouldActivateModule ())
			return false;

		if (mForceModuleActive)
			return true;

		return false;
	}

	public override void DeactivateModule() {
		base.DeactivateModule();

		for (int i = 0; i < WaveVR_Controller.DeviceTypes.Length; i++)
		{
			WaveVR_Controller.EDeviceType _dt = WaveVR_Controller.DeviceTypes [i];
			EventController _event_controller = GetEventController (_dt);
			if (_event_controller != null)
			{
				PointerEventData _ped = _event_controller.event_data;
				if (_ped != null)
				{
					// OnTriggerUp();
					PrintDebugLog ("DeactivateModule() exit " + _ped.pointerEnter);
					HandlePointerExitAndEnter (_ped, null);
				}
			}
		}
	}
	#endregion

	[System.Serializable]
	public class CBeamConfig
	{
		// Default mouse mode configurations.
		public float StartWidth;
		public float EndWidth;
		public float StartOffset;
		public float EndOffset;
		public Color32 StartColor;
		public Color32 EndColor;

		public void assignedTo(CBeamConfig src)
		{
			StartWidth = src.StartWidth;
			EndWidth = src.EndWidth;
			StartOffset = src.StartOffset;
			EndOffset = src.EndOffset;
			StartColor = src.StartColor;
			EndColor = src.EndColor;
		}
	}

	private CBeamConfig mouseBeamConfig = new CBeamConfig {
		StartWidth = 0.000625f,
		EndWidth = 0.00125f,
		StartOffset = 0.015f,
		EndOffset = 0.8f,
		StartColor = new Color32 (255, 255, 255, 255),
		EndColor = new Color32 (255, 255, 255, 77)
	};

	private CBeamConfig fixedBeamConfig = new CBeamConfig {
		StartWidth = 0.000625f,
		EndWidth = 0.00125f,
		StartOffset = 0.015f,
		EndOffset = 9.5f,
		StartColor = new Color32 (255, 255, 255, 255),
		EndColor = new Color32 (255, 255, 255, 255)
	};

	private CBeamConfig flexibleBeamConfig = new CBeamConfig {
		StartWidth = 0.000625f,
		EndWidth = 0.00125f,
		StartOffset = 0.015f,
		EndOffset = 0.8f,
		StartColor = new Color32 (255, 255, 255, 255),
		EndColor = new Color32 (255, 255, 255, 0)
	};

	public class RaycastModeSetting
	{
		public ERaycastMode Mode { get; set; }
		public CBeamConfig Config { get; set; }

		public RaycastModeSetting(ERaycastMode mode, CBeamConfig config)
		{
			this.Mode = mode;
			this.Config = config;
		}
	}

	public class EventController
	{
		public WaveVR_Controller.EDeviceType device {
			get;
			set;
		}

		public GameObject controller {
			get;
			set;
		}

		public GameObject prevRaycastedObject {
			get;
			set;
		}

		public PointerEventData event_data {
			get;
			set;
		}

		public WaveVR_ControllerPointer pointer {
			get;
			set;
		}

		public bool pointerEnabled {
			get;
			set;
		}

		public WaveVR_Beam beam {
			get;
			set;
		}

		public bool beamEnabled {
			get;
			set;
		}

		private List<RaycastModeSetting> raycastModeSettings;
		public void SetBeamConfig(ERaycastMode mode, CBeamConfig config)
		{
			for (int i = 0; i < raycastModeSettings.Count; i++)
			{
				if (raycastModeSettings [i].Mode == mode)
				{
					raycastModeSettings [i].Config.assignedTo (config);
				}
			}
		}
		public CBeamConfig GetBeamConfig(ERaycastMode mode)
		{
			for (int i = 0; i < raycastModeSettings.Count; i++)
			{
				if (raycastModeSettings [i].Mode == mode)
					return raycastModeSettings [i].Config;
			}
			return null;
		}

		public bool eligibleForButtonClick {
			get;
			set;
		}

		public EventController(WaveVR_Controller.EDeviceType type)
		{
			device = type;
			controller = null;
			prevRaycastedObject = null;
			event_data = null;
			eligibleForButtonClick = false;
			beam = null;
			beamEnabled = false;
			pointer = null;
			pointerEnabled = false;
			raycastModeSettings = new List<RaycastModeSetting>();
			for (int i = 0; i < RaycastModes.Length; i++)
			{
				ERaycastMode _mode = RaycastModes[i];
				raycastModeSettings.Add (new RaycastModeSetting(_mode, new CBeamConfig ()));
			}
		}
	}

	private List<EventController> EventControllers = new List<EventController>();
	private EventController GetEventController(WaveVR_Controller.EDeviceType dt)
	{
		for (int i = 0; i < EventControllers.Count; i++)
		{
			if (EventControllers [i].device == dt)
				return EventControllers [i];
		}
		return null;
	}

	private void SetControllerModel()
	{
		for (int i = 0; i < WaveVR_Controller.DeviceTypes.Length; i++)
		{
			WaveVR_Controller.EDeviceType _dt = WaveVR_Controller.DeviceTypes [i];
			// HMD uses Gaze, not controller input module.
			if (_dt == WaveVR_Controller.EDeviceType.Head)
				continue;

			EventController event_controller = GetEventController (_dt);

			GameObject _controller = event_controller.controller;
			GameObject _model = WaveVR_EventSystemControllerProvider.Instance.GetControllerModel (_dt);
			LayerMask _mask = ~0;
			if (_dt == WaveVR_Controller.EDeviceType.Dominant)
				_mask = this.DominantRaycastMask;
			if (_dt == WaveVR_Controller.EDeviceType.NonDominant)
				_mask = this.NonDominantRaycastMask;

			if (_controller == null)
			{
				if (_model != null)
				{
					// replace with new controller instance.
					if (_dt == WaveVR_Controller.EDeviceType.Head)
						PrintDebugLog ("SetControllerModel() Head replace null with new controller instance.");
					if (_dt == WaveVR_Controller.EDeviceType.Dominant)
						PrintDebugLog ("SetControllerModel() Dominant replace null with new controller instance.");
					if (_dt == WaveVR_Controller.EDeviceType.NonDominant)
						PrintDebugLog ("SetControllerModel() Non-Dominant replace null with new controller instance.");
					SetupEventController (event_controller, _model, _mask);
				}
			} else
			{
				if (_model == null)
				{
					if (WaveVR_EventSystemControllerProvider.Instance.HasControllerLoader(_dt))
					{
						// clear controller instance.
						if (_dt == WaveVR_Controller.EDeviceType.Head)
							PrintDebugLog ("SetControllerModel() Head clear controller instance.");
						if (_dt == WaveVR_Controller.EDeviceType.Dominant)
							PrintDebugLog ("SetControllerModel() Dominant clear controller instance.");
						if (_dt == WaveVR_Controller.EDeviceType.NonDominant)
							PrintDebugLog ("SetControllerModel() Non-Dominant clear controller instance.");
						SetupEventController (event_controller, null, _mask);
					}
				} else
				{
					if (!GameObject.ReferenceEquals (_controller, _model))
					{
						// replace with new controller instance.
						if (_dt == WaveVR_Controller.EDeviceType.Head)
							PrintDebugLog ("SetControllerModel() Head set new controller instance.");
						if (_dt == WaveVR_Controller.EDeviceType.Dominant)
							PrintDebugLog ("SetControllerModel() Dominant set new controller instance.");
						if (_dt == WaveVR_Controller.EDeviceType.NonDominant)
							PrintDebugLog ("SetControllerModel() NonDominant set new controller instance.");
						SetupEventController (event_controller, _model, _mask);
					}
				}
			}
		}
	}

	private void SetupEventController(EventController eventController, GameObject controller_model, LayerMask mask)
	{
		// Diactivate old controller, replace with new controller, activate new controller
		if (eventController.controller != null)
		{
			PrintDebugLog ("SetupEventController() deactivate " + eventController.controller.name);
			eventController.controller.SetActive (false);
		}

		eventController.controller = controller_model;

		// Note: must setup beam first.
		if (eventController.controller != null)
		{
			PrintDebugLog ("SetupEventController() activate " + eventController.controller.name);
			eventController.controller.SetActive (true);

			eventController.SetBeamConfig (ERaycastMode.Beam, flexibleBeamConfig);
			eventController.SetBeamConfig (ERaycastMode.Fixed, fixedBeamConfig);
			eventController.SetBeamConfig (ERaycastMode.Mouse, mouseBeamConfig);

			// Get beam of controller.
			eventController.beam = eventController.controller.GetComponentInChildren<WaveVR_Beam> (true);
			if (eventController.beam != null)
			{
				PrintDebugLog ("SetupEventController() set up WaveVR_Beam: " + eventController.beam.gameObject.name + " of " + eventController.device);
				SetupEventControllerBeam (eventController, Vector3.zero, true);
			}

			// Get pointer of controller.
			PhysicsRaycaster _raycaster = eventController.controller.GetComponentInChildren<PhysicsRaycaster> ();

			eventController.pointer = eventController.controller.GetComponentInChildren<WaveVR_ControllerPointer> (true);
			if (eventController.pointer != null)
			{
				PrintDebugLog ("SetupEventController() set up WaveVR_ControllerPointer: " + eventController.pointer.gameObject.name + " of " + eventController.device);

				// Get PhysicsRaycaster of pointer. If none, add new one.
				if (_raycaster == null)
				{
					PrintDebugLog ("SetupEventController() add PhysicsRaycaster on " + eventController.pointer.gameObject.name);
					_raycaster = eventController.pointer.gameObject.AddComponent<PhysicsRaycaster> ();
				}

				SetupEventControllerPointer (eventController);
			} else
			{
				// Get PhysicsRaycaster of controller. If none, add new one.
				if (_raycaster == null)
				{
					PrintDebugLog ("SetupEventController() add PhysicsRaycaster on " + eventController.controller.name);
					_raycaster = eventController.controller.AddComponent<PhysicsRaycaster> ();
				}
			}
			_raycaster.eventMask = mask;
			PrintDebugLog ("SetupEventController() physics mask: " + _raycaster.eventMask.value);

			// Disable Camera to save rendering cost.
			Camera _event_camera = _raycaster.gameObject.GetComponent<Camera> ();
			if (_event_camera != null)
			{
				_event_camera.stereoTargetEye = StereoTargetEyeMask.None;
				_event_camera.enabled = false;
			}

			Camera _controller_camera = eventController.controller.GetComponent<Camera>();
			if (_controller_camera != null)
			{
				PrintDebugLog ("SetupEventController() found controller camera of " + eventController.controller.name);
				_controller_camera.enabled = false;
			}
		}
	}

	private void SetupEventControllerBeam(EventController eventController, Vector3 intersectionPosition, bool updateRaycastConfig = false)
	{
		if (eventController.beam == null)
			return;

		CBeamConfig _config = eventController.GetBeamConfig (this.RaycastMode);

		if (updateRaycastConfig)
		{
			_config.StartWidth = eventController.beam.StartWidth;
			_config.EndWidth = eventController.beam.EndWidth;
			_config.StartOffset = eventController.beam.StartOffset;
			_config.StartColor = eventController.beam.StartColor;
			_config.EndColor = eventController.beam.EndColor;

			switch (this.RaycastMode)
			{
			case ERaycastMode.Beam:
			case ERaycastMode.Mouse:
				_config.EndOffset = eventController.beam.EndOffset;
				break;
			case ERaycastMode.Fixed:
				_config.EndOffset = this.FixedBeamLength;
				break;
			default:
				break;
			}
			eventController.SetBeamConfig (this.RaycastMode, _config);

			PrintDebugLog ("SetupEventControllerBeam() " + eventController.device + ", " + this.RaycastMode + " mode config - "
				+ "StartWidth: " + _config.StartWidth
				+ ", EndWidth: " + _config.EndWidth
				+ ", StartOffset: " + _config.StartOffset
				+ ", EndOffset: " + _config.EndOffset
				+ ", StartColor: " + _config.StartColor.ToString()
				+ ", EndColor: " + _config.EndColor.ToString()
			);
		}

		eventController.beam.StartWidth = _config.StartWidth;
		eventController.beam.EndWidth = _config.EndWidth;
		eventController.beam.StartOffset = _config.StartOffset;
		eventController.beam.EndOffset = _config.EndOffset;
		eventController.beam.StartColor = _config.StartColor;
		eventController.beam.EndColor = _config.EndColor;

		PrintDebugLog ("SetupEventControllerBeam() " + eventController.device + ", " + this.RaycastMode + " mode"
			+ ", StartWidth: " + eventController.beam.StartWidth
			+ ", EndWidth: " + eventController.beam.EndWidth
			+ ", StartOffset: " + eventController.beam.StartOffset
			+ ", length: " + eventController.beam.EndOffset
			+ ", StartColor: " + eventController.beam.StartColor.ToString ()
			+ ", EndColor: " + eventController.beam.EndColor.ToString ());
	}

	private void SetupEventControllerPointer(EventController eventController, Camera eventCamera, Vector3 intersectionPosition)
	{
		if (eventController.pointer == null)
			return;

		float pointerDistanceInMeters = 0, pointerOuterDiameter = 0;
		GameObject _curRaycastedObject = GetRaycastedObject (eventController.device);

		// Due to pointer distance is changed by beam length, do NOT load OEM CONFIG.
		switch (this.RaycastMode)
		{
		case ERaycastMode.Mouse:
			if (eventController.beam != null)
				pointerDistanceInMeters = eventController.beam.EndOffset + eventController.beam.endOffsetMin;
			else
				pointerDistanceInMeters = this.mouseBeamConfig.EndOffset + this.lengthFromBeamToPointer;

			pointerOuterDiameter = eventController.pointer.PointerOuterDiameterMin + (pointerDistanceInMeters / eventController.pointer.kpointerGrowthAngle);

			eventController.pointer.PointerDistanceInMeters = pointerDistanceInMeters;
			eventController.pointer.PointerOuterDiameter = pointerOuterDiameter;
			eventController.pointer.PointerColor = Color.white;
			eventController.pointer.PointerRenderQueue = 5000;
			break;
		case ERaycastMode.Fixed:
			if (eventController.beam != null)
				pointerDistanceInMeters = eventController.beam.EndOffset + eventController.beam.endOffsetMin;
			else
				pointerDistanceInMeters = this.fixedBeamConfig.EndOffset + this.lengthFromBeamToPointer;

			pointerOuterDiameter = eventController.pointer.PointerOuterDiameterMin * pointerDistanceInMeters;
			eventController.pointer.PointerDistanceInMeters = pointerDistanceInMeters;
			eventController.pointer.PointerOuterDiameter = pointerOuterDiameter;
			eventController.pointer.PointerRenderQueue = 1000;
			break;
		case ERaycastMode.Beam:
			if (_curRaycastedObject != null)
			{
				eventController.pointer.OnPointerEnter (eventCamera, _curRaycastedObject, intersectionPosition, true);
				eventController.pointer.PointerColor = FlexiblePointerColor;
			} else
			{
				eventController.pointer.PointerColor = Color.white;
				if (eventController.beam != null)
					eventController.pointer.PointerDistanceInMeters = eventController.beam.EndOffset + eventController.beam.endOffsetMin;
			}

			pointerDistanceInMeters = eventController.pointer.PointerDistanceInMeters;
			pointerOuterDiameter = eventController.pointer.PointerOuterDiameterMin
				+ (pointerDistanceInMeters / eventController.pointer.kpointerGrowthAngle);
			eventController.pointer.PointerOuterDiameter = pointerOuterDiameter;
			eventController.pointer.PointerRenderQueue = 5000;
			break;
		default:
			break;
		}

		PrintDebugLog ("SetupEventControllerPointer() " + eventController.device + ", " + this.RaycastMode + " mode"
			+ ", pointerDistanceInMeters: " + pointerDistanceInMeters
			+ ", pointerOuterDiameter: " + pointerOuterDiameter);
	}

	private void SetupEventControllerPointer(EventController eventController)
	{
		if (eventController.pointer == null)
			return;

		SetupEventControllerPointer (eventController, null, Vector3.zero);
	}

	public void ChangeBeamLength(WaveVR_Controller.EDeviceType dt, float length)
	{
		EventController _eventController = GetEventController (dt);
		if (_eventController == null)
			return;

		if (this.RaycastMode == ERaycastMode.Fixed || this.RaycastMode == ERaycastMode.Mouse)
		{
			CBeamConfig _config = _eventController.GetBeamConfig (this.RaycastMode);
			_config.EndOffset = length;
			_eventController.SetBeamConfig (this.RaycastMode, _config);
		}

		SetupEventControllerBeam (_eventController, Vector3.zero, false);
		SetupEventControllerPointer (_eventController);
	}

	private void SetupPointerCamera(WaveVR_Controller.EDeviceType type)
	{
		if (this.Head == null)
		{
			PrintDebugLog ("SetupPointerCamera() no Head!!");
			return;
		}
		if (type == WaveVR_Controller.EDeviceType.Dominant)
		{
			pointCameraDomint = new GameObject ("PointerCameraR");
			if (pointCameraDomint == null)
				return;

			PrintDebugLog ("SetupPointerCamera() Dominant - add component WaveVR_PointerCameraTracker");
			pointCameraDomint.AddComponent<WaveVR_PointerCameraTracker> ();
			PrintDebugLog ("SetupPointerCamera() Dominant add component - WaveVR_PoseTrackerManager");
			pointCameraDomint.AddComponent<WaveVR_PoseTrackerManager> ();
			PhysicsRaycaster _raycaster = pointCameraDomint.AddComponent<PhysicsRaycaster> ();
			if (_raycaster != null)
			{
				_raycaster.eventMask = this.DominantRaycastMask;
				PrintDebugLog ("SetupPointerCamera() Dominant - set physics raycast mask to " + _raycaster.eventMask.value);
			}
			pointCameraDomint.transform.SetParent (this.Head.transform, false);
			PrintDebugLog ("SetupPointerCamera() Dominant - set pointerCamera parent to " + this.pointCameraDomint.transform.parent.name);
			if (WaveVR_Render.Instance != null && WaveVR_Render.Instance.righteye != null)
			{
				this.pointCameraDomint.transform.position = WaveVR_Render.Instance.righteye.transform.position;
			} else
			{
				if (RaycastStartPoint == ERaycastStartPoint.LeftEye)
				{
					pointCameraDomint.transform.localPosition = new Vector3 (-raycastStartPointOffset, 0f, 0.15f);
				} else if (RaycastStartPoint == ERaycastStartPoint.RightEye)
				{
					pointCameraDomint.transform.localPosition = new Vector3 (raycastStartPointOffset, 0f, 0.15f);
				} else
				{
					pointCameraDomint.transform.localPosition = new Vector3 (0f, 0f, 0.15f);
				}
			}
			Camera pc = pointCameraDomint.GetComponent<Camera> ();
			if (pc != null)
			{
				pc.enabled = false;
				pc.fieldOfView = 1f;
				pc.nearClipPlane = 0.01f;
			}
			WaveVR_PointerCameraTracker pcTracker = pointCameraDomint.GetComponent<WaveVR_PointerCameraTracker> ();
			if (pcTracker != null)
			{
				pcTracker.setDeviceType (type);
			}
			WaveVR_PoseTrackerManager poseTracker = pointCameraDomint.GetComponent<WaveVR_PoseTrackerManager> ();
			if (poseTracker != null)
			{
				PrintDebugLog ("SetupPointerCamera() Dominant - disable WaveVR_PoseTrackerManager");
				poseTracker.Type = type;
				poseTracker.TrackPosition = false;
				poseTracker.TrackRotation = false;
				poseTracker.enabled = false;
			}
		} else if (type == WaveVR_Controller.EDeviceType.NonDominant)
		{
			pointCameraNoDomt = new GameObject ("PointerCameraL");
			if (pointCameraNoDomt == null)
				return;

			PrintDebugLog ("SetupPointerCamera() NonDominant - add component WaveVR_PointerCameraTracker");
			pointCameraNoDomt.AddComponent<WaveVR_PointerCameraTracker> ();
			PrintDebugLog ("SetupPointerCamera() NonDominant add component - WaveVR_PoseTrackerManager");
			pointCameraNoDomt.AddComponent<WaveVR_PoseTrackerManager> ();
			PhysicsRaycaster _raycaster = pointCameraNoDomt.AddComponent<PhysicsRaycaster> ();
			if (_raycaster != null)
			{
				_raycaster.eventMask = this.NonDominantRaycastMask;
				PrintDebugLog ("SetupPointerCamera() NonDominant - set physics raycast mask to " + _raycaster.eventMask.value);
			}
			pointCameraNoDomt.transform.SetParent (this.Head.transform, false);
			PrintDebugLog ("SetupPointerCamera() NonDominant - set pointerCamera parent to " + this.pointCameraNoDomt.transform.parent.name);
			if (WaveVR_Render.Instance != null && WaveVR_Render.Instance.lefteye != null)
			{
				this.pointCameraNoDomt.transform.position = WaveVR_Render.Instance.lefteye.transform.position;
			} else
			{
				if (RaycastStartPoint == ERaycastStartPoint.LeftEye)
				{
					pointCameraNoDomt.transform.localPosition = new Vector3 (-raycastStartPointOffset, 0f, 0.15f);
				} else if (RaycastStartPoint == ERaycastStartPoint.RightEye)
				{
					pointCameraNoDomt.transform.localPosition = new Vector3 (raycastStartPointOffset, 0f, 0.15f);
				} else
				{
					pointCameraNoDomt.transform.localPosition = new Vector3 (0f, 0f, 0.15f);
				}
			}
			Camera pc = pointCameraNoDomt.GetComponent<Camera> ();
			if (pc != null)
			{
				pc.enabled = false;
				pc.fieldOfView = 1f;
				pc.nearClipPlane = 0.01f;
			}
			WaveVR_PointerCameraTracker pcTracker = pointCameraNoDomt.GetComponent<WaveVR_PointerCameraTracker> ();
			if (pcTracker != null)
			{
				pcTracker.setDeviceType (type);
			}
			WaveVR_PoseTrackerManager poseTracker = pointCameraNoDomt.GetComponent<WaveVR_PoseTrackerManager> ();
			if (poseTracker != null)
			{
				PrintDebugLog ("SetupPointerCamera() NonDominant - disable WaveVR_PoseTrackerManager");
				poseTracker.Type = type;
				poseTracker.TrackPosition = false;
				poseTracker.TrackRotation = false;
				poseTracker.enabled = false;
			}
		}
	}

	#region Override BaseInputModule
	private bool enableControllerInputModule = false;
	protected override void OnEnable()
	{
		if (!enableControllerInputModule)
		{
			base.OnEnable ();
			PrintDebugLog ("OnEnable()");

			enableControllerInputModule = true;
			for (int i = 0; i < WaveVR_Controller.DeviceTypes.Length; i++)
				EventControllers.Add (new EventController (WaveVR_Controller.DeviceTypes [i]));

			// Right controller
			if (this.DominantController != null)
			{
				EventController ec = GetEventController (WaveVR_Controller.EDeviceType.Dominant);
				SetupEventController (
					ec,
					this.DominantController,
					this.DominantRaycastMask
				);
			}

			// Left controller
			if (this.NonDominantController != null)
			{
				EventController ec = GetEventController (WaveVR_Controller.EDeviceType.NonDominant);
				SetupEventController (
					ec,
					this.NonDominantController,
					this.NonDominantRaycastMask
				);
			}

			if (this.Head == null)
			{
				if (WaveVR_Render.Instance != null)
				{
					this.Head = WaveVR_Render.Instance.gameObject;
					PrintDebugLog ("OnEnable() set up Head to " + this.Head.name);
				}
			}

			this.preRaycastMode = this.RaycastMode;
		}
	}

	protected override void OnDisable()
	{
		if (enableControllerInputModule)
		{
			base.OnDisable ();
			PrintDebugLog ("OnDisable()");

			enableControllerInputModule = false;
			for (int i = 0; i < WaveVR_Controller.DeviceTypes.Length; i++)
			{
				WaveVR_Controller.EDeviceType _dt = WaveVR_Controller.DeviceTypes [i];
				EventController _event_controller = GetEventController (_dt);
				if (_event_controller != null)
				{
					PointerEventData _ped = _event_controller.event_data;
					if (_ped != null)
					{
						PrintDebugLog ("OnDisable() exit " + _ped.pointerEnter);
						HandlePointerExitAndEnter (_ped, null);
					}
				}
			}
			EventControllers.Clear ();
		}
	}

	public override void Process()
	{
		if (!enableControllerInputModule)
			return;

		SetControllerModel ();

		if (this.Head == null)
		{
			if (WaveVR_Render.Instance != null)
			{
				this.Head = WaveVR_Render.Instance.gameObject;
				PrintDebugLog ("Process() setup Head to " + this.Head.name);
			}
		}

		for (int i = 0; i < WaveVR_Controller.DeviceTypes.Length; i++)
		{
			WaveVR_Controller.EDeviceType _dt = WaveVR_Controller.DeviceTypes [i];
			// -------------------- Conditions for running loop begins -----------------
			// HMD uses Gaze, not controller input module.
			if (_dt == WaveVR_Controller.EDeviceType.Head)
				continue;

			EventController _eventController = GetEventController (_dt);
			if (_eventController == null)
				continue;

			GameObject _controller = _eventController.controller;
			if (_controller == null)
				continue;

			CheckBeamPointerActive (_eventController);

			if (_dt == WaveVR_Controller.EDeviceType.Dominant && this.DomintEventEnabled == false)
				continue;

			if (_dt == WaveVR_Controller.EDeviceType.NonDominant && this.NoDomtEventEnabled == false)
				continue;

			if (WaveVR.Instance.Initialized)
			{
				if (WaveVR.Instance.FocusCapturedBySystem)
				{
					HandlePointerExitAndEnter (_eventController.event_data, null);
					continue;
				}
			}

			bool _connected = false;
			// "connected" from WaveVR means the "pose" is valid or not.
			WaveVR.Device _device = WaveVR.Instance.getDeviceByType (_dt);
			if (_device != null)
				_connected = _device.connected;

			if (!_connected)
				continue;
			// -------------------- Conditions for running loop ends -----------------


			_eventController.prevRaycastedObject = GetRaycastedObject (_dt);

			if (_connected)
			{
				if ((_dt == WaveVR_Controller.EDeviceType.NonDominant && pointCameraNoDomt == null) ||
					(_dt == WaveVR_Controller.EDeviceType.Dominant && pointCameraDomint == null))
					SetupPointerCamera (_dt);
			}

			Camera _event_camera = null;
			// Mouse mode: raycasting from HMD after direct raycasting from controller
			if (RaycastMode == ERaycastMode.Mouse)
			{
				_event_camera = _dt == WaveVR_Controller.EDeviceType.NonDominant ?
					(pointCameraNoDomt != null ? pointCameraNoDomt.GetComponent<Camera> () : null) :
					(pointCameraDomint != null ? pointCameraDomint.GetComponent<Camera> () : null);
				ResetPointerEventData_Hybrid (_dt, _event_camera);
			} else
			{
				_event_camera = (Camera)_controller.GetComponentInChildren (typeof(Camera));
				ResetPointerEventData (_dt);
			}
			if (_event_camera == null)
				continue;

			// 1. Get the nearest graphic raycast object.
			GraphicRaycast (_eventController, _event_camera);

			// 2. Get the physics raycast object if nearer than previous object.
			PhysicsRaycaster _raycaster = null;
			if (RaycastMode == ERaycastMode.Mouse)
			{
				_raycaster = _event_camera.GetComponent<PhysicsRaycaster> ();
			} else
			{
				_raycaster = _controller.GetComponentInChildren<PhysicsRaycaster> ();
			}
			if (_raycaster == null)
				continue;

			if (RaycastMode == ERaycastMode.Mouse)
				ResetPointerEventData_Hybrid (_dt, _event_camera);
			else
				ResetPointerEventData (_dt);

			// Issue: GC.Alloc 40 bytes.
			PhysicsRaycast (_eventController, _raycaster);

			// 3. Exit previous object, enter new object.
			OnTriggerEnterAndExit (_dt, _eventController.event_data);

			// 4. Hover object.
			GameObject _curRaycastedObject = GetRaycastedObject (_dt);
			if (_curRaycastedObject != null && _curRaycastedObject == _eventController.prevRaycastedObject)
			{
				OnTriggerHover (_dt, _eventController.event_data);
			}

			// 5. Get button state.
			bool btnPressDown = false, btnPressed = false, btnPressUp = false;
			for (int b = 0; b < this.ButtonToTrigger.Count; b++)
			{
				btnPressDown |= WaveVR_Controller.Input (_dt).GetPressDown ((WVR_InputId)this.ButtonToTrigger [b]);
				btnPressed |= WaveVR_Controller.Input (_dt).GetPress ((WVR_InputId)this.ButtonToTrigger [b]);
				btnPressUp |= WaveVR_Controller.Input (_dt).GetPressUp ((WVR_InputId)this.ButtonToTrigger [b]);
			}

			if (btnPressDown)
				_eventController.eligibleForButtonClick = true;
			// Pointer Click equals to Button.onClick, we sent Pointer Click in OnTriggerUp()
			//if (btnPressUp && _eventController.eligibleForButtonClick)
			//onButtonClick (_eventController);

			if (!btnPressDown && btnPressed)
			{
				// button hold means to drag.
				if (!UnityMouseMode)
					OnDrag (_dt, _eventController.event_data);
				else
					OnDragMouse (_dt, _eventController.event_data);
			} else if (Time.unscaledTime - _eventController.event_data.clickTime < CLICK_TIME)
			{
				// Delay new events until CLICK_TIME has passed.
			} else if (btnPressDown && !_eventController.event_data.eligibleForClick)
			{
				// 1. button not pressed -> pressed.
				// 2. no pending Click should be procced.
				OnTriggerDown (_dt, _eventController.event_data);
			} else if (!btnPressed)
			{
				// 1. If Down before, send Up event and clear Down state.
				// 2. If Dragging, send Drop & EndDrag event and clear Dragging state.
				// 3. If no Down or Dragging state, do NOTHING.
				if (!UnityMouseMode)
					OnTriggerUp (_dt, _eventController.event_data);
				else
					OnTriggerUpMouse (_dt, _eventController.event_data);
			}

			PointerEventData _event_data = _eventController.event_data;
			Vector3 _intersectionPosition = GetIntersectionPosition (_event_data.enterEventCamera, _event_data.pointerCurrentRaycast);

			WaveVR_RaycastResultProvider.Instance.SetRaycastResult (
				_dt,
				_eventController.event_data.pointerCurrentRaycast.gameObject,
				_intersectionPosition);

			// Update beam & pointer when:
			// 1. Raycast mode changed.
			// 2. Beam or Pointer active state changed.
			if (this.toChangeBeamPointer || this.preRaycastMode != this.RaycastMode)
			{
				if (this.RaycastMode == ERaycastMode.Beam)
					PrintDebugLog ("Process() controller raycast mode is flexible beam.");
				if (this.RaycastMode == ERaycastMode.Fixed)
					PrintDebugLog ("Process() controller raycast mode is fixed beam.");
				if (this.RaycastMode == ERaycastMode.Mouse)
					PrintDebugLog ("Process() controller raycast mode is mouse.");
				SetupEventControllerBeam (_eventController, _intersectionPosition, false);
				SetupEventControllerPointer (_eventController, _event_data.enterEventCamera, _intersectionPosition);

				this.toChangeBeamPointer = false;
			}

			// Update flexible beam and pointer when intersection position changes.
			Vector3 _intersectionPosition_prev =
				(_dt == WaveVR_Controller.EDeviceType.Dominant) ? DomintIntersectPos_prev : NoDomtIntersectPos_prev;
			if (_intersectionPosition_prev != _intersectionPosition)
			{
				_intersectionPosition_prev = _intersectionPosition;
				if (this.RaycastMode == ERaycastMode.Beam && _curRaycastedObject != null)
				{
					if (_eventController.pointer != null)
						_eventController.pointer.OnPointerEnter (_event_data.enterEventCamera, _curRaycastedObject, _intersectionPosition, true);
					if (_eventController.beam != null)
						_eventController.beam.SetEndOffset (_intersectionPosition, false);

					if (Log.gpl.Print)
						PrintDebugLog ("Process() " + _dt + ", _intersectionPosition_prev (" + _intersectionPosition_prev.x + ", " + _intersectionPosition_prev.y + ", " + _intersectionPosition_prev.z + ")");
				}

				if (_dt == WaveVR_Controller.EDeviceType.Dominant)
					DomintIntersectPos_prev = _intersectionPosition_prev;
				if (_dt == WaveVR_Controller.EDeviceType.NonDominant)
					NoDomtIntersectPos_prev = _intersectionPosition_prev;
			}
		}

		this.preRaycastMode = this.RaycastMode;

		SetPointerCameraTracker ();
	}
	#endregion

	#region EventSystem
	private void OnTriggerDown(WaveVR_Controller.EDeviceType type, PointerEventData event_data)
	{
		GameObject _go = GetRaycastedObject (type);
		if (_go == null)
			return;

		// Send Pointer Down. If not received, get handler of Pointer Click.
		event_data.pressPosition = event_data.position;
		event_data.pointerPressRaycast = event_data.pointerCurrentRaycast;
		event_data.pointerPress =
			ExecuteEvents.ExecuteHierarchy(_go, event_data, ExecuteEvents.pointerDownHandler)
			?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(_go);

		PrintDebugLog ("OnTriggerDown() device: " + type + " send Pointer Down to " + event_data.pointerPress + ", current GameObject is " + _go);

		// If Drag Handler exists, send initializePotentialDrag event.
		event_data.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(_go);
		if (event_data.pointerDrag != null)
		{
			PrintDebugLog ("OnTriggerDown() device: " + type + " send initializePotentialDrag to " + event_data.pointerDrag + ", current GameObject is " + _go);
			ExecuteEvents.Execute(event_data.pointerDrag, event_data, ExecuteEvents.initializePotentialDrag);
		}

		// press happened (even not handled) object.
		event_data.rawPointerPress = _go;
		// allow to send Pointer Click event
		event_data.eligibleForClick = true;
		// reset the screen position of press, can be used to estimate move distance
		event_data.delta = Vector2.zero;
		// current Down, reset drag state
		event_data.dragging = false;
		event_data.useDragThreshold = true;
		// record the count of Pointer Click should be processed, clean when Click event is sent.
		event_data.clickCount = 1;
		// set clickTime to current time of Pointer Down instead of Pointer Click.
		// since Down & Up event should not be sent too closely. (< CLICK_TIME)
		event_data.clickTime = Time.unscaledTime;
	}

	private void OnTriggerUp(WaveVR_Controller.EDeviceType type, PointerEventData event_data)
	{
		if (!event_data.eligibleForClick && !event_data.dragging)
		{
			// 1. no pending click
			// 2. no dragging
			// Mean user has finished all actions and do NOTHING in current frame.
			return;
		}

		GameObject _go = GetRaycastedObject (type);
		// _go may be different with event_data.pointerDrag so we don't check null

		if (event_data.pointerPress != null)
		{
			// In the frame of button is pressed -> unpressed, send Pointer Up
			PrintDebugLog ("OnTriggerUp type: " + type + " send Pointer Up to " + event_data.pointerPress);
			ExecuteEvents.Execute (event_data.pointerPress, event_data, ExecuteEvents.pointerUpHandler);
		}
		if (event_data.eligibleForClick)
		{
			// In the frame of button from being pressed to unpressed, send Pointer Click if Click is pending.
			PrintDebugLog ("OnTriggerUp type: " + type + " send Pointer Click to " + event_data.pointerPress);
			ExecuteEvents.Execute(event_data.pointerPress, event_data, ExecuteEvents.pointerClickHandler);
		} else if (event_data.dragging)
		{
			// In next frame of button from being pressed to unpressed, send Drop and EndDrag if dragging.
			PrintDebugLog ("OnTriggerUp type: " + type + " send Pointer Drop to " + _go + ", EndDrag to " + event_data.pointerDrag);
			ExecuteEvents.ExecuteHierarchy(_go, event_data, ExecuteEvents.dropHandler);
			ExecuteEvents.Execute(event_data.pointerDrag, event_data, ExecuteEvents.endDragHandler);

			event_data.pointerDrag = null;
			event_data.dragging = false;
		}

		// Down of pending Click object.
		event_data.pointerPress = null;
		// press happened (even not handled) object.
		event_data.rawPointerPress = null;
		// clear pending state.
		event_data.eligibleForClick = false;
		// Click is processed, clearcount.
		event_data.clickCount = 0;
		// Up is processed thus clear the time limitation of Down event.
		event_data.clickTime = 0;
	}

	private void OnTriggerUpMouse(WaveVR_Controller.EDeviceType type, PointerEventData event_data)
	{
		if (!event_data.eligibleForClick && !event_data.dragging)
		{
			// 1. no pending click
			// 2. no dragging
			// Mean user has finished all actions and do NOTHING in current frame.
			return;
		}

		GameObject _go = GetRaycastedObject (type);
		// _go may be different with event_data.pointerDrag so we don't check null

		if (event_data.pointerPress != null)
		{
			// In the frame of button is pressed -> unpressed, send Pointer Up
			PrintDebugLog ("OnTriggerUpMouse() type: " + type + " send Pointer Up to " + event_data.pointerPress);
			ExecuteEvents.Execute (event_data.pointerPress, event_data, ExecuteEvents.pointerUpHandler);
		}

		if (event_data.eligibleForClick)
		{
			GameObject _pointerClick = ExecuteEvents.GetEventHandler<IPointerClickHandler> (_go);
			if (_pointerClick != null)
			{
				if (_pointerClick == event_data.pointerPress)
				{
					// In the frame of button from being pressed to unpressed, send Pointer Click if Click is pending.
					PrintDebugLog ("OnTriggerUpMouse() type: " + type + " send Pointer Click to " + event_data.pointerPress);
					ExecuteEvents.Execute (event_data.pointerPress, event_data, ExecuteEvents.pointerClickHandler);
				} else
				{
					PrintDebugLog ("OnTriggerUpMouse() type: " + type
					+ " pointer down object " + event_data.pointerPress
					+ " is different with click object " + _pointerClick);
				}
			} else
			{
				if (event_data.dragging)
				{
					GameObject _pointerDrop = ExecuteEvents.GetEventHandler<IDropHandler> (_go);
					if (_pointerDrop == event_data.pointerDrag)
					{
						// In next frame of button from being pressed to unpressed, send Drop and EndDrag if dragging.
						PrintDebugLog ("OnTriggerUpMouse() type: " + type + " send Pointer Drop to " + event_data.pointerDrag);
						ExecuteEvents.Execute (event_data.pointerDrag, event_data, ExecuteEvents.dropHandler);
					}
					PrintDebugLog ("OnTriggerUpMouse() type: " + type + " send Pointer endDrag to " + event_data.pointerDrag);
					ExecuteEvents.Execute (event_data.pointerDrag, event_data, ExecuteEvents.endDragHandler);

					event_data.pointerDrag = null;
					event_data.dragging = false;
				}
			}
		}

		// Down of pending Click object.
		event_data.pointerPress = null;
		// press happened (even not handled) object.
		event_data.rawPointerPress = null;
		// clear pending state.
		event_data.eligibleForClick = false;
		// Click is processed, clearcount.
		event_data.clickCount = 0;
		// Up is processed thus clear the time limitation of Down event.
		event_data.clickTime = 0;
	}

	private void OnDrag(WaveVR_Controller.EDeviceType type, PointerEventData event_data)
	{
		if (event_data.pointerDrag != null && !event_data.dragging)
		{
			PrintDebugLog ("OnDrag() device: " + type + " send BeginDrag to " + event_data.pointerDrag);
			ExecuteEvents.Execute(event_data.pointerDrag, event_data, ExecuteEvents.beginDragHandler);
			event_data.dragging = true;
		}

		// Drag notification
		if (event_data.dragging && event_data.pointerDrag != null)
		{
			// Before doing drag we should cancel any pointer down state
			if (event_data.pointerPress != event_data.pointerDrag)
			{
				PrintDebugLog ("OnDrag device: " + type + " send Pointer Up to " + event_data.pointerPress + ", drag object: " + event_data.pointerDrag);
				ExecuteEvents.Execute(event_data.pointerPress, event_data, ExecuteEvents.pointerUpHandler);

				// since Down state is cleaned, no Click should be processed.
				event_data.eligibleForClick = false;
				event_data.pointerPress = null;
				event_data.rawPointerPress = null;
			}
			/*
			PrintDebugLog ("OnDrag() device: " + type + " send Pointer Drag to " + event_data.pointerDrag +
				"camera: " + event_data.enterEventCamera +
				" (" + event_data.enterEventCamera.ScreenToWorldPoint (
					new Vector3 (
						event_data.position.x,
						event_data.position.y,
						event_data.pointerDrag.transform.position.z
					)) +
				")");
			*/
			ExecuteEvents.Execute(event_data.pointerDrag, event_data, ExecuteEvents.dragHandler);
		}
	}

	private void OnDragMouse(WaveVR_Controller.EDeviceType type, PointerEventData event_data)
	{
		if (event_data.pointerDrag != null && !event_data.dragging)
		{
			PrintDebugLog ("OnDragMouse() device: " + type + " send BeginDrag to " + event_data.pointerDrag);
			ExecuteEvents.Execute(event_data.pointerDrag, event_data, ExecuteEvents.beginDragHandler);
			event_data.dragging = true;
		}

		if (Time.unscaledTime - event_data.clickTime < DRAG_TIME)
			return;

		if (event_data.dragging && event_data.pointerDrag != null)
		{
			ExecuteEvents.Execute(event_data.pointerDrag, event_data, ExecuteEvents.dragHandler);
		}
	}

	private void OnTriggerHover(WaveVR_Controller.EDeviceType type, PointerEventData event_data)
	{
		GameObject _go = GetRaycastedObject (type);

		ExecuteEvents.ExecuteHierarchy(_go, event_data, WaveVR_ExecuteEvents.pointerHoverHandler);
	}

	private void OnTriggerEnterAndExit(WaveVR_Controller.EDeviceType type, PointerEventData event_data)
	{
		GameObject _go = GetRaycastedObject (type);

		// Means hover.
		if (_go != null && event_data.pointerEnter == _go)
			return;

		// Exit before changing event_data.pointerEnter
		if (event_data.pointerEnter != null)
		{
			PrintDebugLog ("OnTriggerEnterAndExit() " + type + ", exit: " + event_data.pointerEnter);
			ExecuteEvents.Execute (event_data.pointerEnter, event_data, ExecuteEvents.pointerExitHandler);
		}

		if (_go != null && event_data.pointerEnter != _go)
		{
			PrintDebugLog ("OnTriggerEnterAndExit() " + type + ", enter: " + _go.name);
			event_data.pointerEnter = _go;
			ExecuteEvents.Execute (event_data.pointerEnter, event_data, ExecuteEvents.pointerEnterHandler);
		}

		if (_go == null)
		{
			event_data.pointerEnter = null;
		}
	}
	#endregion

	private void onButtonClick(EventController event_controller)
	{
		GameObject _go = GetRaycastedObject (event_controller.device);
		event_controller.eligibleForButtonClick = false;

		if (_go == null)
			return;

		Button _btn = _go.GetComponent<Button> ();
		if (_btn != null)
		{
			PrintDebugLog ("onButtonClick() trigger Button.onClick to " + _btn + " from " + event_controller.device);
			_btn.onClick.Invoke ();
		} else
		{
			PrintDebugLog ("onButtonClick() " + event_controller.device + ", " + _go.name + " does NOT contain Button!");
		}
	}

	#region Raycast
	List<RaycastResult> physicsRaycastResults = new List<RaycastResult>();
	private void PhysicsRaycast(EventController event_controller, PhysicsRaycaster raycaster)
	{
		physicsRaycastResults.Clear ();
		Profiler.BeginSample ("PhysicsRaycaster.Raycast()");
		raycaster.Raycast (event_controller.event_data, physicsRaycastResults);
		Profiler.EndSample ();

		RaycastResult _firstResult = FindFirstRaycast (physicsRaycastResults);
		//event_controller.event_data.pointerCurrentRaycast = _firstResult;

		if (_firstResult.module != null)
		{
			//PrintDebugLog ("PhysicsRaycast() device: " + event_controller.device + ", camera: " + _firstResult.module.eventCamera + ", first result = " + _firstResult);
		}

		if (_firstResult.gameObject != null)
		{
			if (_firstResult.worldPosition == Vector3.zero)
			{
				_firstResult.worldPosition = GetIntersectionPosition (
					_firstResult.module.eventCamera,
					//_eventController.event_data.enterEventCamera,
					_firstResult
				);
			}

			float new_dist =
				Mathf.Abs (
					_firstResult.worldPosition.z -
					_firstResult.module.eventCamera.transform.position.z);
			float origin_dist =
				Mathf.Abs (
					event_controller.event_data.pointerCurrentRaycast.worldPosition.z -
					_firstResult.module.eventCamera.transform.position.z);

			if (event_controller.event_data.pointerCurrentRaycast.gameObject == null || origin_dist > new_dist)
			{
				/*
				PrintDebugLog ("PhysicsRaycast()" +
					", raycasted: " + _firstResult.gameObject.name +
					", raycasted position: " + _firstResult.worldPosition +
					", new_dist: " + new_dist +
					", origin target: " +
					(event_controller.event_data.pointerCurrentRaycast.gameObject == null ?
						"null" :
						event_controller.event_data.pointerCurrentRaycast.gameObject.name) +
					", origin position: " + event_controller.event_data.pointerCurrentRaycast.worldPosition +
					", origin distance: " + origin_dist);
				*/
				event_controller.event_data.pointerCurrentRaycast = _firstResult;
				event_controller.event_data.position = _firstResult.screenPosition;
			}
		}
	}

	List<RaycastResult> graphicRaycastResults = new List<RaycastResult>();
	private void GraphicRaycast(EventController event_controller, Camera event_camera)
	{
		/* --------------------- Find GUIs those can be raycasted begins. ---------------------
		// 1. find Canvas by TAG
		GameObject[] _tag_GUIs = GameObject.FindGameObjectsWithTag (CanvasTag);
		// 2. Get Canvas from Pointer Canvas Provider
		GameObject[] _event_GUIs = WaveVR_EventSystemGUIProvider.GetEventGUIs();

		GameObject[] _GUIs = MergeArray (_tag_GUIs, _event_GUIs);
		// --------------------- Find GUIs those can be raycasted ends. --------------------- */

		// Reset pointerCurrentRaycast even no GUI.
		RaycastResult _firstResult = new RaycastResult ();
		event_controller.event_data.pointerCurrentRaycast = _firstResult;

		Profiler.BeginSample ("Find GraphicRaycaster.");
		GraphicRaycaster[] _graphic_raycasters = GameObject.FindObjectsOfType<GraphicRaycaster> ();
		Profiler.EndSample ();

		for (int i = 0; i < _graphic_raycasters.Length; i++)
		{
			if (_graphic_raycasters [i].gameObject != null && _graphic_raycasters [i].gameObject.GetComponent<Canvas> () != null)
				_graphic_raycasters [i].gameObject.GetComponent<Canvas> ().worldCamera = event_camera;
			else
				continue;

			_graphic_raycasters[i].Raycast (event_controller.event_data, graphicRaycastResults);
			if (graphicRaycastResults.Count == 0)
				continue;

			_firstResult = FindFirstRaycast (graphicRaycastResults);
			graphicRaycastResults.Clear ();

			if (_firstResult.module != null)
			{
				//PrintDebugLog ("GraphicRaycast() device: " + event_controller.device + ", camera: " + _firstResult.module.eventCamera + ", first result = " + _firstResult);
			}

			// Found graphic raycasted object!
			if (_firstResult.gameObject != null)
			{
				if (_firstResult.worldPosition == Vector3.zero)
				{
					_firstResult.worldPosition = GetIntersectionPosition (
						_firstResult.module.eventCamera,
						//_eventController.event_data.enterEventCamera,
						_firstResult
					);
				}

				float new_dist =
					Mathf.Abs (
						_firstResult.worldPosition.z -
						_firstResult.module.eventCamera.transform.position.z);
				float origin_dist =
					Mathf.Abs (
						event_controller.event_data.pointerCurrentRaycast.worldPosition.z -
						_firstResult.module.eventCamera.transform.position.z);


				bool _changeCurrentRaycast = false;
				// Raycast to nearest (z-axis) target.
				if (event_controller.event_data.pointerCurrentRaycast.gameObject == null)
				{
					_changeCurrentRaycast = true;
				} else
				{
					/*
					PrintDebugLog ("GraphicRaycast() "
						+ ", raycasted: " + _firstResult.gameObject.name
						+ ", raycasted position: " + _firstResult.worldPosition
						+ ", distance: " + new_dist
						+ ", sorting order: " + _firstResult.sortingOrder
						+ ", origin target: " +
						(event_controller.event_data.pointerCurrentRaycast.gameObject == null ?
							"null" :
							event_controller.event_data.pointerCurrentRaycast.gameObject.name)
						+ ", origin position: " + event_controller.event_data.pointerCurrentRaycast.worldPosition
						+ ", origin distance: " + origin_dist
						+ ", origin sorting order: " + event_controller.event_data.pointerCurrentRaycast.sortingOrder);
					*/
					if (origin_dist > new_dist)
					{
						PrintDebugLog ("GraphicRaycast() " +
							event_controller.event_data.pointerCurrentRaycast.gameObject.name +
							", position: " + event_controller.event_data.pointerCurrentRaycast.worldPosition +
							", distance: " + origin_dist +
							" is farer than " +
							_firstResult.gameObject.name +
							", position: " + _firstResult.worldPosition +
							", new distance: " + new_dist);

						_changeCurrentRaycast = true;
					} else if (origin_dist == new_dist)
					{
						int _so_origin = event_controller.event_data.pointerCurrentRaycast.sortingOrder;
						int _so_result = _firstResult.sortingOrder;

						if (_so_origin < _so_result)
						{
							PrintDebugLog ("GraphicRaycast() "
								+ event_controller.event_data.pointerCurrentRaycast.gameObject.name
								+ " sorting order: " + _so_origin + " is smaller than "
								+ _firstResult.gameObject.name
								+ " sorting order: " + _so_result);

							_changeCurrentRaycast = true;
						}
					}
				}

				if (_changeCurrentRaycast)
				{
					event_controller.event_data.pointerCurrentRaycast = _firstResult;
					event_controller.event_data.position = _firstResult.screenPosition;
				}

				//break;
			}
		}
	}
	#endregion

	private Vector2 centerOfScreen = new Vector2 (0.5f * Screen.width, 0.5f * Screen.height);
	private void ResetPointerEventData(WaveVR_Controller.EDeviceType type)
	{
		EventController _eventController = GetEventController (type);
		if (_eventController != null)
		{
			if (_eventController.event_data == null)
				_eventController.event_data = new PointerEventData (eventSystem);

			_eventController.event_data.Reset ();
			_eventController.event_data.position = centerOfScreen; // center of screen
		}
	}

	private void ResetPointerEventData_Hybrid(WaveVR_Controller.EDeviceType type, Camera eventCam)
	{
		EventController _eventController = GetEventController (type);
		if (_eventController != null && eventCam != null)
		{
			if (_eventController.event_data == null)
				_eventController.event_data = new PointerEventData(EventSystem.current);

			_eventController.event_data.Reset();
			_eventController.event_data.position = new Vector2(0.5f * eventCam.pixelWidth, 0.5f * eventCam.pixelHeight); // center of screen
		}
	}

	/**
	 * @brief get intersection position in world space
	 **/
	private Vector3 GetIntersectionPosition(Camera cam, RaycastResult raycastResult)
	{
		// Check for camera
		if (cam == null) {
			return Vector3.zero;
		}

		float intersectionDistance = raycastResult.distance + cam.nearClipPlane;
		Vector3 intersectionPosition = cam.transform.forward * intersectionDistance + cam.transform.position;
		return intersectionPosition;
	}

	private GameObject GetRaycastedObject(WaveVR_Controller.EDeviceType type)
	{
		EventController _eventController = GetEventController (type);
		if (_eventController != null)
		{
			PointerEventData _ped = _eventController.event_data;
			if (_ped != null)
				return _ped.pointerCurrentRaycast.gameObject;
		}
		return null;
	}

	private void CheckBeamPointerActive(EventController eventController)
	{
		if (eventController == null)
			return;

		if (eventController.pointer != null)
		{
			bool _enabled = eventController.pointer.gameObject.activeSelf && eventController.pointer.ShowPointer;
			if (eventController.pointerEnabled != _enabled)
			{
				eventController.pointerEnabled = _enabled;
				this.toChangeBeamPointer = eventController.pointerEnabled;
				PrintDebugLog ("CheckBeamPointerActive() " + eventController.device + ", pointer is " + (eventController.pointerEnabled ? "active." : "inactive."));
			}
		} else
		{
			eventController.pointerEnabled = false;
		}
		if (eventController.beam != null)
		{
			bool _enabled = eventController.beam.gameObject.activeSelf && eventController.beam.ShowBeam;
			if (eventController.beamEnabled != _enabled)
			{
				eventController.beamEnabled = _enabled;
				this.toChangeBeamPointer = eventController.beamEnabled;
				PrintDebugLog ("CheckBeamPointerActive() " + eventController.device + ", beam is " + (eventController.beamEnabled ? "active." : "inactive."));
			}
		} else
		{
			eventController.beamEnabled = false;
		}
	}

	private void SetPointerCameraTracker()
	{
		for (int i = 0; i < WaveVR_Controller.DeviceTypes.Length; i++)
		{
			WaveVR_Controller.EDeviceType _dt = WaveVR_Controller.DeviceTypes [i];
			// HMD uses Gaze, not controller input module.
			if (_dt == WaveVR_Controller.EDeviceType.Head)
				continue;

			if (GetEventController(_dt) == null)
				continue;

			WaveVR_PointerCameraTracker pcTracker = null;

			switch (_dt)
			{
			case WaveVR_Controller.EDeviceType.Dominant:
				if (pointCameraDomint != null)
					pcTracker = pointCameraDomint.GetComponent<WaveVR_PointerCameraTracker> ();
				break;
			case WaveVR_Controller.EDeviceType.NonDominant:
				if (pointCameraNoDomt != null)
					pcTracker = pointCameraNoDomt.GetComponent<WaveVR_PointerCameraTracker> ();
				break;
			default:
				break;
			}

			if (pcTracker != null && pcTracker.reticleObject == null)
			{
				EventController _eventController = GetEventController (_dt);
				bool isConnected = true;
				WaveVR.Device _device = WaveVR.Instance.getDeviceByType (_dt);
				if (_device != null)
					isConnected = _device.connected;

				if (_eventController != null && isConnected)
				{
					if (_eventController.pointer == null && _eventController.controller != null)
						_eventController.pointer = _eventController.controller.GetComponentInChildren<WaveVR_ControllerPointer> ();
					if (_eventController.pointer != null)
					{
						pcTracker.reticleObject = _eventController.pointer.gameObject;
					}
				}
			}
		}
	}

	private GameObject[] MergeArray(GameObject[] start, GameObject[] end)
	{
		GameObject[] _merged = null;

		if (start == null)
		{
			if (end != null)
				_merged = end;
		} else
		{
			if (end == null)
			{
				_merged = start;
			} else
			{
				uint _duplicate = 0;
				for (int i = 0; i < start.Length; i++)
				{
					for (int j = 0; j < end.Length; j++)
					{
						if (GameObject.ReferenceEquals (start [i], end [j]))
						{
							_duplicate++;
							end [j] = null;
						}
					}
				}

				_merged = new GameObject[start.Length + end.Length - _duplicate];
				uint _merge_index = 0;

				for (int i = 0; i < start.Length; i++)
					_merged [_merge_index++] = start [i];

				for (int j = 0; j < end.Length; j++)
				{
					if (end [j] != null)
						_merged [_merge_index++] = end [j];
				}

				//Array.Copy (start, _merged, start.Length);
				//Array.Copy (end, 0, _merged, start.Length, end.Length);
			}
		}

		return _merged;
	}
}
