using UnityEngine;

public class BillboardToCamera : MonoBehaviour
{
    void LateUpdate()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        transform.LookAt(
            transform.position + mainCamera.transform.rotation * Vector3.forward,
            mainCamera.transform.rotation * Vector3.up);
    }
}
