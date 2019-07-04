using System.Collections;
using UnityEngine;



public class BhapticsShootingCharacter : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform shootPointLeft, shootPointRight;
    [SerializeField] private GameObject bulletPrefab;
    [Space]
    [SerializeField] private Transform[] lookingTransforms;




    private Animator animator;
    private int shootCount = 4;
    private float shootDelay = 2f;
    private float bulletSpeed = 2.5f;






    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        Looking();
    }

    void OnEnable()
    {
        StartCoroutine(RepeatShoot());
    }

    void OnDisable()
    {
        StopAllCoroutines();
    }







    private IEnumerator RepeatShoot()
    {
        yield return new WaitForSeconds(shootDelay);
        animator.SetTrigger("shoot");
        StartCoroutine(RepeatShoot());
    }

    private void ShootTarget(int hand)
    {
        if (target == null)
        {
            Debug.LogError("Target is null");
            return;
        }
        var shootPoint = hand == 0 ? shootPointLeft : shootPointRight;
        var bullet = Instantiate(bulletPrefab, shootPoint.position, Quaternion.identity);
        bullet.transform.forward = (target.position - bullet.transform.position).normalized;
        var rigid = bullet.GetComponent<Rigidbody>();
        if (rigid != null)
        {
            rigid.velocity = bullet.transform.forward * bulletSpeed;
        }
    }

    private void Looking()
    {
        if (target == null)
        {
            Debug.LogError("Target is null");
            return;
        }
        foreach (var look in lookingTransforms)
        {
            if (look == transform)
            {
                look.transform.LookAt(new Vector3(target.position.x, transform.position.y, target.position.z));
            }
            else
            {
                look.transform.LookAt(target);
            }
        }
    }
}