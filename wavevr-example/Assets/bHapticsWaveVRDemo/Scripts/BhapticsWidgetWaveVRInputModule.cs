﻿using UnityEngine;
using UnityEngine.UI;
using wvr;

public class BhapticsWidgetWaveVRInputModule : MonoBehaviour
{
    public WVR_DeviceType deviceType = WVR_DeviceType.WVR_DeviceType_Controller_Left;



    private LineRenderer lineRenderer;
    private Button button;





    void Start()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.material = new Material(Shader.Find("Unlit/Texture"));
    }

    void Update()
    {
        InputWidgetControl();
    }





    private void InputWidgetControl()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, 1 << LayerMask.NameToLayer("UI")))
        {
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, transform.position + transform.forward * Vector3.Distance(transform.position, hit.point));

            if (hit.collider.GetComponent<Button>() != null)
            {
                button = hit.collider.GetComponent<Button>();
                button.Select();
                if (WaveVR_Controller.Input(deviceType).GetPressDown(WVR_InputId.WVR_InputId_Alias1_Trigger))
                {
                    button.OnSubmit(null);
                }
            }
            else if (button != null)
            {
                button.OnDeselect(null);
                button = null;
            }
        }
        else if (button != null)
        {
            button.OnDeselect(null);
            button = null;
        }
        else
        {
            lineRenderer.SetPosition(0, Vector3.zero);
            lineRenderer.SetPosition(1, Vector3.zero);
        }
    }
}