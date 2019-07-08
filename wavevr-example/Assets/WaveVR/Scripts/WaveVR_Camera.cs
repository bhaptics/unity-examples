// "WaveVR SDK 
// © 2017 HTC Corporation. All Rights Reserved.
//
// Unless otherwise required by copyright law and practice,
// upon the execution of HTC SDK license agreement,
// HTC grants you access to and use of the WaveVR SDK(s).
// You shall fully comply with all of HTC’s SDK license agreement terms and
// conditions signed by you and all SDK and API requirements,
// specifications, and documentation provided by HTC to You."

using System.Collections;
using System.Runtime.InteropServices;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using wvr;
using WaveVR_Log;

/**
 * To work with this camera's event, you could register your delegate to the Camera.OnXXX.
 * For example,
 * 
 *  public class YourScript : MonoBehaviour
 *  {
 *      public WVR_Eye eye;
 *      public void MyPreRender(Camera cam)
 *      {
 *          var wvrCam = cam.GetComponent<WaveVR_Camera>();
 *          if (wvrCam == null)
 *              return;
 *          if (wvrCam.eye == eye)
 *          {
 *              // Do your actions here.
 *          }
 *          // or you can...
 *          if (wvrCam == WaveVR_Render.Instance.lefteye) {
 *              // Do your actions here.
 *          }
 *      }
 *      
 *      void OnEnable() {
 *          Camera.onPreRender += MyPreRender;
 *      }
 *      
 *      void OnDisable() {
 *          Camera.onPreRender -= MyPreRender;
 *      }
 *  }
 *  See also: https://docs.unity3d.com/ScriptReference/Camera-onPreRender.html
**/
[RequireComponent(typeof(Camera))]
public class WaveVR_Camera : MonoBehaviour, IEquatable<Camera>
{
    private static string TAG = "WVR_Camera";
    public WVR_Eye eye = WVR_Eye.WVR_Eye_None;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    public Camera GetCamera()
    {
        if (cam != null)
            return cam;
        cam = GetComponent<Camera>();
        return cam;
    }

    [System.Obsolete("Use GetCamera() inestad.")]
    public Camera getCamera()
    {
        if (cam != null)
            return cam;
        cam = GetComponent<Camera>();
        return cam;
    }

    public bool Equals(Camera other)
    {
        return cam == other;
    }

    void OnPreRender()
    {
#if WVR_COMPATIBLE_WITH_2_0
#pragma warning disable CS0618 // Type or member is obsolete
        if (eye == WVR_Eye.WVR_Eye_Left)
        {
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.PRE_RENDER_LEFT);
        }
        else if (eye == WVR_Eye.WVR_Eye_Right)
        {
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.PRE_RENDER_RIGHT);
        }
#pragma warning restore CS0618 // Type or member is obsolete
#endif
        if (eye == WVR_Eye.WVR_Eye_Both)
        {
            SinglePassPreRender();
        }
    }

    void OnPostRender()
    {
        if (eye == WVR_Eye.WVR_Eye_Both)
        {
            SinglePassPostRender();
        }
    }

    // TODO please remove this at SDK 3.0
#if WVR_COMPATIBLE_WITH_2_0
    void OnRenderObject()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        // This is global event but still need to know which eye is working.
        if (Camera.current == cam && eye == WVR_Eye.WVR_Eye_Left)
        {
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.RENDER_OBJECT_LEFT);
        }
        else if (Camera.current == cam && eye == WVR_Eye.WVR_Eye_Right)
        {
            WaveVR_Utils.Event.Send(WaveVR_Utils.Event.RENDER_OBJECT_RIGHT);
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }
#endif

#region single_pass

    //Shader Variables used for single-pass stereo rendering
    private Matrix4x4[] unity_StereoMatrixP = new Matrix4x4[2];
    private Matrix4x4[] unity_StereoMatrixInvP = new Matrix4x4[2];
    private Matrix4x4[] unity_StereoWorldToCamera = new Matrix4x4[2];
    private Matrix4x4[] unity_StereoCameraToWorld = new Matrix4x4[2];
    private Matrix4x4[] unity_StereoMatrixVP = new Matrix4x4[2];

    private Vector3[] eyesOffset = new Vector3[2];
    private Matrix4x4[] eyesOffsetMatrix = new Matrix4x4[2];

    public void SetEyesPosition(Vector3 left, Vector3 right)
    {
        eyesOffset[0] = left;
        eyesOffset[1] = right;
        eyesOffsetMatrix[0] = Matrix4x4.TRS(left, Quaternion.identity, Vector3.one);
        eyesOffsetMatrix[1] = Matrix4x4.TRS(right, Quaternion.identity, Vector3.one);
    }

    public void SetStereoProjectionMatrix(Matrix4x4 left, Matrix4x4 right)
    {
        unity_StereoMatrixP[0] = left;
        unity_StereoMatrixInvP[0] = left.inverse;

        unity_StereoMatrixP[1] = right;
        unity_StereoMatrixInvP[1] = right.inverse;

#if UNITY_2017_1_OR_NEWER
        // TODO should we find out how to set a correct 
        GetCamera();
        //cam.SetStereoViewMatrix(Camera.StereoscopicEye.Left, left);
        //cam.SetStereoViewMatrix(Camera.StereoscopicEye.Right, right);
        if (!cam.areVRStereoViewMatricesWithinSingleCullTolerance)
        {
            debugLogMatrix(left, "left proj");
            debugLogMatrix(right, "right proj");

            //Log.e(TAG, "The Camera.areVRStereoViewMatricesWithinSingleCullTolerance are false.  SinglePass may not enabled.", true);
        }
#endif
    }

    void debugLogMatrix(Matrix4x4 m, string name)
    {
        Log.d(TAG, name + ":");
        Log.d(TAG, "/ "  + m.m00 + " " + m.m01 + " " + m.m02 + " " + m.m03 + " \\");
        Log.d(TAG, "| "  + m.m10 + " " + m.m11 + " " + m.m12 + " " + m.m13 + " |");
        Log.d(TAG, "| "  + m.m20 + " " + m.m21 + " " + m.m22 + " " + m.m23 + " |");
        Log.d(TAG, "\\ " + m.m30 + " " + m.m31 + " " + m.m32 + " " + m.m33 + " /");
    }

    CommandBuffer cmdBufBeforeForwardOpaque, cmdBufBeforeSkybox, cmdBufAfterSkybox;

    void PrepareCommandBuffers()
    {
        // Make sure all command buffer can run in editor mode
        if (cmdBufBeforeForwardOpaque == null)
        {
            cmdBufBeforeForwardOpaque = new CommandBuffer();
#if UNITY_EDITOR
            if (Application.isEditor)
                WaveVR_Utils.SendRenderEvent(cmdBufBeforeForwardOpaque, WaveVR_Utils.RENDEREVENTID_EditorEmptyOperation);
            else
#endif
                WaveVR_Utils.SendRenderEvent(cmdBufBeforeForwardOpaque, WaveVR_Utils.RENDEREVENTID_SinglePassBeforeForwardOpaque);
            cmdBufBeforeForwardOpaque.ClearRenderTarget(true, true, cam.backgroundColor);
            cmdBufBeforeForwardOpaque.name = "SinglePassPrepare";
        }
        cam.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, cmdBufBeforeForwardOpaque);
        cam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, cmdBufBeforeForwardOpaque);

        //The workaround for Skybox rendering
        //Since Unity5, skybox rendering after forward opaque
        //As skybox need a particular MatrixVP, two CommandBuffer used to handle this.
        //The MatrixVP must be changed back after skybox rendering.
        if (cmdBufAfterSkybox == null)
            cmdBufAfterSkybox = new CommandBuffer();

        cam.RemoveCommandBuffer(CameraEvent.AfterSkybox, cmdBufAfterSkybox);

        cmdBufAfterSkybox.Clear();
        cmdBufAfterSkybox.SetGlobalMatrixArray("unity_StereoMatrixVP", unity_StereoMatrixVP);
        cmdBufAfterSkybox.name = "SinglePassAfterSkyBox";
        cam.AddCommandBuffer(CameraEvent.AfterSkybox, cmdBufAfterSkybox);

        //Skybox View Matrix should be at world zero point.
        //As in OpenGL, camera's forward is the negative Z axis
        if (cmdBufBeforeSkybox == null)
            cmdBufBeforeSkybox = new CommandBuffer();

        Matrix4x4 viewMatrix1 = Matrix4x4.LookAt(Vector3.zero, cam.transform.forward, cam.transform.up) * Matrix4x4.Scale(new Vector3(1, 1, -1));
        //Change it from column major to row major.
        viewMatrix1 = viewMatrix1.transpose;
        Matrix4x4 proj0 = unity_StereoMatrixP[0];
        Matrix4x4 proj1 = unity_StereoMatrixP[1];
        //Trick here. I supporse skybox doesn't need clip in Projection Matrix
        //And m22 and m23 is calculated by clip near/far, -1 is the default value of m22.
        proj0.m22 = -1.0f;
        proj1.m22 = -1.0f;
        Matrix4x4[] skybox_MatrixVP = new Matrix4x4[2];
        skybox_MatrixVP[0] = proj0 * viewMatrix1;
        skybox_MatrixVP[1] = proj1 * viewMatrix1;

        cam.RemoveCommandBuffer(CameraEvent.BeforeSkybox, cmdBufBeforeSkybox);

        //The MatrixVP should be set before skybox rendering.
        cmdBufBeforeSkybox.Clear();
        cmdBufBeforeSkybox.SetGlobalMatrixArray("unity_StereoMatrixVP", skybox_MatrixVP);
        cmdBufBeforeSkybox.name = "SinglePassAfterSkybox";

        cam.AddCommandBuffer(CameraEvent.BeforeSkybox, cmdBufBeforeSkybox);
    }

    void SinglePassPreRender()
    {
        //Unity will not handle these Stereo shader variables for us, so we have to set it all by ourselves

        Shader.SetGlobalMatrixArray("unity_StereoCameraProjection", unity_StereoMatrixP);
        Shader.SetGlobalMatrixArray("unity_StereoCameraInvProjection", unity_StereoMatrixInvP);
        Shader.SetGlobalMatrixArray("unity_StereoMatrixP", unity_StereoMatrixP);

        //Since eyes are moving, so below variables need to re-calculate every frame.
        Matrix4x4 world2Camera = cam.worldToCameraMatrix;
        Matrix4x4 camera2World = cam.cameraToWorldMatrix;

        unity_StereoCameraToWorld[0] = camera2World * eyesOffsetMatrix[0];
        unity_StereoCameraToWorld[1] = camera2World * eyesOffsetMatrix[1];
        Shader.SetGlobalMatrixArray("unity_StereoCameraToWorld", unity_StereoCameraToWorld);

        unity_StereoWorldToCamera[0] = eyesOffsetMatrix[0].inverse * world2Camera;
        unity_StereoWorldToCamera[1] = eyesOffsetMatrix[1].inverse * world2Camera;
        Shader.SetGlobalMatrixArray("unity_StereoWorldToCamera", unity_StereoWorldToCamera);

        //So the camera positons
        Vector4[] stereoWorldSpaceCameraPos = {
                cam.transform.position + eyesOffset[0],
                cam.transform.position + eyesOffset[1]
            };
        Shader.SetGlobalVectorArray("unity_StereoWorldSpaceCameraPos", stereoWorldSpaceCameraPos);

        //camera.worldToCameraMatrix is the view matrix
        Shader.SetGlobalMatrixArray("unity_StereoMatrixV", unity_StereoWorldToCamera);
        Shader.SetGlobalMatrixArray("unity_StereoMatrixInvV", unity_StereoCameraToWorld);

        //MatrixVP is the value UNITY_MATRIX_VP used in shader
        unity_StereoMatrixVP[0] = unity_StereoMatrixP[0] * unity_StereoWorldToCamera[0];
        unity_StereoMatrixVP[1] = unity_StereoMatrixP[1] * unity_StereoWorldToCamera[1];
        Shader.SetGlobalMatrixArray("unity_StereoMatrixVP", unity_StereoMatrixVP);

        PrepareCommandBuffers();
    }

    void SinglePassPostRender()
    {
    }
#endregion
}
