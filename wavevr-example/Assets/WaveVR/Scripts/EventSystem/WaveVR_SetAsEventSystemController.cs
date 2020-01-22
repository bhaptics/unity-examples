using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using wvr;

public class WaveVR_SetAsEventSystemController : MonoBehaviour
{
	private bool added = false;
	public WaveVR_Controller.EDeviceType Type = WaveVR_Controller.EDeviceType.Dominant;

	void OnEnable()
	{
		WaveVR_EventSystemControllerProvider.Instance.SetControllerModel (Type, gameObject);
		added = true;
	}

	void OnDisable()
	{
		if (added)
		{
			WaveVR_EventSystemControllerProvider.Instance.SetControllerModel (Type, null);
			added = false;
		}
	}
}
