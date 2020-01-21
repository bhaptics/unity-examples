using UnityEngine;
using System.Collections;


public class BhapticsOculusTeleport : MonoBehaviour
{
    [SerializeField] private OVRInput.Button teleportButton = OVRInput.Button.PrimaryIndexTrigger;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private Transform vrCameraTransform;
    [SerializeField] private Material laserMaterial;




    private LineRenderer laser;
    private Vector3 destination;
    private float teleportSpeed = 100f;
    private float cooldownTime = 0.5f;
    private bool hitSomething;
    private bool canTeleport = true;





    void Start()
    {
        SetLaser();
    }

    void OnEnable()
    {
        canTeleport = true;
    }

    void Update()
    {
        OculusInputForTeleport();
    }








    private void SetLaser()
    {
        laser = gameObject.AddComponent<LineRenderer>();
        laser.startWidth = laser.endWidth = 0.015f;
        laser.SetPositions(new Vector3[] { transform.position, transform.position });
        laser.material = laserMaterial;
        laser.enabled = false;
    }

    private void OculusInputForTeleport()
    {
        if (OVRInput.Get(teleportButton))
        {
            ShootRayForTeleport();
        }
        else if (OVRInput.GetUp(teleportButton))
        {
            Teleport();
        }
    }

    private void ShootRayForTeleport()
    {
        if (shootPoint == null)
        {
            laser.enabled = false;
            return;
        }
        RaycastHit raycastHit;
        if (hitSomething = Physics.Raycast(shootPoint.position, shootPoint.forward, out raycastHit, 10f))
        {
            destination = raycastHit.point;
            var bhapticsTeleportPoint = raycastHit.collider.GetComponent<BhapticsTeleportPoint>();
            if (bhapticsTeleportPoint != null)
            {
                destination = bhapticsTeleportPoint.transform.position;
            }
        }
        laser.enabled = true;
        laser.material.color = hitSomething ? Color.green : Color.red;
        laser.SetPosition(0, shootPoint.position);
        laser.SetPosition(1, hitSomething ? raycastHit.point : shootPoint.position + shootPoint.forward * 10f);
    }

    private void Teleport()
    {
        laser.enabled = false;
        if (hitSomething && canTeleport)
        {
            StartCoroutine(TeleportCooldownTime());
            StartCoroutine(TeleportCoroutine());
        }
    }

    private IEnumerator TeleportCoroutine()
    {
        var tempDestination = destination;
        if (vrCameraTransform != null)
        {
            var offset = transform.root.position - vrCameraTransform.position;
            offset.y = 0f;
            tempDestination += offset;
        }
        var positionWithoutY = new Vector3(transform.root.position.x, 0f, transform.root.position.z);
        var destinationWithoutY = new Vector3(tempDestination.x, 0f, tempDestination.z);
        while (0.01f < Vector3.Distance(positionWithoutY, destinationWithoutY))
        {
            transform.root.position = Vector3.MoveTowards(transform.root.position, tempDestination, teleportSpeed * Time.deltaTime);
            positionWithoutY = new Vector3(transform.root.position.x, 0f, transform.root.position.z);
            yield return null;
        }
    }

    private IEnumerator TeleportCooldownTime()
    {
        canTeleport = false;
        yield return new WaitForSeconds(cooldownTime);
        canTeleport = true;
    }
}