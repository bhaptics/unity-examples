using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using wvr;
using WaveVR_Log;
using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

public class WaveVR_RenderModel : MonoBehaviour
{
    private static string LOG_TAG = "WaveVR_RenderModel";
    private void PrintDebugLog(string msg)
    {
#if UNITY_EDITOR
        Debug.Log(LOG_TAG + "  Hand: " + WhichHand + ", " + msg);
#endif
        Log.d(LOG_TAG, "Hand: " + WhichHand + ", " + msg);
    }

    private void PrintInfoLog(string msg)
    {
#if UNITY_EDITOR
        PrintDebugLog(msg);
#endif
        Log.i(LOG_TAG, "Hand: " + WhichHand + ", " + msg);
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
        Controller_Right,
        Controller_Left
    };

    [System.Serializable]
    private class BatteryIndicator
    {
        public int level;
        public float min;
        public float max;
        public string texturePath;
        public bool textureLoaded;
        public Texture2D batteryTexture;
    }

    public ControllerHand WhichHand = ControllerHand.Controller_Right;
    public GameObject defaultModel = null;
    public bool updateDynamically = false;
    public bool mergeToOneBone = false;

    private GameObject controllerSpawned = null;
    private WaveVR_Controller.EDeviceType deviceType = WaveVR_Controller.EDeviceType.Dominant;
    private bool connected = false;
    private string renderModelNamePath = "";
    private string renderModelName = "";
    private IntPtr ptrParameterName = IntPtr.Zero;
    private IntPtr ptrResult = IntPtr.Zero;
    private uint sessionid = 0;
    private const string VRACTIVITY_CLASSNAME = "com.htc.vr.unity.WVRUnityVRActivity";
    private const string FILEUTILS_CLASSNAME = "com.htc.vr.unity.FileUtils";

    private List<Color32> colors = new List<Color32>();
    private GameObject meshCom = null;
    private GameObject meshGO = null;
    private Mesh updateMesh;
    private Texture2D MatImage = null;
    private Material modelMat;

    private FBXInfo_t[] FBXInfo;
    private MeshInfo_t[] SectionInfo;
    private uint sectionCount;
    private Thread mthread;
    private bool isChecking = false;

    private Material ImgMaterial;
    private WaitForEndOfFrame wfef = null;
    private WaitForSeconds wfs = null;
    private bool bLoadMesh = false;
    private bool isProcessing = false;
    private bool showBatterIndicator = true;
    private bool isBatteryIndicatorReady = false;
    private List<BatteryIndicator> batteryTextureList = new List<BatteryIndicator>();
    private BatteryIndicator currentBattery;
    private GameObject batteryGO = null;
    private MeshRenderer batteryMR = null;

    void OnEnable()
    {
        PrintDebugLog("OnEnable");
        sessionid = 0;

        if (WhichHand == ControllerHand.Controller_Right)
        {
            deviceType = WaveVR_Controller.EDeviceType.Dominant;
        }
        else
        {
            deviceType = WaveVR_Controller.EDeviceType.NonDominant;
        }

        connected = checkConnection();

        if (connected)
        {
            WaveVR.Device _device = WaveVR.Instance.getDeviceByType(this.deviceType);
            if (!checkChild())
            {
                if (isProcessing)
                {
                    PrintDebugLog("OnEnable - Controller connected, model is loading!");
                }
                else
                {
                    PrintDebugLog("Controller load when OnEnable!");
                    onLoadController(_device.type);
                }
            } else
            {
                if (isRenderModelNameSameAsPrevious())
                {
                    PrintDebugLog("OnEnable - Controller connected, model was loaded!");
                }
                else
                {
                    PrintDebugLog("Controller load when OnEnable, render model is different!");
                    deleteChild();
                    onLoadController(_device.type);
                }
            }
        }
#if !UNITY_EDITOR
        WaveVR_Utils.Event.Listen(WaveVR_Utils.Event.DEVICE_CONNECTED, onDeviceConnected);
        WaveVR_Utils.Event.Listen(WaveVR_Utils.Event.DEVICE_ROLE_CHANGED, onDeviceRoleChanged);
        WaveVR_Utils.Event.Listen(WaveVR_Utils.Event.OEM_CONFIG_CHANGED, onOEMConfigChanged);
#endif
    }

    void OnDisable()
    {
        PrintDebugLog("OnDisable, release native session mesh: " + sessionid);
        isProcessing = false;
#if !UNITY_EDITOR
        WaveVR_Utils.Assimp.releaseMesh(sessionid);

        WaveVR_Utils.Event.Remove(WaveVR_Utils.Event.DEVICE_CONNECTED, onDeviceConnected);
        WaveVR_Utils.Event.Remove(WaveVR_Utils.Event.DEVICE_ROLE_CHANGED, onDeviceRoleChanged);
        WaveVR_Utils.Event.Listen(WaveVR_Utils.Event.OEM_CONFIG_CHANGED, onOEMConfigChanged);
#endif
    }

    private void onOEMConfigChanged(params object[] args)
    {
        PrintDebugLog("onOEMConfigChanged");
        ReadJsonValues();
    }

    private void ReadJsonValues()
    {
        showBatterIndicator = false;
        string json_values = WaveVR_Utils.OEMConfig.getBatteryConfig();

        if (!json_values.Equals(""))
        {
            try
            {
                SimpleJSON.JSONNode jsNodes = SimpleJSON.JSONNode.Parse(json_values);

                string node_value = "";
                node_value = jsNodes["show"].Value;
                if (!node_value.Equals(""))
                {
                    if (node_value.Equals("2")) // always
                    {
                        showBatterIndicator = true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.e(LOG_TAG,"JsonParse failed: " + e.ToString());
            }
        }

        PrintDebugLog("showBatterIndicator: " + showBatterIndicator);
    }

    private void onDeviceConnected(params object[] args)
    {
        WVR_DeviceType eventType = (WVR_DeviceType)args[0];
        WVR_DeviceType _type = WVR_DeviceType.WVR_DeviceType_Invalid;

        bool _connected = false;
#if UNITY_EDITOR
        if (Application.isEditor)
        {
            _connected = WaveVR_Controller.Input(this.deviceType).connected;
            _type = WaveVR_Controller.Input(this.deviceType).DeviceType;
        }
        else
#endif
        {
            WaveVR.Device _device = WaveVR.Instance.getDeviceByType(this.deviceType);
            _connected = _device.connected;
            _type = _device.type;

            if (eventType != _type)
            {
                PrintDebugLog("onDeviceConnected() event type is " + eventType + ", this.deviceType is " + _type + ", skip");
                return;
            }
        }

        PrintDebugLog("onDeviceConnected() " + _type + " is " + (_connected ? "connected" : "disconnected") + ", left-handed? " + WaveVR_Controller.IsLeftHanded);

        if (connected != _connected)
        {
            connected = _connected;

            if (connected)
            {
                if (!checkChild())
                {
                    if (isProcessing)
                    {
                        PrintDebugLog("onDeviceConnected - Controller connected, model is loading!");
                    }
                    else
                    {
                        PrintDebugLog("Controller load when onDeviceConnected!");
                        onLoadController(_type);
                    }
                }
                else
                {
                    if (isRenderModelNameSameAsPrevious())
                    {
                        PrintDebugLog("onDeviceConnected - Controller connected, model was loaded!");
                    }
                    else
                    {
                        PrintDebugLog("Controller load when onDeviceConnected, render model is different!");
                        deleteChild();
                        onLoadController(_type);
                    }
                }
            }
        }
    }

    private void onDeviceRoleChanged(params object[] args)
    {
        PrintDebugLog("onDeviceRoleChanged() ");
        WVR_DeviceType _type = WVR_DeviceType.WVR_DeviceType_Invalid;
        this.connected = false;
        isProcessing = false;

        if (!checkChild())
        {
            PrintDebugLog("delete sub-models when onDeviceRoleChanged");
            deleteChild();
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

    private bool isRenderModelNameSameAsPrevious()
    {
        WVR_DeviceType type = WaveVR_Controller.Input(this.deviceType).DeviceType;
        bool _connected = WaveVR_Controller.Input(this.deviceType).connected;
        bool _same = false;

        if (!_connected)
            return _same;

        string parameterName = "GetRenderModelName";
        ptrParameterName = Marshal.StringToHGlobalAnsi(parameterName);
        ptrResult = Marshal.AllocHGlobal(64);
        uint resultVertLength = 64;

        Interop.WVR_GetParameters(type, ptrParameterName, ptrResult, resultVertLength);
        string tmprenderModelName = Marshal.PtrToStringAnsi(ptrResult);

        PrintDebugLog("previous render model: " + renderModelName + ", current render model name: " + tmprenderModelName);

        if (tmprenderModelName == renderModelName)
        {
            _same = true;
        }
        Marshal.FreeHGlobal(ptrParameterName);
        Marshal.FreeHGlobal(ptrResult);

        return _same;
    }

    // Use this for initialization
    void Start()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            bool _connected = WaveVR_Controller.Input(this.deviceType).connected;
            WVR_DeviceType _type = WaveVR_Controller.Input(this.deviceType).DeviceType;
            onLoadController(_type);
            return;
        }
#endif
        PrintDebugLog("start() connect: " + connected + " Which hand: " + WhichHand);
        wfs = new WaitForSeconds(1.0f);
        ReadJsonValues();

        if (updateDynamically)
        {
            PrintDebugLog("updateDynamically, start a coroutine to check connection and render model name periodly");
            StartCoroutine(checkRenderModelAndDelete());
        }
    }

    int t = 0;
    bool IsFocusCapturedBySystemLastFrame = false;

    // Update is called once per frame
    void Update()
    {
        if (Interop.WVR_IsInputFocusCapturedBySystem())
        {
            IsFocusCapturedBySystemLastFrame = true;
            return;
        }

        if (IsFocusCapturedBySystemLastFrame || (t-- < 0))
        {
            updateBatteryLevel();
            t = 200;
            IsFocusCapturedBySystemLastFrame = false;
        }

        Log.gpl.d(LOG_TAG, "Update() render model " + WhichHand + " connect ? " + this.connected + ", child object count ? " + transform.childCount + ", showBatterIndicator: " + showBatterIndicator + ", hasBattery: " + isBatteryIndicatorReady);
    }

    private void onLoadController(WVR_DeviceType type)
    {
        isProcessing = true;
        PrintDebugLog("Pos: " + this.transform.localPosition.x + " " + this.transform.localPosition.y + " " + this.transform.localPosition.z);
        PrintDebugLog("Rot: " + this.transform.localEulerAngles);
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            PrintDebugLog("onLoadController in play mode");
            if (defaultModel != null)
            {
                controllerSpawned = Instantiate(defaultModel, this.transform);
                controllerSpawned.transform.parent = this.transform;
            }
            isProcessing = false;
            return;
        }
#endif

        if (Interop.WVR_GetWaveRuntimeVersion() < 2)
        {
            PrintDebugLog("onLoadController in old service");
            if (defaultModel != null)
            {
                controllerSpawned = Instantiate(defaultModel, this.transform);
                controllerSpawned.transform.parent = this.transform;
            }
            isProcessing = false;
            return;
        }

        bool loadControllerAssets = true;
        var found = false;

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
        bool _ret = int.TryParse(Marshal.PtrToStringAnsi(ptrResultDeviceIndex), out _out);
        if (_ret)
            deviceIndex = _out;

        PrintInfoLog("get controller id from runtime is " + renderModelName + ", deviceIndex = " + deviceIndex);

        // 1. check if there are assets in private folder
        string renderModelFolderPath = Application.temporaryCachePath + "/";
        string renderModelUnzipFolder = renderModelFolderPath + renderModelName + "/";
        renderModelNamePath = renderModelFolderPath + renderModelName + "/Model";

        if (!Directory.Exists(renderModelNamePath))
        {
            PrintWarningLog(renderModelName + " assets, start to deploy");
            loadControllerAssets = deployZIPFile(deviceIndex, renderModelUnzipFolder);
        }
        isProcessing = loadControllerAssets;
        if (loadControllerAssets)
        {
            found = loadMeshAndImageByDevice(renderModelNamePath);
            isProcessing = found;
            if (found)
            {
                bool renderModelReady = makeupControllerModel(renderModelNamePath, sessionid);
                PrintDebugLog("renderModelReady = " + renderModelReady);
                isProcessing = renderModelReady;
                Marshal.FreeHGlobal(ptrParameterName);
                Marshal.FreeHGlobal(ptrResult);
                return;
            }
        }

        if (defaultModel != null)
        {
            PrintDebugLog("Can't load controller model from DS, load default model");
            controllerSpawned = Instantiate(defaultModel, this.transform);
            controllerSpawned.transform.parent = this.transform;
        }

        Marshal.FreeHGlobal(ptrParameterName);
        Marshal.FreeHGlobal(ptrResult);
    }

    private bool deployZIPFile(int deviceIndex, string renderModelUnzipFolder)
    {
        AndroidJavaClass ajc = new AndroidJavaClass(VRACTIVITY_CLASSNAME);

        if (ajc == null || deviceIndex == -1)
        {
            PrintWarningLog("AndroidJavaClass vractivity is null, deviceIndex = " + deviceIndex);
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
                            ajc = null;
                            PrintInfoLog("doUnZIPAndDeploy success");
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

    private bool loadMeshAndImageByDevice(string renderModelNamePath)
    {
        IntPtr ptrError = Marshal.AllocHGlobal(64);
        string FBXFile = renderModelNamePath + "/" + "controller00.fbx";
        bool ret = false;
        string errorCode = "";

        if (File.Exists(FBXFile))
        {
            ret = WaveVR_Utils.Assimp.OpenMesh(FBXFile, ref sessionid, ptrError, mergeToOneBone);
            errorCode = Marshal.PtrToStringAnsi(ptrError);
        }

        if (!ret)
        {
            errorCode = "controller00.fbx load fail!";
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.DS_ASSETS_NOT_FOUND, deviceType);
        }

        PrintDebugLog("ret = " + ret + " error code = " + errorCode);
        if (ret)
        {
            string imageFile = renderModelNamePath + "/" + "controller00.png";
            bool fileExist = File.Exists(imageFile);
            PrintInfoLog("controller00.png exist: " + fileExist);
            ret = fileExist;

            if (!fileExist)
            {
                WaveVR_Utils.Event.Send(WaveVR_Utils.Event.DS_ASSETS_NOT_FOUND, deviceType);
            }
        }
        PrintInfoLog("loadMeshAndImageByDevice ret: " + ret);
        Marshal.FreeHGlobal(ptrError);

        return ret;
    }

    public bool makeupControllerModel(string renderModelNamePath, uint sid)
    {
        sectionCount = 0;
        sessionid = sid;
        if (checkChild()) deleteChild();

        string imageFile = renderModelNamePath + "/controller00.png";

        if (!File.Exists(imageFile))
        {
            PrintDebugLog("failed to load texture");
            WaveVR_Utils.Assimp.releaseMesh(sessionid);
            return false;
        }
        byte[] imgByteArray = File.ReadAllBytes(imageFile);
        MatImage = new Texture2D(2, 2, TextureFormat.BGRA32, false);
        bool retLoad = MatImage.LoadImage(imgByteArray);
        PrintDebugLog("load image ret: " + retLoad + " size: " + imgByteArray.Length);
        if (!retLoad)
        {
            PrintDebugLog("failed to load texture");
            WaveVR_Utils.Assimp.releaseMesh(sessionid);
            return false;
        }
        bLoadMesh = false;
        ImgMaterial = new Material(Shader.Find("Unlit/Texture"));
        wfef = new WaitForEndOfFrame();

        PrintDebugLog("reset bLoadMesh, start to spawn game object after new connection");
        StartCoroutine(SpawnRenderModel());
        ThreadStart threadStart = new ThreadStart(readNativeData);
        mthread = new Thread(threadStart);
        mthread.Start();

        isChecking = true;
        return true;
    }

    string emitterMeshName = "__CM__Emitter";

    IEnumerator SpawnRenderModel()
    {
        while(true)
        {
            if (bLoadMesh) break;
            PrintDebugLog("SpawnRenderModel is waiting");
            yield return wfef;
        }
        spawnMesh();
    }

    void spawnMesh()
    {
        if (!bLoadMesh)
        {
            PrintDebugLog("bLoadMesh is false, skipping spawn objects");
            isProcessing = false;
            return;
        }

        PrintDebugLog("SpawnMesh");
        string meshName = "";
        for (uint i = 0; i < sectionCount; i++)
        {
            meshName = Marshal.PtrToStringAnsi(FBXInfo[i].meshName);
            meshCom = null;
            meshGO = null;

            bool meshAlready = false;

            for (uint j = 0; j < i; j++)
            {
                string tmp = Marshal.PtrToStringAnsi(FBXInfo[j].meshName);

                if (tmp.Equals(meshName))
                {
                    meshAlready = true;
                }
            }

            if (meshAlready)
            {
                PrintDebugLog(meshName + " is created! skip.");
                continue;
            }

            if (mergeToOneBone && SectionInfo[i]._active)
            {
                meshName = "Merge_" + meshName;
            }
            updateMesh = new Mesh();
            meshCom = new GameObject();
            meshCom.AddComponent<MeshRenderer>();
            meshCom.AddComponent<MeshFilter>();
            meshGO = Instantiate(meshCom);
            meshGO.transform.parent = this.transform;
            meshGO.name = meshName;

            Matrix4x4 t = WaveVR_Utils.RigidTransform.toMatrix44(FBXInfo[i].matrix);

            Vector3 x = WaveVR_Utils.GetPosition(t);
            meshGO.transform.localPosition = new Vector3(x.x, x.y, -x.z);

            meshGO.transform.localRotation = WaveVR_Utils.GetRotation(t);
            Vector3 r = meshGO.transform.localEulerAngles;
            meshGO.transform.localEulerAngles = new Vector3(-r.x, r.y, r.z);
            meshGO.transform.localScale = WaveVR_Utils.GetScale(t);

            PrintDebugLog("i = " + i + " MeshGO = " + meshName + ", localPosition: " + meshGO.transform.localPosition.x + ", " + meshGO.transform.localPosition.y + ", " + meshGO.transform.localPosition.z);
            PrintDebugLog("i = " + i + " MeshGO = " + meshName + ", localRotation: " + meshGO.transform.localEulerAngles);
            PrintDebugLog("i = " + i + " MeshGO = " + meshName + ", localScale: " + meshGO.transform.localScale);

            var meshfilter = meshGO.GetComponent<MeshFilter>();
            updateMesh.Clear();
            updateMesh.vertices = SectionInfo[i]._vectice;
            updateMesh.uv = SectionInfo[i]._uv;
            updateMesh.uv2 = SectionInfo[i]._uv;
            updateMesh.colors32 = colors.ToArray();
            updateMesh.normals = SectionInfo[i]._normal;
            updateMesh.SetIndices(SectionInfo[i]._indice, MeshTopology.Triangles, 0);
            updateMesh.name = meshName;
            if (meshfilter != null)
            {
                meshfilter.mesh = updateMesh;
            }
            var meshRenderer = meshGO.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                if (ImgMaterial == null)
                {
                    PrintDebugLog("ImgMaterial is null");
                }
                meshRenderer.material = ImgMaterial;
                meshRenderer.material.mainTexture = MatImage;
                meshRenderer.enabled = true;
            }

            if (meshName.Equals(emitterMeshName))
            {
                PrintDebugLog(meshName + " is found, set " + meshName + " active: true");
                meshGO.SetActive(true);
            }
            else if (meshName.Equals("__CM__Battery"))
            {
                getBatteryIndicatorParam();
                if (isBatteryIndicatorReady)
                {
                    batteryMR = meshGO.GetComponent<MeshRenderer>();
                    var mat = Resources.Load("TransparentMat") as Material;
                    if (mat != null)
                    {
                        batteryMR.material = mat;
                    }

                    batteryMR.material.mainTexture = batteryTextureList[0].batteryTexture;
                    batteryMR.enabled = true;
                }
                meshGO.SetActive(false);
                PrintDebugLog(meshName + " is found, set " + meshName + " active: false (waiting for update");
                batteryGO = meshGO;
            }
            else if (meshName == "__CM__TouchPad_Touch")
            {
                PrintDebugLog(meshName + " is found, set " + meshName + " active: false");
                meshGO.SetActive(false);
            } else
            {
                PrintDebugLog("set " + meshName + " active: " + SectionInfo[i]._active);
                meshGO.SetActive(SectionInfo[i]._active);
            }
        }
        PrintDebugLog("send " + deviceType + " ADAPTIVE_CONTROLLER_READY ");
        WaveVR_Utils.Event.Send(WaveVR_Utils.Event.ADAPTIVE_CONTROLLER_READY, deviceType);
        cleanNativeData();
        bLoadMesh = false;
        Resources.UnloadUnusedAssets();
        isProcessing = false;
    }

    void getBatteryIndicatorParam()
    {
        isBatteryIndicatorReady = false;

        string batteryJsonFile = renderModelNamePath + "/" + "BatteryIndicator.json";

        if (!File.Exists(batteryJsonFile))
        {
            PrintDebugLog(batteryJsonFile + " is not found!");
            return ;
        }

        StreamReader json_sr = new StreamReader(batteryJsonFile);

        string JsonString = json_sr.ReadToEnd();
        PrintDebugLog("BatteryIndicator json: " + JsonString);
        json_sr.Close();

        if (JsonString.Equals(""))
        {
            PrintDebugLog("JsonString is empty!");
            return ;
        }

        SimpleJSON.JSONNode jsNodes = SimpleJSON.JSONNode.Parse(JsonString);

        string tmpStr = "";
        tmpStr = jsNodes["LevelCount"].Value;

        if (tmpStr.Equals(""))
        {
            PrintDebugLog("Battery level is not found!");
            return ;
        }

        int batteryLevel = int.Parse(tmpStr);
        PrintDebugLog("Battery level is " + batteryLevel);

        if (batteryLevel <= 0)
        {
            PrintDebugLog("Battery level is less or equal to 0!");
            return;
        }

        for (int i=0; i<batteryLevel; i++)
        {
            string minStr = jsNodes["BatteryLevel"][i]["min"].Value;
            string maxStr = jsNodes["BatteryLevel"][i]["max"].Value;
            string pathStr = jsNodes["BatteryLevel"][i]["path"].Value;

            if (minStr.Equals("") || maxStr.Equals("") || pathStr.Equals(""))
            {
                PrintDebugLog("Min, Max or Path is not found!");
                batteryLevel = 0;
                batteryTextureList.Clear();
                return;
            }

            string batteryLevelFile = renderModelNamePath + "/" + pathStr;

            if (!File.Exists(batteryLevelFile))
            {
                PrintDebugLog(batteryLevelFile + " is not found!");
                batteryLevel = 0;
                batteryTextureList.Clear();
                return;
            }

            BatteryIndicator tmpBI = new BatteryIndicator();
            tmpBI.level = i;
            tmpBI.min = float.Parse(minStr);
            tmpBI.max = float.Parse(maxStr);
            tmpBI.texturePath = batteryLevelFile;

            byte[] imgByteArray = File.ReadAllBytes(batteryLevelFile);
            PrintDebugLog("Image size: " + imgByteArray.Length);

            tmpBI.batteryTexture = new Texture2D(2, 2, TextureFormat.BGRA32, false);
            tmpBI.textureLoaded = tmpBI.batteryTexture.LoadImage(imgByteArray);

            PrintDebugLog("Battery Level: " + tmpBI.level + " min: " + tmpBI.min + " max: " + tmpBI.max + " path: " + tmpBI.texturePath + " loaded: " + tmpBI.textureLoaded);

            batteryTextureList.Add(tmpBI);
        }

        isBatteryIndicatorReady = true;
        PrintDebugLog("BatteryIndicator is ready!");
    }

    void cleanNativeData()
    {
        PrintDebugLog("cleanNativeData sessionid = " + sessionid);
        for (int i = 0; i < sectionCount; i++)
        {
            for (int j = 0; j < FBXInfo[i].verticeCount; j++)
            {
                SectionInfo[i]._vectice[j] = Vector3.zero;
            }
            SectionInfo[i]._vectice = null;

            for (int j = 0; j < FBXInfo[i].verticeCount; j++)
            {
                SectionInfo[i]._normal[j] = Vector3.zero;
            }
            SectionInfo[i]._normal = null;

            for (int j = 0; j < FBXInfo[i].verticeCount; j++)
            {
                SectionInfo[i]._uv[j] = Vector2.zero;
            }
            SectionInfo[i]._uv = null;

            for (int j = 0; j < FBXInfo[i].verticeCount; j++)
            {
                SectionInfo[i]._indice[j] = 0;
            }
            SectionInfo[i]._indice = null;

            Marshal.FreeHGlobal(FBXInfo[i].meshName);
        }
        sectionCount = 0;
        SectionInfo = null;
        FBXInfo = null;
        WaveVR_Utils.Assimp.releaseMesh(sessionid);
    }

    void readNativeData()
    {
        bool ret = false;
        PrintDebugLog("sessionid = " + sessionid);
        bool finishLoading = WaveVR_Utils.Assimp.getSectionCount(sessionid, ref sectionCount);

        if (!finishLoading || sectionCount == 0)
        {
            PrintDebugLog("failed to load mesh");
            return;
        }

        FBXInfo = new FBXInfo_t[sectionCount];
        SectionInfo = new MeshInfo_t[sectionCount];
        for (int i = 0; i < sectionCount; i++)
        {
            FBXInfo[i] = new FBXInfo_t();
            SectionInfo[i] = new MeshInfo_t();

            FBXInfo[i].meshName = Marshal.AllocHGlobal(256);
        }

        ret = WaveVR_Utils.Assimp.getMeshData(sessionid, FBXInfo);
        if (!ret)
        {
            for (int i = 0; i < sectionCount; i++)
            {
                Marshal.FreeHGlobal(FBXInfo[i].meshName);
            }

            SectionInfo = null;
            FBXInfo = null;
            WaveVR_Utils.Assimp.releaseMesh(sessionid);
            return;
        }

        for (uint i = 0; i < sectionCount; i++)
        {
            SectionInfo[i]._vectice = new Vector3[FBXInfo[i].verticeCount];
            for (int j = 0; j < FBXInfo[i].verticeCount; j++)
            {
                SectionInfo[i]._vectice[j] = new Vector3();
            }
            SectionInfo[i]._normal = new Vector3[FBXInfo[i].normalCount];
            for (int j = 0; j < FBXInfo[i].verticeCount; j++)
            {
                SectionInfo[i]._normal[j] = new Vector3();
            }
            SectionInfo[i]._uv = new Vector2[FBXInfo[i].uvCount];
            for (int j = 0; j < FBXInfo[i].verticeCount; j++)
            {
                SectionInfo[i]._uv[j] = new Vector2();
            }
            SectionInfo[i]._indice = new int[FBXInfo[i].indiceCount];
            for (int j = 0; j < FBXInfo[i].verticeCount; j++)
            {
                SectionInfo[i]._indice[j] = new int();
            }

            bool active = false;

            bool tret = WaveVR_Utils.Assimp.getSectionData(sessionid, i, SectionInfo[i]._vectice, SectionInfo[i]._normal, SectionInfo[i]._uv, SectionInfo[i]._indice, ref active);
            if (!tret) continue;

            SectionInfo[i]._active = active;

            PrintDebugLog("i = " + i + ", name = " + Marshal.PtrToStringAnsi(FBXInfo[i].meshName) + ", active = " + SectionInfo[i]._active);
            PrintDebugLog("i = " + i + ", relative transform = [" + FBXInfo[i].matrix.m0 + " , " + FBXInfo[i].matrix.m1 + " , " + FBXInfo[i].matrix.m2 + " , " + FBXInfo[i].matrix.m3 + "] ");
            PrintDebugLog("i = " + i + ", relative transform = [" + FBXInfo[i].matrix.m4 + " , " + FBXInfo[i].matrix.m5 + " , " + FBXInfo[i].matrix.m6 + " , " + FBXInfo[i].matrix.m7 + "] ");
            PrintDebugLog("i = " + i + ", relative transform = [" + FBXInfo[i].matrix.m8 + " , " + FBXInfo[i].matrix.m9 + " , " + FBXInfo[i].matrix.m10 + " , " + FBXInfo[i].matrix.m11 + "] ");
            PrintDebugLog("i = " + i + ", relative transform = [" + FBXInfo[i].matrix.m12 + " , " + FBXInfo[i].matrix.m13 + " , " + FBXInfo[i].matrix.m14 + " , " + FBXInfo[i].matrix.m15 + "] ");
            PrintDebugLog("i = " + i + ", vertice count = " + FBXInfo[i].verticeCount + ", normal count = " + FBXInfo[i].normalCount + ", uv count = " + FBXInfo[i].uvCount + ", indice count = " + FBXInfo[i].indiceCount);
        }

        bLoadMesh = true;
    }

    void OnApplicationPause(bool pauseStatus)
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
#endif

        if (updateDynamically)
        {
            if (!pauseStatus) // resume
            {
                isChecking = DeleteControllerWhenDisconnect();
            }
            else
            {
                isChecking = false;
            }
        }
    }

    void updateBatteryLevel()
    {
        if (batteryGO != null)
        {
            if (showBatterIndicator && isBatteryIndicatorReady)
            {
                bool found = false;

                WaveVR.Device _device = WaveVR.Instance.getDeviceByType(this.deviceType);

                float batteryP = Interop.WVR_GetDeviceBatteryPercentage(_device.type);

                if (batteryP < 0)
                {
                    PrintDebugLog("updateBatteryLevel BatteryPercentage is negative, return");
                    batteryGO.SetActive(false);
                    return;
                }

                foreach (BatteryIndicator bi in batteryTextureList)
                {
                    if (batteryP >= bi.min/100 && batteryP <= bi.max/100)
                    {
                        currentBattery = bi;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    if (batteryMR != null)
                    {
                        batteryMR.material.mainTexture = currentBattery.batteryTexture;
                        PrintDebugLog("updateBatteryLevel battery level to " + currentBattery.level + ", battery percent: " + batteryP);
                        batteryGO.SetActive(true);
                    }
                    else
                    {
                        PrintDebugLog("updateBatteryLevel Can't get battery mesh renderer");
                        batteryGO.SetActive(false);
                    }
                } else
                {
                    batteryGO.SetActive(false);
                }
            } else
            {
                batteryGO.SetActive(false);
            }
        }
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

    private void deleteChild()
    {
        var ch = transform.childCount;

        for (int i = 0; i < ch; i++)
        {
            PrintInfoLog("deleteChild: " + transform.GetChild(i).gameObject.name);

            GameObject CM = transform.GetChild(i).gameObject;

            Destroy(CM);
        }
    }

    private bool checkChild()
    {
        var ch = transform.childCount;

        return (ch > 0) ? true : false;
    }

    private bool DeleteControllerWhenDisconnect()
    {
        if (!checkChild()) return false;

        bool _connected = WaveVR_Controller.Input(this.deviceType).connected;

        if (_connected)
        {
            WVR_DeviceType type = WaveVR_Controller.Input(this.deviceType).DeviceType;
            string parameterName = "GetRenderModelName";
            ptrParameterName = Marshal.StringToHGlobalAnsi(parameterName);
            ptrResult = Marshal.AllocHGlobal(64);
            uint resultVertLength = 64;

            Interop.WVR_GetParameters(type, ptrParameterName, ptrResult, resultVertLength);
            string tmprenderModelName = Marshal.PtrToStringAnsi(ptrResult);

            if (tmprenderModelName != renderModelName)
            {
                PrintInfoLog("Destroy controller prefeb because render model is different");
                deleteChild();
                Marshal.FreeHGlobal(ptrParameterName);
                Marshal.FreeHGlobal(ptrResult);
                return false;
            }
            Marshal.FreeHGlobal(ptrParameterName);
            Marshal.FreeHGlobal(ptrResult);
        }
        else
        {
            PrintInfoLog("Destroy controller prefeb because it is disconnect");
            deleteChild();
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
        }
        else
#endif
        {
            WaveVR.Device _device = WaveVR.Instance.getDeviceByType(this.deviceType);
            return _device.connected;
        }
    }
}
