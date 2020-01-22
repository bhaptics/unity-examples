#pragma warning disable 0414 // private field assigned but not used.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WVR_Log;
using UnityEngine.UI;
using wvr;

public class WaveVR_GazeInputModule : PointerInputModule
{
	private static string LOG_TAG = "WaveVR_GazeInputModule";
	private void PrintDebugLog(string msg)
	{
		if (Log.EnableDebugLog)
			Log.d (LOG_TAG, msg);
	}

	public enum EGazeTriggerMouseKey
	{
		LeftClick,
		RightClick,
		MiddleClick
	}

	public enum EGazeInputEvent
	{
		PointerDown,
		PointerClick,
		PointerSubmit
	}

	#region Editor Variables.
	public bool UseWaveVRReticle = false;

	public bool TimerControl = true;
	private bool timerControlDefault = false;
	public void EnableTimerControl(bool enable)
	{
		if (Log.gpl.Print)
			PrintDebugLog ("EnableTimerControl() enable: " + enable);
		this.TimerControl = enable;
		this.timerControlDefault = TimerControl;
	}
	public float TimeToGaze = 2.0f;

	public bool ProgressRate = false;  // The switch to show how many percent to click by TimeToGaze
	public float RateTextZPosition = 0.5f;
	public bool ProgressCounter = false;  // The switch to show how long to click by TimeToGaze
	public float CounterTextZPosition = 0.5f;

	public EGazeInputEvent InputEvent = EGazeInputEvent.PointerSubmit;
	public bool ButtonControl = false;
	public List<WaveVR_Controller.EDeviceType> ButtonControlDevices = new List<WaveVR_Controller.EDeviceType>();
	public List<WaveVR_ButtonList.EButtons> ButtonControlKeys = new List<WaveVR_ButtonList.EButtons>();

	public GameObject Head = null;
	#endregion

	private bool btnPressDown = false;
	private bool btnPressed = false;
	private bool btnPressUp = false;
	private bool HmdEnterPressDown = false;
	private float currUnscaledTime = 0;

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
		Vector3 intersectionPosition = cam.transform.position + cam.transform.forward * intersectionDistance;
		return intersectionPosition;
	}

	private PointerEventData pointerData;

	private void CastToCenterOfScreen()
	{
		if (pointerData == null)
			pointerData = new PointerEventData (eventSystem);

		pointerData.Reset();
		pointerData.position = new Vector2 (0.5f * Screen.width, 0.5f * Screen.height);  // center of screen

		if (Head != null)
		{
			Camera _event_camera = Head.GetComponent<Camera> ();
			GraphicRaycast (_event_camera);

			if (pointerData.pointerCurrentRaycast.gameObject == null)
			{
				PhysicsRaycaster _raycaster = Head.GetComponent<PhysicsRaycaster> ();
				PhysicRaycast (_raycaster);
			}
		}
	}

	private void GraphicRaycast(Camera event_camera)
	{
		List<RaycastResult> _raycast_results = new List<RaycastResult>();

		// Reset pointerCurrentRaycast even no GUI.
		RaycastResult _firstResult = new RaycastResult ();
		pointerData.pointerCurrentRaycast = _firstResult;

		for (int i = 0; i < sceneCanvases.Length; i++)
		{
			GraphicRaycaster _gr = sceneCanvases [i].GetComponent<GraphicRaycaster> ();
			if (_gr == null)
				continue;

			// 1. Change event camera.
			sceneCanvases [i].worldCamera = event_camera;

			// 2.
			_gr.Raycast (pointerData, _raycast_results);

			_firstResult = FindFirstRaycast (_raycast_results);
			pointerData.pointerCurrentRaycast = _firstResult;
			_raycast_results.Clear ();

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
					pointerData.pointerCurrentRaycast = _firstResult;
				}

				pointerData.position = _firstResult.screenPosition;
				break;
			}
		}
	}

	private void PhysicRaycast(PhysicsRaycaster raycaster)
	{
		if (raycaster == null)
			return;

		List<RaycastResult> _raycast_results = new List<RaycastResult>();
		raycaster.Raycast (pointerData, _raycast_results);

		RaycastResult _firstResult = FindFirstRaycast (_raycast_results);
		pointerData.pointerCurrentRaycast = _firstResult;

		//PrintDebugLog ("PhysicRaycast() first result = " + _firstResult);

		if (_firstResult.gameObject != null)
		{
			if (_firstResult.worldPosition == Vector3.zero)
			{
				_firstResult.worldPosition = GetIntersectionPosition (
					_firstResult.module.eventCamera,
					//_eventController.event_data.enterEventCamera,
					_firstResult
				);
				pointerData.pointerCurrentRaycast = _firstResult;
			}

			pointerData.position = _firstResult.screenPosition;
		}
	}

	private float gazeTime = 0.0f;
	// { ------- Reticle --------
	private Text progressText = null;
	private Text counterText = null;
	private WaveVR_Reticle gazePointer = null;
	private GameObject percentCanvas = null, counterCanvas = null;

	private GameObject GetCurrentGameObject(PointerEventData pointerData) {
		if (pointerData != null && pointerData.enterEventCamera != null)
			return pointerData.pointerCurrentRaycast.gameObject;

		return null;
	}

	private Vector3 GetIntersectionPosition(PointerEventData pointerData) {
		if (null == pointerData.enterEventCamera)
			return Vector3.zero;

		float intersectionDistance = pointerData.pointerCurrentRaycast.distance + pointerData.enterEventCamera.nearClipPlane;
		Vector3 intersectionPosition = pointerData.enterEventCamera.transform.position + pointerData.enterEventCamera.transform.forward * intersectionDistance;
		return intersectionPosition;
	}

	private void UpdateProgressDistance(PointerEventData pointerEvent) {
		Vector3 intersectionPosition = GetIntersectionPosition(pointerEvent);
		if (gazePointer == null)
			return;

		if (percentCanvas != null) {
			Vector3 tmpVec = new Vector3(percentCanvas.transform.localPosition.x, percentCanvas.transform.localPosition.y, intersectionPosition.z - (RateTextZPosition >= 0 ? RateTextZPosition : 0));
			percentCanvas.transform.localPosition = tmpVec;
		}

		if (counterCanvas != null) {
			Vector3 tmpVec = new Vector3(counterCanvas.transform.localPosition.x, counterCanvas.transform.localPosition.y, intersectionPosition.z - (CounterTextZPosition >= 0 ? CounterTextZPosition : 0));
			counterCanvas.transform.localPosition = tmpVec;
		}
	}

	private void UpdateReticle (GameObject preGazedObject, PointerEventData pointerEvent) {
		if (gazePointer == null)
			return;

		GameObject curGazeObject = GetCurrentGameObject(pointerEvent);
		Vector3 intersectionPosition = GetIntersectionPosition(pointerEvent);

		WaveVR_RaycastResultProvider.Instance.SetRaycastResult (
			WaveVR_Controller.EDeviceType.Head,
			curGazeObject,
			intersectionPosition);

		bool isInteractive = pointerEvent.pointerPress != null || ExecuteEvents.GetEventHandler<IPointerClickHandler>(curGazeObject) != null;

		if (curGazeObject == preGazedObject) {
			if (curGazeObject != null) {
				gazePointer.OnGazeStay(pointerEvent.enterEventCamera, curGazeObject, intersectionPosition, isInteractive);
			} else {
				gazePointer.OnGazeExit(pointerEvent.enterEventCamera, preGazedObject);
				return;
			}
		} else {
			if (preGazedObject != null) {
				gazePointer.OnGazeExit(pointerEvent.enterEventCamera, preGazedObject);
			}
			if (curGazeObject != null) {
				gazePointer.OnGazeEnter(pointerEvent.enterEventCamera, curGazeObject, intersectionPosition, isInteractive);
			}
		}
		UpdateProgressDistance(pointerEvent);
	}
	// --------- Reticle -------- }

	private void UpdateButtonStates()
	{
		btnPressDown = false;
		btnPressed = false;
		btnPressUp = false;

		for (int d = 0; d < this.ButtonControlDevices.Count; d++)
		{
			for (int k = 0; k < this.ButtonControlKeys.Count; k++)
			{
				btnPressDown |= WaveVR_Controller.Input (this.ButtonControlDevices [d]).GetPressDown (this.ButtonControlKeys [k]);
				btnPressed |= WaveVR_Controller.Input (this.ButtonControlDevices [d]).GetPress (this.ButtonControlKeys [k]);
				btnPressUp |= WaveVR_Controller.Input (this.ButtonControlDevices [d]).GetPressUp (this.ButtonControlKeys [k]);
			}
		}
	}

	private void UpdateProgressText()
	{
		if (!this.ProgressRate || this.Head == null)
		{
			if (this.progressText != null)
				this.progressText.text = "";
			return;
		}

		if (this.progressText == null)
		{
			Text[] _texts = this.Head.transform.GetComponentsInChildren<Text> ();
			for (int t = 0; t < _texts.Length; t++)
			{
				if (_texts [t].gameObject.name.Equals ("ProgressText"))
				{
					PrintDebugLog ("UpdateProgressText() Found ProgressText.");
					this.progressText = _texts [t];
					break;
				}
			}
		}

		if (this.progressText == null)
			return;

		GameObject _curr_go = pointerData.pointerCurrentRaycast.gameObject;
		if (_curr_go == null)
		{
			this.progressText.text = "";
			return;
		}

		float _rate = (((this.currUnscaledTime - this.gazeTime) % TimeToGaze) / TimeToGaze) * 100;
		this.progressText.text = Mathf.Floor (_rate) + "%";
	}

	private void UpdateCounterText()
	{
		if (!this.ProgressCounter || this.Head == null)
		{
			if (this.counterText != null)
				this.counterText.text = "";
			return;
		}

		if (this.counterText == null)
		{
			Text[] _texts = this.Head.transform.GetComponentsInChildren<Text> ();
			for (int t = 0; t < _texts.Length; t++)
			{
				if (_texts [t].gameObject.name.Equals ("CounterText"))
				{
					PrintDebugLog ("UpdateCounterText() Found CounterText.");
					this.counterText = _texts [t];
					break;
				}
			}
		}

		if (this.counterText == null)
			return;

		GameObject _curr_go = pointerData.pointerCurrentRaycast.gameObject;
		if (_curr_go == null)
		{
			this.counterText.text = "";
			return;
		}

		if (counterText != null)
			counterText.text = System.Math.Round(TimeToGaze - ((this.currUnscaledTime - this.gazeTime) % TimeToGaze), 2).ToString();
	}

	private Vector3 ringPos = Vector3.zero;
	private void OnTriggeGaze()
	{
		UpdateReticle(preGazeObject, pointerData);
		// The gameobject to which raycast positions
		var currentOverGO = pointerData.pointerCurrentRaycast.gameObject;
		bool isInteractive = pointerData.pointerPress != null || ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGO) != null;

		bool sendEvent = false;
		this.HmdEnterPressDown = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.Head).GetPressDown (WVR_InputId.WVR_InputId_Alias1_Enter);
		if (this.HmdEnterPressDown)
			sendEvent = true;

		if (pointerData.pointerEnter != currentOverGO)
		{
			PrintDebugLog ("OnTriggeGaze() pointerEnter: " + pointerData.pointerEnter + ", currentOverGO: " + currentOverGO);
			HandlePointerExitAndEnter (pointerData, currentOverGO);

			if (currentOverGO != null)
				gazeTime = this.currUnscaledTime;
		}
		else
		{
			if (currentOverGO != null)
			{
				if (this.UseWaveVRReticle && gazePointer != null)
					gazePointer.triggerProgressBar (true);

				if (this.TimerControl)
				{
					if (this.currUnscaledTime - gazeTime > TimeToGaze)
					{
						sendEvent = true;
						gazeTime = this.currUnscaledTime;
					}
					float rate = ((this.currUnscaledTime - gazeTime) / TimeToGaze) * 100;
					if (this.UseWaveVRReticle && gazePointer != null)
						gazePointer.setProgressBarTime (rate);
					else
					{
						if (ringMesh != null)
						{
							ringMesh.RingPercent = isInteractive ? (int)rate : 0;
						}
					}
				}

				if (this.ButtonControl)
				{
					if (!this.TimerControl)
					{
						if (this.UseWaveVRReticle && gazePointer != null)
							gazePointer.triggerProgressBar (false);
						else
						{
							if (ringMesh != null)
								ringMesh.RingPercent = 0;
						}
					}

					UpdateButtonStates ();
					if (btnPressDown)
					{
						sendEvent = true;
						this.gazeTime = this.currUnscaledTime;
					}
				}
			} else
			{
				if (this.UseWaveVRReticle && gazePointer != null)
					gazePointer.triggerProgressBar (false);
				else
				{
					if (ringMesh != null)
						ringMesh.RingPercent = 0;
				}
			}
		}

		// Standalone Input Module information
		pointerData.delta = Vector2.zero;
		pointerData.dragging = false;

		DeselectIfSelectionChanged (currentOverGO, pointerData);

		if (sendEvent)
		{
			PrintDebugLog ("OnTriggeGaze() selected " + currentOverGO.name);
			if (InputEvent == EGazeInputEvent.PointerClick)
			{
				ExecuteEvents.ExecuteHierarchy (currentOverGO, pointerData, ExecuteEvents.pointerClickHandler);
				pointerData.clickTime = this.currUnscaledTime;
			} else if (InputEvent == EGazeInputEvent.PointerDown)
			{
				// like "mouse" action, press->release soon, do NOT keep the pointerPressRaycast cause do NOT need to controll "down" object while not gazing.
				pointerData.pressPosition = pointerData.position;
				pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;

				var _pointerDownGO = ExecuteEvents.ExecuteHierarchy (currentOverGO, pointerData, ExecuteEvents.pointerDownHandler);
				ExecuteEvents.ExecuteHierarchy (_pointerDownGO, pointerData, ExecuteEvents.pointerUpHandler);
			} else if (InputEvent == EGazeInputEvent.PointerSubmit)
			{
				ExecuteEvents.ExecuteHierarchy (currentOverGO, pointerData, ExecuteEvents.submitHandler);
			}
		}
	}

	private GameObject preGazeObject = null;
	private void GazeControl()
	{
		bool _connD = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.Dominant).connected;
		bool _connN = WaveVR_Controller.Input (WaveVR_Controller.EDeviceType.NonDominant).connected;

		this.TimerControl = this.timerControlDefault;
		if (WaveVR_ButtonList.Instance == null || !this.ButtonControl)
		{
			// Set timer gaze if no button support.
			this.TimerControl = true;
		} else
		{
			if (!_connD && !_connN)
			{
				// Set timer gaze if no controller connected and HMD enter is unavailable.
				if (!WaveVR_ButtonList.Instance.IsButtonAvailable (WaveVR_Controller.EDeviceType.Head, WVR_InputId.WVR_InputId_Alias1_Enter))
					this.TimerControl = true;
			}
		}

		preGazeObject = GetCurrentGameObject (pointerData);
		CastToCenterOfScreen ();

		this.currUnscaledTime = Time.unscaledTime;
		OnTriggeGaze();

		UpdateProgressText ();
		UpdateCounterText ();
	}

	private bool EnableGaze = false;
	private Canvas[] sceneCanvases = null;
	private RingMeshDrawer ringMesh = null;
	protected override void OnEnable()
	{
		base.OnEnable ();

		EnableGaze = true;

		if (Head == null)
		{
			if (WaveVR_InputModuleManager.Instance != null)
				Head = WaveVR_InputModuleManager.Instance.gameObject;
			else
				Head = WaveVR_Render.Instance.gameObject;
		}

		if (gazePointer == null)
		{
			// Set gazePointer only when null, or it will got null when WaveVR_Reticle gameObject is SetActive(false).
			if (Head != null)
				gazePointer = Head.GetComponentInChildren<WaveVR_Reticle> ();
		}

		if (gazePointer != null)
		{
			if (this.UseWaveVRReticle)
			{
				PrintDebugLog ("OnEnable() Head: " + Head.name + ", enable pointer, percent and counter canvas.");
				percentCanvas = gazePointer.transform.Find ("PercentCanvas").gameObject;
				counterCanvas = gazePointer.transform.Find ("CounterCanvas").gameObject;
				ActivateGazePointerCanvas (true);
			} else
			{
				ActivateGazePointerCanvas (false);
			}
		}

		sceneCanvases = GameObject.FindObjectsOfType<Canvas> ();
		this.timerControlDefault = this.TimerControl;

		if (ringMesh == null)
		{
			if (this.Head != null)
			{
				ringMesh = this.Head.GetComponentInChildren<RingMeshDrawer> ();
				PrintDebugLog ("OnEnable() found ringMesh " + (ringMesh != null ? ringMesh.gameObject.name : "null"));
			}
		}

		if (ringMesh != null)
			ActivateMeshDrawer (!this.UseWaveVRReticle);
	}

	protected override void OnDisable()
	{
		PrintDebugLog ("OnDisable()");
		base.OnDisable ();

		EnableGaze = false;
		ActivateGazePointerCanvas (false);

		if (pointerData != null)
			HandlePointerExitAndEnter (pointerData, null);

		ActivateMeshDrawer (false);
		ringMesh = null;
	}

	private bool focusCapturedBySystem = false;
	private void ActivateGazePointerCanvas(bool active)
	{
		if (gazePointer != null)
		{
			MeshRenderer _mr = gazePointer.gameObject.GetComponentInChildren<MeshRenderer>();
			if (_mr != null)
			{
				PrintDebugLog (active ? "ActivateGazePointerCanvas() enable pointer." : "ActivateGazePointerCanvas() disable pointer.");
				_mr.enabled = active;
			} else
			{
				Log.e (LOG_TAG, "ActivateGazePointerCanvas() no MeshRenderer!!!");
			}
		}
		if (percentCanvas != null)
		{
			PrintDebugLog (active ? "ActivateGazePointerCanvas() enable percentCanvas." : "ActivateGazePointerCanvas() disable percentCanvas.");
			percentCanvas.SetActive (active);
		}
		if (counterCanvas != null)
		{
			PrintDebugLog (active ? "ActivateGazePointerCanvas() enable counterCanvas." : "ActivateGazePointerCanvas() disable counterCanvas.");
			counterCanvas.SetActive (active);
		}
	}

	private void ActivateMeshDrawer(bool active)
	{
		if (ringMesh != null)
		{
			MeshRenderer _mr = ringMesh.gameObject.GetComponentInChildren<MeshRenderer> ();
			if (_mr != null)
			{
				PrintDebugLog (active ? "ActivateMeshDrawer() enable ring mesh." : "ActivateMeshDrawer() disable ring mesh.");
				_mr.enabled = active;
			} else
			{
				Log.e (LOG_TAG, "ActivateMeshDrawer() Oooooooooooops! No MeshRenderer of " + ringMesh.name);
			}
		}
	}

	public override void Process()
	{
		if (WaveVR.Instance.Initialized)
		{
			if (focusCapturedBySystem != WaveVR.Instance.FocusCapturedBySystem)
			{
				focusCapturedBySystem = WaveVR.Instance.FocusCapturedBySystem;
				// Do not gaze if focus is cpatured by system.
				if (focusCapturedBySystem)
				{
					PrintDebugLog ("Process() focus is captured by system.");
					EnableGaze = false;
					ActivateGazePointerCanvas (false);

					if (pointerData != null)
						HandlePointerExitAndEnter (pointerData, null);
				} else
				{
					PrintDebugLog ("Process() get focus.");
					EnableGaze = true;
					ActivateGazePointerCanvas (true);
				}
			}
		}

		if (EnableGaze)
			GazeControl ();
	}
}
