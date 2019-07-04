using UnityEngine;
using Bhaptics.Tact;
using Bhaptics.Tact.Unity;

public class BhapticsVisualFeedbackOnMotors : MonoBehaviour
{
    [SerializeField] private PositionType tactPositionType = PositionType.Vest;
    [SerializeField] private GameObject visualMotorsObject;
    [SerializeField] private Gradient hapticColor;




    private BhapticsManager bhapticsManager;
    private GameObject[] visualMotors;





    void Start()
    {
        bhapticsManager = FindObjectOfType<BhapticsManager>();

        if (visualMotorsObject == null)
        {
            Debug.LogError("BhapticsVisualFeedbackOnMotors.cs / visualMotorsObject is null");
            return;
        }
        visualMotors = new GameObject[visualMotorsObject.transform.childCount];
        for (int i = 0; i < visualMotorsObject.transform.childCount; ++i)
        {
            visualMotors[i] = visualMotorsObject.transform.GetChild(i).gameObject;
        }
    }

    void Update()
    {
        if (visualMotors == null || bhapticsManager == null)
        {
            return;
        }
        //ShowHapticFeedbackOnMotors(feedback);
    }




    public void ShowHapticFeedbackOnMotors(HapticFeedback feedback)
    {
        if (visualMotors == null)
        {
            return;
        }

        for (int i = 0; i < visualMotors.Length; i++)
        {
            var motor = visualMotors[i];
            var power = feedback.Values[i] / 100f;
            var meshRenderer = motor.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                //meshRenderer.material.color = new Color(0.8f + power * 0.2f, 0.8f + power * 0.01f, 0.8f - power * 0.79f, 1f);
                meshRenderer.material.color = hapticColor.Evaluate(power);
            }
        }
    }
}
