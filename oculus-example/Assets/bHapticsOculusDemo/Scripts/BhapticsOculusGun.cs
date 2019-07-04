using Bhaptics.Tact.Unity;
using UnityEngine;


public class BhapticsOculusGun : MonoBehaviour
{
    public enum Hand
    {
        Left, Right
    }

    [SerializeField] private Hand hand = Hand.Left;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private TactSource shootTactSource;




    private float bulletSpeed = 10f;






    void Start()
    {
        if (shootTactSource != null)
        {
            shootTactSource.IsReflectTactosy = hand == Hand.Right;
        }
    }

    void Update()
    {
        OculusInputForShoot();
    }





    private void OculusInputForShoot()
    {
        if (hand == Hand.Left)
        {
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
            {
                Shoot();
            }
        }
        else
        {
            if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
            {
                Shoot();
            }
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
    }
}