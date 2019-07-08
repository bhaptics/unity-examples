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
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using wvr;
using WaveVR_Log;
using wvr.render;
using System;
using System.Collections;
using UnityEngine.SceneManagement;

#if UNITY_2017_2_OR_NEWER
using UnityEngine.XR;
#else
using UnityEngine.VR;
#endif

//[RequireComponent(typeof(Camera))]
public class WaveVR_Render : MonoBehaviour
{
    private static string TAG = "WVR_Render";
    private static WaveVR_Render instance = null;
    public static WaveVR_Render Instance {
        get
        {
            return instance;
        }
    }
    [Tooltip("Only used on editor.")]
    public float ipd = 0.063f;
    private int targetFPS = -1;
    private static bool surfaceChanged = false;
    private static bool isNeedTimeout = false;
    private static bool isGraphicInitialized = false;
    private static bool isSetActiveSceneChangedCB = false;
    public bool IsGraphicReady { get { return isGraphicInitialized; } }

    public float sceneWidth { get; private set; }
    public float sceneHeight { get; private set; }
    public float[] projRawL { get; private set; }
    public float[] projRawR { get; private set; }
    private WaveVR_Utils.RigidTransform[] _eyes = new WaveVR_Utils.RigidTransform[] {
            new WaveVR_Utils.RigidTransform(new Vector3(-0.063f / 2, 0.15f, 0.12f), Quaternion.identity),
            new WaveVR_Utils.RigidTransform(new Vector3(0.063f / 2, 0.15f, 0.12f), Quaternion.identity)
        };
    public WaveVR_Utils.RigidTransform[] eyes { get { return _eyes; } private set { _eyes = value; } }

    [Tooltip("You can trigger a configuration change on editor " +
        "by checking this.  Help to test related delegate.")]
    public bool configurationChanged = false;

    public enum StereoRenderingPath
    {
        MultiPass,
        SinglePass,
        //SinglePassInstanced  // not supported now
        Auto = SinglePass,
    }

    [Tooltip("SinglePass is an experimental feature.  Use it at your own risk.\n\n" +
        "Choose a preferred stereo rendering path setting according to your scene.  " +
        "The actural rendering path will still depend on your project PlayerSettings and VR device.  " +
        "It will fallback to multi-pass if not qualified.  Changing in runtime will take no effect.  " +
        "Default is Auto (SinglePass)."), SerializeField]
    private StereoRenderingPath PreferredStereoRenderingPath = StereoRenderingPath.Auto;
    public StereoRenderingPath acturalStereoRenderingPath {
        get
        {
            return IsSinglePass ? StereoRenderingPath.SinglePass : StereoRenderingPath.MultiPass;
        }
    }

    public bool IsSinglePass { get; private set; }

#region delegate
    public delegate void RenderCallback(WaveVR_Render render);
    public delegate void RenderCallbackWithEye(WaveVR_Render render, WVR_Eye eye);
    public delegate void RenderCallbackWithEyeAndCamera(WaveVR_Render render, WVR_Eye eye, WaveVR_Camera wvrCamera);

    // Expand will be happened in Start().  Register these delegate in OnEnable().
    public RenderCallback beforeRenderExpand;
    public RenderCallbackWithEye beforeEyeExpand;
    public RenderCallbackWithEyeAndCamera afterEyeExpand;
    public RenderCallback afterRenderExpand;

    // Configuration changed
    public RenderCallback onConfigurationChanged;

    public RenderCallback onSDKGraphicReady;
    public RenderCallback onFirstFrame;

    // Render eye
    public RenderCallbackWithEyeAndCamera beforeRenderEye;
    public RenderCallbackWithEyeAndCamera afterRenderEye;
#endregion  // delegate

    public class RenderThreadSynchronizer
    {
        RenderTexture mutable = new RenderTexture(1,1,0);
        public RenderThreadSynchronizer()
        {
            mutable.useMipMap = false;
            mutable.Create();
        }

        // May call eglMakeCurrent inside.
        public void sync()
        {
            mutable.GetNativeTexturePtr();
        }
    }
    private RenderThreadSynchronizer synchronizer;

    public T GetComponentFromChildren<T>(string name)
    {
        var children = transform.Find(name);
        if (children != null)
        {
            var component = children.GetComponent<T>();
            return component;
        }
        return default(T);
    }
    const string OBJ_NAME_EYE_CENTER = "Eye Center";
    const string OBJ_NAME_LEFT_EYE = "Eye Left";
    const string OBJ_NAME_RIGHT_EYE = "Eye Right";
    const string OBJ_NAME_BOTH_EYES = "Eye Both";
    const string OBJ_NAME_EAR = "Ear";
    const string OBJ_NAME_DISTORTION = "Distortion";
    const string OBJ_NAME_RETICLE = "Reticle";
    const string OBJ_NAME_LOADING = "Loading";

    // Checked by custom editor
    public bool isExpanded
    {
        get
        {
            if (centerWVRCamera == null)
                centerWVRCamera = GetComponentFromChildren<WaveVR_Camera>(OBJ_NAME_EYE_CENTER);
            if (lefteye == null)
                lefteye = GetComponentFromChildren<WaveVR_Camera>(OBJ_NAME_LEFT_EYE);
            if (righteye == null)
                righteye = GetComponentFromChildren<WaveVR_Camera>(OBJ_NAME_RIGHT_EYE);
            if (botheyes == null)
                botheyes = GetComponentFromChildren<WaveVR_Camera>(OBJ_NAME_BOTH_EYES);
#if UNITY_EDITOR
            if (distortion == null)
                distortion = GetComponentFromChildren<WaveVR_Distortion>(OBJ_NAME_DISTORTION);
            if (Application.isEditor)
                return !(centerWVRCamera == null || lefteye == null || righteye == null || distortion == null || botheyes == null);
#endif
            return !(centerWVRCamera == null || lefteye == null || righteye == null || botheyes == null);
        }
    }

    public Camera centerCamera { get { return centerWVRCamera == null ? null : centerWVRCamera.GetCamera(); } }
    public WaveVR_Camera centerWVRCamera = null;
    public WaveVR_Camera lefteye = null;
    public WaveVR_Camera righteye = null;
    public WaveVR_Camera botheyes = null;
    public WaveVR_Distortion distortion = null;
    public GameObject loadingCanvas = null;  // Loading canvas will force clean black to avoid any thing draw on screen before Wave's Graphic's ready.
    public GameObject ear = null;

    public TextureManager textureManager { get; private set; }

    public static int globalOrigin = -1;
    public static int globalPreferredStereoRenderingPath = -1;

    [HideInInspector]
    public ColorSpace QSColorSpace { get; private set; }
    public WVR_PoseOriginModel _origin = WVR_PoseOriginModel.WVR_PoseOriginModel_OriginOnGround;
    public WVR_PoseOriginModel origin { get { return _origin; } set { _origin = value; OnIpdChanged(null); } }

    public static void InitializeGraphic(RenderThreadSynchronizer synchronizer = null)
    {
#if UNITY_EDITOR
        if (Application.isEditor) return;
#endif
        WaveVR_Utils.SendRenderEvent(WaveVR_Utils.RENDEREVENTID_INIT_GRAPHIC);
        if (synchronizer != null)
            synchronizer.sync();
    }

    public void OnIpdChanged(params object[] args)
    {
        Log.d(TAG, "OnIpdChanged");
#if UNITY_EDITOR
        if (Application.isEditor) return;
#endif

        WVR_NumDoF dof;
        if (WaveVR.Instance.is6DoFTracking() == 3)
        {
            dof = WVR_NumDoF.WVR_NumDoF_3DoF;
        }
        else
        {
            if (origin == WVR_PoseOriginModel.WVR_PoseOriginModel_OriginOnHead_3DoF)
                dof = WVR_NumDoF.WVR_NumDoF_3DoF;
            else
                dof = WVR_NumDoF.WVR_NumDoF_6DoF;
        }

        //for update EyeToHead transform
        WVR_Matrix4f_t eyeToHeadL = Interop.WVR_GetTransformFromEyeToHead(WVR_Eye.WVR_Eye_Left, dof);
        WVR_Matrix4f_t eyeToHeadR = Interop.WVR_GetTransformFromEyeToHead(WVR_Eye.WVR_Eye_Right, dof);

        eyes = new WaveVR_Utils.RigidTransform[] {
                new WaveVR_Utils.RigidTransform(eyeToHeadL),
                new WaveVR_Utils.RigidTransform(eyeToHeadR)
            };

        ipd = Vector3.Distance(eyes[1].pos, eyes[0].pos);

        //for update projection matrix
        Interop.WVR_GetClippingPlaneBoundary(WVR_Eye.WVR_Eye_Left, ref projRawL[0], ref projRawL[1], ref projRawL[2], ref projRawL[3]);
        Interop.WVR_GetClippingPlaneBoundary(WVR_Eye.WVR_Eye_Right, ref projRawR[0], ref projRawR[1], ref projRawR[2], ref projRawR[3]);

        Log.d(TAG, "targetFPS=" + targetFPS + " sceneWidth=" + sceneWidth + " sceneHeight=" + sceneHeight +
            "\nprojRawL[0]=" + projRawL[0] + " projRawL[1]=" + projRawL[1] + " projRawL[2]=" + projRawL[2] + " projRawL[3]=" + projRawL[3] +
            "\nprojRawR[0]=" + projRawR[0] + " projRawR[1]=" + projRawR[1] + " projRawR[2]=" + projRawR[2] + " projRawR[3]=" + projRawR[3] +
            "\neyes[L]=" + eyes[0].pos.x + "," + eyes[0].pos.y + "," + eyes[0].pos.z + "   eyes[R]=" + eyes[1].pos.x + "," + eyes[1].pos.y + "," + eyes[1].pos.z);
        configurationChanged = true;
    }

    public static bool IsVRSinglePassBuildTimeSupported()
    {
#if WAVEVR_SINGLEPASS_ENABLED || UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }

    // This check combine runtime check and buildtime check.
    bool checkVRSinglePassSupport()
    {
#if UNITY_2017_2_OR_NEWER
        var devices = XRSettings.supportedDevices;
#else
        var devices = VRSettings.supportedDevices;
#endif
        string deviceName = "";
        foreach (var dev in devices)
        {
            var lower = dev.ToLower();
            if (lower.Contains("split"))
            {
                deviceName = dev;
                break;
            }
        }

        bool active = false;
        if (!String.IsNullOrEmpty(deviceName))
        {
#if UNITY_2017_2_OR_NEWER
            //XRSettings.LoadDeviceByName(deviceName);
            //XRSettings.enabled = true;
            active = XRSettings.isDeviceActive;
#else
            //VRSettings.LoadDeviceByName(deviceName);
            //VRSettings.enabled = true;
            active = VRSettings.isDeviceActive;
#endif
        }

        int sdkNativeSupport = 0;
#if UNITY_EDITOR
        if (Application.isEditor)
            sdkNativeSupport = 1;
        else
#endif
            sdkNativeSupport = WaveVR_Utils.IsSinglePassSupported();


        bool globalIsMultiPass = false;
        if (globalPreferredStereoRenderingPath > -1)
        {
            // We won't let a scene which doesn't support singlepass to enable singlepass.
            if (PreferredStereoRenderingPath == StereoRenderingPath.SinglePass)
                globalIsMultiPass = globalPreferredStereoRenderingPath == 0;
        }


        bool result;
        if (PreferredStereoRenderingPath != StereoRenderingPath.SinglePass || globalIsMultiPass)
            result = false;
        else
            result = sdkNativeSupport > 0 && active && IsVRSinglePassBuildTimeSupported();

        var msg = "VRSupport: deviceName " + deviceName + ", Graphic support " + sdkNativeSupport +
            ", XRSettings.isDeviceActive " + active + ", BuildTimeSupport " + IsVRSinglePassBuildTimeSupported() +
            ", preferred " + PreferredStereoRenderingPath + ", global " + globalPreferredStereoRenderingPath +
            ", IsSinglePass " + result;
        Log.d(TAG, msg, true);
        return result;
    }

    private void SwitchKeywordAndDeviceView(bool enable)
    {
        if (enable)
        {
            //Enable these keywords to let the unity shaders works for single pass stereo rendering
            Shader.EnableKeyword("STEREO_MULTIVIEW_ON");
            Shader.EnableKeyword("UNITY_SINGLE_PASS_STEREO");

            // This can avoid the centerCamera be rendered
            bool showDeviceView = false;
#if UNITY_EDITOR
            showDeviceView = true;
#endif

#if UNITY_2017_2_OR_NEWER
            XRSettings.showDeviceView = showDeviceView;
#else
            UnityEngine.VR.VRSettings.showDeviceView = showDeviceView;
#endif
        }
        else
        {
#if UNITY_2017_2_OR_NEWER
            XRSettings.showDeviceView = true;
#else
            UnityEngine.VR.VRSettings.showDeviceView = true;
#endif

            Shader.DisableKeyword("STEREO_MULTIVIEW_ON");
            Shader.DisableKeyword("UNITY_SINGLE_PASS_STEREO");
        }
    }

    void Awake()
    {
        Log.d(TAG, "Awake()+");
        Log.d(TAG, "Version of the runtime: " + Application.unityVersion);
        if (instance == null)
            instance = this;
        else
            Log.w(TAG, "Render already Awaked");

        QualitySettings.SetQualityLevel(QualitySettings.GetQualityLevel(), true);
        synchronizer = new RenderThreadSynchronizer();

        if (globalOrigin >= 0 && globalOrigin <= 3)
        {
            _origin = (WVR_PoseOriginModel) globalOrigin;
            Log.d(TAG, "Has global tracking space " + _origin);
        }

        if (WaveVR_Init.Instance == null || WaveVR.Instance == null)
            Log.e(TAG, "Fail to initialize");

#if UNITY_EDITOR
        if (Application.isEditor)
        {
            sceneWidth = Mathf.Max(Screen.width / 2, Screen.height);
            sceneHeight = sceneWidth;

            projRawL = new float[4] { -1.0f, 1.0f, 1.0f, -1.0f };
            projRawR = new float[4] { -1.0f, 1.0f, 1.0f, -1.0f };

            IsSinglePass = checkVRSinglePassSupport();
        }
        else
#endif
        {
            QSColorSpace = QualitySettings.activeColorSpace;
            // We dont know the device support the single pass or not.  If not, fallback to multipass.

            IsSinglePass = checkVRSinglePassSupport();

            // This command can make sure native's render code are initialized in render thread.
            // InitializeGraphic(synchronizer);

            // Setup render values
            uint w = 0, h = 0;
            Interop.WVR_GetRenderTargetSize(ref w, ref h);
            sceneWidth = (float)w;
            sceneHeight = (float)h;

            projRawL = new float[4] { 0.0f, 0.0f, 0.0f, 0.0f };
            projRawR = new float[4] { 0.0f, 0.0f, 0.0f, 0.0f };

            WVR_RenderProps_t props = new WVR_RenderProps_t();
            Interop.WVR_GetRenderProps(ref props);
            targetFPS = (int)props.refreshRate;

            OnIpdChanged(null);
        }

        Log.d(TAG, "Actural StereoRenderingPath is " + acturalStereoRenderingPath);

        WaveVR_Utils.IssueEngineEvent(WaveVR_Utils.EngineEventID.HMD_INITIAILZED);

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = targetFPS;
        Log.d(TAG, "Awake()-");
    }

    private Coroutine renderLooperCoroutine = null;
    private void enableRenderLoop(bool start)
    {
        if (start && enabled)
        {
            if (renderLooperCoroutine != null)
                return;
            var renderLoop = RenderLoop();
            renderLooperCoroutine = StartCoroutine(renderLoop);
        }
        else
        {
            if (renderLooperCoroutine != null)
                StopCoroutine(renderLooperCoroutine);
            renderLooperCoroutine = null;
        }
    }

    void OnEnable()
    {
        Log.d(TAG, "OnEnable()+");
        WaveVR_Utils.Event.Listen("IpdChanged", OnIpdChanged);
        enableRenderLoop(true);
        setLoadingCanvas(true);
        WaveVR_Utils.IssueEngineEvent(WaveVR_Utils.EngineEventID.UNITY_ENABLE);
        if (!isSetActiveSceneChangedCB)
        {
            Log.d(TAG, "Added scene loaded callback");
            SceneManager.sceneLoaded += OnSceneLoaded;
            isSetActiveSceneChangedCB = true;
        }
        Log.d(TAG, "OnEnable()-");
    }

    void Start()
    {
        Log.d(TAG, "Start()+");

        WaveVR_Render.Expand(this);

        // Not to modify developer's design.
        if (Camera.main == null)
            centerCamera.tag = "MainCamera";

        // if you need the Camera.main workable you can enable the centerCamera when OnConfigurationChanged 
        centerCamera.enabled = false;

        // these camera will be enabled in RenderLoop
        botheyes.GetCamera().enabled = false;
        lefteye.GetCamera().enabled = false;
        righteye.GetCamera().enabled = false;

        Log.d(TAG, "onConfigurationChanged+");
        WaveVR_Utils.Event.Send(WaveVR_Utils.Event.RENDER_CONFIGURATION_CHANGED);
        WaveVR_Utils.SafeExecuteAllDelegate<RenderCallback>(onConfigurationChanged, a => a(this));
        configurationChanged = false;
        Log.d(TAG, "onConfigurationChanged-");
        Log.d(TAG, "Start()-");
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Log.d(TAG, "OnSceneLoaded Scene name: " + scene.name + ", mode: " + mode);

#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
#endif

        try
        {
            Log.d(TAG, "OnSceneLoaded Set TrackingSpaceOrigin: " + WaveVR_Render.instance.origin);
            Log.d(TAG, "OnSceneLoaded WVR_GetDegreeOfFreedom(HMD): " + Interop.WVR_GetDegreeOfFreedom(WVR_DeviceType.WVR_DeviceType_HMD));
            Log.d(TAG, "OnSceneLoaded HMD Pose DOF: " + (WaveVR.Instance.hmd.pose.pose.Is6DoFPose ? "6DoF" : "3DoF"));
            Log.d(TAG, "OnSceneLoaded Left-hand mode: " + WaveVR_Controller.IsLeftHanded);
            WaveVR.Device rightController = WaveVR.Instance.getDeviceByType(WVR_DeviceType.WVR_DeviceType_Controller_Right);
            if (rightController != null && rightController.connected)
            {
                Log.d(TAG, "OnSceneLoaded WVR_GetDegreeOfFreedom(Controller_Right): " + Interop.WVR_GetDegreeOfFreedom(WVR_DeviceType.WVR_DeviceType_Controller_Right));
                Log.d(TAG, "OnSceneLoaded Right Controller Pose DOF: " + (rightController.pose.pose.Is6DoFPose ? "6DoF" : "3DoF"));
            }
            WaveVR.Device leftController = WaveVR.Instance.getDeviceByType(WVR_DeviceType.WVR_DeviceType_Controller_Left);
            if (rightController != null && leftController.connected)
            {
                Log.d(TAG, "OnSceneLoaded WVR_GetDegreeOfFreedom(Controller_Left): " + Interop.WVR_GetDegreeOfFreedom(WVR_DeviceType.WVR_DeviceType_Controller_Left));
                Log.d(TAG, "OnSceneLoaded Left Controller Pose DOF: " + (leftController.pose.pose.Is6DoFPose ? "6DoF" : "3DoF"));
            }
            if (WaveVR_InputModuleManager.Instance != null)
            {
                Log.d(TAG, "OnSceneLoaded enable Input module: " + WaveVR_InputModuleManager.Instance.EnableInputModule + ", Interaction mode: " + WaveVR_InputModuleManager.Instance.GetInteractionMode());
                Log.d(TAG, "OnSceneLoaded override system settings: " + WaveVR_InputModuleManager.Instance.OverrideSystemSettings + ", custom input module: " + WaveVR_InputModuleManager.Instance.CustomInputModule);
                Log.d(TAG, "OnSceneLoaded TimeToGaze: " + WaveVR_InputModuleManager.Instance.Gaze.TimeToGaze + ", Gaze trigger type: " + WaveVR_InputModuleManager.Instance.GetUserGazeTriggerType());
                Log.d(TAG, "OnSceneLoaded Controller Raycast Mode: " + WaveVR_InputModuleManager.Instance.Controller.RaycastMode);
            }
        } catch (Exception e) {
            Log.e(TAG, "Error during OnSceneLoaded\n" + e.ToString());
        }
    }

    public static void signalSurfaceState(string msg) {
        Log.d(TAG, "signalSurfaceState[ " + msg + " ]");
        if (String.Equals(msg, "CHANGED")) {
            surfaceChanged = false;
        } else if (String.Equals(msg, "CHANGED_WRONG")) {
            surfaceChanged = false;
            isNeedTimeout = true;
        } else if (String.Equals(msg, "CHANGED_RIGHT")) {
            surfaceChanged = true;
        } else if (String.Equals(msg, "DESTROYED")) {
            surfaceChanged = false;
            Log.d(TAG, "surfaceDestroyed");
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        Log.d(TAG, "Pause(" + pauseStatus + ")");

        if (pauseStatus)
        {
            WaveVR_Utils.IssueEngineEvent(WaveVR_Utils.EngineEventID.UNITY_APPLICATION_PAUSE);
            if (synchronizer != null)
                synchronizer.sync();
            if (lefteye != null)
                lefteye.GetCamera().targetTexture = null;
            if (righteye != null)
                righteye.GetCamera().targetTexture = null;
            if (botheyes != null)
                botheyes.GetCamera().targetTexture = null;
            if (textureManager != null)
                textureManager.ReleaseTexturePools();

            // Prevent snowy screen when resume.  The loading canvas (ScreenSpace overlay) need work under non SinglePass mode.
            if (IsSinglePass)
                SwitchKeywordAndDeviceView(false);
        }
        else
        {
            WaveVR_Utils.IssueEngineEvent(WaveVR_Utils.EngineEventID.UNITY_APPLICATION_RESUME);
        }

        setLoadingCanvas(true);
        enableRenderLoop(!pauseStatus);
    }

    public int SetQualityLevel(int level, bool applyExpensiveChanges = true)
    {
        if (level < 0) return -1;
        string[] names = QualitySettings.names;
        if (level >= names.Length) return -1;
        int qualityLevel = QualitySettings.GetQualityLevel();
        if (qualityLevel != level)
        {
            QualitySettings.SetQualityLevel(level, false);
            if (applyExpensiveChanges)
            {
                Scene s = SceneManager.GetActiveScene();
                SceneManager.LoadScene(s.name);
            }
            qualityLevel = QualitySettings.GetQualityLevel();
        }
        return qualityLevel;
    }

    void LateUpdate()
    {
        Log.gpl.check();
    }

    void OnApplicationQuit()
    {
        WaveVR_Utils.IssueEngineEvent(WaveVR_Utils.EngineEventID.UNITY_APPLICATION_QUIT);
        if (synchronizer != null)
            synchronizer.sync();
#if UNITY_EDITOR
        WaveVR.EndSimulator();
#endif
    }

    void OnDisable()
    {
        using (var ee = Log.ee(TAG, "OnDisable()+", "OnDisable()-"))
        {
            enableRenderLoop(false);
#if UNITY_EDITOR
            if (!Application.isEditor)
#endif
            {
                WaveVR_Utils.IssueEngineEvent(WaveVR_Utils.EngineEventID.UNITY_DISABLE);
                if (synchronizer != null)
                    synchronizer.sync();
            }
            WaveVR_Utils.Event.Remove("IpdChanged", OnIpdChanged);
            setLoadingCanvas(false);

            if (lefteye != null)
                lefteye.GetCamera().targetTexture = null;
            if (righteye != null)
                righteye.GetCamera().targetTexture = null;
            if (botheyes != null)
                botheyes.GetCamera().targetTexture = null;
            if (textureManager != null)
                textureManager.ReleaseTexturePools();

            if (isSetActiveSceneChangedCB)
            {
                Log.d(TAG, "Removed scene loaded callback");
                SceneManager.sceneLoaded -= OnSceneLoaded;
                isSetActiveSceneChangedCB = false;
            }
        }
    }

    void OnDestroy()
    {
        using (var ee = Log.ee(TAG, "OnDestroy()+", "OnDestroy()-"))
        {
            textureManager = null;
            instance = null;
            WaveVR_Utils.IssueEngineEvent(WaveVR_Utils.EngineEventID.UNITY_DESTROY);
        }
    }

    private WaitForEndOfFrame cachedWaitForEndOfFrame;

    private IEnumerator RenderLoop()
    {
        Log.d(TAG, "RenderLoop() is started");
        if (cachedWaitForEndOfFrame == null)
            cachedWaitForEndOfFrame = new WaitForEndOfFrame();
        yield return cachedWaitForEndOfFrame;
        yield return cachedWaitForEndOfFrame;

        if (isGraphicInitialized == false) {
            InitializeGraphic();
            // sync here to wait InitializeGraphic done because InitializeGraphic is migration to render thread.
            synchronizer.sync();
            isGraphicInitialized = true;
        }

        while (!isExpanded)
            yield return cachedWaitForEndOfFrame;

        if (textureManager == null)
        {
            bool allowMSAA = false;
            if (IsSinglePass)
                allowMSAA = botheyes.GetCamera().allowMSAA;
            else
                allowMSAA = lefteye.GetCamera().allowMSAA && lefteye.GetCamera().allowMSAA;
            textureManager = new TextureManager(IsSinglePass, allowMSAA);
        }

        WaveVR_Utils.SafeExecuteAllDelegate<RenderCallback>(onSDKGraphicReady, a => a(this));

#if UNITY_EDITOR
        if (!Application.isEditor)
#endif
        {
            // Time Control
            var tim = Time.realtimeSinceStartup;

            // Restart ATW thread before rendering.
            while (!WaveVR_Utils.WVR_IsATWActive()) {
                yield return cachedWaitForEndOfFrame;
                if (surfaceChanged && isNeedTimeout == false)
                    break;
                if (Time.realtimeSinceStartup - tim > 1.0f)
                {
                    Log.w(TAG, "Waiting for surface change is timeout.");
                    break;
                }
            }
            // Reset isNeedTimeout flag
            isNeedTimeout = false;

            if (textureManager != null)
            {
                if (!textureManager.validate())
                    textureManager.reset();
            }

            // SinglePass
            if (IsSinglePass)
            {
                Vector4[] unity_StereoScaleOffset = new Vector4[2];
                unity_StereoScaleOffset[0] = new Vector4(1.0f, 1.0f, 0f, 0f);
                unity_StereoScaleOffset[1] = new Vector4(1.0f, 1.0f, 0.5f, 0f);
                Shader.SetGlobalVectorArray("unity_StereoScaleOffset", unity_StereoScaleOffset);

                // Do this in first frame.....
                // Create TextureQueue....
                var antiAliasing = botheyes.GetCamera().allowMSAA ? QualitySettings.antiAliasing : 0;
                WaveVR_Utils.SendRenderEvent(WaveVR_Utils.RENDEREVENTID_SinglePassPrepare + antiAliasing);
            }
        }

        setLoadingCanvas(false);
        if (IsSinglePass)
            SwitchKeywordAndDeviceView(true);

        Log.d(TAG, "RenderLoop() is running");

        Log.d(TAG, "First frame");
        WaveVR_Utils.IssueEngineEvent(WaveVR_Utils.EngineEventID.FIRST_FRAME);
        WaveVR_Utils.SafeExecuteAllDelegate<RenderCallback>(onFirstFrame, a => a(this));

        while (true)
        {
            Log.gpl.d(TAG, "RenderLoop() is still running");
            WaveVR_Utils.Trace.BeginSection("RenderLoop", false);
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                if (WaveVR.Instance.isSimulatorOn) {
                    WaveVR.Instance.UpdatePoses(origin, true);
                } else {
                    WaveVR_Utils.Event.Send(WaveVR_Utils.Event.NEW_POSES, default(WVR_DevicePosePair_t[]), default(WaveVR_Utils.RigidTransform[]));
                    WaveVR_Utils.Event.Send(WaveVR_Utils.Event.AFTER_NEW_POSES);
                }
                if (textureManager != null)
                    textureManager.Next();
            }
            else
#endif
            {
                WaveVR.Instance.UpdatePoses(origin);
                // Set next texture before running any graphic command.
                if (textureManager != null)
                    textureManager.Next();
            }

            if (configurationChanged)
            {
                WaveVR_Render.Expand(this);
                Log.d(TAG, "onConfigurationChanged+");
                WaveVR_Utils.Event.Send(WaveVR_Utils.Event.RENDER_CONFIGURATION_CHANGED);
                WaveVR_Utils.SafeExecuteAllDelegate<RenderCallback>(onConfigurationChanged, a => a(this));
                configurationChanged = false;
                Log.d(TAG, "onConfigurationChanged-");
            }

            if (IsSinglePass)
            {
                RenderEyeBoth(botheyes);
            }
            else
            {
                botheyes.GetCamera().enabled = false;
                RenderEye(lefteye, WVR_Eye.WVR_Eye_Left);
                RenderEye(righteye, WVR_Eye.WVR_Eye_Right);

#if UNITY_EDITOR
                // Because the latest unity will mess up the render order if submit after each eye is rendered.
                // Move the distortion here to have better framedebug resoult.
                if (Application.isEditor)
                {
                    distortion.RenderEye(WVR_Eye.WVR_Eye_Left, textureManager.left.currentRt);
                    distortion.RenderEye(WVR_Eye.WVR_Eye_Right, textureManager.right.currentRt);
                }
#endif
            }
            WaveVR_Utils.Trace.EndSection(false);

            // Put here to control the time of next frame.
            TimeControl();

            Log.gpl.d(TAG, "End of frame");
            yield return cachedWaitForEndOfFrame;
        }
    }

    private void RenderEyeBoth(WaveVR_Camera wvrCamera)
    {
        var camera = wvrCamera.GetCamera();
        var rt = textureManager.both.currentRt;
        rt.DiscardContents();

#if UNITY_EDITOR
        if (Application.isEditor)
        {
            // It was disabled in Start()
            camera.enabled = true;
            SafeExecuteRenderEyeCallback(beforeRenderEye, WVR_Eye.WVR_Eye_Both, wvrCamera);
            SafeExecuteRenderEyeCallback(afterRenderEye, WVR_Eye.WVR_Eye_Both, wvrCamera);
            return;
        }
#endif

        WaveVR_Utils.Trace.BeginSection("Render_WVR_Eye_Both");
        Log.gpl.d(TAG, "Render_WVR_Eye_Both");

        WaveVR_Utils.SetRenderTextureWithDepth(textureManager.both.currentPtr, textureManager.both.currentDepthPtr);
        WaveVR_Utils.SendRenderEventNative(WaveVR_Utils.k_nRenderEventID_RenderEyeBoth);

        camera.enabled = true;
        camera.targetTexture = rt;
        camera.forceIntoRenderTexture = true;
        SafeExecuteRenderEyeCallback(beforeRenderEye, WVR_Eye.WVR_Eye_Both, wvrCamera);
        camera.Render();
        SafeExecuteRenderEyeCallback(afterRenderEye, WVR_Eye.WVR_Eye_Both, wvrCamera);
        camera.enabled = false;

        WaveVR_Utils.SendRenderEventNative(WaveVR_Utils.k_nRenderEventID_SubmitBoth);
        WaveVR_Utils.SendRenderEventNative(WaveVR_Utils.k_nRenderEventID_RenderEyeEndBoth);

        WaveVR_Utils.Trace.EndSection();
    }

    private void RenderEye(WaveVR_Camera wvrCamera, WVR_Eye eye)
    {
        var camera = wvrCamera.GetCamera();
        WaveVR_Utils.Trace.BeginSection((eye == WVR_Eye.WVR_Eye_Left) ? "Render_WVR_Eye_Left" : "Render_WVR_Eye_Right");
        Log.gpl.d(TAG, (eye == WVR_Eye.WVR_Eye_Left) ? "Render_WVR_Eye_Left" : "Render_WVR_Eye_Right");

        bool isleft = eye == WVR_Eye.WVR_Eye_Left;
        RenderTexture rt = textureManager.GetRenderTextureLR(isleft);
        rt.DiscardContents();

#if UNITY_EDITOR
        if (!Application.isEditor)
#endif
        {
            WaveVR_Utils.SetRenderTexture(isleft ?
                textureManager.left.currentPtr :
                textureManager.right.currentPtr);

            WaveVR_Utils.SendRenderEventNative(isleft ?
                WaveVR_Utils.k_nRenderEventID_RenderEyeL :
                WaveVR_Utils.k_nRenderEventID_RenderEyeR);
        }

        camera.enabled = true;
        camera.targetTexture = rt;
        SafeExecuteRenderEyeCallback(beforeRenderEye, eye, wvrCamera);
        camera.Render();
        SafeExecuteRenderEyeCallback(afterRenderEye, eye, wvrCamera);
        camera.enabled = false;

#if UNITY_EDITOR
        if (Application.isEditor)
            return;
#endif

        // Do submit
        WaveVR_Utils.SendRenderEventNative(isleft ?
            WaveVR_Utils.k_nRenderEventID_SubmitL :
            WaveVR_Utils.k_nRenderEventID_SubmitR);

        WaveVR_Utils.SendRenderEventNative(isleft ?
            WaveVR_Utils.k_nRenderEventID_RenderEyeEndL :
            WaveVR_Utils.k_nRenderEventID_RenderEyeEndR);
        WaveVR_Utils.Trace.EndSection();
    }

    private static void AddRaycaster(GameObject obj)
    {
        PhysicsRaycaster ray = obj.GetComponent<PhysicsRaycaster>();
        if (ray == null)
            ray = obj.AddComponent<PhysicsRaycaster>();
        LayerMask mask = -1;
        mask.value = LayerMask.GetMask("Default", "TransparentFX", "Water");
        ray.eventMask = mask;
    }

    private WaveVR_Camera CreateCenterCamera()
    {
        Log.d(TAG, "CreateEye(None)+");
        if (beforeEyeExpand != null)
        {
            WaveVR_Utils.SafeExecuteAllDelegate<RenderCallbackWithEye>(beforeEyeExpand, a => a(this, WVR_Eye.WVR_Eye_None));
            Log.d(TAG, "CreateEye(None)+custom");
        }

        WaveVR_Camera vrcamera = centerWVRCamera;
        Camera camera;

        // If WaveVR_Render attached to an camera, recreate a copy to center camera game object.
        if (vrcamera == null)
        {
            var obj = new GameObject(OBJ_NAME_EYE_CENTER, typeof(Camera), typeof(WaveVR_Camera));
            obj.transform.SetParent(transform, false);
            obj.transform.localPosition = (eyes[0].pos + eyes[1].pos) / 2.0f;

            camera = obj.GetComponent<Camera>();
            vrcamera = obj.GetComponent<WaveVR_Camera>();

            Camera attachedCamera = GetComponent<Camera>();
            if (attachedCamera != null)
            {
                camera.CopyFrom(attachedCamera);
                attachedCamera.enabled = false;
            }
            else
            {
                // The stereoTargetEye will modify fov.  Disable it first
                camera.stereoConvergence = 0;
                camera.stereoTargetEye = StereoTargetEyeMask.None;

                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 1000f;
                camera.renderingPath = RenderingPath.Forward;
                camera.allowMSAA = false;
                camera.fieldOfView = 100;
            }
        }
        camera = vrcamera.GetCamera();

        camera.allowHDR = false;
#if UNITY_2017_3_OR_NEWER
        camera.allowDynamicResolution = false;
#endif
        camera.stereoConvergence = 0;
        camera.stereoTargetEye = StereoTargetEyeMask.None;

        // The stereo settings will reset the localPosition. That is an Unity's bug.  Set the pos after stereo settings.
        vrcamera.transform.localPosition = (eyes[0].pos + eyes[1].pos) / 2.0f;

#if UNITY_EDITOR
        // After main center camera is ready, use it's fov to set projection raw.
        projRawL = projRawR = GetEditorProjectionRaw(camera.fieldOfView, sceneWidth, sceneHeight);
#endif

        if (afterEyeExpand != null)
        {
            Log.d(TAG, "CreateEye(None)-custom");
            WaveVR_Utils.SafeExecuteAllDelegate<RenderCallbackWithEyeAndCamera>(afterEyeExpand, a => a(this, WVR_Eye.WVR_Eye_None, vrcamera));
        }
        Log.d(TAG, "CreateEye(None)-");
        return vrcamera;
    }

    private WaveVR_Camera CreateEyeBoth()
    {
        Log.d(TAG, "CreateEye(Both)+");
        if (beforeEyeExpand != null)
        {
            WaveVR_Utils.SafeExecuteAllDelegate<RenderCallbackWithEye>(beforeEyeExpand, a => a(this, WVR_Eye.WVR_Eye_Both));
            Log.d(TAG, "CreateEye(Both)+custom");
        }

        Camera camera;
        var vrcamera = botheyes;
        if (vrcamera == null)
        {
            GameObject go = new GameObject(OBJ_NAME_BOTH_EYES, typeof(Camera), typeof(FlareLayer), typeof(WaveVR_Camera));
            go.transform.SetParent(transform, false);
            camera = go.GetComponent<Camera>();
            camera.CopyFrom(centerCamera);

            vrcamera = go.GetComponent<WaveVR_Camera>();

#if UNITY_2017_1_OR_NEWER
            if (false)
#endif
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CS0162 // Unreachable code detected
                vrcamera.gameObject.AddComponent<GUILayer>();
#pragma warning restore CS0162 // Unreachable code detected
#pragma warning restore CS0618 // Type or member is obsolete
        }
        else
        {
            camera = vrcamera.GetCamera();
        }

        vrcamera.eye = WVR_Eye.WVR_Eye_Both;

        if (Camera.main == null)
            vrcamera.tag = "MainCamera";
        else if (Camera.main == centerCamera)
        {
            centerCamera.tag = "Untagged";
            vrcamera.tag = "MainCamera";
        }

        //// When render to texture, the rect is no function.  We still reset it to full size.
        //camera.rect = new Rect(0, 0, 1, 1);

        // Settings here doesn't matter the result.  Just set it.
        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        camera.stereoSeparation = ipd;
        camera.stereoConvergence = 0;  // Not support convergence because the projection is created by SDK

        // We don't create HDR rendertexture.  And we may have our own 'dynamic resolution' resolution.
        camera.allowHDR = false;
#if UNITY_2017_3_OR_NEWER
        camera.allowDynamicResolution = false;
#endif
        Matrix4x4 projL, projR;
        projL = GetProjection(projRawL, camera.nearClipPlane, camera.farClipPlane);
        projR = GetProjection(projRawR, camera.nearClipPlane, camera.farClipPlane);

        // In some device the head center is not at the eye center.
        var l = eyes[0].pos;
        var r = eyes[1].pos;
        var center = (l + r) / 2.0f;
        vrcamera.transform.localPosition = center;
        vrcamera.transform.localRotation = Quaternion.identity;

        // Because the BothEye camera is already at center pos, the eyes pos should not have y, and z parts.
        vrcamera.SetEyesPosition(l - center, r - center);
        vrcamera.SetStereoProjectionMatrix(projL, projR);

        if (afterEyeExpand != null)
        {
            Log.d(TAG, "CreateEye(Both)-custom");
            WaveVR_Utils.SafeExecuteAllDelegate<RenderCallbackWithEyeAndCamera>(afterEyeExpand, a => a(this, WVR_Eye.WVR_Eye_Both, vrcamera));
        }
        Log.d(TAG, "CreateEye(Both)-");
        return vrcamera;
    }

    private WaveVR_Camera CreateEye(WVR_Eye eye)
    {
        Log.d(TAG, "CreateEye(" + eye + ")+");
        if (beforeEyeExpand != null)
        {
            WaveVR_Utils.SafeExecuteAllDelegate<RenderCallbackWithEye>(beforeEyeExpand, a => a(this, eye));
            Log.d(TAG, "CreateEye(" + eye + ")+custom");
        }

        bool isleft = eye == WVR_Eye.WVR_Eye_Left;
        WaveVR_Camera vrcamera = isleft ? lefteye : righteye;
        Camera camera;
        if (vrcamera == null)
        {
            string eyename = isleft ? OBJ_NAME_LEFT_EYE : OBJ_NAME_RIGHT_EYE;
            GameObject go = new GameObject(eyename, typeof(Camera), typeof(FlareLayer), typeof(WaveVR_Camera));
            go.transform.SetParent(transform, false);
            camera = go.GetComponent<Camera>();
            camera.CopyFrom(centerCamera);

#if !UNITY_2017_1_OR_NEWER
            go.AddComponent<GUILayer>();
#endif
            vrcamera = go.GetComponent<WaveVR_Camera>();
        }
        else
        {
            camera = vrcamera.GetComponent<Camera>();
        }

        vrcamera.eye = eye;
        camera.enabled = false;

        // Settings here doesn't matter the result.  Just set it.
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        camera.stereoSeparation = ipd;
        camera.stereoConvergence = 0;  // Not support convergence because the projection is created by SDK

        camera.transform.localPosition = eyes[isleft ? 0 : 1].pos;

        // We don't create HDR rendertexture.  And we may have our own 'dynamic resolution' resolution.
        camera.allowHDR = false;
#if UNITY_2017_3_OR_NEWER
        camera.allowDynamicResolution = false;
#endif

        var projRaw = isleft ? projRawL : projRawR;
        camera.projectionMatrix = GetProjection(projRaw, camera.nearClipPlane, camera.farClipPlane);
        camera.fieldOfView = GetFieldOfView(projRaw);

        if (afterEyeExpand != null)
        {
            Log.d(TAG, "CreateEye(" + eye + ")-custom");
            WaveVR_Utils.SafeExecuteAllDelegate<RenderCallbackWithEyeAndCamera>(afterEyeExpand, a => a(this, eye, vrcamera));
        }
        Log.d(TAG, "CreateEye(" + eye + ")-");
        return vrcamera;
    }

#if UNITY_EDITOR
    private void createDistortion()
    {
        Log.d(TAG, "createDistortion()+");
        if (distortion == null)
        {
            GameObject distortionobj = new GameObject(OBJ_NAME_DISTORTION, typeof(Camera), typeof(WaveVR_Distortion));
            distortionobj.transform.SetParent(transform, false);
            distortion = distortionobj.GetComponent<WaveVR_Distortion>();
        }
        var cam = distortion.GetComponent<Camera>();
        cam.allowHDR = false;
        cam.allowMSAA = false;
#if UNITY_2017_3_OR_NEWER
        cam.allowDynamicResolution = false;
#endif
        cam.stereoTargetEye = StereoTargetEyeMask.None;


        distortion.init();
        Log.d(TAG, "createDistortion()-");
    }
#endif

    /**
     * The loading black is used to block the other camera or UI drawing on the display.
     * The native render will use the screen after WaitForEndOfFrame.  And the
     * native render need time to be ready for sync with Android's flow.  Therefore, the
     * Screen or HMD may show othehr camera or UI's drawing.  For example, the graphic
     * raycast need the camera has real output on screen.  We draw it, and cover it by
     * binocular vision.  It let the gaze or the controller work well.  If we don't
     * have a black canvas and the native render is delayed, the screen may show a BG
     * color or the raycast image on the screen for a while.
    **/
    private void createLoadingBlack()
    {
        var found = GetComponentFromChildren<Canvas>(OBJ_NAME_LOADING);
        if (found == null)
        {
            loadingCanvas = new GameObject(OBJ_NAME_LOADING);
            var canvas = loadingCanvas.AddComponent<Canvas>();
            loadingCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject loadingImage = new GameObject("Loading Image");
            loadingImage.transform.SetParent(loadingCanvas.transform, false);
            loadingImage.AddComponent<CanvasRenderer>();
            UnityEngine.UI.Image loading = loadingImage.AddComponent<UnityEngine.UI.Image>();
            loading.material = null;
            loading.color = Color.black;
            loading.raycastTarget = false;
            loading.rectTransform.anchoredPosition = new Vector2(0.5f, 0.5f);
            loading.rectTransform.anchorMin = new Vector2(0, 0);
            loading.rectTransform.anchorMax = new Vector2(1, 1);
            loading.rectTransform.offsetMin = new Vector2(0, 0);
            loading.rectTransform.offsetMax = new Vector2(0, 0);

            canvas.enabled = false;  // Avoid black in Editor GameView preview or configuraiton change.
            loadingCanvas.transform.SetParent(transform, true);
        }
    }

    private void setLoadingCanvas(Boolean enabled)
    {
        if (loadingCanvas)
        {
            var canvas = loadingCanvas.GetComponent<Canvas>();
            if (canvas)
                canvas.enabled = enabled;
        }
    }

#if UNITY_EDITOR
    public static void EditorInitial(WaveVR_Render head)
    {
        // Because the variables in runtime need be initialized in Awake, when user click the
        // inspector expand button, these variables will be used without initialized.
        if (!Application.isPlaying)
        {
            head.eyes = new WaveVR_Utils.RigidTransform[] {
                new WaveVR_Utils.RigidTransform(new Vector3(-head.ipd / 2, 0.15f, 0.12f), Quaternion.identity),
                new WaveVR_Utils.RigidTransform(new Vector3(head.ipd / 2, 0.15f, 0.12f), Quaternion.identity)
            };

            head.sceneWidth = Mathf.Max(Screen.width / 2, Screen.height);
            head.sceneHeight = head.sceneWidth;
            Debug.Log("WaveVR_Render internal variables initialized in editor mode.");
        }
    }
#endif

    public static void Expand(WaveVR_Render head)
    {
        Log.d(TAG, "Expand()+");
        if (head.beforeRenderExpand != null)
        {
            WaveVR_Utils.SafeExecuteAllDelegate<RenderCallback>(head.beforeRenderExpand, a => a(head));
            Log.d(TAG, "Expand()+custom");
        }

#if UNITY_EDITOR
        EditorInitial(head);
#endif

        if (head.isExpanded) {
            //Debug.Log("Expanded");
        }

        head.centerWVRCamera = head.CreateCenterCamera();
        head.botheyes = head.CreateEyeBoth();
        head.righteye = head.CreateEye(WVR_Eye.WVR_Eye_Right);
        head.lefteye = head.CreateEye(WVR_Eye.WVR_Eye_Left);

#if UNITY_EDITOR
        head.createDistortion();
#endif

        var found = head.GetComponentFromChildren<AudioListener>(OBJ_NAME_EAR);
        if (found == null) {
            var earObj = new GameObject(OBJ_NAME_EAR);
            earObj.transform.SetParent(head.transform, false);
            earObj.transform.localPosition = new Vector3(0, 0, -0.01f);  // TODO if 6DOF should be around -0.025f
            earObj.AddComponent<AudioListener>();
            head.ear = earObj;
        }

        AddRaycaster(head.centerCamera.gameObject);

        head.createLoadingBlack();

        if (head.afterRenderExpand != null)
        {
            Log.d(TAG, "Expand()-custom");
            WaveVR_Utils.SafeExecuteAllDelegate<RenderCallback>(head.afterRenderExpand, a => a(head));
        }
        Log.d(TAG, "Expand()-");
    }

    public static void Collapse(WaveVR_Render head)
    {
        if (head.lefteye != null)
            DestroyImmediate(head.lefteye.gameObject);
        head.lefteye = null;

        if (head.righteye != null)
            DestroyImmediate(head.righteye.gameObject);
        head.righteye = null;

        if (head.distortion != null)
            DestroyImmediate(head.distortion.gameObject);
        head.distortion = null;

        if (head.botheyes != null)
            DestroyImmediate(head.botheyes.gameObject);
        head.botheyes = null;

        if (head.centerWVRCamera != null)
            DestroyImmediate(head.centerWVRCamera.gameObject);
        head.centerWVRCamera = null;

        Transform ear = head.transform.Find(OBJ_NAME_EAR);
        if (ear != null)
            DestroyImmediate(ear.gameObject);
        head.ear = null;

        var raycast = head.GetComponent<PhysicsRaycaster>();
        if (raycast != null)
            DestroyImmediate(raycast);
        raycast = null;

        if (head.loadingCanvas != null)
        {
            var loading = head.loadingCanvas.gameObject;
            head.loadingCanvas = null;
            DestroyImmediate(loading);
        }
    }

    private float GetFieldOfView(float[] projRaw)
    {
        float max = 0;
        // assume near is 1. find max tangent angle.
        foreach (var v in projRaw)
        {
            max = Mathf.Max(Mathf.Abs(v), max);
        }
        return Mathf.Atan2(max, 1) * Mathf.Rad2Deg * 2;
    }

    private static float[] GetEditorProjectionRaw(float fov, float width, float height)
    {
        if (fov < 1)
            fov = 1;
        if (fov > 179)
            fov = 179;

        Matrix4x4 proj = Matrix4x4.identity;
        float w, h;
        if (height > width)
        {
            h = Mathf.Tan(fov / 2 * Mathf.Deg2Rad);
            w = h / height * width;
        }
        else
        {
            w = Mathf.Tan(fov / 2 * Mathf.Deg2Rad);
            h = w / width * height;
        }
        float l = -w, r = w, t = h, b = -h;
        return new float[] { l, r, t, b };

        // This log can help debug projection problem.  Keep it.
        //float[] lrtb = { l, r, t, b };
        //Debug.LogError(" reversed fov " + GetFieldOfView(lrtb) + " real fov " + fov);
    }

    private static Matrix4x4 GetProjection(float[] projRaw, float near, float far)
    {
        Log.d(TAG, "GetProjection()");
        if (near < 0.01f)
            near = 0.01f;
        if (far < 0.02f)
            far = 0.02f;

        Matrix4x4 proj = Matrix4x4.identity;

        // The values in ProjectionRaw are made by assuming the near value is 1.
        proj = MakeProjection(projRaw[0], projRaw[1], projRaw[2], projRaw[3], near, far);
        return proj;
    }

    //private void frustum(float ipd, float dpi, int width, int height, float fov, float near, float far, float)

    public static Matrix4x4 MakeProjection(float l, float r, float t, float b, float n, float f)
    {
        Matrix4x4 m = Matrix4x4.zero;
        m[0, 0] = 2 / (r - l);
        m[1, 1] = 2 / (t - b);
        m[0, 2] = (r + l) / (r - l);
        m[1, 2] = (t + b) / (t - b);
        m[2, 2] = -(f + n) / (f - n);
        m[2, 3] = -2 * f * n / (f - n);
        m[3, 2] = -1;
        return m;
    }

#region TimeControl
    // TimeControl: Set Time.timeScale = 0 if input focus in gone.
    private bool previousInputFocus = true;

    [Tooltip("Allow render to set Time.timeScale = 0 if input focus in gone.")]
    public bool needTimeControl = false;

    private void TimeControl()
    {
        if (needTimeControl)
        {
#if UNITY_EDITOR
            // Nothing can simulate the focus lost in editor.  Just leave.
            if (Application.isEditor)
                return;
#endif
            bool hasInputFocus = !WaveVR.Instance.FocusCapturedBySystem;

            if (!previousInputFocus || !hasInputFocus)
            {
                previousInputFocus = hasInputFocus;
                Time.timeScale = hasInputFocus ? 1 : 0;
                Log.d(TAG, "InputFocus " + hasInputFocus + "Time.timeScale " + Time.timeScale);
            }
        }
    }
#endregion

    public void SafeExecuteRenderEyeCallback(RenderCallbackWithEyeAndCamera multi, WVR_Eye eye, WaveVR_Camera wvrCamera)
    {
        if (multi == null)
            return;

        try
        {
            multi(this, eye, wvrCamera);
        }
        catch (Exception e)
        {
            Log.e(TAG, e.ToString(), true);
        }
    }
}
