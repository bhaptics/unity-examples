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






    void Update()
    {
        WaveVRInputForShoot();
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
}