using UnityEngine;


public class BhapticsVRBody : MonoBehaviour
{
    [SerializeField] private Transform vrCamera;
    [SerializeField] private float distance;






    void Update()
    {
        FollowVRCamera();

    }




    private void FollowVRCamera()
    {
        if (vrCamera == null)
        {
            return;
        }

        transform.position = vrCamera.position - new Vector3(0f, distance, 0f);
        transform.eulerAngles = new Vector3(0f, vrCamera.eulerAngles.y, 0f);
    }
}