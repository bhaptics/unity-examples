// "WaveVR SDK
// © 2017 HTC Corporation. All Rights Reserved.
//
// Unless otherwise required by copyright law and practice,
// upon the execution of HTC SDK license agreement,
// HTC grants you access to and use of the WaveVR SDK(s).
// You shall fully comply with all of HTC’s SDK license agreement terms and
// conditions signed by you and all SDK and API requirements,
// specifications, and documentation provided by HTC to You."

#pragma warning disable 0219
#pragma warning disable 0414

using UnityEngine;
using wvr;
using System;
using WaveVR_Log;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(WaveVR_ControllerPointer))]
public class WaveVR_ControllerPointerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        WaveVR_ControllerPointer myScript = target as WaveVR_ControllerPointer;

        myScript.ShowPointer = (bool)EditorGUILayout.Toggle("Show Controller Pointer", myScript.ShowPointer);
        if (true == myScript.ShowPointer)
        {
            myScript.device = (WaveVR_Controller.EDeviceType)EditorGUILayout.EnumPopup("device", myScript.device);
            myScript.UseSystemConfig = EditorGUILayout.Toggle("Use system config", myScript.UseSystemConfig);

            if (false == myScript.UseSystemConfig)
            {
                EditorGUILayout.LabelField("Custom settings");
                myScript.Blink = EditorGUILayout.Toggle("Pointer blinking", myScript.Blink);
                myScript.PointerOuterDiameterMin = EditorGUILayout.FloatField("Min. pointer diameter", myScript.PointerOuterDiameterMin);

                EditorGUILayout.Space();
                myScript.UseDefaultTexture = EditorGUILayout.Toggle("Use default texture", myScript.UseDefaultTexture);

                if (false == myScript.UseDefaultTexture)
                {
                    myScript.CustomTexture = (Texture2D)EditorGUILayout.ObjectField("Custom texture", myScript.CustomTexture, typeof(Texture2D), false);
                }
            }
        }

        if (GUI.changed)
            EditorUtility.SetDirty((WaveVR_ControllerPointer)target);
    }
}
#endif

/// <summary>
/// Draws a pointer of controller to indicate to which object is pointed.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class WaveVR_ControllerPointer : MonoBehaviour {
    private const string LOG_TAG = "WaveVR_ControllerPointer";
    private void PrintDebugLog(string msg)
    {
        Log.d (LOG_TAG, this.device + ", " + gameObject.name + ", " + msg, true);
    }

    private void PrintIntervalLog(Log.PeriodLog.StringProcessDelegate del)
    {
        Log.gpl.d (LOG_TAG, () => gameObject.name + ", " + del(), true);
    }

    public WaveVR_Controller.EDeviceType device;

    #region Variables Setter
    public bool useSystemConfig = true;
    public bool UseSystemConfig
    {
        get { return this.useSystemConfig; }
        set {
            this.useSystemConfig = value;
            PrintDebugLog ("UseSystemConfig set: " + this.useSystemConfig);
        }
    }

    public float pointerOuterDiameterMin = 0.01f;           // Current outer diameters of the pointer, before distance multiplication.
    public float PointerOuterDiameterMin
    {
        get { return this.pointerOuterDiameterMin; }
        set {
            this.pointerOuterDiameterMin = value;
            PrintDebugLog ("PointerOuterDiameterMin set: " + this.pointerOuterDiameterMin);
        }
    }

    public float pointerOuterDiameter = 0.0f;               // Current outer diameters of the pointer, before distance multiplication.
    public float PointerOuterDiameter
    {
        get { return this.pointerOuterDiameter; }
        set {
            this.pointerOuterDiameter = value;
            PrintDebugLog ("PointerOuterDiameter set: " + this.pointerOuterDiameter);
        }
    }

    private const float pointerDistanceMin = 0.5f;         // Min length of Beam
    private const float pointerDistanceMax = 10.0f;        // Max length of Beam + 0.5m
    public float pointerDistanceInMeters = 1.3f;            // Current distance of the pointer (in meters) = beam.endOffset (0.8) + 0.5
    public float PointerDistanceInMeters
    {
        get { return this.pointerDistanceInMeters; }
        set {
            if (value > pointerDistanceMax || value < pointerDistanceMin)
                return;

            this.pointerDistanceInMeters = value;
            PrintDebugLog ("PointerDistanceInMeters set: " + this.pointerDistanceInMeters);
        }
    }

    private bool useTexture = true;
    private MeshFilter pointerMeshFilter = null;
    private Mesh pointerMesh;

    /// <summary>
    /// Material resource of pointer.
    /// It contains shader **WaveVR/CtrlrPointer** and there are 5 attributes can be changed in runtime:
    /// <para>
    /// - _OuterDiameter
    /// - _DistanceInMeters
    /// - _MainTex
    /// - _Color
    /// - _useTexture
    /// 
    /// If _useTexture is set (default), the texture assign in _MainTex will be used.
    /// </summary>
    private const string defaultPointerResource_Material = "ControllerPointer";
    private Material pointerMaterial = null;
    private Material pointerMaterialInstance = null;

    private const string defaultPointerResource_Texture = "focused_dot";
    private Texture2D defaultTexture = null;
    [HideInInspector]
    public Texture2D customTexture = null;
    public Texture2D CustomTexture
    {
        get { return this.customTexture; }
        set {
            this.customTexture = value;
            PrintDebugLog ("CustomTexture is changed.");
        }
    }

    /// <summary>
    /// True: use defaultTexture,
    /// False: use CustomTexture
    /// </summary>
    /// [HideInInspector]
    public bool useDefaultTexture = true;
    public bool UseDefaultTexture
    {
        get { return this.useDefaultTexture; }
        set {
            this.useDefaultTexture = value;
            PrintDebugLog ("UseDefaultTexture set: " + this.useDefaultTexture);
        }
    }
    [HideInInspector]
    public Color pointerColor = Color.white;               // #FFFFFFFF
    public Color PointerColor
    {
        get { return this.pointerColor; }
        set {
            this.pointerColor = value;
            PrintDebugLog ("PointerColor set: " + this.pointerColor);
        }
    }

    private Color borderColor = new Color(119, 119, 119, 255);      // #777777FF
    private Color focusColor = new Color(255, 255, 255, 255);       // #FFFFFFFF
    private Color focusBorderColor = new Color(119, 119, 119, 255); // #777777FF

    private const int pointerRenderQueueMin = 1000;
    private const int pointerRenderQueueMax = 5000;
    [HideInInspector]
    public int pointerRenderQueue = pointerRenderQueueMin;
    public int PointerRenderQueue
    {
        get { return this.pointerRenderQueue; }
        set {
            if (value > pointerRenderQueueMax || value < pointerRenderQueueMin)
                return;

            this.pointerRenderQueue = (int)value;
            PrintDebugLog ("PointerRenderQueue() " + this.pointerRenderQueue);
        }
    }
    [HideInInspector]
    public bool showPointer = true;                         // true: show pointer, false: remove pointer
    public bool ShowPointer
    {
        get { return this.showPointer; }
        set {
            this.showPointer = value;
            PrintDebugLog ("ShowPointer set: " + this.showPointer);
        }
    }

    private string textureName = null;

    public bool blink = false;
    public bool Blink
    {
        get { return this.blink; }
        set {
            this.blink = value;
            PrintDebugLog ("Blink set: " + this.blink);
        }
    }

    private Color colorFactor = Color.white;               // The color variable of the pointer
    #endregion

    #region OEM CONFIG JSON parser
    /**
     * OEM Config
     * \"pointer\": {
       \"diameter\": 0.01,
       \"distance\": 1.3,
       \"use_texture\": true,
       \"color\": \"#FFFFFFFF\",
       \"border_color\": \"#777777FF\",
       \"focus_color\": \"#FFFFFFFF\",
       \"focus_border_color\": \"#777777FF\",
       \"texture_name\":  null,
       \"blink\": false
       },
     **/
    private void ReadJsonValues()
    {
        string json_values = WaveVR_Utils.OEMConfig.getControllerConfig ();

        if (!json_values.Equals(""))
        {
            try
            {
                SimpleJSON.JSONNode jsNodes = SimpleJSON.JSONNode.Parse(json_values);
                string node_value = "";
                node_value = jsNodes["pointer"]["diameter"].Value;
                if (!node_value.Equals("") && IsFloat(node_value) == true)
                    this.pointerOuterDiameterMin = float.Parse(node_value);

                node_value = jsNodes["pointer"]["distance"].Value;
                if (!node_value.Equals("") && IsFloat(node_value) == true)
                    this.pointerDistanceInMeters = float.Parse(node_value);

                node_value = jsNodes["pointer"]["use_texture"].Value;
                if (!node_value.Equals("") && IsBoolean(node_value) == true)
                    this.useTexture = bool.Parse(node_value);

                if (node_value.ToLower().Equals("false"))
                {
                    Log.d(LOG_TAG, "controller_pointer_use_texture = false, create texture");
                    if (this.pointerMaterialInstance != null)
                    {
                        node_value = jsNodes["pointer"]["color"].Value;
                        if (!node_value.Equals(""))
                            this.pointerColor = StringToColor32(node_value,0);

                        node_value = jsNodes["pointer"]["border_color"].Value;
                        if (!node_value.Equals(""))
                            this.borderColor = StringToColor32(node_value,1);

                        node_value = jsNodes["pointer"]["focus_color"].Value;
                        if (!node_value.Equals(""))
                            this.focusColor = StringToColor32(node_value,2);

                        node_value = jsNodes["pointer"]["focus_border_color"].Value;
                        if (!node_value.Equals(""))
                            this.focusBorderColor = StringToColor32(node_value,3);
                    }
                }
                else
                {
                    Log.d(LOG_TAG, "controller_pointer_use_texture = true");
                    node_value = jsNodes["pointer"]["pointer_texture_name"].Value;
                    if (!node_value.Equals(""))
                        this.textureName = node_value;
                }


                node_value = jsNodes["pointer"]["blink"].Value;
                if (!node_value.Equals("") && IsBoolean(node_value) == true)
                    this.blink = bool.Parse(node_value);

                PrintDebugLog("ReadJsonValues() diameter: " + this.pointerOuterDiameterMin
                    + ", distance: " + this.pointerDistanceInMeters
                    + ", use_texture: " + this.useTexture
                    + ", color: " + this.pointerColor
                    + ", pointer_texture_name: " + this.textureName
                    + ", blink: " + this.blink);
            }
            catch (Exception e) {
                Log.e(LOG_TAG, e.ToString());
            }
        }
    }

    private static bool IsBoolean(string value)
    {
        try
        {
            bool i = Convert.ToBoolean(value);
            Log.d(LOG_TAG, value + " Convert to bool success: " + i.ToString());
            return true;
        }
        catch (Exception e)
        {
            Log.e(LOG_TAG, value + " Convert to bool failed: " + e.ToString());
            return false;
        }
    }

    private static bool IsFloat(string value)
    {
        try
        {
            float i = Convert.ToSingle(value);
            Log.d(LOG_TAG, value + " Convert to float success: " + i.ToString());
            return true;
        }
        catch (Exception e)
        {
            Log.e(LOG_TAG, value + " Convert to float failed: " + e.ToString());
            return false;
        }
    }

    private static bool IsNumeric(string value)
    {
        try
        {
            int i = Convert.ToInt32(value);
            Log.d(LOG_TAG, value + " Convert to int success: " + i.ToString());
            return true;
        }
        catch (Exception e)
        {
            Log.e(LOG_TAG, value + " Convert to Int failed: " + e.ToString());
            return false;
        }
    }

    private Color32 StringToColor32(string color_string , int value)
    {
        try
        {
            byte[] _color_r = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(1, 2), 16));
            byte[] _color_g = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(3, 2), 16));
            byte[] _color_b = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(5, 2), 16));
            byte[] _color_a = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(7, 2), 16));

            return new Color32(_color_r[0], _color_g[0], _color_b[0], _color_a[0]);
        }
        catch (Exception e)
        {
            Log.e(LOG_TAG, "StringToColor32: " + e.ToString());
            switch (value)
            {
            case 1:
                return new Color(119, 119, 119, 255);
            case 2:
                return new Color(255, 255, 255, 255);
            case 3:
                return new Color(119, 119, 119, 255);
            }
            return Color.white;
        }
    }
    #endregion

    private int reticleSegments = 20;

    [HideInInspector]
    public float kpointerGrowthAngle = 90f;                 // Angle at which to expand the pointer when intersecting with an object (in degrees).

    private float colorFlickerTime = 0.0f;                  // The color flicker time

    #region MonoBehaviour overrides
    void Start()
    {
        if (this.useSystemConfig)
        {
            PrintDebugLog ("Start() use system config in WaveVR_ControllerPointer!");
            ReadJsonValues ();
        } else
        {
            PrintDebugLog ("Start() use custom config in WaveVR_ControllerPointer!");
        }
    }

    private bool isPointerEnabled = false;
    void OnEnable ()
    {
        if (!isPointerEnabled)
        {
            // Load default pointer material resource and create instance.
            this.pointerMaterial = Resources.Load (defaultPointerResource_Material) as Material;
            if (this.pointerMaterial != null)
                this.pointerMaterialInstance = Instantiate<Material> (this.pointerMaterial);
            if (this.pointerMaterialInstance == null)
                PrintDebugLog ("OnEnable() Can NOT load default material");
            else
                PrintDebugLog ("OnEnable() controller pointer material: " + this.pointerMaterialInstance.name);

            // Load default pointer texture resource.
            // If developer does not specify custom texture, default texture will be used.
            this.defaultTexture = (Texture2D)Resources.Load (defaultPointerResource_Texture);
            if (this.defaultTexture == null)
                Log.e (LOG_TAG, "OnEnable() Can NOT load default texture", true);

            // Get MeshFilter instance.
            this.pointerMeshFilter = gameObject.GetComponent<MeshFilter>();
            if (this.pointerMeshFilter == null)
                this.pointerMeshFilter = gameObject.AddComponent<MeshFilter>();

            // Get Quad mesh as default pointer mesh.
            // If developer does not use texture, pointer mesh will be created in CreatePointerMesh()
            GameObject _primGO = GameObject.CreatePrimitive (PrimitiveType.Quad);
            this.pointerMesh = _primGO.GetComponent<MeshFilter> ().sharedMesh;
            this.pointerMesh.name = "CtrlQuadPointer";
            _primGO.SetActive (false);
            GameObject.Destroy (_primGO);

            isPointerEnabled = true;
        }
    }

    void OnDisable()
    {
        PrintDebugLog ("OnDisable()");
        removePointer ();
        isPointerEnabled = false;
    }

    /// <summary>
    /// The attributes
    /// <para>
    /// - _Color
    /// - _OuterDiameter
    /// - _DistanceInMeters
    /// can be updated directly by changing 
    /// - colorFactor
    /// - pointerOuterDiameter
    /// - pointerDistanceInMeters
    /// But if developer need to update texture in runtime, developer should
    /// 1.set showPointer to false to hide pointer first.
    /// 2.assign customTexture
    /// 3.set useSystemConfig to false
    /// 4.set useDefaultTexture to false
    /// 5.set showPointer to true to generate new pointer.
    /// </summary>
    void Update()
    {
        if (this.showPointer)
        {
            if (!this.pointerInitialized)
            {
                PrintDebugLog ("Update() show pointer.");
                initialPointer ();
            }
        } else
        {
            if (this.pointerInitialized)
            {
                PrintDebugLog ("Update() hide pointer.");
                removePointer ();
            }
            return;
        }

        // Pointer distance.
        this.pointerDistanceInMeters = Mathf.Clamp (this.pointerDistanceInMeters, pointerDistanceMin, pointerDistanceMax);

        if (this.blink == true)
        {
            if (Time.unscaledTime - colorFlickerTime >= 0.5f)
            {
                colorFlickerTime = Time.unscaledTime;
                this.colorFactor = (this.colorFactor != Color.white) ? this.colorFactor = Color.white : this.colorFactor = Color.black;
            }
        } else
        {
            this.colorFactor = this.pointerColor;
        }

        if (this.pointerMaterialInstance != null)
        {
            this.pointerMaterialInstance.renderQueue = this.pointerRenderQueue;
            this.pointerMaterialInstance.SetColor ("_Color", this.colorFactor);
            this.pointerMaterialInstance.SetFloat ("_useTexture", this.useTexture ? 1.0f : 0.0f);
            this.pointerMaterialInstance.SetFloat ("_OuterDiameter", this.pointerOuterDiameter);
            this.pointerMaterialInstance.SetFloat ("_DistanceInMeters", this.pointerDistanceInMeters);
        } else
        {
            Log.gpl.d (LOG_TAG, "Pointer material is null!!", true);
        }

        Log.gpl.d(LOG_TAG, this.device + " " + gameObject.name
            + " is " + (this.showPointer ? "shown" : "hidden")
            + ", pointer color: " + this.colorFactor
            + ", use texture: " + this.useTexture
            + ", pointer outer diameter: " + this.pointerOuterDiameter
            + ", pointer distance: " + this.pointerDistanceInMeters
            + ", render queue: " + this.pointerRenderQueue, true);
    }
    #endregion

    private void CreatePointerMesh()
    {
        int vertexCount = (reticleSegments + 1) * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        for (int vi = 0, si = 0; si <= reticleSegments; si++)
        {
            float angle = (float)si / (float)reticleSegments * Mathf.PI * 2.0f;
            float x = Mathf.Sin (angle);
            float y = Mathf.Cos (angle);
            vertices [vi++] = new Vector3 (x, y, 0.0f);
            vertices [vi++] = new Vector3 (x, y, 1.0f);
        }

        int indicesCount = (reticleSegments + 1) * 6;
        int[] indices = new int[indicesCount];
        int vert = 0;
        for (int ti = 0, si = 0; si < reticleSegments; si++)
        {
            indices [ti++] = vert + 1;
            indices [ti++] = vert;
            indices [ti++] = vert + 2;
            indices [ti++] = vert + 1;
            indices [ti++] = vert + 2;
            indices [ti++] = vert + 3;

            vert += 2;
        }

        Log.i (LOG_TAG, "CreatePointerMesh() create Mesh and add MeshFilter component.", true);
        this.pointerMesh = new Mesh ();
        this.pointerMesh.vertices = vertices;
        this.pointerMesh.triangles = indices;
        this.pointerMesh.RecalculateBounds ();
    }

    private bool pointerInitialized = false;                     // true: the mesh of reticle is created, false: the mesh of reticle is not ready
    private void initialPointer()
    {
        if (!this.isPointerEnabled)
        {
            PrintDebugLog ("initialPointer() pointer is not enabled yet, do NOT initial.");
            return;
        }

        Log.i (LOG_TAG, "initialPointer()", true);
        if (this.useTexture == false)
        {
            colorFlickerTime = Time.unscaledTime;
            CreatePointerMesh ();
        }
        this.pointerMeshFilter.mesh = this.pointerMesh;

        if (this.pointerMaterialInstance != null)
        {
            if (this.useSystemConfig || this.useDefaultTexture || (null == this.customTexture))
            {
                PrintDebugLog ("initialPointer() use default texture.");
                this.pointerMaterialInstance.mainTexture = this.defaultTexture;
                this.pointerMaterialInstance.SetTexture ("_MainTex", this.defaultTexture);
            } else
            {
                PrintDebugLog ("initialPointer() use custom texture.");
                this.pointerMaterialInstance.mainTexture = this.customTexture;
                this.pointerMaterialInstance.SetTexture ("_MainTex", this.customTexture);
            }
        } else
        {
            Log.e (LOG_TAG, "initialPointer() Pointer material is null!!", true);
        }

        Renderer _rend = GetComponent<Renderer> ();
        _rend.enabled = true;
        _rend.material = this.pointerMaterialInstance;
        _rend.sortingOrder = 10000;

        this.pointerInitialized = true;
    }

    private void removePointer() {
        PrintDebugLog ("removePointer()");
        Renderer _rend = GetComponent<Renderer> ();
        _rend = GetComponent<Renderer>();
        _rend.enabled = false;
        this.pointerInitialized = false;
    }

    public void OnPointerEnter (Camera camera, GameObject target, Vector3 intersectionPosition, bool isInteractive) {
        SetPointerTarget(intersectionPosition, isInteractive);
    }

    /*
    public void OnPointerExit (Camera camera, GameObject target) {
        pointerDistanceInMeters = pointerDistanceMax;
        pointerOuterDiameter = pointerOuterDiameterMin + (pointerDistanceInMeters / kpointerGrowthAngle);
        PrintDebugLog ("OnPointerExit() pointerDistanceInMeters: " + this.pointerDistanceInMeters
        + ", pointerOuterDiameter: " + this.pointerOuterDiameter);
    }
    */

    #region Pointer Distance
    private void SetPointerTarget (Vector3 target, bool interactive)
    {
        Vector3 targetLocalPosition = transform.InverseTransformPoint (target);
        this.pointerDistanceInMeters = Mathf.Clamp (targetLocalPosition.z, pointerDistanceMin, pointerDistanceMax);
        this.pointerOuterDiameter = pointerOuterDiameterMin + (this.pointerDistanceInMeters / kpointerGrowthAngle);

        PrintIntervalLog (() => "SetPointerTarget() interactive: " + interactive
            + ", targetLocalPosition.z: " + targetLocalPosition.z
            + ", pointerDistanceInMeters: " + this.pointerDistanceInMeters
            + ", pointerOuterDiameter: " + pointerOuterDiameter);
    }
    #endregion
}
