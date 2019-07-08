using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using wvr;
using WaveVR_Log;
using System;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(WaveVR_ControllerLoader))]
public class WaveVR_ControllerLoaderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WaveVR_ControllerLoader myScript = target as WaveVR_ControllerLoader;

        myScript.WhichHand = (WaveVR_ControllerLoader.ControllerHand)EditorGUILayout.EnumPopup ("Type", myScript.WhichHand);
        myScript.ControllerComponents = (WaveVR_ControllerLoader.CComponent)EditorGUILayout.EnumPopup ("Controller Components", myScript.ControllerComponents);

        myScript.TrackPosition = EditorGUILayout.Toggle ("Track Position", myScript.TrackPosition);
        if (true == myScript.TrackPosition)
        {
            myScript.SimulationOption = (WVR_SimulationOption)EditorGUILayout.EnumPopup ("    Simulate Position", myScript.SimulationOption);
            if (myScript.SimulationOption == WVR_SimulationOption.ForceSimulation || myScript.SimulationOption == WVR_SimulationOption.WhenNoPosition)
            {
                myScript.FollowHead = (bool)EditorGUILayout.Toggle ("        Follow Head", myScript.FollowHead);
            }
        }

        myScript.TrackRotation = EditorGUILayout.Toggle ("Track Rotation", myScript.TrackRotation);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Controller model");
        myScript.adaptiveLoading = EditorGUILayout.Toggle("  Adaptive loading", myScript.adaptiveLoading);
        if (true == myScript.adaptiveLoading)
        {

            EditorGUILayout.LabelField("    Emitter");
            myScript.enableEmitter = EditorGUILayout.Toggle("      Enable emitter", myScript.enableEmitter);
            if (true == myScript.enableEmitter)
            {
                EditorGUILayout.LabelField("        Event");
                myScript.sendEvent = EditorGUILayout.Toggle("          Send event", myScript.sendEvent);

                EditorGUILayout.LabelField("        Beam");
                myScript.ShowBeam = EditorGUILayout.Toggle("          Show beam", myScript.ShowBeam);
                if (true == myScript.ShowBeam)
                {
                    myScript.useBeamSystemConfig = EditorGUILayout.Toggle("          Apply system config", myScript.useBeamSystemConfig);
                    if (true != myScript.useBeamSystemConfig)
                    {
                        EditorGUILayout.LabelField("            Custom settings");
                        myScript.updateEveryFrame = EditorGUILayout.Toggle("              Need to update every frame", myScript.updateEveryFrame);
                        myScript.StartOffset = EditorGUILayout.FloatField("              Start offset ", myScript.StartOffset);
                        myScript.StartWidth = EditorGUILayout.FloatField("              Start width ", myScript.StartWidth);
                        myScript.EndOffset = EditorGUILayout.FloatField("              End offset ", myScript.EndOffset);
                        myScript.EndWidth = EditorGUILayout.FloatField("              End offset ", myScript.EndWidth);

                        EditorGUILayout.Space();
                        myScript.useDefaultMaterial = EditorGUILayout.Toggle("              Use default material", myScript.useDefaultMaterial);

                        if (false == myScript.useDefaultMaterial)
                        {
                            myScript.customMat = (Material)EditorGUILayout.ObjectField("                Custom material", myScript.customMat, typeof(Material), false);
                        }
                        else
                        {
                            myScript.StartColor = EditorGUILayout.ColorField("                Start color", myScript.StartColor);
                            myScript.EndColor = EditorGUILayout.ColorField("                End color", myScript.EndColor);
                        }
                    }
                }

                EditorGUILayout.LabelField("        Controller pointer");
                myScript.showPointer = EditorGUILayout.Toggle("          Show controller pointer", myScript.showPointer);
                if (true == myScript.showPointer)
                {
                    myScript.useCtrPointerSystemConfig = EditorGUILayout.Toggle("          Apply system config", myScript.useCtrPointerSystemConfig);
                    if (true != myScript.useCtrPointerSystemConfig)
                    {
                        EditorGUILayout.LabelField("            Custom settings");
                        myScript.blink = EditorGUILayout.Toggle("              Controller pointer will blink", myScript.blink);
                        myScript.PointerOuterDiameterMin = EditorGUILayout.FloatField("              Min. pointer diameter ", myScript.PointerOuterDiameterMin);
                        myScript.UseDefaultTexture = EditorGUILayout.Toggle("              Use default pointer texture ", myScript.UseDefaultTexture);
                        if (false == myScript.UseDefaultTexture)
                        {
                            myScript.customTexture = (Texture2D)EditorGUILayout.ObjectField("                Custom pointer texture", myScript.customTexture, typeof(Texture2D), false);
                        }
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("  Button effect");
            myScript.enableButtonEffect = EditorGUILayout.Toggle("    Enable button effect", myScript.enableButtonEffect);
            if (true == myScript.enableButtonEffect)
            {
                myScript.useEffectSystemConfig = EditorGUILayout.Toggle("      Apply system config", myScript.useEffectSystemConfig);
                if (true != myScript.useEffectSystemConfig)
                {
                    myScript.buttonEffectColor = EditorGUILayout.ColorField("          Button effect color", myScript.buttonEffectColor);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("  Indication feature");
            myScript.overwriteIndicatorSettings = true;
            myScript.showIndicator = EditorGUILayout.Toggle("    Show Indicator", myScript.showIndicator);
            if (true == myScript.showIndicator)
            {
                myScript.useIndicatorSystemConfig = EditorGUILayout.Toggle("    Use system config", myScript.useIndicatorSystemConfig);
                if (false == myScript.useIndicatorSystemConfig)
                {
                    myScript.basedOnEmitter = EditorGUILayout.Toggle("      Indicator based on emitter ", myScript.basedOnEmitter);
                    myScript.hideIndicatorByRoll = EditorGUILayout.Toggle("      Hide Indicator when roll angle > 90 ", myScript.hideIndicatorByRoll);
                    myScript.showIndicatorAngle = EditorGUILayout.FloatField("      Show When Angle > ", myScript.showIndicatorAngle);
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("      Line customization");
                    myScript.lineLength = EditorGUILayout.FloatField("        Line Length", myScript.lineLength);
                    myScript.lineStartWidth = EditorGUILayout.FloatField("        Line Start Width", myScript.lineStartWidth);
                    myScript.lineEndWidth = EditorGUILayout.FloatField("        Line End Width", myScript.lineEndWidth);
                    myScript.lineColor = EditorGUILayout.ColorField("        Line Color", myScript.lineColor);
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("      Text customization");
                    myScript.textCharacterSize = EditorGUILayout.FloatField("        Text Character Size", myScript.textCharacterSize);
                    myScript.zhCharactarSize = EditorGUILayout.FloatField("        Chinese Character Size", myScript.zhCharactarSize);
                    myScript.textFontSize = EditorGUILayout.IntField("        Text Font Size", myScript.textFontSize);
                    myScript.textColor = EditorGUILayout.ColorField("        Text Color", myScript.textColor);
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("      Key indication");
                    var list = myScript.buttonIndicationList;

                    int newCount = Mathf.Max(0, EditorGUILayout.IntField("        Button indicator size", list.Count));

                    while (newCount < list.Count)
                        list.RemoveAt(list.Count - 1);
                    while (newCount > list.Count)
                        list.Add(new ButtonIndication());

                    for (int i = 0; i < list.Count; i++)
                    {
                        EditorGUILayout.LabelField("        Button indication " + i);
                        myScript.buttonIndicationList[i].keyType = (ButtonIndication.KeyIndicator)EditorGUILayout.EnumPopup("        Key Type", myScript.buttonIndicationList[i].keyType);
                        myScript.buttonIndicationList[i].alignment = (ButtonIndication.Alignment)EditorGUILayout.EnumPopup("        Alignment", myScript.buttonIndicationList[i].alignment);
                        myScript.buttonIndicationList[i].indicationOffset = EditorGUILayout.Vector3Field("        Indication offset", myScript.buttonIndicationList[i].indicationOffset);
                        myScript.buttonIndicationList[i].useMultiLanguage = EditorGUILayout.Toggle("        Use multi-language", myScript.buttonIndicationList[i].useMultiLanguage);
                        if (myScript.buttonIndicationList[i].useMultiLanguage)
                            myScript.buttonIndicationList[i].indicationText = EditorGUILayout.TextField("        Indication key", myScript.buttonIndicationList[i].indicationText);
                        else
                            myScript.buttonIndicationList[i].indicationText = EditorGUILayout.TextField("        Indication text", myScript.buttonIndicationList[i].indicationText);
                        myScript.buttonIndicationList[i].followButtonRotation = EditorGUILayout.Toggle("        Follow button rotation", myScript.buttonIndicationList[i].followButtonRotation);
                        EditorGUILayout.Space();
                    }
                }
            }
        }
        else
        {
            EditorGUILayout.LabelField("Indication feature");

            myScript.overwriteIndicatorSettings = EditorGUILayout.Toggle("  Overwrite Indicator Settings", myScript.overwriteIndicatorSettings);
            if (true == myScript.overwriteIndicatorSettings)
            {
                myScript.showIndicator = EditorGUILayout.Toggle("    Show Indicator", myScript.showIndicator);
                if (true == myScript.showIndicator)
                {
                    myScript.useIndicatorSystemConfig = EditorGUILayout.Toggle("    Use system config", myScript.useIndicatorSystemConfig);
                    if (false == myScript.useIndicatorSystemConfig)
                    {
                        myScript.basedOnEmitter = EditorGUILayout.Toggle("    Indicator based on emitter ", myScript.basedOnEmitter);
                        myScript.hideIndicatorByRoll = EditorGUILayout.Toggle("    Hide Indicator when roll angle > 90 ", myScript.hideIndicatorByRoll);
                        myScript.showIndicatorAngle = EditorGUILayout.FloatField("    Show When Angle > ", myScript.showIndicatorAngle);
                        EditorGUILayout.Space();

                        EditorGUILayout.LabelField("    Line customization");
                        myScript.lineLength = EditorGUILayout.FloatField("      Line Length", myScript.lineLength);
                        myScript.lineStartWidth = EditorGUILayout.FloatField("      Line Start Width", myScript.lineStartWidth);
                        myScript.lineEndWidth = EditorGUILayout.FloatField("      Line End Width", myScript.lineEndWidth);
                        myScript.lineColor = EditorGUILayout.ColorField("      Line Color", myScript.lineColor);
                        EditorGUILayout.Space();

                        EditorGUILayout.LabelField("    Text customization");
                        myScript.textCharacterSize = EditorGUILayout.FloatField("      Text Character Size", myScript.textCharacterSize);
                        myScript.zhCharactarSize = EditorGUILayout.FloatField("      Chinese Character Size", myScript.zhCharactarSize);
                        myScript.textFontSize = EditorGUILayout.IntField("      Text Font Size", myScript.textFontSize);
                        myScript.textColor = EditorGUILayout.ColorField("      Text Color", myScript.textColor);
                        EditorGUILayout.Space();

                        EditorGUILayout.LabelField("    Key indication");
                        var list = myScript.buttonIndicationList;

                        int newCount = Mathf.Max(0, EditorGUILayout.IntField("      Button indicator size", list.Count));

                        while (newCount < list.Count)
                            list.RemoveAt(list.Count - 1);
                        while (newCount > list.Count)
                            list.Add(new ButtonIndication());

                        for (int i = 0; i < list.Count; i++)
                        {
                            EditorGUILayout.LabelField("      Button indication " + i);
                            myScript.buttonIndicationList[i].keyType = (ButtonIndication.KeyIndicator)EditorGUILayout.EnumPopup("        Key Type", myScript.buttonIndicationList[i].keyType);
                            myScript.buttonIndicationList[i].alignment = (ButtonIndication.Alignment)EditorGUILayout.EnumPopup("        Alignment", myScript.buttonIndicationList[i].alignment);
                            myScript.buttonIndicationList[i].indicationOffset = EditorGUILayout.Vector3Field("        Indication offset", myScript.buttonIndicationList[i].indicationOffset);
                            myScript.buttonIndicationList[i].useMultiLanguage = EditorGUILayout.Toggle("        Use multi-language", myScript.buttonIndicationList[i].useMultiLanguage);
                            if (myScript.buttonIndicationList[i].useMultiLanguage)
                                myScript.buttonIndicationList[i].indicationText = EditorGUILayout.TextField("        Indication key", myScript.buttonIndicationList[i].indicationText);
                            else
                                myScript.buttonIndicationList[i].indicationText = EditorGUILayout.TextField("        Indication text", myScript.buttonIndicationList[i].indicationText);
                            myScript.buttonIndicationList[i].followButtonRotation = EditorGUILayout.Toggle("        Follow button rotation", myScript.buttonIndicationList[i].followButtonRotation);
                            EditorGUILayout.Space();
                        }
                    }
                }
            }
        }

        if (GUI.changed)
            EditorUtility.SetDirty ((WaveVR_ControllerLoader)target);
    }
}
#endif

public class WaveVR_ControllerLoader : MonoBehaviour {
    private static string LOG_TAG = "WaveVR_ControllerLoader";
    private void PrintDebugLog(string msg)
    {
        #if UNITY_EDITOR
        Debug.Log(LOG_TAG + "  Hand: " + WhichHand + ", " + msg);
        #endif
        Log.d (LOG_TAG, "Hand: " + WhichHand + ", " + msg);
    }

    private void PrintInfoLog(string msg)
    {
        #if UNITY_EDITOR
        PrintDebugLog(msg);
        #endif
        Log.i (LOG_TAG, "Hand: " + WhichHand + ", " + msg);
    }

    private void PrintWarningLog(string msg)
    {
#if UNITY_EDITOR
        PrintDebugLog(msg);
#endif
        Log.w(LOG_TAG, "Hand: " + WhichHand + ", " + msg);
    }

    public enum ControllerHand
    {
        Dominant,
        Non_Dominant
    };

    public enum CComponent
    {
        One_Bone,
        Multi_Component
    };

    public enum CTrackingSpace
    {
        REAL_POSITION_ONLY,
        FAKE_POSITION_ONLY,
        AUTO_POSITION_ONLY,
        ROTATION_ONLY,
        ROTATION_AND_REAL_POSITION,
        ROTATION_AND_FAKE_POSITION,
        ROTATION_AND_AUTO_POSITION,
        CTS_SYSTEM
    };

    public enum ControllerType
    {
        ControllerType_None,
        ControllerType_Generic,
        ControllerType_Resources,
        ControllerType_AssetBundles,
        ControllerType_AdaptiveController
    }

    [Header("Loading options")]
    public ControllerHand WhichHand = ControllerHand.Dominant;
    public CComponent ControllerComponents = CComponent.Multi_Component;
    public bool TrackPosition = true;
    public WVR_SimulationOption SimulationOption = WVR_SimulationOption.WhenNoPosition;
    public bool FollowHead = false;
    public bool TrackRotation = true;

    [Header("Indication feature")]
    public bool overwriteIndicatorSettings = true;
    public bool showIndicator = false;
    public bool hideIndicatorByRoll = true;
    public bool basedOnEmitter = true;

    [Range(0, 90.0f)]
    public float showIndicatorAngle = 30.0f;

    [Header("Line customization")]
    [Range(0.01f, 0.1f)]
    public float lineLength = 0.03f;
    [Range(0.0001f, 0.1f)]
    public float lineStartWidth = 0.0004f;
    [Range(0.0001f, 0.1f)]
    public float lineEndWidth = 0.0004f;
    public Color lineColor = Color.white;

    [Header("Text customization")]
    [Range(0.01f, 0.2f)]
    public float textCharacterSize = 0.08f;
    [Range(0.01f, 0.2f)]
    public float zhCharactarSize = 0.07f;
    [Range(50, 200)]
    public int textFontSize = 100;
    public Color textColor = Color.white;

    [Header("Indications")]
    public bool useIndicatorSystemConfig = true;
    public List<ButtonIndication> buttonIndicationList = new List<ButtonIndication>();

    [Header("AdaptiveLoading")]
    public bool adaptiveLoading = true;  // flag to describe if enable adaptive controller loading feature
    public bool enableEmitter = true;
    public bool sendEvent = true;

    [Header("ButtonEffect")]
    public bool enableButtonEffect = true;
    public bool useEffectSystemConfig = true;
    public Color32 buttonEffectColor = new Color32(0, 179, 227, 255);

    [Header("Beam")]
    public bool ShowBeam = true;
    public bool useBeamSystemConfig = true;
    public bool updateEveryFrame = false;
    public float StartWidth = 0.000625f;    // in x,y axis
    public float EndWidth = 0.00125f;       // let the bean seems the same width in far distance.
    public float StartOffset = 0.015f;
    public float EndOffset = 0.8f;
    public Color32 StartColor = new Color32(255, 255, 255, 255);
    public Color32 EndColor = new Color32(255, 255, 255, 77);
    public bool useDefaultMaterial = true;
    public Material customMat;

    [Header("Controller pointer")]
    public bool showPointer = true;
    public bool useCtrPointerSystemConfig = true;
    public bool blink = false;
    public bool UseDefaultTexture = true;
    public Texture2D customTexture = null;
    public float PointerOuterDiameterMin = 0.01f;

    private ControllerType controllerType = ControllerType.ControllerType_None;
    private GameObject controllerPrefab = null;
    private GameObject originalControllerPrefab = null;
    private string controllerFileName = "";
    private string controllerModelFoler = "Controller/";
    private string genericControllerFileName = "Generic_";
    private List<AssetBundle> loadedAssetBundle = new List<AssetBundle>();
    private string renderModelNamePath = "";
    private WaveVR_Controller.EDeviceType deviceType = WaveVR_Controller.EDeviceType.Dominant;
    private bool connected = false;
    //private uint sessionid = 0;
    private string renderModelName = "";
    private IntPtr ptrParameterName = IntPtr.Zero;
    private IntPtr ptrResult = IntPtr.Zero;
    private bool isChecking = false;
    private WaitForSeconds wfs = null;

    private WaveVR_ControllerInstanceManager CtrInstanceMgr;
    private int ControllerIdx = 0;

#if UNITY_EDITOR
    public delegate void ControllerModelLoaded(GameObject go);
    public static event ControllerModelLoaded onControllerModelLoaded = null;
#endif

    private void checkAndCreateCIM()
    {
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        foreach (GameObject go in allObjects) {
            if (go.name.Equals("WaveVR_ControllerInstanceManager"))
            {
                PrintDebugLog("WaveVR_ControllerInstanceManager is found in scene!");
                CtrInstanceMgr = go.GetComponent<WaveVR_ControllerInstanceManager>();
                break;
            }
        }

        if (CtrInstanceMgr == null)
        {
            PrintDebugLog("controllerInstanceManager is NOT found in scene! create it.");

            CtrInstanceMgr = WaveVR_ControllerInstanceManager.Instance;
        }
    }

    void OnEnable()
    {
        controllerPrefab = null;
        controllerFileName = "";
        genericControllerFileName = "Generic_";
        if (WhichHand == ControllerHand.Dominant)
        {
            this.deviceType = WaveVR_Controller.EDeviceType.Dominant;
        }
        else
        {
            this.deviceType = WaveVR_Controller.EDeviceType.NonDominant;
        }
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            WVR_DeviceType _type = WaveVR_Controller.Input(this.deviceType).DeviceType;
            onLoadController(_type);
            return;
        }
#endif

        WaveVR_Utils.Event.Listen(WaveVR_Utils.Event.DEVICE_CONNECTED, onDeviceConnected);
        WaveVR_Utils.Event.Listen(WaveVR_Utils.Event.DEVICE_ROLE_CHANGED, onDeviceRoleChanged);
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            return;
        }
#endif
        WaveVR_Utils.Event.Remove(WaveVR_Utils.Event.DEVICE_CONNECTED, onDeviceConnected);
        WaveVR_Utils.Event.Remove(WaveVR_Utils.Event.DEVICE_ROLE_CHANGED, onDeviceRoleChanged);
    }

    void OnDestroy()
    {
        PrintDebugLog("OnDestroy");
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            return;
        }
#endif
        removeControllerFromMgr("OnDestroy()");
    }

    // Use this for initialization
    void Start()
    {
        wfs = new WaitForSeconds(1.0f);
        loadedAssetBundle.Clear();
        if (checkConnection () != connected)
            connected = !connected;

        if (connected)
        {
            WaveVR.Device _device = WaveVR.Instance.getDeviceByType (this.deviceType);
            onLoadController (_device.type);
        }

        WaveVR_EventSystemControllerProvider.Instance.MarkControllerLoader (deviceType, true);
        PrintDebugLog("start a coroutine to check connection and render model name periodly");
        StartCoroutine(checkRenderModelAndDelete());
    }

    private void onDeviceConnected(params object[] args)
    {
        WVR_DeviceType _type = (WVR_DeviceType)args [0];
        bool _connected = (bool)args [1];
        PrintDebugLog ("onDeviceConnected() device " + _type + " is " + (_connected ? "connected." : "disconnected."));

        if (_type != WaveVR_Controller.Input (this.deviceType).DeviceType)
            return;

        this.connected = _connected;
        if (this.connected)
        {
            if (controllerPrefab == null)
                onLoadController (_type);
        }
    }

    private void onDeviceRoleChanged(params object[] args)
    {
        PrintDebugLog("onDeviceRoleChanged() ");
        WVR_DeviceType _type = WVR_DeviceType.WVR_DeviceType_Invalid;
        this.connected = false;

        if (controllerPrefab != null)
        {
            PrintInfoLog("Destroy controller prefeb because role change, broadcast " + this.deviceType + " CONTROLLER_MODEL_UNLOADED");
            removeControllerFromMgr("onDeviceRoleChanged()");
            Destroy(controllerPrefab);
            controllerPrefab = null;
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.CONTROLLER_MODEL_UNLOADED, this.deviceType);
        }

        WaveVR.Device _dev = WaveVR.Instance.getDeviceByType(this.deviceType);
        if (_dev != null)
        {
            this.connected = _dev.connected;
            _type = _dev.type;

            PrintDebugLog("onDeviceRoleChanged() device " + _type + " is " + (this.connected ? "connected." : "disconnected."));

            if (this.connected)
            {
                onLoadController(_type);
            }
        }
    }

    private void removeControllerFromMgr(string funcName)
    {
        if (CtrInstanceMgr != null)
        {
            if (ControllerIdx != 0)
            {
                CtrInstanceMgr.removeControllerInstance(ControllerIdx);
                PrintDebugLog(funcName + " remove controller: " + ControllerIdx);
                ControllerIdx = 0;
            }
        }
    }

    private const string VRACTIVITY_CLASSNAME = "com.htc.vr.unity.WVRUnityVRActivity";
    private const string FILEUTILS_CLASSNAME = "com.htc.vr.unity.FileUtils";

    private void onLoadController(WVR_DeviceType type)
    {
        controllerFileName = "";
        controllerModelFoler = "Controller/";
        genericControllerFileName = "Generic_";

        // Make up file name
        // Rule =
        // ControllerModel_TrackingMethod_CComponent_Hand
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            genericControllerFileName = "Generic_";

            genericControllerFileName += "MC_";

            if (WhichHand == ControllerHand.Dominant)
            {
                genericControllerFileName += "R";
            }
            else
            {
                genericControllerFileName += "L";
            }

            originalControllerPrefab = Resources.Load(controllerModelFoler + genericControllerFileName) as GameObject;
            if (originalControllerPrefab == null)
            {
                PrintDebugLog("Cant load generic controller model, Please check file under Resources/" + controllerModelFoler + genericControllerFileName + ".prefab is exist!");
            }
            else
            {
                PrintDebugLog(genericControllerFileName + " controller model is found!");
                SetControllerOptions(originalControllerPrefab);
                SetControllerPointerParameters(originalControllerPrefab);
                SetControllerBeamParameters_Editor(originalControllerPrefab);
                controllerPrefab = Instantiate(originalControllerPrefab);
                controllerPrefab.transform.parent = this.transform.parent;

                PrintDebugLog("Controller model loaded");
                ApplyIndicatorParameters();
                if (onControllerModelLoaded != null)
                {
                    PrintDebugLog("trigger delegate");
                    onControllerModelLoaded(controllerPrefab);
                }

                WaveVR_EventSystemControllerProvider.Instance.SetControllerModel(deviceType, controllerPrefab);
            }
            return;
        }
#endif
        checkAndCreateCIM();
        string parameterName = "GetRenderModelName";
        ptrParameterName = Marshal.StringToHGlobalAnsi(parameterName);
        ptrResult = Marshal.AllocHGlobal(64);
        uint resultVertLength = 64;
        Interop.WVR_GetParameters(type, ptrParameterName, ptrResult, resultVertLength);
        renderModelName = Marshal.PtrToStringAnsi(ptrResult);

        int deviceIndex = -1;
        parameterName = "backdoor_get_device_index";
        ptrParameterName = Marshal.StringToHGlobalAnsi(parameterName);
        IntPtr ptrResultDeviceIndex = Marshal.AllocHGlobal(2);
        Interop.WVR_GetParameters(type, ptrParameterName, ptrResultDeviceIndex, 2);

        int _out = 0;
        bool _ret = int.TryParse (Marshal.PtrToStringAnsi (ptrResultDeviceIndex), out _out);
        if (_ret)
            deviceIndex = _out;

        PrintInfoLog("get controller id from runtime is " + renderModelName);

        controllerFileName += renderModelName;
        controllerFileName += "_";

        if (ControllerComponents == CComponent.Multi_Component)
        {
            controllerFileName += "MC_";
        }
        else
        {
            controllerFileName += "OB_";
        }

        if (WhichHand == ControllerHand.Dominant)
        {
            controllerFileName += "R";
        }
        else
        {
            controllerFileName += "L";
        }

        PrintInfoLog("controller file name is " + controllerFileName);
        var found = false;
        controllerType = ControllerType.ControllerType_None;

        if (adaptiveLoading)
        {
            if (Interop.WVR_GetWaveRuntimeVersion() >= 2)
            {
                PrintInfoLog("Start adaptive loading");
                // try to adaptive loading
                bool loadControllerAssets = false;

                // 1. check if there are assets in private folder
                string renderModelFolderPath = Application.temporaryCachePath + "/";
                string renderModelUnzipFolder = renderModelFolderPath + renderModelName + "/";
                renderModelNamePath = renderModelFolderPath + renderModelName + "/Unity";

                // unzip assets from runtime
                if (!Directory.Exists(renderModelNamePath))
                {
                    PrintWarningLog(renderModelName + " not exist, start to transfer and unzip");
                    loadControllerAssets = deployZIPFile(deviceIndex, renderModelUnzipFolder);
                } else
                {
                    loadControllerAssets = true;
                    PrintWarningLog(renderModelName + " found, skip unzip");
                }

                // load model from runtime
                if (loadControllerAssets) {
                    // try emitter folder
                    string modelPath = renderModelUnzipFolder + "Model";
                    found = loadMeshAndImageByDevice(modelPath);

                    if (found)
                    {
                        PrintInfoLog("Model FBX is found!");
                    }

                    if (!found)
                    {
                        string UnityVersion = Application.unityVersion;
                        PrintInfoLog("Application built by Unity version : " + UnityVersion);

                        int assetVersion = checkAssetBundlesVersion(UnityVersion);

                        if (assetVersion == 1)
                        {
                            renderModelNamePath += "/5.6";
                        }
                        else if (assetVersion == 2)
                        {
                            renderModelNamePath += "/2017.3";
                        }

                        // try root path
                        found = tryLoadModelFromRuntime(renderModelNamePath, controllerFileName);

                        // try to load generic from runtime
                        if (!found)
                        {
                            PrintInfoLog("Try to load generic controller model from runtime");
                            string tmpGeneric = genericControllerFileName;
                            if (WhichHand == ControllerHand.Dominant)
                            {
                                tmpGeneric += "MC_R";
                            }
                            else
                            {
                                tmpGeneric += "MC_L";
                            }
                            found = tryLoadModelFromRuntime(renderModelNamePath, tmpGeneric);
                        }
                    }
                }

                // load model from package
                if (!found)
                {
                    PrintWarningLog("Can not find controller model from runtime");
                    originalControllerPrefab = Resources.Load(controllerModelFoler + controllerFileName) as GameObject;
                    if (originalControllerPrefab == null)
                    {
                        Log.e(LOG_TAG, "Can't load preferred controller model from package: " + controllerFileName);
                    }
                    else
                    {
                        PrintInfoLog(controllerFileName + " controller model is found!");
                        controllerType = ControllerType.ControllerType_Resources;
                        found = true;
                    }
                }
            } else
            {
                PrintInfoLog("API Level(2) is larger than Runtime Version (" + Interop.WVR_GetWaveRuntimeVersion() + ")");
            }
        } else
        {
            PrintInfoLog("Start package resource loading");
            if (Interop.WVR_GetWaveRuntimeVersion() >= 2) {
                // load resource from package
                originalControllerPrefab = Resources.Load(controllerModelFoler + controllerFileName) as GameObject;
                if (originalControllerPrefab == null)
                {
                    Log.e(LOG_TAG, "Can't load preferred controller model: " + controllerFileName);
                }
                else
                {
                    PrintInfoLog(controllerFileName + " controller model is found!");
                    controllerType = ControllerType.ControllerType_Resources;
                    found = true;
                }
            } else
            {
                PrintInfoLog("API Level(2) is larger than Runtime Version (" + Interop.WVR_GetWaveRuntimeVersion() + "), use generic controller model!");
            }
        }

        // Nothing exist, load generic
        if (!found)
        {
            PrintInfoLog(controllerFileName + " controller model is not found from runtime and package!");

            originalControllerPrefab = loadGenericControllerModelFromPackage(genericControllerFileName);
            if (originalControllerPrefab == null)
            {
                Log.e(LOG_TAG, "Can't load generic controller model, Please check file under Resources/" + controllerModelFoler + genericControllerFileName + ".prefab is exist!");
            }
            else
            {
                PrintInfoLog(genericControllerFileName + " controller model is found!");
                controllerType = ControllerType.ControllerType_Generic;
                found = true;
            }
        }

        if (found && (originalControllerPrefab != null))
        {
            PrintInfoLog("Instantiate controller model, controller type: " + controllerType);
            SetControllerOptions(originalControllerPrefab);
            if (controllerType == ControllerType.ControllerType_AdaptiveController) PresetAdaptiveControllerParameters(originalControllerPrefab);
            SetControllerPointerParameters(originalControllerPrefab);
            controllerPrefab = Instantiate(originalControllerPrefab);
            controllerPrefab.transform.parent = this.transform.parent;
            ApplyIndicatorParameters();

            WaveVR_EventSystemControllerProvider.Instance.SetControllerModel(deviceType, controllerPrefab);

            if (controllerType == ControllerType.ControllerType_AdaptiveController) setEventSystemParameter();

            if (CtrInstanceMgr != null)
            {
                // To sync with overlay, the Dominant is always right, NonDominant is always left.
                WVR_DeviceType _type = this.deviceType == WaveVR_Controller.EDeviceType.Dominant ?
                    WVR_DeviceType.WVR_DeviceType_Controller_Right : WVR_DeviceType.WVR_DeviceType_Controller_Left;
                ControllerIdx = CtrInstanceMgr.registerControllerInstance(_type, controllerPrefab);
                PrintDebugLog("onLoadController() controller index: " + ControllerIdx);
            }
            PrintDebugLog("onLoadController() broadcast " + this.deviceType + " CONTROLLER_MODEL_LOADED");
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.CONTROLLER_MODEL_LOADED, this.deviceType, controllerPrefab);
        }

        if (adaptiveLoading && controllerType == ControllerType.ControllerType_AssetBundles)
        {
            PrintInfoLog("loadedAssetBundle length: " + loadedAssetBundle.Count);
            foreach (AssetBundle tmpAB in loadedAssetBundle)
            {
                tmpAB.Unload(false);
            }
            loadedAssetBundle.Clear();
        }
        Marshal.FreeHGlobal(ptrParameterName);
        Marshal.FreeHGlobal(ptrResult);
        Marshal.FreeHGlobal(ptrResultDeviceIndex);
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
        isChecking = true;
    }

    private bool loadMeshAndImageByDevice(string renderModelNamePath)
    {
        IntPtr ptrError = Marshal.AllocHGlobal(64);
        string FBXFile = renderModelNamePath + "/" + "controller00.fbx";
        bool ret = false;

        ret = File.Exists(FBXFile);
        PrintInfoLog("controller00.fbx exist = " + ret);

        if (ret)
        {
            string imageFile = renderModelNamePath + "/" + "controller00.png";
            bool fileExist = File.Exists(imageFile);
            PrintInfoLog("controller00.png exist: " + fileExist);
            ret = fileExist;

            if (ret)
            {
                originalControllerPrefab = Resources.Load("AdaptiveController") as GameObject;
                ret = (originalControllerPrefab != null) ? true : false;
            } else
            {
                WaveVR_Utils.Event.Send(WaveVR_Utils.Event.DS_ASSETS_NOT_FOUND, this.deviceType);
            }
        } else
        {
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.DS_ASSETS_NOT_FOUND, this.deviceType);
        }
        PrintInfoLog("loadMeshAndImageByDevice ret: " + ret);

        if (ret)
        {
            controllerType = ControllerType.ControllerType_AdaptiveController;
        }
        Marshal.FreeHGlobal(ptrError);
        return ret;
    }

    // used for asset bundles
    private bool tryLoadModelFromRuntime(string renderModelNamePath, string modelName)
    {
        if (renderModelName.Equals("WVR_CONTROLLER_ASPEN_XA_XB"))
        {
            PrintInfoLog(renderModelName + " will use resource!");
            return false;
        }

        string renderModelAssetBundle = renderModelNamePath + "/" + "Unity";
        PrintInfoLog("tryLoadModelFromRuntime, path is " + renderModelAssetBundle);
        // clear unused asset bundles
        foreach (AssetBundle tmpAB in loadedAssetBundle)
        {
            tmpAB.Unload(false);
        }
        loadedAssetBundle.Clear();
        // check root folder
        AssetBundle ab = AssetBundle.LoadFromFile(renderModelAssetBundle);
        if (ab != null)
        {
            loadedAssetBundle.Add(ab);
            AssetBundleManifest abm = ab.LoadAsset<AssetBundleManifest>("AssetBundleManifest");

            if (abm != null)
            {
                PrintDebugLog(renderModelAssetBundle + " loaded");
                string[] assetsName = abm.GetAllAssetBundles();

                for (int i = 0; i < assetsName.Length; i++)
                {
                    string subRMAsset = renderModelNamePath + "/" + assetsName[i];
                    ab = AssetBundle.LoadFromFile(subRMAsset);

                    loadedAssetBundle.Add(ab);
                    PrintDebugLog(subRMAsset + " loaded");
                }
                PrintInfoLog("All asset Bundles loaded, start loading asset");
                originalControllerPrefab = ab.LoadAsset<GameObject>(modelName);

                if (originalControllerPrefab != null)
                {
                    if (verifyControllerPrefab(originalControllerPrefab))
                    {
                        PrintInfoLog("adaptive load controller model " + modelName + " success");
                        controllerType = ControllerType.ControllerType_AssetBundles;
                        return true;
                    }
                }
            }
            else
            {
                PrintWarningLog("Can't find AssetBundleManifest!!");
            }
        }
        else
        {
            PrintWarningLog("Load " + renderModelAssetBundle + " failed");
        }
        PrintInfoLog("adaptive load controller model " + modelName + " from " + renderModelNamePath + " fail!");
        return false;
    }

    private bool verifyControllerPrefab(GameObject go)
    {
        bool ret = true;

        PrintInfoLog(go.name + " active: " + go.activeInHierarchy);

        WaveVR_Beam wb = go.GetComponent<WaveVR_Beam>();

        if (wb != null)
            return true;

        WaveVR_ControllerPointer wcp = go.GetComponent<WaveVR_ControllerPointer>();

        if (wcp != null)
            return true;

        MeshRenderer mr = go.GetComponent<MeshRenderer>();

        if (mr != null)
        {
            foreach (Material mat in mr.materials)
            {
                if (mat == null)
                {
                    PrintWarningLog(go.name + " material is null");
                    ret = false;
                } else
                {
                    if (mat.shader == null)
                    {
                        PrintWarningLog(go.name + " shader is null");
                        ret = false;
                    } else if (mat.mainTexture == null)
                    {
                        PrintWarningLog(go.name + " texture is null");
                        ret = false;
                    }
                }
            }
        }

        if (ret)
        {
            var ch = go.transform.childCount;

            for (int i = 0; i < ch; i++)
            {
                ret = verifyControllerPrefab(go.transform.GetChild(i).gameObject);
                if (!ret) break;
            }
        }

        return ret;
    }

    private bool deployZIPFile(int deviceIndex, string renderModelUnzipFolder)
    {
        AndroidJavaClass ajc = new AndroidJavaClass(VRACTIVITY_CLASSNAME);

        if (ajc == null || deviceIndex == -1)
        {
            PrintWarningLog("AndroidJavaClass vractivity is null, deviceIndex" + deviceIndex);
            return false;
        }
        else
        {
            AndroidJavaObject activity = ajc.CallStatic<AndroidJavaObject>("getInstance");
            if (activity != null)
            {
                AndroidJavaObject afd = activity.Call<AndroidJavaObject>("getControllerModelFileDescriptor", deviceIndex);
                if (afd != null)
                {
                    AndroidJavaObject fileUtisObject = new AndroidJavaObject(FILEUTILS_CLASSNAME, activity, afd);

                    if (fileUtisObject != null)
                    {
                        bool retUnzip = fileUtisObject.Call<bool>("doUnZIPAndDeploy", renderModelUnzipFolder);

                        fileUtisObject = null;
                        if (!retUnzip)
                        {
                            PrintWarningLog("doUnZIPAndDeploy failed");
                        }
                        else
                        {
                            PrintInfoLog("doUnZIPAndDeploy success");
                            ajc = null;
                            return true;
                        }
                    }
                    else
                    {
                        PrintWarningLog("fileUtisObject is null");
                    }
                }
                else
                {
                    PrintWarningLog("get fd failed");
                }
            }
            else
            {
                PrintWarningLog("getInstance failed");
            }
        }
        ajc = null;
        return false;
    }

    // used for asset bundles
    private int checkAssetBundlesVersion(string version)
    {
        if (version.StartsWith("5.6.3") || version.StartsWith("5.6.4") || version.StartsWith("5.6.5") || version.StartsWith("5.6.6") || version.StartsWith("2017.1") || version.StartsWith("2017.2"))
        {
            return 1;
        }

        if (version.StartsWith("2017.3") || version.StartsWith("2017.4") || version.StartsWith("2018.1"))
        {
            return 2;
        }

        return 0;
    }

    private GameObject loadGenericControllerModelFromPackage(string tmpGeneric)
    {
        if (WhichHand == ControllerHand.Dominant)
        {
            tmpGeneric += "MC_R";
        }
        else
        {
            tmpGeneric += "MC_L";
        }
        Log.w(LOG_TAG, "Can't find preferred controller model, load generic controller : " + tmpGeneric);
        if (adaptiveLoading) PrintInfoLog("Please update controller models from device service to have better experience!");
        return Resources.Load(controllerModelFoler + tmpGeneric) as GameObject;
    }

    private void SetControllerOptions(GameObject controller_prefab)
    {
        WaveVR_PoseTrackerManager _ptm = controller_prefab.GetComponent<WaveVR_PoseTrackerManager> ();
        if (_ptm != null)
        {
            _ptm.TrackPosition = TrackPosition;
            _ptm.SimulationOption = SimulationOption;
            _ptm.FollowHead = FollowHead;
            _ptm.TrackRotation = TrackRotation;
            _ptm.Type = this.deviceType;
            PrintInfoLog("set " + this.deviceType + " to WaveVR_PoseTrackerManager");
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
#endif
        if (!pauseStatus) // resume
        {
            PrintInfoLog("App resume and check controller connection");
            isChecking = DeleteControllerWhenDisconnect();
        } else
        {
            isChecking = false;
        }
    }

    // Update is called once per frame
    void Update () {
    }

    IEnumerator checkRenderModelAndDelete()
    {
        while (true)
        {
            if (isChecking)
            {
                isChecking = DeleteControllerWhenDisconnect();
            }
            yield return wfs;
        }
    }

    private bool DeleteControllerWhenDisconnect()
    {
        if (controllerPrefab == null) return false;

        bool _connected = WaveVR_Controller.Input(this.deviceType).connected;

        if (_connected)
        {
            WVR_DeviceType type = WaveVR_Controller.Input(this.deviceType).DeviceType;
            string parameterName = "GetRenderModelName";
            ptrParameterName = Marshal.StringToHGlobalAnsi(parameterName);
            ptrResult = Marshal.AllocHGlobal(64);
            uint resultVertLength = 64;

            uint ret = Interop.WVR_GetParameters(type, ptrParameterName, ptrResult, resultVertLength);
            string tmprenderModelName = Marshal.PtrToStringAnsi(ptrResult);

            if (tmprenderModelName != renderModelName)
            {
                PrintInfoLog("Destroy controller prefeb because render model is changed, ret: " + ret + ", broadcast " + this.deviceType + " CONTROLLER_MODEL_UNLOADED");
                removeControllerFromMgr("DeleteControllerWhenDisconnect()");
                Destroy(controllerPrefab);
                controllerPrefab = null;
                WaveVR_Utils.Event.Send(WaveVR_Utils.Event.CONTROLLER_MODEL_UNLOADED, this.deviceType);
                Marshal.FreeHGlobal(ptrParameterName);
                Marshal.FreeHGlobal(ptrResult);
                Resources.UnloadUnusedAssets();
                System.GC.Collect();

                if (ret > 0)
                {
                    WaveVR.Device _dev = WaveVR.Instance.getDeviceByType(this.deviceType);
                    if (_dev != null)
                    {
                        WVR_DeviceType _type = WVR_DeviceType.WVR_DeviceType_Invalid;
                        this.connected = _dev.connected;
                        _type = _dev.type;

                        PrintDebugLog("Render model change device: " + _type + ", new render model name: " + tmprenderModelName);

                        if (this.connected)
                        {
                            onLoadController(_type);
                        }
                    }
                    return true;
                }
                return false;
            }
            Marshal.FreeHGlobal(ptrParameterName);
            Marshal.FreeHGlobal(ptrResult);
        }
        else
        {
            PrintInfoLog("Destroy controller prefeb because it is disconnect, broadcast " + this.deviceType + " CONTROLLER_MODEL_UNLOADED");
            removeControllerFromMgr("DeleteControllerWhenDisconnect()");
            Destroy(controllerPrefab);
            controllerPrefab = null;
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.CONTROLLER_MODEL_UNLOADED, this.deviceType);
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
            return false;
        }
        return true;
    }

    private bool checkConnection()
    {
        #if UNITY_EDITOR
        if (Application.isEditor)
        {
            return false;
        } else
        #endif
        {
            WaveVR.Device _device = WaveVR.Instance.getDeviceByType (this.deviceType);
            return _device.connected;
        }
    }

    private void SetControllerPointerParameters(GameObject ctrlr)
    {
        if (ctrlr == null)
            return;

        WaveVR_ControllerPointer _wcp = ctrlr.GetComponentInChildren<WaveVR_ControllerPointer> ();
        if (_wcp != null)
        {
            if (enableEmitter)
            {
                _wcp.ShowPointer = this.showPointer;
            } else
            {
                _wcp.ShowPointer = false;
            }
            PrintInfoLog("Forced set Controller Pointer to " + _wcp.ShowPointer);
            _wcp.device = this.deviceType;
            _wcp.UseSystemConfig = this.useCtrPointerSystemConfig;
            _wcp.Blink = this.blink;
            _wcp.PointerOuterDiameterMin = this.PointerOuterDiameterMin;
            _wcp.UseDefaultTexture = this.UseDefaultTexture;
            _wcp.CustomTexture = this.customTexture;
            PrintDebugLog ("Pointer -> show: " + _wcp.ShowPointer
                + ", device: " + _wcp.device
                + ", useSystemConfig: " + _wcp.UseSystemConfig
                + ", SetControllerPointerParameters() blink: " + _wcp.Blink
                + ", PointerOuterDiameterMin: " + _wcp.PointerOuterDiameterMin
                + ", UseDefaultTexture: " + _wcp.UseDefaultTexture
                + ", customTexture: " + _wcp.CustomTexture);
        }
    }

    #if UNITY_EDITOR
    private void SetControllerBeamParameters_Editor(GameObject ctrlr)
    {
        WaveVR_Beam wb = ctrlr.GetComponentInChildren<WaveVR_Beam> ();

        if (wb != null)
        {
            PrintInfoLog("SetControllerBeamParameters_Editor() Beam is found");
            if (enableEmitter)
            {
                wb.ShowBeam = this.ShowBeam;
            }
            else
            {
                wb.ShowBeam = false;
            }
            PrintInfoLog("SetControllerBeamParameters_Editor() Forced set Beam to " + wb.ShowBeam);

            wb.useSystemConfig = this.useBeamSystemConfig;
            wb.ListenToDevice = true;
            wb.device = this.deviceType;
            if (!wb.useSystemConfig)
            {
                PrintInfoLog("SetControllerBeamParameters_Editor() Beam doesn't use system config");
                wb.updateEveryFrame = this.updateEveryFrame;
                wb.StartWidth = this.StartWidth;
                wb.EndWidth = this.EndWidth;
                wb.StartOffset = this.StartOffset;
                wb.EndOffset = this.EndOffset;
                wb.StartColor = this.StartColor;
                wb.EndColor = this.EndColor;
                wb.useDefaultMaterial = this.useDefaultMaterial;
                wb.customMat = this.customMat;
            }

            PrintDebugLog("SetControllerBeamParameters_Editor() Beam ->show: " + wb.ShowBeam
                + ", ListenToDevice: " + wb.ListenToDevice
                + ", device: " + wb.device
                + ", useSystemConfig: " + wb.useSystemConfig
                + ", updateEveryFrame: " + wb.updateEveryFrame
                + ", StartWidth: " + wb.StartWidth
                + ", EndWidth: " + wb.EndWidth
                + ", StartOffset: " + wb.StartOffset
                + ", EndOffset: " + wb.EndOffset
                + ", StartColor: " + wb.StartColor
                + ", EndColor: " + wb.EndColor
                + ", useDefaultMaterial: " + wb.useDefaultMaterial
                + ", customMat: " + wb.customMat);
        }
    }
    #endif

    private void UpdateStartColor(string color_string)
    {
        byte[] _color_r = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(1, 2), 16));
        byte[] _color_g = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(3, 2), 16));
        byte[] _color_b = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(5, 2), 16));
        byte[] _color_a = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(7, 2), 16));

        this.StartColor.r = _color_r [0];
        this.StartColor.g = _color_g [0];
        this.StartColor.b = _color_b [0];
        this.StartColor.a = _color_a [0];
    }

    private void UpdateEndColor(string color_string)
    {
        byte[] _color_r = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(1, 2), 16));
        byte[] _color_g = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(3, 2), 16));
        byte[] _color_b = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(5, 2), 16));
        byte[] _color_a = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(7, 2), 16));

        this.EndColor.r = _color_r [0];
        this.EndColor.g = _color_g [0];
        this.EndColor.b = _color_b [0];
        this.EndColor.a = _color_a [0];
    }

    private void ReadJsonValues_Beam()
    {
        string json_values = WaveVR_Utils.OEMConfig.getControllerConfig ();

        if (!json_values.Equals (""))
        {
            SimpleJSON.JSONNode jsNodes = SimpleJSON.JSONNode.Parse (json_values);

            string node_value = "";
            node_value = jsNodes ["beam"] ["start_width"].Value;
            if (!node_value.Equals (""))
                this.StartWidth = float.Parse (node_value);

            node_value = jsNodes ["beam"] ["end_width"].Value;
            if (!node_value.Equals (""))
                this.EndWidth = float.Parse (node_value);

            node_value = jsNodes ["beam"] ["start_offset"].Value;
            if (!node_value.Equals (""))
                this.StartOffset = float.Parse (node_value);

            node_value = jsNodes ["beam"] ["length"].Value;
            if (!node_value.Equals (""))
                this.EndOffset = float.Parse (node_value);

            node_value = jsNodes ["beam"] ["start_color"].Value;
            if (!node_value.Equals (""))
                UpdateStartColor (node_value);

            node_value = jsNodes ["beam"] ["end_color"].Value;
            if (!node_value.Equals (""))
                UpdateEndColor (node_value);
        }
    }

    private void PresetAdaptiveControllerParameters(GameObject ctrPrefab)
    {
        WaveVR_ControllerRootToEmitter macr = ctrPrefab.GetComponent<WaveVR_ControllerRootToEmitter>();
        if (macr != null)
        {
            PrintInfoLog("set WaveVR_ControllerRootToEmitter deviceType to " + this.deviceType);
            macr.deviceType = this.deviceType;
        } else
        {
            PrintInfoLog("No WaveVR_ControllerRootToEmitter!");
        }

        var ch = ctrPrefab.transform.childCount;

        for (int i = 0; i < ch; i++)
        {
            PrintInfoLog(ctrPrefab.transform.GetChild(i).gameObject.name);

            // get model
            if (ctrPrefab.transform.GetChild(i).gameObject.name == "Model")
            {
                GameObject CM = ctrPrefab.transform.GetChild(i).gameObject;

                WaveVR_RenderModel rm = CM.GetComponent<WaveVR_RenderModel>();

                if (rm != null)
                {
                    rm.WhichHand = (WaveVR_RenderModel.ControllerHand)this.WhichHand;
                    rm.updateDynamically = false;
                    rm.mergeToOneBone = true;

                    PrintDebugLog("Model -> WhichHand: " + rm.WhichHand);
                }

                WaveVR_AdaptiveControllerActions aca = CM.GetComponent<WaveVR_AdaptiveControllerActions>();

                if (aca != null)
                {
                    aca.enableButtonEffect = this.enableButtonEffect;
                    if (aca.enableButtonEffect)
                    {
                        PrintInfoLog("AdaptiveController button effect is active");
                        aca.device = this.deviceType;
                        aca.useSystemConfig = this.useEffectSystemConfig;
                        if (!this.useEffectSystemConfig) aca.buttonEffectColor = this.buttonEffectColor;

                        PrintDebugLog("Effect -> device: " + aca.device
                            + ", useSystemConfig: " + aca.useSystemConfig
                            + "buttonEffectColor" + aca.buttonEffectColor);
                    }
                    aca.collectInStart = false;
                }
            }

            // get beam
            if (ctrPrefab.transform.GetChild(i).gameObject.name == "Beam")
            {
                GameObject CM = ctrPrefab.transform.GetChild(i).gameObject;
                WaveVR_Beam wb = CM.GetComponent<WaveVR_Beam>();

                if (wb != null)
                {
                    PrintInfoLog ("Beam is found");
                    if (enableEmitter)
                    {
                        wb.ShowBeam = this.ShowBeam;
                    } else
                    {
                        wb.ShowBeam = false;
                    }
                    PrintInfoLog ("Forced set Beam to " + wb.ShowBeam);

                    wb.useSystemConfig = this.useBeamSystemConfig;
                    wb.ListenToDevice = true;
                    wb.device = this.deviceType;

                    if (wb.useSystemConfig)
                        ReadJsonValues_Beam ();

                    PrintInfoLog ("PresetAdaptiveControllerParameters() change beam configurations.");
                    wb.updateEveryFrame = this.updateEveryFrame;
                    wb.StartWidth = this.StartWidth;
                    wb.EndWidth = this.EndWidth;
                    wb.StartOffset = this.StartOffset;
                    wb.EndOffset = this.EndOffset;
                    wb.StartColor = this.StartColor;
                    wb.EndColor = this.EndColor;
                    wb.useDefaultMaterial = this.useDefaultMaterial;
                    wb.customMat = this.customMat;

                    PrintDebugLog ("PresetAdaptiveControllerParameters() Beam ->show: " + wb.ShowBeam
                    + ", ListenToDevice: " + wb.ListenToDevice
                    + ", device: " + wb.device
                    + ", useSystemConfig: " + wb.useSystemConfig
                    + ", updateEveryFrame: " + wb.updateEveryFrame
                    + ", StartWidth: " + wb.StartWidth
                    + ", EndWidth: " + wb.EndWidth
                    + ", StartOffset: " + wb.StartOffset
                    + ", EndOffset: " + wb.EndOffset
                    + ", StartColor: " + wb.StartColor
                    + ", EndColor: " + wb.EndColor
                    + ", useDefaultMaterial: " + wb.useDefaultMaterial
                    + ", customMat: " + wb.customMat);
                }
            }
        }
    }

    private GameObject eventSystem = null;
    private void setEventSystemParameter()
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
                switch (this.deviceType)
                {
                case WaveVR_Controller.EDeviceType.Dominant:
                    wcim.DomintEventEnabled = this.enableEmitter ? this.sendEvent : false;
                    PrintInfoLog("Forced set RightEventEnabled to " + wcim.DomintEventEnabled);
                    break;
                case WaveVR_Controller.EDeviceType.NonDominant:
                    wcim.NoDomtEventEnabled = this.enableEmitter ? this.sendEvent : false;
                    PrintInfoLog("Forced set LeftEventEnabled to " + wcim.NoDomtEventEnabled);
                    break;
                default:
                    break;
                }
            }
        }
    }

    private void ApplyIndicatorParameters()
    {
        if (!overwriteIndicatorSettings) return;
        WaveVR_ShowIndicator si = null;

        var ch = controllerPrefab.transform.childCount;
        bool found = false;

        for (int i = 0; i < ch; i++)
        {
            PrintInfoLog(controllerPrefab.transform.GetChild(i).gameObject.name);

            GameObject CM = controllerPrefab.transform.GetChild(i).gameObject;

            si = CM.GetComponentInChildren<WaveVR_ShowIndicator>();

            if (si != null)
            {
                found = true;
                break;
            }
        }

        if (found)
        {
            PrintInfoLog("WaveVR_ControllerLoader forced update WaveVR_ShowIndicator parameter!");
            si.showIndicator = this.showIndicator;

            if (showIndicator != true)
            {
                PrintInfoLog("WaveVR_ControllerLoader forced don't show WaveVR_ShowIndicator!");
                return;
            }
            si.showIndicator = this.showIndicator;
            si.showIndicatorAngle = showIndicatorAngle;
            si.hideIndicatorByRoll = hideIndicatorByRoll;
            si.basedOnEmitter = basedOnEmitter;
            si.lineColor = lineColor;
            si.lineEndWidth = lineEndWidth;
            si.lineStartWidth = lineStartWidth;
            si.lineLength = lineLength;
            si.textCharacterSize = textCharacterSize;
            si.zhCharactarSize = zhCharactarSize;
            si.textColor = textColor;
            si.textFontSize = textFontSize;

            si.buttonIndicationList.Clear();
            if (useIndicatorSystemConfig)
            {
                PrintInfoLog("WaveVR_ControllerLoader uses system default button indication!");
                addbuttonIndicationList();
            }
            else
            {
                PrintInfoLog("WaveVR_ControllerLoader uses customized button indication!");
                if (buttonIndicationList.Count == 0)
                {
                    PrintInfoLog("WaveVR_ControllerLoader doesn't have button indication!");
                    return;
                }
            }

            foreach (ButtonIndication bi in buttonIndicationList)
            {
                PrintInfoLog("use multilanguage: " + bi.useMultiLanguage);
                PrintInfoLog("indication: " + bi.indicationText);
                PrintInfoLog("alignment: " + bi.alignment);
                PrintInfoLog("offset: " + bi.indicationOffset);
                PrintInfoLog("keyType: " + bi.keyType);
                PrintInfoLog("followRotation: " + bi.followButtonRotation);

                si.buttonIndicationList.Add(bi);
            }

            si.createIndicator();
        } else
        {
            PrintInfoLog("Controller model doesn't support button indication feature!");
        }
    }

    private void addbuttonIndicationList()
    {
        buttonIndicationList.Clear();

        ButtonIndication home = new ButtonIndication();
        home.keyType = ButtonIndication.KeyIndicator.Home;
        home.alignment = ButtonIndication.Alignment.RIGHT;
        home.indicationOffset = new Vector3(0f, 0f, 0f);
        home.useMultiLanguage = true;
        home.indicationText = "system";
        home.followButtonRotation = true;

        buttonIndicationList.Add(home);

        ButtonIndication app = new ButtonIndication();
        app.keyType = ButtonIndication.KeyIndicator.App;
        app.alignment = ButtonIndication.Alignment.LEFT;
        app.indicationOffset = new Vector3(0f, 0.0004f, 0f);
        app.useMultiLanguage = true;
        app.indicationText = "system";
        app.followButtonRotation = true;

        buttonIndicationList.Add(app);

        ButtonIndication grip = new ButtonIndication();
        grip.keyType = ButtonIndication.KeyIndicator.Grip;
        grip.alignment = ButtonIndication.Alignment.RIGHT;
        grip.indicationOffset = new Vector3(0f, 0f, 0.01f);
        grip.useMultiLanguage = true;
        grip.indicationText = "system";
        grip.followButtonRotation = true;

        buttonIndicationList.Add(grip);

        ButtonIndication trigger = new ButtonIndication();
        trigger.keyType = ButtonIndication.KeyIndicator.Trigger;
        trigger.alignment = ButtonIndication.Alignment.RIGHT;
        trigger.indicationOffset = new Vector3(0f, 0f, 0f);
        trigger.useMultiLanguage = true;
        trigger.indicationText = "system";
        trigger.followButtonRotation = true;

        buttonIndicationList.Add(trigger);

        ButtonIndication dt = new ButtonIndication();
        dt.keyType = ButtonIndication.KeyIndicator.DigitalTrigger;
        dt.alignment = ButtonIndication.Alignment.RIGHT;
        dt.indicationOffset = new Vector3(0f, 0f, 0f);
        dt.useMultiLanguage = true;
        dt.indicationText = "system";
        dt.followButtonRotation = true;

        buttonIndicationList.Add(dt);

        ButtonIndication touchpad = new ButtonIndication();
        touchpad.keyType = ButtonIndication.KeyIndicator.TouchPad;
        touchpad.alignment = ButtonIndication.Alignment.LEFT;
        touchpad.indicationOffset = new Vector3(0f, 0f, 0f);
        touchpad.useMultiLanguage = true;
        touchpad.indicationText = "system";
        touchpad.followButtonRotation = true;

        buttonIndicationList.Add(touchpad);

        ButtonIndication vol = new ButtonIndication();
        vol.keyType = ButtonIndication.KeyIndicator.Volume;
        vol.alignment = ButtonIndication.Alignment.RIGHT;
        vol.indicationOffset = new Vector3(0f, 0f, 0f);
        vol.useMultiLanguage = true;
        vol.indicationText = "system";
        vol.followButtonRotation = true;

        buttonIndicationList.Add(vol);
    }
}
