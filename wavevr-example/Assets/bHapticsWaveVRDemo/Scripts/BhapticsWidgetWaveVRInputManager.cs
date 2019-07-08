using UnityEngine;


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
        SetInputModule();
    }







    private void SetInputModule()
    {
        WaveVR_ControllerLoader leftController = null, rightController = null;
        var controllerLoaders = FindObjectsOfType<WaveVR_ControllerLoader>();
        foreach (var controller in controllerLoaders)
        {
            if (controller.WhichHand == WaveVR_ControllerLoader.ControllerHand.Dominant)
            {
                rightController = controller;
            }
            else if (controller.WhichHand == WaveVR_ControllerLoader.ControllerHand.Non_Dominant)
            {
                leftController = controller;
            }
        }

        var leftInputObject = new GameObject("[Left Input]");
        var rightInputObject = new GameObject("[Right Input]");

        leftInputObject.AddComponent<BhapticsWidgetWaveVRInputModule>().deviceType = wvr.WVR_DeviceType.WVR_DeviceType_Controller_Left;
        rightInputObject.AddComponent<BhapticsWidgetWaveVRInputModule>().deviceType = wvr.WVR_DeviceType.WVR_DeviceType_Controller_Right;

        leftInputObject.transform.parent = leftController == null ? transform : leftController.transform;
        rightInputObject.transform.parent = rightController == null ? transform : rightController.transform;

        leftInputObject.transform.localPosition = leftPositionOffset;
        leftInputObject.transform.localRotation = Quaternion.Euler(leftRotataionOffset);

        rightInputObject.transform.localPosition = rightPositionOffset;
        rightInputObject.transform.localRotation = Quaternion.Euler(rightRotataionOffset);
    }
}