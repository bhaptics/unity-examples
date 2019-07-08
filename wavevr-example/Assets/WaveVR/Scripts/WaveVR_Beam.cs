// "WaveVR SDK
// © 2017 HTC Corporation. All Rights Reserved.
//
// Unless otherwise required by copyright law and practice,
// upon the execution of HTC SDK license agreement,
// HTC grants you access to and use of the WaveVR SDK(s).
// You shall fully comply with all of HTC’s SDK license agreement terms and
// conditions signed by you and all SDK and API requirements,
// specifications, and documentation provided by HTC to You."

#pragma warning disable 0414

using UnityEngine;
using System.Collections.Generic;
using wvr;
using WaveVR_Log;
using System;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(WaveVR_Beam))]
public class WaveVR_BeamEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        WaveVR_Beam myScript = target as WaveVR_Beam;
        myScript.ShowBeam = EditorGUILayout.Toggle("Show beam", myScript.ShowBeam);
        if (true == myScript.ShowBeam)
        {
            myScript.ListenToDevice = EditorGUILayout.Toggle("  Listen to device", myScript.ListenToDevice);

            if (true == myScript.ListenToDevice)
            {
                myScript.device = (WaveVR_Controller.EDeviceType)EditorGUILayout.EnumPopup("    device", myScript.device);
            }

            myScript.useSystemConfig = EditorGUILayout.Toggle("  Use system config", myScript.useSystemConfig);

            if (false == myScript.useSystemConfig)
            {
                EditorGUILayout.LabelField("    Custom settings");
                myScript.updateEveryFrame = EditorGUILayout.Toggle("    Update beam per frame", myScript.updateEveryFrame);
                myScript.StartWidth = EditorGUILayout.FloatField("    Start width", myScript.StartWidth);
                myScript.EndWidth = EditorGUILayout.FloatField("    End width", myScript.EndWidth);
                myScript.StartOffset = EditorGUILayout.FloatField("    Start offset", myScript.StartOffset);
                myScript.EndOffset = EditorGUILayout.FloatField("    End offset", myScript.EndOffset);

                EditorGUILayout.Space();
                myScript.useDefaultMaterial = EditorGUILayout.Toggle("    Use default material", myScript.useDefaultMaterial);

                if (false == myScript.useDefaultMaterial)
                {

                    myScript.customMat = (Material)EditorGUILayout.ObjectField("      Custom material", myScript.customMat, typeof(Material), false);
                }
                else
                {
                    myScript.StartColor = EditorGUILayout.ColorField("      Start color", myScript.StartColor);
                    myScript.EndColor = EditorGUILayout.ColorField("      End color", myScript.EndColor);
                }
            }
        }

        if (GUI.changed)
            EditorUtility.SetDirty((WaveVR_Beam)target);
    }
}
#endif

/**
 * OEM Config
   \"beam\": {
       \"start_width\": 0.000625,
       \"end_width\": 0.00125,
       \"start_offset\": 0.015,
       \"length\":  0.8,
       \"start_color\": \"#FFFFFFFF\",
       \"end_color\": \"#FFFFFF4D\"
       },
 **/

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaveVR_Beam: MonoBehaviour
{
    private static string LOG_TAG = "WaveVR_Beam";
    private void PrintDebugLog(string msg)
    {
        Log.d (LOG_TAG, this.device + ", " + msg, true);
    }

    private void PrintIntervalLog(Log.PeriodLog.StringProcessDelegate del)
    {
        Log.gpl.d (LOG_TAG, del, true);
    }

    public bool ShowBeam = true;
    public bool useSystemConfig = true;
    public bool useDefaultMaterial = true;
    [HideInInspector]
    public float startWidth = 0.000625f;
    public float StartWidth
    {
        get { return this.startWidth; }
        set {
            this.startWidth = value;
            this.toUpdateBeam = true;
        }
    }

    [HideInInspector]
    public float endWidth = 0.00125f;
    public float EndWidth
    {
        get { return this.endWidth; }
        set {
            this.endWidth = value;
            this.toUpdateBeam = true;
        }
    }

    private const float minimal_length = 0.1f;
    [HideInInspector]
    public float startOffset = 0.015f;
    public float StartOffset
    {
        get { return this.startOffset; }
        set {
            if (value >= (this.endOffset - minimal_length))
                return;

            this.startOffset = value;
            this.toUpdateBeam = true;
            PrintDebugLog ("StartOffset() " + this.startOffset);
        }
    }

    [HideInInspector]
    public float endOffset = 0.8f;
    public float EndOffset
    {
        get { return this.endOffset; }
        set {
            if (value < endOffsetMin)
                isBeamLengthValid = false;
            else
            {
                this.endOffset = Mathf.Clamp (value, endOffsetMin, endOffsetMax);
                isBeamLengthValid = true;
                this.toUpdateBeam = true;
                PrintDebugLog ("EndOffset() " + this.endOffset);
            }
        }
    }

    public void SetEndOffset(float end_offset)
    {
        if (end_offset < endOffsetMin)
            isBeamLengthValid = false;
        else
        {
            this.endOffset = Mathf.Clamp (end_offset, endOffsetMin, endOffsetMax);
            isBeamLengthValid = true;
            this.toUpdateBeam = true;
            PrintDebugLog ("SetEndOffset() " + this.endOffset);
        }
    }
    [HideInInspector]
    public float endOffsetMin = 0.5f;    // Minimum distance of end offset (in meters).
    [HideInInspector]
    public float endOffsetMax = 9.5f;    // Maximum distance of end offset (in meters).
    public Material customMat;

    [HideInInspector]
    public Color32 startColor = new Color32 (255, 255, 255, 255);
    public Color32 StartColor
    {
        get { return this.startColor; }
        set {
            this.startColor = value;
            this.toUpdateBeam = true;
        }
    }
    private Color32 TailColor = new Color32 (255, 255, 255, 255);
    [HideInInspector]
    public Color32 endColor = new Color32 (255, 255, 255, 255);
    public Color32 EndColor
    {
        get { return this.endColor; }
        set {
            this.endColor = value;
            this.toUpdateBeam = true;
        }
    }

    private void ReadJsonValues()
    {
        string json_values = WaveVR_Utils.OEMConfig.getControllerConfig ();

        if (!json_values.Equals (""))
        {
            try
            {
                SimpleJSON.JSONNode jsNodes = SimpleJSON.JSONNode.Parse(json_values);

                string node_value = "";
                node_value = jsNodes["beam"]["start_width"].Value;
                if (!node_value.Equals("") && IsFloat(node_value) == true)
                    this.startWidth = float.Parse(node_value);

                node_value = jsNodes["beam"]["end_width"].Value;
                if (!node_value.Equals("") && IsFloat(node_value) == true)
                    this.endWidth = float.Parse(node_value);

                node_value = jsNodes["beam"]["start_offset"].Value;
                if (!node_value.Equals("") && IsFloat(node_value) == true)
                    this.startOffset = float.Parse(node_value);

                node_value = jsNodes["beam"]["length"].Value;
                if (!node_value.Equals("") && IsFloat(node_value) == true)
                    this.endOffset = float.Parse(node_value);

                node_value = jsNodes["beam"]["start_color"].Value;
                if (!node_value.Equals(""))
                    UpdateStartColor(node_value);

                node_value = jsNodes["beam"]["end_color"].Value;
                if (!node_value.Equals(""))
                    UpdateEndColor(node_value);

                PrintDebugLog("ReadJsonValues() "
                    + ", startWidth: " + this.startWidth
                    + ", endWidth: " + this.endWidth
                    + ", startOffset: " + this.startOffset
                    + ", endOffset: " + this.endOffset
                    + ", startColor: " + this.startColor.ToString()
                    + ", endColor: " + this.endColor.ToString());
            }
            catch (Exception e)
            {
                Log.e(LOG_TAG, "JsonParse failed: " + e.ToString());
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

    private bool isBeamLengthValid = true;  // create or remove mesh

    public int count = 3;
    public bool updateEveryFrame = true;
    public bool makeTail = true; // Offset from 0

    //private bool useTexture = true;
    private string textureName;

    private int maxUVAngle = 30;
    private const float epsilon = 0.001f;
    private bool isBeamEnable = false;
    private bool connected = false;

    private void Validate()
    {

        if (this.startWidth < epsilon)
            this.startWidth = epsilon;

        if (this.endWidth < epsilon)
            this.endWidth = epsilon;

        if (this.startOffset < epsilon)
            this.startOffset = epsilon;

        if (this.endOffset < epsilon * 2)
            this.endOffset = epsilon * 2;

        if (this.endOffset < this.startOffset)
            this.endOffset = this.startOffset + epsilon;

        if (count < 3)
            count = 3;

        /**
         * The texture pattern should be a radiated image starting 
         * from the texture center.
         * If the mesh's count is too low, the uv map can't keep a 
         * good radiation shap.  Therefore the maxUVAngle should be
         * reduced to avoid the uv area cutting the radiation circle.
        **/
        int uvAngle = 360 / count;
        if (uvAngle > 30)
            maxUVAngle = 30;
        else
            maxUVAngle = uvAngle;
    }

    private int Count = -1, verticesCount = -1, indicesCount = -1;

    public List<Vector3> vertices;
    public List<Vector2> uvs;
    public List<Vector3> normals;
    public List<int> indices;
    public List<Color32> colors;
    public Vector3 position;
    public bool ListenToDevice = false;
    public WaveVR_Controller.EDeviceType device;

    private Mesh emptyMesh;
    private Mesh updateMesh;
    private Material materialComp;
    private MeshFilter mf_beam;
    private MeshRenderer meshRenderer;
    private bool meshIsCreated = false;

    private bool toUpdateBeam = false;

    #region Monobehaviour overrides
    void Awake()
    {
        this.emptyMesh = new Mesh();
        this.updateMesh = new Mesh();
    }

    void Start()
    {
        if (useSystemConfig)
        {
            PrintDebugLog ("Start() use system config in WaveVR_Beam!");
            ReadJsonValues ();
        } else
        {
            PrintDebugLog ("Start() use custom config in WaveVR_Beam!");
        }
    }

    void OnEnable()
    {
        if (!isBeamEnable)
        {
            TailColor = this.startColor;

            Count = count + 1;
            verticesCount = Count * 2 + (makeTail ? 1 : 0);
            indicesCount = Count * 6 + (makeTail ? count * 3 : 0);

            //uvs = new List<Vector2>(verticesCount);
            //vertices = new List<Vector3> (verticesCount);
            //normals = new List<Vector3>(verticesCount);
            //indices = new List<int>(indicesCount);

            GameObject parentGo = transform.parent.gameObject;
            PrintDebugLog("Parent name: " + parentGo.name + ", localPos: " + parentGo.transform.localPosition.x + ", " + parentGo.transform.localPosition.y + ", " + parentGo.transform.localPosition.z);
            PrintDebugLog("Parent local EulerAngles " + parentGo.transform.localEulerAngles);

            this.mf_beam = GetComponent<MeshFilter>();
            this.meshRenderer = GetComponent<MeshRenderer> ();

            if (!useSystemConfig && customMat != null)
            {
                PrintDebugLog("OnEnable() Use custom config and material");
                this.meshRenderer.material = customMat;
            }
            else
            {
                PrintDebugLog("OnEnable() Use default material");
                var tmp = Resources.Load("CtrColorBeam3") as Material;
                if (tmp == null) PrintDebugLog("Can NOT load default material");
                this.meshRenderer.material = tmp;
            }

            // Not draw mesh in OnEnable(), thus set the meshRenderer to disable.
            this.meshRenderer.enabled = false;

            PrintDebugLog("OnEnable() show beam: " + this.meshRenderer.enabled + ", startWidth: " + this.startWidth
                + ", endWidth: " + this.endWidth + ", startOffset: " + this.startOffset + ", endOffset: " + this.endOffset
                + ", startColor: " + this.startColor.ToString() + ", endColor: " + this.endColor.ToString());

            this.isBeamEnable = true;
        }
        connected = false;
    }

    void OnDisable()
    {
        PrintDebugLog ("OnDisable()");
        showBeamMesh (false);
        isBeamEnable = false;
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus) // resume
        {
            PrintDebugLog("App resume, reset connection");
            connected = false;
        }
    }

    public void Update()
    {
        // Redraw mesh if updated.
        if (this.updateEveryFrame || this.toUpdateBeam)
        {
            createBeamMesh ();

            if (this.toUpdateBeam)
                this.toUpdateBeam = !this.toUpdateBeam;
        }

        bool _show = (
            this.ShowBeam
            && this.isBeamLengthValid
        );

        if (this.ListenToDevice)
        {
            bool _connected = checkConnection ();
            if (this.connected != _connected)
                this.connected = _connected;

            showBeamMesh (_show && this.connected);
        } else
        {
            showBeamMesh (_show);
        }

        Log.gpl.d (LOG_TAG, gameObject.name + " is " + (this.ShowBeam ? "shown" : "hidden")
            + ", start offset: " + this.startOffset
            + ", end offset: " + this.endOffset
            + ", start width: " + this.startWidth
            + ", end width: " + this.endWidth
            + ", start color: " + this.startColor
            + ", end color: " + this.endColor, true);
    }
    #endregion

    public void SetEndOffset (Vector3 target, bool interactive)
    {
        Vector3 targetLocalPosition = transform.InverseTransformPoint (target);

        if (targetLocalPosition.z < endOffsetMin)
            isBeamLengthValid = false;
        else
        {
            this.endOffset = targetLocalPosition.z - endOffsetMin;
            this.endOffset = Mathf.Clamp (this.endOffset, endOffsetMin, endOffsetMax);
            isBeamLengthValid = true;
            this.toUpdateBeam = true;
        }

        PrintIntervalLog (() => "SetEndOffset() targetLocalPosition.z: " + targetLocalPosition.z
            + ", endOffset: " + this.endOffset
            + ", endOffsetMin: " + endOffsetMin
            + ", endOffsetMax: " + endOffsetMax);
    }

    public void ResetEndOffset()
    {
        this.endOffset = endOffsetMax;
        isBeamLengthValid = true;
        this.toUpdateBeam = true;

        PrintDebugLog ("ResetEndOffset() "
        + ", endOffset: " + this.endOffset
        + ", endOffsetMin: " + endOffsetMin
        + ", endOffsetMax: " + endOffsetMax);
    }

    private Matrix4x4 mat44_rot = Matrix4x4.zero;
    private Matrix4x4 mat44_uv = Matrix4x4.zero;
    private Vector3 vec3_vertices_start = Vector3.zero;
    private Vector3 vec3_vertices_end = Vector3.zero;

    private readonly Vector2 vec2_05_05 = new Vector2 (0.5f, 0.5f);
    private readonly Vector3 vec3_0_05_0 = new Vector3 (0, 0.5f, 0);
    private void createMesh()
    {
        updateMesh.Clear ();
        uvs.Clear ();
        vertices.Clear ();
        normals.Clear ();
        indices.Clear ();
        colors.Clear ();

        mat44_rot = Matrix4x4.zero;
        mat44_uv = Matrix4x4.zero;

        for (int i = 0; i < Count; i++)
        {
            int angle = (int) (i * 360.0f / count);
            int UVangle = (int)(i * maxUVAngle / count);
            // make rotation matrix
            mat44_rot.SetTRS(Vector3.zero, Quaternion.AngleAxis(angle, Vector3.forward), Vector3.one);
            mat44_uv.SetTRS(Vector3.zero, Quaternion.AngleAxis(UVangle, Vector3.forward), Vector3.one);

            // start
            vec3_vertices_start.y = this.startWidth;
            vec3_vertices_start.z = this.startOffset;
            vertices.Add (mat44_rot.MultiplyVector (vec3_vertices_start));
            uvs.Add (vec2_05_05);
            colors.Add (this.startColor);
            normals.Add (mat44_rot.MultiplyVector (Vector3.up).normalized);

            // end
            vec3_vertices_end.y = this.endWidth;
            vec3_vertices_end.z = this.endOffset;
            vertices.Add (mat44_rot.MultiplyVector (vec3_vertices_end));
            Vector2 uv = mat44_uv.MultiplyVector (vec3_0_05_0);
            uv.x = uv.x + 0.5f;
            uv.y = uv.y + 0.5f;
            uvs.Add(uv);
            colors.Add (this.endColor);
            normals.Add(mat44_rot.MultiplyVector(Vector3.up).normalized);
        }

        for (int i = 0; i < count; i++)
        {
            // bd
            // ac
            int a, b, c, d;
            a = i * 2;
            b = i * 2 + 1;
            c = i * 2 + 2;
            d = i * 2 + 3;

            // first
            indices.Add(a);
            indices.Add(d);
            indices.Add(b);

            // second
            indices.Add(a);
            indices.Add(c);
            indices.Add(d);
        }

        // Make Tail
        if (makeTail)
        {
            vertices.Add (Vector3.zero);
            colors.Add (TailColor);
            uvs.Add (vec2_05_05);
            normals.Add (Vector3.zero);
            int tailIdx = count * 2;
            for (int i = 0; i < count; i++)
            {
                int idx = i * 2;

                indices.Add(tailIdx);
                indices.Add(idx + 2);
                indices.Add(idx);
            }
        }
        updateMesh.vertices = vertices.ToArray();
        //updateMesh.SetUVs(0, uvs);
        //updateMesh.SetUVs(1, uvs);
        updateMesh.colors32  = colors.ToArray ();
        updateMesh.normals = normals.ToArray();
        updateMesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
        updateMesh.name = "Beam";
    }

    private void createBeamMesh()
    {
        Validate ();
        createMesh ();  // generate this.updateMesh
        this.meshIsCreated = true;
    }

    private void showBeamMesh(bool show)
    {
        if (!this.meshIsCreated && show == true)
            createBeamMesh ();

        if (this.meshRenderer.enabled != show)
        {
            PrintDebugLog ("showBeamMesh() " + (show ? "show" : "hide") + " beam.");
            this.mf_beam.mesh = show ? this.updateMesh : this.emptyMesh;
            this.meshRenderer.enabled = show;
        }
    }

    private Color32 StringToColor32(string color_string)
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
            Log.e(LOG_TAG, "StringToColor32 failed: " + e.ToString());
            return new Color32(255, 255, 255, 77);
        }
    }

    private void UpdateStartColor(string color_string)
    {

        try
        {
            byte[] _color_r = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(1, 2), 16));
            byte[] _color_g = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(3, 2), 16));
            byte[] _color_b = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(5, 2), 16));
            byte[] _color_a = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(7, 2), 16));

            this.startColor.r = _color_r[0];
            this.startColor.g = _color_g[0];
            this.startColor.b = _color_b[0];
            this.startColor.a = _color_a[0];
        }
        catch (Exception e)
        {
            Log.e(LOG_TAG, "UpdateStartColor failed: " + e.ToString());
            this.startColor = new Color32(255, 255, 255, 255);
        }
    }

    private void UpdateEndColor(string color_string)
    {
        try
        {
            byte[] _color_r = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(1, 2), 16));
            byte[] _color_g = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(3, 2), 16));
            byte[] _color_b = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(5, 2), 16));
            byte[] _color_a = BitConverter.GetBytes(Convert.ToInt32(color_string.Substring(7, 2), 16));

            this.endColor.r = _color_r[0];
            this.endColor.g = _color_g[0];
            this.endColor.b = _color_b[0];
            this.endColor.a = _color_a[0];
        }
        catch (Exception e)
        {
            string defaultEndColor = "#FFFFFF4D";

            Log.e(LOG_TAG, "UpdateEndColor failed: " + e.ToString());
            this.endColor = StringToColor32(defaultEndColor);
        }
    }

    private bool checkConnection()
    {
        bool _connected = false;
        WVR_DeviceType _type = WVR_DeviceType.WVR_DeviceType_Invalid;

#if UNITY_EDITOR
        if (Application.isEditor)
        {
            _connected = WaveVR_Controller.Input(this.device).connected;
            _type = WaveVR_Controller.Input(this.device).DeviceType;
        }
        else
#endif
        {
            WaveVR.Device _device = WaveVR.Instance.getDeviceByType(this.device);
            _connected = _device.connected;
            _type = _device.type;
        }

        return _connected;
    }
}
