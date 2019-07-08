using UnityEngine;
using wvr;
using System;
using System.Collections;
using UnityEngine.EventSystems;
using WaveVR_Log;

public class WaveVR_EventSystemControllerProvider
{
    private const string LOG_TAG = "WaveVR_EventSystemControllerProvider";

    private void PrintDebugLog(string msg)
    {
        #if UNITY_EDITOR
        Debug.Log(LOG_TAG + " " + msg);
        #endif
        Log.d (LOG_TAG, msg);
    }

    public static WaveVR_EventSystemControllerProvider Instance
    {
        get
        {
            if (instance == null)
                instance = new WaveVR_EventSystemControllerProvider();
            return instance;
        }
    }
    private static WaveVR_EventSystemControllerProvider instance = null;

    public class ControllerModel
    {
        public GameObject model {
            get;
            set;
        }

        // Has controller loader?
        public bool HasLoader {
            get;
            set;
        }

        public ControllerModel()
        {
            model = null;
            HasLoader = false;
        }
    }


    private Hashtable ControllerModels = new Hashtable();

    private WaveVR_EventSystemControllerProvider()
    {
        foreach (WaveVR_Controller.EDeviceType _dt in Enum.GetValues(typeof(WaveVR_Controller.EDeviceType)))
        {
            // init all items as null.
            ControllerModels.Add (_dt, new ControllerModel());
        }
    }

    public void SetControllerModel (WaveVR_Controller.EDeviceType type, GameObject model)
    {
        PrintDebugLog ("SetControllerModel() type: " + type + ", model: " + (model != null ? model.name : "null"));
        if (((ControllerModel)ControllerModels [type]).model != null)
            ((ControllerModel)ControllerModels [type]).model.SetActive (false);
        ((ControllerModel)ControllerModels [type]).model = model;
        if (((ControllerModel)ControllerModels [type]).model != null)
            ((ControllerModel)ControllerModels [type]).model.SetActive (true);
    }

    public GameObject GetControllerModel(WaveVR_Controller.EDeviceType type)
    {
        return ((ControllerModel)ControllerModels [type]).model;
    }

    public void MarkControllerLoader(WaveVR_Controller.EDeviceType type, bool value)
    {
        PrintDebugLog (type + " " + (value ? "has" : "doesn't have") + " ControllerLoader.");
        ((ControllerModel)ControllerModels [type]).HasLoader = value;
    }

    public bool HasControllerLoader(WaveVR_Controller.EDeviceType type)
    {
        return ((ControllerModel)ControllerModels [type]).HasLoader;
    }
}
