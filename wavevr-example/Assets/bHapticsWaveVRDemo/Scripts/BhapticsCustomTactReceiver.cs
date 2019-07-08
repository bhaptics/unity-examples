﻿using Bhaptics.Tact.Unity;
using UnityEngine;



public class BhapticsCustomTactReceiver : MonoBehaviour
{
    public bool IsActive = true;
    public PositionTag PositionTag = PositionTag.Body;

    void Awake()
    {
        var col = GetComponent<Collider>();

        if (col == null)
        {
            Debug.Log("collider is not detected");
        }
    }

    void OnTriggerEnter(Collider bullet)
    {
        if (IsActive)
        {
            Handle(bullet.transform.position, bullet.GetComponent<TactSender>());
        }

    }

    void OnCollisionEnter(Collision bullet)
    {
        if (IsActive)
        {
            ReflectHandle(bullet.contacts[0].point, bullet.gameObject.GetComponent<TactSender>());
        }
    }

    private void Handle(Vector3 contactPoint, TactSender tactSender)
    {
        if (tactSender != null)
        {
            var targetCollider = GetComponent<Collider>();
            tactSender.Play(PositionTag, contactPoint, targetCollider);
        }
    }

    private void ReflectHandle(Vector3 contactPoint, TactSender tactSender)
    {
        if (tactSender != null)
        {
            var targetCollider = GetComponent<Collider>();
            contactPoint += new Vector3(0f, 0f, (targetCollider.transform.position.z - contactPoint.z) * 2f);
            tactSender.Play(PositionTag, contactPoint, targetCollider);
        }
    }
}