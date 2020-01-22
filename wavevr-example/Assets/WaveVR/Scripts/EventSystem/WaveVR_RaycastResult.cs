using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class WaveVR_RaycastResult
{
	public GameObject gameObject
	{
		get;
		set;
	}
	public Vector3 worldPosition
	{
		get;
		set;
	}

	public WaveVR_RaycastResult()
	{
		this.gameObject = null;
		this.worldPosition = Vector3.zero;
	}
}
