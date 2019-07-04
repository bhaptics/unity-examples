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




    private float bulletSpeed = 5f;





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