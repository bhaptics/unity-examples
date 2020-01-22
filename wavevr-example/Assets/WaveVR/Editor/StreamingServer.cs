using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System;
using System.Linq;
using System.IO;

public class StreamingServer
{
	public static Process myProcess = new Process();

	[UnityEditor.MenuItem("WaveVR/DirectPreview/Start Streaming Server")]
	static void StartStreamingServerMenu()
	{
		StartStreamingServer();
	}

	[UnityEditor.MenuItem("WaveVR/DirectPreview/Stop Streaming Server")]
	static void StopStreamingServerMenu()
	{
		StopStreamingServer();
	}

	// Launch rrServer
	public static void StartStreamingServer()
	{
		try
		{
			var monoScripts = MonoImporter.GetAllRuntimeMonoScripts();
			var monoScript = monoScripts.FirstOrDefault(script => script.GetClass() == typeof(WaveVR));
			var path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(monoScript));
			var fullPath = Path.GetFullPath((path.Substring(0, path.Length - "Scripts".Length) + "Platform/Windows").Replace("\\", "/"));

			var wvrAarFolder = fullPath.Substring(fullPath.IndexOf("Assets"), fullPath.Length - fullPath.IndexOf("Assets"));
			UnityEngine.Debug.Log("StartStreamingServer at " + wvrAarFolder);
			//Get the path of the Game data folder
			myProcess.StartInfo.FileName = "C:\\Windows\\system32\\cmd.exe";
			myProcess.StartInfo.Arguments = "/c cd " + wvrAarFolder + " && dpServer";
			myProcess.Start();
		}
		catch (Exception e)
		{
			UnityEngine.Debug.LogError(e);
		}
	}
	// Stop rrServer
	public static void StopStreamingServer()
	{
		try
		{
			UnityEngine.Debug.Log("Stop Streaming Server.");
			myProcess.StartInfo.FileName = "C:\\Windows\\system32\\cmd.exe";
			myProcess.StartInfo.Arguments = "/c taskkill /F /IM dpServer.exe";
			myProcess.Start();	
		}
		catch (Exception e)
		{
			UnityEngine.Debug.LogError(e);
		}
	}
}
