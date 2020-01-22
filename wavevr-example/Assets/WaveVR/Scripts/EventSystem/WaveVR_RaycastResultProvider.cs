using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using wvr;
using WVR_Log;
using UnityEngine.EventSystems;
using System;

public class WaveVR_RaycastResultProvider
{
	private const string LOG_TAG = "WaveVR_RaycastResultProvider";
	private void PrintDebugLog(string msg)
	{
		Log.d (LOG_TAG, msg);
	}

	public static WaveVR_RaycastResultProvider Instance
	{
		get
		{
			if (instance == null)
				instance = new WaveVR_RaycastResultProvider();
			return instance;
		}
	}
	private static WaveVR_RaycastResultProvider instance = null;

	public class ERaycastResult
	{
		public WaveVR_Controller.EDeviceType Type;
		public WaveVR_RaycastResult Result;
		public ERaycastResult(WaveVR_Controller.EDeviceType type, WaveVR_RaycastResult result)
		{
			this.Type = type;
			this.Result = result;
		}
	}
	private List<ERaycastResult> RaycastResults = new List<ERaycastResult>();

	private WaveVR_RaycastResultProvider()
	{
		for (int i = 0; i < WaveVR_Controller.DeviceTypes.Length; i++)
		{
			RaycastResults.Add (new ERaycastResult (WaveVR_Controller.DeviceTypes [i], new WaveVR_RaycastResult ()));
		}
	}

	public void SetRaycastResult (WaveVR_Controller.EDeviceType device, GameObject gameObject, Vector3 worldPosition)
	{
		for (int i = 0; i < RaycastResults.Count; i++)
		{
			if (RaycastResults [i].Type == device)
			{
				RaycastResults [i].Result.gameObject = gameObject;
				RaycastResults [i].Result.worldPosition = worldPosition;
				break;
			}
		}
	}

	public WaveVR_RaycastResult GetRaycastResult(WVR_DeviceType type)
	{
		WaveVR_Controller.EDeviceType device = WaveVR_Controller.GetEDeviceByWVRType (type);
		return GetRaycastResult (device);
	}

	public WaveVR_RaycastResult GetRaycastResult(WaveVR_Controller.EDeviceType device)
	{
		for (int i = 0; i < RaycastResults.Count; i++)
		{
			if (RaycastResults [i].Type == device)
			{
				return RaycastResults [i].Result;
			}
		}
		return null;
	}

}
