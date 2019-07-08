using Bhaptics.Tact.Unity;
using UnityEngine;
using wvr;


public class BhapticsWaveVRGun : MonoBehaviour
{
    [SerializeField] private WVR_DeviceType deviceType = WVR_DeviceType.WVR_DeviceType_Controller_Left;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private TactSource shootTactSource;




    private float bulletSpeed = 10f;




    void Start()
    {
        Invoke("FindParent", 1f);
    }

    void Update()
    {
        WaveVRInputForShoot();
    }






    private void FindParent()
    {
        var poseTrackers = FindObjectsOfType<WaveVR_ControllerPoseTracker>();
        foreach (var tracker in poseTrackers)
        {
            if (deviceType == WVR_DeviceType.WVR_DeviceType_Controller_Right
                && tracker.Type == WaveVR_Controller.EDeviceType.Dominant)
            {
                Attch(tracker.transform, transform);
            }
            else if (deviceType == WVR_DeviceType.WVR_DeviceType_Controller_Left
                && tracker.Type == WaveVR_Controller.EDeviceType.NonDominant)
            {
                Attch(tracker.transform, transform);
            }
        }
    }

    private void WaveVRInputForShoot()
    {
        if (WaveVR_Controller.Input(deviceType).GetPressDown(WVR_InputId.WVR_InputId_Alias1_Trigger))
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        var bullet = Instantiate(bulletPrefab, shootPoint.position, shootPoint.rotation);
        var rigid = bullet.GetComponent<Rigidbody>();
        if (rigid != null)
        {
            rigid.velocity = bullet.transform.forward * bulletSpeed;
        }
        if (shootTactSource != null)
        {
            shootTactSource.Play();
        }
    }

    private void Attch(Transform parentTransform, Transform childTransform)
    {
        childTransform.parent = parentTransform.transform;
        childTransform.localPosition = Vector3.zero;
        childTransform.localRotation = Quaternion.identity;
        childTransform.localScale = Vector3.one;
    }
}