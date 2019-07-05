using UnityEngine;


    public class BhapticsWidgetOculusInputManager : MonoBehaviour
    {
        [Header("Left Controller Transform Offset")]
        [SerializeField] private Vector3 leftPositionOffset;
        [SerializeField] private Vector3 leftRotataionOffset;

        [Header("Right Controller Transform Offset")]
        [SerializeField] private Vector3 rightPositionOffset;
        [SerializeField] private Vector3 rightRotataionOffset;




        private OVRCameraRig cameraRig;




        void Start()
        {
            cameraRig = FindObjectOfType<OVRCameraRig>();

            if (cameraRig == null)
            {
                Debug.LogError("OVRCameraRig has not found...");
                return;
            }

            SetInputModule();
        }







        private void SetInputModule()
        {
            var leftAnchor = cameraRig.leftHandAnchor;
            var rightAnchor = cameraRig.rightHandAnchor;

            var leftInputObject = new GameObject("[Left Input]");
            var rightInputObject = new GameObject("[Right Input]");

            leftInputObject.AddComponent<BhapticsWidgetOculusInputModule>().selectButton = OVRInput.Button.PrimaryIndexTrigger;
            rightInputObject.AddComponent<BhapticsWidgetOculusInputModule>().selectButton = OVRInput.Button.SecondaryIndexTrigger;

            leftInputObject.transform.parent = leftAnchor;
            rightInputObject.transform.parent = rightAnchor;

            leftInputObject.transform.localPosition = leftPositionOffset;
            leftInputObject.transform.localRotation = Quaternion.Euler(leftRotataionOffset);

            rightInputObject.transform.localPosition = rightPositionOffset;
            rightInputObject.transform.localRotation = Quaternion.Euler(rightRotataionOffset);
        }
    }