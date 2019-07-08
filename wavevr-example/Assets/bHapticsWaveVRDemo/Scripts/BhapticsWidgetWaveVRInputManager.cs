using UnityEngine;
using wvr;

public class BhapticsWidgetWaveVRInputManager : MonoBehaviour
{
    [Header("Left Controller Transform Offset")]
    [SerializeField] private Vector3 leftPositionOffset;
    [SerializeField] private Vector3 leftRotataionOffset;

    [Header("Right Controller Transform Offset")]
    [SerializeField] private Vector3 rightPositionOffset;
    [SerializeField] private Vector3 rightRotataionOffset;








    void Start()
    {
        Invoke("SetInputModule", 1f);
    }




    private void SetInputModule()
    {
        WaveVR_ControllerPoseTracker leftController = null, rightController = null;
        var poseTrackers = FindObjectsOfType<WaveVR_ControllerPoseTracker>();
        if (poseTrackers.Length == 0)
        {
            Invoke("SetInputModule", 1f);
            return;
        }

        foreach (var pt in poseTrackers)
        {
            if (pt.Type == WaveVR_Controller.EDeviceType.Dominant)
            {
                rightController = pt;
            }
            else if (pt.Type == WaveVR_Controller.EDeviceType.NonDominant)
            {
                leftController = pt;
            }
        }

        if (rightController != null)
        {
            var rightInputObject = new GameObject("[Right Input]");
            rightInputObject.AddComponent<BhapticsWidgetWaveVRInputModule>().deviceType = wvr.WVR_DeviceType.WVR_DeviceType_Controller_Right;
            rightInputObject.transform.parent = rightController.transform;
            rightInputObject.transform.localPosition = rightPositionOffset;
            rightInputObject.transform.localRotation = Quaternion.Euler(rightRotataionOffset);
        }
        if (leftController != null)
        {
            var leftInputObject = new GameObject("[Left Input]");
            leftInputObject.AddComponent<BhapticsWidgetWaveVRInputModule>().deviceType = wvr.WVR_DeviceType.WVR_DeviceType_Controller_Left;
            leftInputObject.transform.parent = leftController.transform;
            leftInputObject.transform.localPosition = leftPositionOffset;
            leftInputObject.transform.localRotation = Quaternion.Euler(leftRotataionOffset);
        }
    }
}