﻿// "WaveVR SDK 
// © 2017 HTC Corporation. All Rights Reserved.
//
// Unless otherwise required by copyright law and practice,
// upon the execution of HTC SDK license agreement,
// HTC grants you access to and use of the WaveVR SDK(s).
// You shall fully comply with all of HTC’s SDK license agreement terms and
// conditions signed by you and all SDK and API requirements,
// specifications, and documentation provided by HTC to You."

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using wvr;
using WaveVR_Log;
using System;

public class WaveVR_Controller
{
    private static string LOG_TAG = "WaveVR_Controller";

    public static bool IsLeftHanded
    {
        get
        {
            return isLeftHanded;
        }
    }
    private static bool isLeftHanded = false;

    public static void SetLeftHandedMode(bool lefthanded)
    {
        isLeftHanded = lefthanded;
        #if UNITY_EDITOR
        Debug.Log (LOG_TAG + " SetLeftHandedMode() " + isLeftHanded);
        #endif
        Log.d (LOG_TAG, "SetLeftHandedMode() left handed? " + isLeftHanded);
    }

    public enum EDeviceType
    {
        Head = 1,
        Dominant = 2,
        NonDominant = 3
    };

    public static EDeviceType GetEDeviceByWVRType(WVR_DeviceType type)
    {
        if (type == Input(EDeviceType.Dominant).DeviceType)
            return EDeviceType.Dominant;
        if (type == Input(EDeviceType.NonDominant).DeviceType)
            return EDeviceType.NonDominant;
        return EDeviceType.Head;
    }

    private static Device[] devices;

    /// <summary>
    /// Get the controller by device index.
    /// </summary>
    /// <param name="deviceIndex">The index of the controller.</param>
    /// <returns></returns>
    public static Device Input(WVR_DeviceType deviceIndex)
    {
        if (isLeftHanded)
        {
            switch (deviceIndex)
            {
            case WVR_DeviceType.WVR_DeviceType_Controller_Right:
                deviceIndex = WVR_DeviceType.WVR_DeviceType_Controller_Left;
                break;
            case WVR_DeviceType.WVR_DeviceType_Controller_Left:
                deviceIndex = WVR_DeviceType.WVR_DeviceType_Controller_Right;
                break;
            default:
                break;
            }
        }

        return ChangeRole (deviceIndex);
    }

    public static Device Input(EDeviceType type)
    {
        WVR_DeviceType _type = WVR_DeviceType.WVR_DeviceType_Invalid;
        switch (type)
        {
        case EDeviceType.Head:
            _type = WVR_DeviceType.WVR_DeviceType_HMD;
            break;
        case EDeviceType.Dominant:
            _type = isLeftHanded ? WVR_DeviceType.WVR_DeviceType_Controller_Left : WVR_DeviceType.WVR_DeviceType_Controller_Right;
            break;
        case EDeviceType.NonDominant:
            _type = isLeftHanded ? WVR_DeviceType.WVR_DeviceType_Controller_Right : WVR_DeviceType.WVR_DeviceType_Controller_Left;
            break;
        default:
            break;
        }

        return ChangeRole (_type);
    }

    private static Device ChangeRole(WVR_DeviceType deviceIndex)
    {
        if (devices == null)
        {
            devices = new Device[Enum.GetNames (typeof(WVR_DeviceType)).Length];
            uint i = 0;
            devices [i++] = new Device (WVR_DeviceType.WVR_DeviceType_Invalid);
            devices [i++] = new Device (WVR_DeviceType.WVR_DeviceType_HMD);
            devices [i++] = new Device (WVR_DeviceType.WVR_DeviceType_Controller_Right);
            devices [i++] = new Device (WVR_DeviceType.WVR_DeviceType_Controller_Left);
        }

        for (uint i = 0; i < devices.Length; i++)
        {
            if (deviceIndex == devices [i].DeviceType)
            {
                return devices [i];
            }
        }

        return null;
    }

    public class Device
    {
        #region button definition
        // ============================== press ==============================
        static WVR_InputId[] pressIds = new WVR_InputId[] {
            WVR_InputId.WVR_InputId_Alias1_System,
            WVR_InputId.WVR_InputId_Alias1_Menu,
            WVR_InputId.WVR_InputId_Alias1_Grip,
            WVR_InputId.WVR_InputId_Alias1_DPad_Left,
            WVR_InputId.WVR_InputId_Alias1_DPad_Up,
            WVR_InputId.WVR_InputId_Alias1_DPad_Right,
            WVR_InputId.WVR_InputId_Alias1_DPad_Down,
            WVR_InputId.WVR_InputId_Alias1_Volume_Up,
            WVR_InputId.WVR_InputId_Alias1_Volume_Down,
            WVR_InputId.WVR_InputId_Alias1_Digital_Trigger,
            WVR_InputId.WVR_InputId_Alias1_Enter,
            WVR_InputId.WVR_InputId_Alias1_Touchpad,
            WVR_InputId.WVR_InputId_Alias1_Trigger,
            WVR_InputId.WVR_InputId_Alias1_Thumbstick
        };

        // Timer of each button (has press state) should be seperated.
        int[] prevFrameCount_press = new int[pressIds.Length];
        bool[] state_press = new bool[pressIds.Length];
        bool[] prevState_press = new bool[pressIds.Length];
        bool[] event_state_press = new bool[pressIds.Length];


        // ============================== touch ==============================
        static WVR_InputId[] touchIds = new WVR_InputId[] {
            WVR_InputId.WVR_InputId_Alias1_Touchpad,
            WVR_InputId.WVR_InputId_Alias1_Trigger,
            WVR_InputId.WVR_InputId_Alias1_Thumbstick
        };

        // Timer of each button (has touch state) should be seperated.
        int[] prevFrameCount_touch = new int[touchIds.Length];
        bool[] state_touch = new bool[touchIds.Length];
        bool[] prevState_touch = new bool[touchIds.Length];
        bool[] event_state_touch = new bool[touchIds.Length];
        #endregion

        public Device(WVR_DeviceType dt)
        {
            Log.i (LOG_TAG, "Initialize WaveVR_Controller Device: " + dt);
            DeviceType = dt;

            ResetAllButtonStates();
        }

        public void SetEventState_Press(WVR_InputId btn, bool down)
        {
            for (int _p = 0; _p < pressIds.Length; _p++)
            {
                if (pressIds [_p] == btn)
                {
                    event_state_press [_p] = down;
                    Log.d (LOG_TAG, "SetEventState_Press() " + this.DeviceType + ", " + pressIds [_p] + ": " + event_state_press [_p]);
                    break;
                }
            }
        }

        public bool GetEventState_Press(WVR_InputId btn)
        {
            bool _press = false;
            for (int _p = 0; _p < pressIds.Length; _p++)
            {
                if (pressIds [_p] == btn)
                {
                    _press = event_state_press [_p];
                    break;
                }
            }
            return _press;
        }

        public void SetEventState_Touch(WVR_InputId btn, bool down)
        {
            for (int _t = 0; _t < touchIds.Length; _t++)
            {
                if (touchIds [_t] == btn)
                {
                    event_state_touch [_t] = down;
                    Log.d (LOG_TAG, "SetEventState_Touch() " + this.DeviceType + ", " + touchIds [_t] + ": " + event_state_touch [_t]);
                    break;
                }
            }
        }

        public bool GetEventState_Touch(WVR_InputId btn)
        {
            bool _touch = false;
            for (int _t = 0; _t < touchIds.Length; _t++)
            {
                if (touchIds [_t] == btn)
                {
                    _touch = event_state_touch [_t];
                    break;
                }
            }
            return _touch;
        }

        public WVR_DeviceType DeviceType
        {
            get;
            private set;
        }

        int prevFrame_connected = -1;
        private bool AllowGetConnectionState()
        {
            if (Time.frameCount != prevFrame_connected)
            {
                prevFrame_connected = Time.frameCount;
                return true;
            }

            return false;
        }

        internal bool _connected = false;
        /// Whether is the device connected.
        public bool connected
        {
            get
            {
                #if UNITY_EDITOR || UNITY_STANDALONE
                if (Application.isEditor)
                {
                    if (WaveVR.Instance.isSimulatorOn)
                    {
                        if (AllowGetConnectionState ())
                            _connected = WaveVR_Utils.WVR_IsDeviceConnected_S ((int)DeviceType);
                    }
                } else
                #endif
                {
                    if (AllowGetConnectionState ())
                        _connected = Interop.WVR_IsDeviceConnected (DeviceType);
                }
                Log.gpl.d (LOG_TAG, "Device " + DeviceType + " is " + (_connected ? "connected." : "disconnected."), true);
                return _connected;
            }
            set {
                _connected = value;
                Log.d (LOG_TAG, "Device " + DeviceType + " is " + (_connected ? "connected." : "disconnected."));
            }
        }

        int prevFrame_pose = -1;
        private bool AllowGetPoseState()
        {
            if (!this._connected)
                return false;

            if (Time.frameCount != prevFrame_pose)
            {
                prevFrame_pose = Time.frameCount;
                return true;
            }

            return false;
        }

        internal WVR_PoseState_t pose;
        /// Gets the RigidTransform {pos=Vector3, rot=Rotation}
        public WaveVR_Utils.RigidTransform transform
        {
            get
            {
                #if UNITY_EDITOR || UNITY_STANDALONE
                if (isEditorMode)
                {
                    var system = WaveVR_PoseSimulator.Instance;
                    return system.GetRigidTransform(DeviceType);
                }
                #endif

                if (DeviceType == WVR_DeviceType.WVR_DeviceType_HMD)
                {
                    // WVR_GetSyncPose and WVR_GetPoseState (WVR_DeviceType.WVR_DeviceType_HMD) should not be called within 1 frame.
                    // We already used WVR_GetSyncPose every frame in WaveVR.cs
                    // So if device type is HMD, we get RigidTransform from WaveVR directly.
                    WaveVR.Device _device = WaveVR.Instance.getDeviceByType (DeviceType);
                    return _device.rigidTransform;
                } else
                {
                    if (AllowGetPoseState ())
                    {
                        Interop.WVR_GetPoseState (
                            DeviceType,
                            WaveVR_Render.Instance.origin,
                            500,
                            ref pose);
                    }
                    return new WaveVR_Utils.RigidTransform (pose.PoseMatrix);
                }
            }
        }

        public WVR_Vector3f_t velocity
        {
            get {
                #if UNITY_EDITOR
                if (isEditorMode)
                {
                    var system = WaveVR_PoseSimulator.Instance;
                    return system.GetVelocity(DeviceType);
                }
                #endif

                if (AllowGetPoseState ())
                {
                    Interop.WVR_GetPoseState (
                        DeviceType,
                        WaveVR_Render.Instance.origin,
                        //WVR_PoseOriginModel.WVR_PoseOriginModel_OriginOnGround,
                        500,
                        ref pose);
                }
                return pose.Velocity;
            }
        }

        public WVR_Vector3f_t AngularVelocity
        {
            get {
                #if UNITY_EDITOR
                if (isEditorMode)
                {
                    var system = WaveVR_PoseSimulator.Instance;
                    return system.GetAngularVelocity(DeviceType);
                }
                #endif

                if (AllowGetPoseState ())
                {
                    Interop.WVR_GetPoseState (
                        DeviceType,
                        WaveVR_Render.Instance.origin,
                        //WVR_PoseOriginModel.WVR_PoseOriginModel_OriginOnGround,
                        500,
                        ref pose);
                }
                return pose.AngularVelocity;
            }
        }

        internal WVR_Axis_t axis;
        //internal WaveVR_Utils.WVR_ButtonState_t state, pre_state;

        #if UNITY_EDITOR || UNITY_STANDALONE
        private bool isEditorMode = true;
        #endif

        #region Timer
        private bool AllowPressActionInAFrame(WVR_InputId _id)
        {
            if (!this._connected)
                return false;

            for (uint i = 0; i < pressIds.Length; i++)
            {
                if (_id == pressIds [i])
                {
                    if (Time.frameCount != prevFrameCount_press [i])
                    {
                        prevFrameCount_press [i] = Time.frameCount;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool AllowTouchActionInAFrame(WVR_InputId _id)
        {
            if (!this._connected)
                return false;

            for (uint i = 0; i < touchIds.Length; i++)
            {
                if (_id == touchIds [i])
                {
                    if (Time.frameCount != prevFrameCount_touch [i])
                    {
                        prevFrameCount_touch [i] = Time.frameCount;
                        return true;
                    }
                }
            }

            return false;
        }
        #endregion

        private void Update_PressState(WVR_InputId _id)
        {
            if (AllowPressActionInAFrame (_id))
            {
                bool _pressed = false;

                #if UNITY_EDITOR || UNITY_STANDALONE
                if (isEditorMode && !WaveVR.Instance.isSimulatorOn)
                {
                    var system = WaveVR_PoseSimulator.Instance;
                    _pressed = system.GetButtonPressState(DeviceType, _id);
                } else
                #endif
                {
                    _pressed = GetEventState_Press(_id);
                    /*
                    if (this._connected)
                        _pressed = Interop.WVR_GetInputButtonState (DeviceType, _id);
                    if (_pressed)
                        Log.gpl.d (LOG_TAG, "Update_PressState(), " + DeviceType + " " + _id + " is pressed." + "left-handed? " + IsLeftHanded);
                    */
                }

                for (int _p = 0; _p < pressIds.Length; _p++)
                {
                    if (_id == pressIds [_p])
                    {
                        prevState_press [_p] = state_press [_p];
                        state_press [_p] = _pressed;
                    }
                }
            }
        }

        private void Update_TouchState(WVR_InputId _id)
        {
            if (AllowTouchActionInAFrame (_id))
            {
                bool _touched = false;

                #if UNITY_EDITOR || UNITY_STANDALONE
                if (isEditorMode && !WaveVR.Instance.isSimulatorOn)
                {
                    var system = WaveVR_PoseSimulator.Instance;
                    _touched = system.GetButtonTouchState(DeviceType, _id);
                } else
                #endif
                {
                    _touched = GetEventState_Touch(_id);
                    /*
                    if(this._connected)
                        _touched = Interop.WVR_GetInputTouchState (DeviceType, _id);
                    if (_touched)
                        Log.gpl.d (LOG_TAG, "Update_TouchState(), " + DeviceType + " " + _id + " is touched." + "left-handed? " + IsLeftHanded);
                    */
                }

                for (int _t = 0; _t < touchIds.Length; _t++)
                {
                    if (_id == touchIds [_t])
                    {
                        prevState_touch [_t] = state_touch [_t];
                        state_touch [_t] = _touched;
                    }
                }
            }
        }

        public void ResetAllButtonStates()
        {
            for (int _p = 0; _p < pressIds.Length; _p++)
            {
                prevFrameCount_press[_p] = -1;
                state_press[_p] = false;
                prevState_press[_p] = false;
                event_state_press [_p] = false;
            }
            for (int _t = 0; _t < touchIds.Length; _t++)
            {
                prevFrameCount_touch[_t] = -1;
                state_touch[_t] = false;
                prevState_touch[_t] = false;
                event_state_touch [_t] = false;
            }
        }

        /// <summary>
        /// Resets the button event only.
        /// The flow in Update() of end of frame will be:
        /// 1. prevState = state
        /// 2. state = event_state
        /// If preState = true, state will change from true -> false which means button up.
        /// </summary>
        public void ResetButtonEvents()
        {
            #if UNITY_EDITOR
            if (Application.isEditor)
            {
                for (int _p = 0; _p < pressIds.Length; _p++)
                    event_state_press [_p] = false;

                for (int _t = 0; _t < touchIds.Length; _t++)
                    event_state_touch [_t] = false;
            } else
            #endif
            {
                if (WaveVR.Instance == null)
                    return;

                for (int _p = 0; _p < pressIds.Length; _p++)
                {
                    bool _pressed = Interop.WVR_GetInputButtonState (DeviceType, pressIds [_p]);
                    event_state_press [_p] = _pressed;
                    Log.d (LOG_TAG, "ResetButtonEvents() " + DeviceType + " " + pressIds [_p] + " is " + (event_state_press [_p] ? "pressed." : "not pressed."));
                }
                for (int _t = 0; _t < touchIds.Length; _t++)
                {
                    bool _touched = Interop.WVR_GetInputTouchState (DeviceType, touchIds [_t]);
                    event_state_touch [_t] = _touched;
                    Log.d (LOG_TAG, "ResetButtonEvents() " + DeviceType + " " + touchIds [_t] + " is " + (event_state_touch [_t] ? "touched." : "not touched."));
                }
            }
        }

        #region Button Press state
        /// <summary>
        /// Check if button state is equivallent to specified state.
        /// </summary>
        /// <returns><c>true</c>, equal, <c>false</c> otherwise.</returns>
        /// <param name="_id">input button</param>
        public bool GetPress(WVR_InputId _id)
        {
            bool _state = false;
            Update_PressState (_id);

            for (int _p = 0; _p < pressIds.Length; _p++)
            {
                if (_id == pressIds [_p])
                {
                    _state = state_press [_p];
                    break;
                }
            }

            return _state;
        }

        /// <summary>
        /// If true, button with _id is pressed, else unpressed.
        /// </summary>
        /// <returns><c>true</c>, if press down was gotten, <c>false</c> otherwise.</returns>
        /// <param name="_id">WVR_ButtonId, id of button</param>
        public bool GetPressDown(WVR_InputId _id)
        {
            bool _state = false;
            Update_PressState (_id);

            for (int _p = 0; _p < pressIds.Length; _p++)
            {
                if (_id == pressIds [_p])
                {
                    _state = (
                        (prevState_press [_p] == false) &&
                        (state_press [_p] == true)
                    );
                    break;
                }
            }

            return _state;
        }

        /// <summary>
        /// If true, button with _id is unpressed, else pressed.
        /// </summary>
        /// <returns><c>true</c>, if unpress up was gotten, <c>false</c> otherwise.</returns>
        /// <param name="_id">WVR_ButtonId, id of button</param>
        public bool GetPressUp(WVR_InputId _id)
        {
            bool _state = false;
            Update_PressState (_id);

            for (int _p = 0; _p < pressIds.Length; _p++)
            {
                if (_id == pressIds [_p])
                {
                    _state = (
                        (prevState_press [_p] == true) &&
                        (state_press [_p] == false)
                    );
                    break;
                }
            }

            return _state;
        }
        #endregion

        #region Button Touch state
        /// <summary>
        /// If true, button with _id is touched, else untouched..
        /// </summary>
        /// <returns><c>true</c>, if touch was gotten, <c>false</c> otherwise.</returns>
        /// <param name="_id">WVR_ButtonId, id of button</param>
        public bool GetTouch(WVR_InputId _id)
        {
            bool _state = false;
            Update_TouchState (_id);

            for (int _t = 0; _t < touchIds.Length; _t++)
            {
                if (_id == touchIds [_t])
                {
                    _state = state_touch [_t];
                    break;
                }
            }

            return _state;
        }

        /// <summary>
        /// If true, button with _id is touched, else untouched..
        /// </summary>
        /// <returns><c>true</c>, if touch was gotten, <c>false</c> otherwise.</returns>
        /// <param name="_id">WVR_ButtonId, id of button</param>
        public bool GetTouchDown(WVR_InputId _id)
        {
            bool _state = false;
            Update_TouchState (_id);

            for (int _t = 0; _t < touchIds.Length; _t++)
            {
                if (_id == touchIds [_t])
                {
                    _state = (
                        (prevState_touch [_t] == false) &&
                        (state_touch [_t] == true)
                    );
                    break;
                }
            }

            return _state;
        }

        /// <summary>
        /// If true, button with _id is touched, else untouched..
        /// </summary>
        /// <returns><c>true</c>, if touch was gotten, <c>false</c> otherwise.</returns>
        /// <param name="_id">WVR_ButtonId, id of button</param>
        public bool GetTouchUp(WVR_InputId _id)
        {
            bool _state = false;
            Update_TouchState (_id);

            for (int _t = 0; _t < touchIds.Length; _t++)
            {
                if (_id == touchIds [_t])
                {
                    _state = (
                        (prevState_touch [_t] == true) &&
                        (state_touch [_t] == false)
                    );
                    break;
                }
            }

            return _state;
        }
        #endregion

        public Vector2 GetAxis(WVR_InputId _id)
        {
            if (!this._connected)
                return Vector2.zero;

            bool _isTouchId = false;

            for (int _t = 0; _t < touchIds.Length; _t++)
            {
                if (_id == touchIds [_t])
                {
                    _isTouchId = true;
                    break;
                }
            }

            if (!_isTouchId)
            {
                Log.e (LOG_TAG, "GetAxis, button " + _id + " does NOT have axis!");
                return Vector2.zero;
            }

            #if UNITY_EDITOR || UNITY_STANDALONE
            if (isEditorMode)
            {
                if (!WaveVR.Instance.isSimulatorOn) {
                    var system = WaveVR_PoseSimulator.Instance;
                    axis = system.GetAxis(DeviceType, _id);
                }
                else {
                    axis = WaveVR_Utils.WVR_GetInputAnalogAxis_S ((int)DeviceType, (int)_id);
                }
            } else
            #endif
            {
                axis = Interop.WVR_GetInputAnalogAxis (DeviceType, _id);
            }

            //Log.d (LOG_TAG, "GetAxis: {" + axis.x + ", " + axis.y + "}");
            return new Vector2 (axis.x, axis.y);
        }

        public void TriggerHapticPulse(
            ushort _durationMicroSec = 500, 
            WVR_InputId _id = WVR_InputId.WVR_InputId_Alias1_Touchpad
        )
        {
            #if UNITY_EDITOR || UNITY_STANDALONE
            if (isEditorMode)
            {
                var system = WaveVR_PoseSimulator.Instance;
                system.TriggerHapticPulse (DeviceType, _id, _durationMicroSec);
            } else
            #endif
            {
                Interop.WVR_TriggerVibrator (DeviceType, _id, _durationMicroSec);
            }
        }

    } // public class Device
}
