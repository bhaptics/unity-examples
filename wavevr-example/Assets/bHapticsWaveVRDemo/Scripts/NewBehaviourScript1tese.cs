using UnityEngine;
using System.Collections;

public class NewBehaviourScript1tese : MonoBehaviour
{
    [SerializeField] private GameObject contactPoint;
    [SerializeField] private GameObject normal;


    void Start()
    {
        
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Reflect();
        }
    }


    private void Reflect()
    {
        contactPoint.transform.position += new Vector3(0f, 0f, (normal.transform.position.z - contactPoint.transform.position.z) * 2f);
    }
}
