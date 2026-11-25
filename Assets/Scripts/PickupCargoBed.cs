using UnityEngine;

// Placement bounds come from the inside edges of the pickup's bed.
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshCollider))]
public sealed class PickupCargoBed : MonoBehaviour
{
    [SerializeField] Vector2 floorMin = new Vector2(-0.98f, -2.98f);
    [SerializeField] Vector2 floorMax = new Vector2(0.98f, -0.40f);

    const float DropHeight = 0.8f;
    const float EdgeAllowance = 0.1f;
    const float Clearance = 0.015f;
    MeshCollider truckBody;

    void Awake() => truckBody = GetComponent<MeshCollider>();

    internal bool TryPlace(BoxSlot box, out Vector3 localPosition, out Quaternion localRotation)
    {
        localPosition = default;
        localRotation = Quaternion.identity;

        Transform cargo = box.transform;
        Vector3 releasedAt = transform.InverseTransformPoint(cargo.position);
        Vector3 forward = transform.InverseTransformDirection(cargo.forward);
        float quarterTurns = Mathf.Round(Mathf.Atan2(forward.x, forward.z) / (Mathf.PI * 0.5f));
        localRotation = Quaternion.Euler(0f, quarterTurns * 90f, 0f);
        Vector3 size = LocalSize(box);
        Vector3 alignedSize = localRotation * size;
        Vector2 half = new Vector2(Mathf.Abs(alignedSize.x), Mathf.Abs(alignedSize.z)) * 0.5f;
        Vector2 min = floorMin + half;
        Vector2 max = floorMax - half;
        if (min.x > max.x || min.y > max.y ||
            releasedAt.x < floorMin.x - EdgeAllowance || releasedAt.x > floorMax.x + EdgeAllowance ||
            releasedAt.z < floorMin.y - EdgeAllowance || releasedAt.z > floorMax.y + EdgeAllowance)
            return false;

        float x = Mathf.Clamp(releasedAt.x, min.x, max.x);
        float z = Mathf.Clamp(releasedAt.z, min.y, max.y);
        var floorRay = new Ray(transform.TransformPoint(new Vector3(x, 3f, z)), -transform.up);
        if (!truckBody.Raycast(floorRay, out RaycastHit hit, 4f * Mathf.Abs(transform.lossyScale.y)))
            return false;

        float bottom = transform.InverseTransformPoint(hit.point).y;
        float releasedBottom = releasedAt.y - size.y * 0.5f;
        if (releasedBottom < bottom - EdgeAllowance || releasedBottom > bottom + DropHeight)
            return false;

        foreach (Transform child in transform)
        {
            if (child == cargo || !child.TryGetComponent(out BoxSlot loaded)) continue;
            Vector3 occupied = child.localRotation * LocalSize(loaded);
            Vector3 center = child.localPosition;
            if (Mathf.Abs(x - center.x) < half.x + Mathf.Abs(occupied.x) * 0.5f + Clearance &&
                Mathf.Abs(z - center.z) < half.y + Mathf.Abs(occupied.z) * 0.5f + Clearance)
                return false;
        }

        localPosition = new Vector3(x, bottom + size.y * 0.5f + 0.005f, z);
        return true;
    }

    Vector3 LocalSize(BoxSlot box)
    {
        Vector3 world = Vector3.Scale(box.boxSize, box.transform.lossyScale);
        Vector3 scale = transform.lossyScale;
        return new Vector3(Mathf.Abs(world.x / scale.x), Mathf.Abs(world.y / scale.y), Mathf.Abs(world.z / scale.z));
    }
}
