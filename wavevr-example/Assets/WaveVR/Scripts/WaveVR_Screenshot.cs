using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using wvr;
using WVR_Log;
using System;
using System.Runtime.InteropServices;

public class WaveVR_Screenshot {
	private static string LOG_TAG = "WaveVR_Screenshot";

	private static void PrintDebugLog(string msg)
	{
		Log.d(LOG_TAG, msg);
	}

	public static bool requestScreenshot(WVR_ScreenshotMode mode, string filename)
	{
		uint width = 0;
		uint height = 0;
		IntPtr fnPtr = Marshal.StringToHGlobalAnsi(filename);

		Interop.WVR_GetRenderTargetSize(ref width, ref height);
		PrintDebugLog("Width = " + width + ", Height = " + height + ", Mode = " + mode + ", File name = " + filename);
		return Interop.WVR_RequestScreenshot(width, height, mode, fnPtr);
	}
}
