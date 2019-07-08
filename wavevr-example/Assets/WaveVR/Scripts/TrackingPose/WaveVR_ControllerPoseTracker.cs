﻿// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using wvr;
//using UnityEngine.VR;
using System.Collections;
using WaveVR_Log;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(WaveVR_ControllerPoseTracker))]
public class WaveVR_ControllerPoseTrackerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WaveVR_ControllerPoseTracker myScript = target as WaveVR_ControllerPoseTracker;

        myScript.Type = (WaveVR_Controller.EDeviceType)EditorGUILayout.EnumPopup ("Type", myScript.Type);
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
        myScript.TrackTiming = (WVR_TrackTiming)EditorGUILayout.EnumPopup ("Track Timing", myScript.TrackTiming);

        if (GUI.changed)
            EditorUtility.SetDirty ((WaveVR_ControllerPoseTracker)target);
    }
}
#endif

public class WaveVR_ControllerPoseTracker : MonoBehaviour
{
    private static string LOG_TAG = "WaveVR_ControllerPoseTracker";
    private void PrintDebugLog(string msg)
    {
        #if UNITY_EDITOR
        Debug.Log(LOG_TAG + " : " + this.Type + ", " + msg);
        #endif
        Log.d (LOG_TAG, this.Type + ", " + msg);
    }
    private void PrintErrorLog(string msg)
    {
        #if UNITY_EDITOR
        Debug.Log(LOG_TAG + " : " + this.Type + ", " + msg);
        #endif
        Log.e (LOG_TAG, this.Type + ", " + msg);
    }

    #region Developer variables
    public WaveVR_Controller.EDeviceType Type;
    public bool InversePosition = false;
    public bool TrackPosition = true;
    public WVR_SimulationOption SimulationOption = WVR_SimulationOption.WhenNoPosition;
    private WVR_SimulationOption currSimulationOption = WVR_SimulationOption.WhenNoPosition;
    private bool bSetupSimulation = false;
    public bool FollowHead = false;
    private bool currFollowHead = false;
    private bool bSetupFollowHead = false;
    public bool InverseRotation = false;
    public bool TrackRotation = true;
    public WVR_TrackTiming TrackTiming = WVR_TrackTiming.WhenNewPoses;
    [HideInInspector]
    public bool ApplyNeckOffset = false;

    /// Height of the elbow  (m).
    [Range(0.0f, 0.2f)]
    public float ElbowRaiseYaxis = 0.0f;

    /// Depth of the elbow  (m).
    [Range(0.0f, 0.4f)]
    public float ElbowRaiseZaxis = 0.0f;
    #endregion

    private GameObject Head = null;

    /// The Downward tilt or pitch of the laser pointer relative to the controller (degrees).
    [HideInInspector]
    [Range(0.0f, 30.0f)]
    public float pointerTiltAngle = 15.0f;

    /// Quaternion to represent the pointer's rotation relative to
    /// the controller.
    public Quaternion PointerRotationFromController
    {
        get
        {
            return Quaternion.AngleAxis(pointerTiltAngle, Vector3.right);
        }
    }

    /// Offset of the laser pointer origin relative to the wrist (meters)
    //private static readonly Vector3 POINTER_OFFSET = new Vector3(0.0f, -0.009f, 0.099f);


    /// Vector to present the arm model position of controller.
    /// NOTE: This is in meatspace coordinates.
    private Vector3 controllerSimulatedPosition;

    /// Quaternion to present the arm model rotation of controller.
    /// NOTE: This is in meatspace coordinates.
    private Quaternion controllerSimulatedRotation;

    /// controller lerp speed for smooth movement between with head position case and without head position case
    private float smoothMoveSpeed = 0.3f;

    /// X axis of arm is reverse in left / right arm.
    private Vector3 v3ChangeArmXAxis = new Vector3(0, 1, 1);
    [HideInInspector]
    private bool SetCustomHand = false;

    /// Head's real rotation, not local rotation.
    private Quaternion headRotation;

    /// Vector to present the body's rotation.
    private Vector3 bodyDirection;

    /// Quaternion to present the shoulder's rotation.
    /// NOTE: This is in meatspace coordinates.
    private Quaternion bodyRotation;

    /// Simulated direction of wrist.
    private Quaternion wristOrientation;

    /// Offset (position) related to view center. (meters)
    private Vector3 HEADTOELBOW_OFFSET = new Vector3(0.2f, -0.7f, 0f);
    private Vector3 NECK_OFFSET = new Vector3(0, -0.15f, -0.08f);
    // if ApplyNeckOffset, z should be 0.08 -> 0.16 (+ NECK_OFFSET.z)
    private Vector3 ELBOW_PITCH_OFFSET = new Vector3(-0.2f, 0.55f, 0.08f);
    private float ELBOW_PITCH_ANGLE_MIN = 0, ELBOW_PITCH_ANGLE_MAX = 60;
    /// The elbow curve in XY-plane is not smooth.
    /// The lerp value increases much rapider when elbow raises up.
    private const float ELBOW_TO_XYPLANE_LERP_MIN = 0.45f;
    private const float ELBOW_TO_XYPLANE_LERP_MAX = 0.65f;
    private Vector3 ELBOWTOWRIST_OFFSET = new Vector3(0.0f, 0.0f, 0.15f);
    private Vector3 WRISTTOCONTROLLER_OFFSET = new Vector3(0.0f, 0.0f, 0.05f);

    private void ReadJsonValues()
    {
        string json_values = WaveVR_Utils.OEMConfig.getControllerConfig ();
        if (!json_values.Equals (""))
        {
            SimpleJSON.JSONNode jsNodes = SimpleJSON.JSONNode.Parse (json_values);
            string node_value = "";

            char[] split_symbols = { '[', ',', ']' };

            node_value = jsNodes ["arm"] ["head_to_elbow_offset"].Value;
            if (!node_value.Equals (""))
            {
                string[] split_value = node_value.Split (split_symbols);
                HEADTOELBOW_OFFSET = new Vector3 (float.Parse (split_value [1]), float.Parse (split_value [2]), float.Parse (split_value [3]));
            }

            node_value = jsNodes ["arm"] ["elbow_to_wrist_offset"].Value;
            if (!node_value.Equals (""))
            {
                string[] split_value = node_value.Split (split_symbols);
                ELBOWTOWRIST_OFFSET = new Vector3 (float.Parse (split_value [1]), float.Parse (split_value [2]), float.Parse (split_value [3]));
            }

            node_value = jsNodes ["arm"] ["wrist_to_controller_offset"].Value;
            if (!node_value.Equals (""))
            {
                string[] split_value = node_value.Split (split_symbols);
                WRISTTOCONTROLLER_OFFSET = new Vector3 (float.Parse (split_value [1]), float.Parse (split_value [2]), float.Parse (split_value [3]));
            }

            node_value = jsNodes ["arm"] ["elbow_pitch_angle_offset"].Value;
            if (!node_value.Equals (""))
            {
                string[] split_value = node_value.Split (split_symbols);
                ELBOW_PITCH_OFFSET = new Vector3 (float.Parse (split_value [1]), float.Parse (split_value [2]), float.Parse (split_value [3]));
            }

            node_value = jsNodes ["arm"] ["elbow_pitch_angle_min"].Value;
            if (!node_value.Equals (""))
            {
                ELBOW_PITCH_ANGLE_MIN = float.Parse (node_value);
            }

            node_value = jsNodes ["arm"] ["elbow_pitch_angle_max"].Value;
            if (!node_value.Equals (""))
            {
                ELBOW_PITCH_ANGLE_MAX = float.Parse (node_value);
            }

            PrintDebugLog ( 
                "HEADTOELBOW_OFFSET: {"         + HEADTOELBOW_OFFSET.x + ", "       + HEADTOELBOW_OFFSET.y + ", "       + HEADTOELBOW_OFFSET.z +
                "\nELBOWTOWRIST_OFFSET: "       + ELBOWTOWRIST_OFFSET.x + ", "      + ELBOWTOWRIST_OFFSET.y + ", "      + ELBOWTOWRIST_OFFSET.z +
                "\nWRISTTOCONTROLLER_OFFSET: "  + WRISTTOCONTROLLER_OFFSET.x + ", " + WRISTTOCONTROLLER_OFFSET.y + ", " + WRISTTOCONTROLLER_OFFSET.z +
                "\nELBOW_PITCH_OFFSET: "        + ELBOW_PITCH_OFFSET.x + ", "       + ELBOW_PITCH_OFFSET.y + ", "       + ELBOW_PITCH_OFFSET.z +
                "\nELBOW_PITCH_ANGLE_MIN: " + ELBOW_PITCH_ANGLE_MIN + "\nELBOW_PITCH_ANGLE_MAX: " + ELBOW_PITCH_ANGLE_MAX);
        }
    }

    #region Monobehaviour
    private bool cptEnabled = false;
    void OnEnable()
    {
        if (!cptEnabled)
        {
            if (Head == null)
            {
                if (WaveVR_Render.Instance != null)
                    Head = WaveVR_Render.Instance.gameObject;
            }
            if (Head != null)
                defaultHeadPosition = Head.transform.localPosition;

            ReadJsonValues ();

            if (this.TrackTiming == WVR_TrackTiming.WhenNewPoses)
                WaveVR_Utils.Event.Listen (WaveVR_Utils.Event.NEW_POSES, OnNewPoses);
            WaveVR_Utils.Event.Listen (wvr.WVR_EventType.WVR_EventType_RecenterSuccess3DoF.ToString (), OnRecentered);
            WaveVR_Utils.Event.Listen (WaveVR_Utils.Event.ALL_VREVENT, OnEvent);

            SetupPoseSimulation();

            cptEnabled = true;

            PrintDebugLog ("OnEnable() TrackPosition: " + this.TrackPosition
                + ", SimulationOption: " + this.SimulationOption
                + ", FollowHead: " + this.FollowHead
                + ", TrackRotation: " + this.TrackRotation
                + ", TrackTiming: " + this.TrackTiming);
        }
    }

    void OnDisable()
    {
        PrintDebugLog ("OnDisable()" +
            "\nLocal Pos: " + transform.localPosition.x + ", "  + transform.localPosition.y + ", "  + transform.localPosition.z +
            "\nPos: "       + transform.position.x + ", "       + transform.position.y + ", "       + transform.position.z +
            "\nLocal rot: " + transform.localRotation.x + ", "  + transform.localRotation.y + ", "  + transform.localRotation.z);

        if (this.TrackTiming == WVR_TrackTiming.WhenNewPoses)
            WaveVR_Utils.Event.Remove(WaveVR_Utils.Event.NEW_POSES, OnNewPoses);
        WaveVR_Utils.Event.Remove (wvr.WVR_EventType.WVR_EventType_RecenterSuccess3DoF.ToString (), OnRecentered);
        WaveVR_Utils.Event.Remove (WaveVR_Utils.Event.ALL_VREVENT, OnEvent);
        this.bSetupSimulation = false;
        this.bSetupFollowHead = false;
        cptEnabled = false;
    }

    private float mFPS = 60.0f;
    void Update ()
    {
        mFPS = 1.0f / Time.deltaTime;

        // FollowHead changes in runtime.
        if (this.FollowHead != this.currFollowHead)
            this.bSetupFollowHead = false;
        // SimulationOption changes in runtime.
        if (this.SimulationOption != this.currSimulationOption)
            this.bSetupSimulation = false;
        SetupPoseSimulation ();

        if (TrackTiming == WVR_TrackTiming.WhenNewPoses)
            return;
        if (WaveVR.Instance == null)
            return;

        WaveVR.Device device = WaveVR.Instance.getDeviceByType (this.Type);
        if (device.connected)
        {
            updatePose (device.pose, device.rigidTransform);
        }
    }
    #endregion

    void OnEvent(params object[] args)
    {
        WVR_Event_t _event = (WVR_Event_t)args[0];
        PrintDebugLog ("OnEvent() " + _event.common.type);
        switch (_event.common.type)
        {
        case WVR_EventType.WVR_EventType_ButtonPressed:
            // Get system key
            if (_event.input.inputId == WVR_InputId.WVR_InputId_Alias1_System)
            {
                PrintDebugLog ("OnEvent() WVR_InputId_Alias1_System is pressed.");
            }
            break;
        case WVR_EventType.WVR_EventType_RecenterSuccess:
        case WVR_EventType.WVR_EventType_RecenterSuccess3DoF:
            PrintDebugLog ("OnEvent() recentered.");
            break;
        }
    }

    private void SetupPoseSimulation()
    {
        #if UNITY_EDITOR
        if (Application.isEditor)
        {
        } else
        #endif
        {
            if (WaveVR.Instance != null)
            {
                if (!this.bSetupSimulation)
                {
                    this.currSimulationOption = this.SimulationOption;
                    if (this.currSimulationOption == WVR_SimulationOption.WhenNoPosition)
                        Interop.WVR_SetArmModel (WVR_SimulationType.WVR_SimulationType_Auto);
                    else if (this.currSimulationOption == WVR_SimulationOption.ForceSimulation)
                        Interop.WVR_SetArmModel (WVR_SimulationType.WVR_SimulationType_ForceOn);
                    else
                        Interop.WVR_SetArmModel (WVR_SimulationType.WVR_SimulationType_ForceOff);

                    this.bSetupSimulation = true;
                    PrintDebugLog ("SetupPoseSimulation() simulation option: " + this.currSimulationOption);
                }
                if (!this.bSetupFollowHead)
                {
                    this.currFollowHead = this.FollowHead;
                    Interop.WVR_SetArmSticky (this.currFollowHead);
                    this.bSetupFollowHead = true;
                    PrintDebugLog ("SetupPoseSimulation() follow head: " + this.currFollowHead);
                }
            }
        }
    }

    private void OnRecentered(params object[] args)
    {
        // do something when recentered.
    }

    #if UNITY_EDITOR
    private WVR_DevicePosePair_t wvr_pose = new WVR_DevicePosePair_t ();
    private WaveVR_Utils.RigidTransform rigid_pose = WaveVR_Utils.RigidTransform.identity;
    #endif

    #region Pose Update
    private void OnNewPoses(params object[] args)
    {
        #if UNITY_EDITOR
        if (Application.isEditor && !WaveVR.Instance.isSimulatorOn)
        {
            WVR_DeviceType _type = WaveVR_Controller.Input(this.Type).DeviceType;
            var system = WaveVR_PoseSimulator.Instance;
            system.GetTransform (_type, ref wvr_pose, ref rigid_pose);
            updatePose (wvr_pose, rigid_pose);
        } else
        #endif
        {
            WaveVR.Device _device = WaveVR.Instance.getDeviceByType (this.Type);
            if (_device.connected)
            {
                updatePose (_device.pose, _device.rigidTransform);
            }
        }
    }

    private void updatePose(WVR_DevicePosePair_t pose, WaveVR_Utils.RigidTransform rtPose)
    {
        switch (this.Type)
        {
        case WaveVR_Controller.EDeviceType.Dominant:
            v3ChangeArmXAxis.x = WaveVR_Controller.IsLeftHanded ? -1.0f : 1.0f;
            break;
        case WaveVR_Controller.EDeviceType.NonDominant:
            v3ChangeArmXAxis.x = WaveVR_Controller.IsLeftHanded ? 1.0f : -1.0f;
            break;
        default:
            break;
        }

        if (SetCustomHand)
        {
            v3ChangeArmXAxis.x = (v3ChangeArmXAxis.x == 1.0f) ? -1.0f : 1.0f;
        }

        #if UNITY_EDITOR
        if (Application.isEditor)
        {
            switch (SimulationOption)
            {
            case WVR_SimulationOption.NoSimulation:
                updateDevicePose (pose, rtPose);
                break;
            case WVR_SimulationOption.ForceSimulation:
                updateControllerPose (pose, rtPose);
                break;
            case WVR_SimulationOption.WhenNoPosition:
                if (pose.pose.Is6DoFPose == false)
                    updateControllerPose (pose, rtPose);
                else
                    updateDevicePose (pose, rtPose);
                break;
            default:
                break;
            }
        } else
        #endif
        {
            updateDevicePose (pose, rtPose);
        }
    }

    private void updateDevicePose(WVR_DevicePosePair_t pose, WaveVR_Utils.RigidTransform rtPose)
    {
        if (TrackPosition)
        {
            if (InversePosition)
                transform.localPosition = -rtPose.pos;
            else
                transform.localPosition = rtPose.pos;

            if (SetCustomHand && pose.pose.Is6DoFPose == false)
                transform.localPosition = Vector3.Scale (transform.localPosition, v3ChangeArmXAxis);
        }
        if (TrackRotation)
        {
            if (InverseRotation)
                transform.localRotation = Quaternion.Inverse(rtPose.rot);
            else
                transform.localRotation = rtPose.rot;
        }
    }

//    private uint posePulseCount = 0;
    private void updateControllerPose(WVR_DevicePosePair_t pose, WaveVR_Utils.RigidTransform rtPose)
    {
        bodyRotation = Quaternion.identity;

        UpdateHeadAndBodyPose (pose, rtPose);

        ComputeControllerPose (pose, rtPose);
/*
        if (controllerSimulatedPosition.y - transform.localPosition.y > 0.8f || transform.localPosition.y - controllerSimulatedPosition.y > 0.8f)
        {
            posePulseCount++;
            if (posePulseCount <= 5)
            {
                Log.w (LOG_TAG, "Skip rendering wrong pose!!");
                return;
            } else
            {
                posePulseCount = 0;
            }
        }
*/
        if (TrackPosition)
        {
            if (WaveVR_Render.Instance.centerWVRCamera != null)
                controllerSimulatedPosition += WaveVR_Render.Instance.centerWVRCamera.transform.localPosition;
            transform.localPosition = Vector3.Lerp (transform.localPosition, controllerSimulatedPosition, smoothMoveSpeed);
        }
        if (TrackRotation)
            transform.localRotation = controllerSimulatedRotation;

        Log.gpl.d (LOG_TAG, "Type " + this.Type + ", updateControllerPose()" +
        "\nLocal Pos: " + transform.localPosition.x + ", " + transform.localPosition.y + ", " + transform.localPosition.z +
        "\nPos: " + transform.position.x + ", " + transform.position.y + ", " + transform.position.z +
        "\nLocal rot: " + transform.localRotation.x + ", " + transform.localRotation.y + ", " + transform.localRotation.z);
    }
    #endregion

    private Vector3 GetHeadForward()
    {
        return GetHeadRotation() * Vector3.forward;
    }

    private Quaternion GetHeadRotation()
    {
        Quaternion _headrot = Quaternion.identity;

        if (Head == null)
            Head = WaveVR_Render.Instance.gameObject;
        if (Head != null)
            _headrot = Head.transform.localRotation;
            
        return _headrot;
    }

    private Vector3 GetHeadPosition()
    {
        Vector3 _headpos = Vector3.zero;

        if (Head == null)
            Head = WaveVR_Render.Instance.gameObject;
        if (Head != null)
            _headpos = Head.transform.localPosition;

        return FollowHead ? _headpos : defaultHeadPosition;
    }
    private Vector3 defaultHeadPosition = Vector3.zero;

    private const float BodyAngularVelocityUpperBound = 0.2f;
    private const float ControllerAngularVelocityUpperBound = 30.0f;
    private float BodyRotationFilter1(WVR_DevicePosePair_t pose)
    {
        Vector3 _v3AngularVelocity =
            new Vector3 (pose.pose.AngularVelocity.v0, pose.pose.AngularVelocity.v1, pose.pose.AngularVelocity.v2);
        float _v3magnitude = _v3AngularVelocity.magnitude;

        // If magnitude < body angular velocity upper bound, it means body rotation.
        // Thus the controller lerp filter will be 0 then controller will not move in scene.
        // If magnitude > body angular velocity upper bound, it means controller movement instead of body rotation.
        // In order to move controller smoothly, let the lerp max value to 0.2f means controller will move to correct position in 0.5s.
        // If controller angular velocity reaches upper bound, it means user wants the controller to move fast!
        float _bodyLerpFilter = Mathf.Clamp ((_v3magnitude - BodyAngularVelocityUpperBound) / ControllerAngularVelocityUpperBound, 0, 0.2f);

        return _bodyLerpFilter;
    }

    private const float BodyAngleBound = 0.01f;
    private const float BodyAngleLimitation = 0.3f; // bound of controller angle in SPEC provided to provider.
    private uint framesOfFreeze = 0;                // if framesOfFreeze >= mFPS, means controller freezed.
    private float BodyRotationFilter2(WaveVR_Utils.RigidTransform rtPose)
    {
        float _bodyLerpFilter = 0;
        if (!transform)
            return _bodyLerpFilter;

        try {
            Quaternion _rot_old = transform.localRotation;
            Quaternion _rot_new = rtPose.rot;
            float _rot_XY_angle_old = 0, _rot_XY_angle_new = 0;

            Vector3 _rot_forward = Vector3.zero;
            Quaternion _rot_XY_rotation = Quaternion.identity;

            _rot_forward = _rot_old * Vector3.forward;
            _rot_XY_rotation = Quaternion.FromToRotation (Vector3.forward, _rot_forward);
            _rot_XY_angle_old = Quaternion.Angle (_rot_XY_rotation, Quaternion.identity);

            _rot_forward = _rot_new * Vector3.forward;
            _rot_XY_rotation = Quaternion.FromToRotation (Vector3.forward, _rot_forward);
            _rot_XY_angle_new = Quaternion.Angle (_rot_XY_rotation, Quaternion.identity);

            float _diff_angle = _rot_XY_angle_new - _rot_XY_angle_old;
            _diff_angle = _diff_angle > 0 ? _diff_angle : -_diff_angle;

            _bodyLerpFilter = Mathf.Clamp ((_diff_angle - BodyAngleBound) / BodyAngleLimitation, 0, 1.0f);

            framesOfFreeze = _bodyLerpFilter < 1.0f ? framesOfFreeze + 1 : 0;

            if (framesOfFreeze > mFPS)
                _bodyLerpFilter = 0;
        } catch (NullReferenceException e) {
            PrintErrorLog ("BodyRotationFilter2() NullReferenceException " + e.Message);
        } catch (MissingReferenceException e) {
            PrintErrorLog ("BodyRotationFilter2() MissingReferenceException " + e.Message);
        } catch (MissingComponentException e) {
            PrintErrorLog ("BodyRotationFilter2() MissingComponentException " + e.Message);
        } catch (IndexOutOfRangeException e) {
            PrintErrorLog ("BodyRotationFilter2() IndexOutOfRangeException " + e.Message);
        }
        return _bodyLerpFilter;
    }

    private void UpdateHeadAndBodyPose(WVR_DevicePosePair_t pose, WaveVR_Utils.RigidTransform rtPose)
    {
        // Determine the gaze direction horizontally.
        Vector3 gazeDirection = GetHeadForward();
        gazeDirection.y = 0;
        gazeDirection.Normalize();

        //float _bodyLerpFilter = BodyRotationFilter1 (pose);
        float _bodyLerpFilter = BodyRotationFilter2 (rtPose);
        if (_bodyLerpFilter > 0)
        {
            if (!FollowHead)
            {
                if (Head == null)
                    Head = WaveVR_Render.Instance.gameObject;
                if (Head != null)
                    defaultHeadPosition = Head.transform.localPosition;
            }
        }

        bodyDirection = Vector3.Slerp (bodyDirection, gazeDirection, _bodyLerpFilter);
        bodyRotation = Quaternion.FromToRotation(Vector3.forward, bodyDirection);
    }

    /// <summary>
    /// Gets the relative controller rotation.
    /// <para></para>
    /// <para>When user moves controller and body concurrently,
    /// the controller rotation seen by user is not real rotation of controller in world space.</para>
    /// The rotation related to body is
    /// <para></para>
    /// (angle of controller rotation) - (angle of body rotation).
    /// </summary>
    /// <returns>The relative controller rotation.</returns>
    /// <param name="rot">rotation of controller in world space.</param>
    private Quaternion GetRelativeControllerRotation(Quaternion rot)
    {
        Quaternion _inverseBodyRotation = Quaternion.Inverse (bodyRotation);

        Quaternion _relativeRotation = _inverseBodyRotation * rot;
        return _relativeRotation;
    }

    private Vector3 prevVOffset = Vector3.zero;
    private Vector3 GetVelocityOffset(WVR_DevicePosePair_t pose)
    {
        Vector3 velocity = new Vector3 (pose.pose.Velocity.v0, pose.pose.Velocity.v1, pose.pose.Velocity.v2);
        Vector3 offset = velocity * Time.deltaTime;
        offset.x = offset.x == 0 ? 0 : Mathf.Clamp (offset.x + prevVOffset.x, -0.05f, 0.05f);
        offset.y = offset.y == 0 ? 0 : Mathf.Clamp (offset.y + prevVOffset.y, -0.1f, 0.1f);
        offset.z = offset.z == 0 ? 0 : Mathf.Clamp (offset.z + prevVOffset.z, -0.1f, 0.1f);
        prevVOffset = offset;
        return offset;
    }

    private Vector3 ApplyNeckToHead(Vector3 headPosition)
    {
        Quaternion _headRotation = GetHeadRotation();
        Vector3 _neckOffset = _headRotation * NECK_OFFSET;
        _neckOffset.y -= NECK_OFFSET.y;  // add neck length
        headPosition += _neckOffset;

        return headPosition;
    }

    /// <summary>
    /// Get the position of controller in Arm Model
    /// 
    /// Consider the parts construct controller position:
    /// Parts contain elbow, wrist and controller and each part has default offset from head.
    /// 1. simulated elbow offset = default elbow offset apply body rotation = body rotation (Quaternion) * elbow offset (Vector3)
    /// 2. simulated wrist offset = default wrist offset apply elbow rotation = elbow rotation (Quaternion) * wrist offset (Vector3)
    /// 3. simulated controller offset = default controller offset apply wrist rotation = wrist rotation (Quat) * controller offset (V3)
    /// head + 1 + 2 + 3 = controller position.
    /// </summary>
    /// <param name="pose">WVR_DevicePosePair_t</param>
    /// <param name="rtPose">WaveVR_Utils.RigidTransform</param>
    private void ComputeControllerPose(WVR_DevicePosePair_t pose, WaveVR_Utils.RigidTransform rtPose)
    {
        // if bodyRotation angle is θ, _inverseBodyRation is -θ
        // the operator * of Quaternion in Unity means concatenation, not multipler.
        // If quaternion qA has angle θ, quaternion qB has angle ε,
        // qA * qB will plus θ and ε which means rotating angle θ then rotating angle ε.
        // (_inverseBodyRotation * rotation of controller in world space) means angle ε subtracts angle θ.
        Quaternion _controllerRotation = Quaternion.Inverse(bodyRotation) * rtPose.rot;
        Vector3 _headPosition = GetHeadPosition ();
        if (ApplyNeckOffset)
        {
            _headPosition = ApplyNeckToHead (_headPosition);
        }

        /// 1. simulated elbow offset = default elbow offset apply body rotation = body rotation (Quaternion) * elbow offset (Vector3)
        // Default left / right elbow offset.
        Vector3 _elbowOffset = Vector3.Scale (HEADTOELBOW_OFFSET, v3ChangeArmXAxis);
        // Default left / right elbow pitch offset.
        Vector3 _elbowPitchOffset = Vector3.Scale (ELBOW_PITCH_OFFSET, v3ChangeArmXAxis) + new Vector3(0.0f, ElbowRaiseYaxis, ElbowRaiseZaxis);

        // Use controller pitch to simulate elbow pitch.
        // Range from ELBOW_PITCH_ANGLE_MIN ~ ELBOW_PITCH_ANGLE_MAX.
        // The percent of pitch angle will be used to calculate the position offset.
        Vector3 _controllerForward = _controllerRotation * Vector3.forward;
        float _controllerPitch = 90.0f - Vector3.Angle (_controllerForward, Vector3.up);    // 0~90
        float _controllerPitchRadio = (_controllerPitch - ELBOW_PITCH_ANGLE_MIN) / (ELBOW_PITCH_ANGLE_MAX - ELBOW_PITCH_ANGLE_MIN);
        _controllerPitchRadio = Mathf.Clamp (_controllerPitchRadio, 0.0f, 1.0f);

        // According to pitch angle percent, plus offset to elbow position.
        _elbowOffset += _elbowPitchOffset * _controllerPitchRadio;
        // Apply body rotation and head position to calculate final elbow position.
        _elbowOffset = _headPosition + bodyRotation * _elbowOffset;


        // Rotation from Z-axis to XY-plane used to simulated elbow & wrist rotation.
        Quaternion _controllerXYRotation = Quaternion.FromToRotation (Vector3.forward, _controllerForward);
        float _controllerXYRotationRadio = (Quaternion.Angle (_controllerXYRotation, Quaternion.identity)) / 180;
        // Simulate the elbow raising curve.
        float _elbowCurveLerpValue = ELBOW_TO_XYPLANE_LERP_MIN + (_controllerXYRotationRadio * (ELBOW_TO_XYPLANE_LERP_MAX - ELBOW_TO_XYPLANE_LERP_MIN));
        Quaternion _controllerXYLerpRotation = Quaternion.Lerp (Quaternion.identity, _controllerXYRotation, _elbowCurveLerpValue);


        /// 2. simulated wrist offset = default wrist offset apply elbow rotation = elbow rotation (Quaternion) * wrist offset (Vector3)
        // Default left / right wrist offset
        Vector3 _wristOffset = Vector3.Scale (ELBOWTOWRIST_OFFSET, v3ChangeArmXAxis);
        // elbow rotation + curve = wrist rotation
        // wrist rotation = controller XY rotation
        // => elbow rotation + curve = controller XY rotation
        // => elbow rotation = controller XY rotation - curve
        Quaternion _elbowRotation = bodyRotation * Quaternion.Inverse(_controllerXYLerpRotation) * _controllerXYRotation;
        // Apply elbow offset and elbow rotation to calculate final wrist position.
        _wristOffset = _elbowOffset + _elbowRotation * _wristOffset;


        /// 3. simulated controller offset = default controller offset apply wrist rotation = wrist rotation (Quat) * controller offset (V3)
        // Default left / right controller offset.
        Vector3 _controllerOffset = Vector3.Scale (WRISTTOCONTROLLER_OFFSET, v3ChangeArmXAxis);
        Quaternion _wristRotation = _controllerXYRotation;
        // Apply wrist offset and wrist rotation to calculate final controller position.
        _controllerOffset = _wristOffset + _wristRotation * _controllerOffset;

        controllerSimulatedPosition = /*bodyRotation */ _controllerOffset;
        controllerSimulatedRotation = bodyRotation * _controllerRotation;
    }

    private void ApplyHeadRotation(Vector3 startPosition, Quaternion startRotation)
    {
        float head_yaw = Head.transform.eulerAngles.y;                  // new yaw angle
        Quaternion quat_head_yaw = Quaternion.Euler (0, head_yaw, 0);   // new yaw rotation
        //float whirl_movement = head_yaw - headRotation.eulerAngles.y;               // (new - old) yaw angle
        //Quaternion quat_whirl_movement = Quaternion.Euler(0, whirl_movement, 0);    // (new - old) yaw rotation

        transform.position = Quaternion.AngleAxis (head_yaw, Vector3.up) * startPosition;
        transform.localRotation = startRotation * quat_head_yaw;

        //headRotation = Head.transform.rotation; // refresh old rotation
    }
}
