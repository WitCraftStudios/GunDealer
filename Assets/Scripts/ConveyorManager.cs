using UnityEngine;
using System.Collections; // Added this for IEnumerator

public class ConveyorManager : MonoBehaviour
{
    public Transform dropPoint;
    public Transform endPoint;
    public float speed = 2f;

    public void DeliverCase(GameObject gunCase)
    {
        if (gunCase == null || dropPoint == null || endPoint == null)
        {
            GameFeedback.Error("The conveyor delivery path is missing.");
            return;
        }

        Transform transitRoot = dropPoint.parent != null ? dropPoint.parent : transform;
        gunCase.transform.SetParent(transitRoot, true);
        gunCase.transform.position = dropPoint.position;
        gunCase.transform.rotation = dropPoint.rotation;
        GunCase caseComponent = gunCase.GetComponent<GunCase>();
        if (caseComponent != null)
        {
            caseComponent.BeginTransit();
        }

        Rigidbody rb = gunCase.GetComponent<Rigidbody>();
        if (rb != null)
        {
            PrepareRigidbody(rb, true);
        }

        StartCoroutine(MoveCase(gunCase, transitRoot));
    }

    IEnumerator MoveCase(GameObject gunCase, Transform transitRoot)
    {
        float t = 0;
        Vector3 start = transitRoot.InverseTransformPoint(dropPoint.position);
        Vector3 end = transitRoot.InverseTransformPoint(endPoint.position);
        Quaternion transitRotation = Quaternion.Inverse(transitRoot.rotation) * dropPoint.rotation;
        float distance = Vector3.Distance(start, end);
        if (distance <= 0.01f)
        {
            gunCase.transform.localPosition = end;
            gunCase.transform.localRotation = transitRotation;
            FinishDelivery(gunCase);
            yield break;
        }

        while (t < 1)
        {
            t += Time.deltaTime * speed / distance;
            gunCase.transform.localPosition = Vector3.Lerp(start, end, t);
            gunCase.transform.localRotation = transitRotation;
            yield return null;
        }

        FinishDelivery(gunCase);
    }

    void FinishDelivery(GameObject gunCase)
    {
        Destroy(gunCase);
        if (RewardManager.Instance != null) RewardManager.Instance.GiveReward();
    }

    void PrepareRigidbody(Rigidbody rb, bool makeKinematic)
    {
        bool wasKinematic = rb.isKinematic;
        if (wasKinematic)
        {
            rb.isKinematic = false;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = makeKinematic;
    }
}
