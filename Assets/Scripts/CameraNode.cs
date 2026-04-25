using System.Collections.Generic;
using UnityEngine;

public class CameraNode : MonoBehaviour
{
    public Vector2 yawLimits;
    public Vector2 pitchLimits;

    public bool hasYawLimits;

    public List<CameraNode> connectedCams;

    public Collider GetClickLocationCollider() { return GetComponent<Collider>(); }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.2f);

        // Show where the camera is facing
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward);
    }

    public Vector3 getPosition() {  return transform.position; }

    public Vector3 getRotation() { return transform.rotation.eulerAngles; }
}
