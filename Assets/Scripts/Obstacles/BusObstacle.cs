using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class BusObstacle : Obstacle
{
    [Tooltip("-1 = random lane, 0 = left, 1 = middle, 2 = right.")]
    public int laneIndex = -1;
    [Tooltip("Optional local offset after lane placement.")]
    public Vector3 localOffset = Vector3.zero;

    [Header("Ride On Bus")]
    [Tooltip("If enabled, hitting this obstacle starts riding on it instead of losing health.")]
    public bool rideableOnContact = true;
    [Tooltip("Fallback character local Y height while riding. Used if collider top is lower than this or no collider is passed.")]
    public float rideHeight = 1.8f;
    [Tooltip("Use the touched mesh/collider bounds to calculate the ride height. Good for mesh colliders.")]
    public bool useColliderTopAsRideHeight = true;
    [Tooltip("Small extra height above the touched collider top so the player does not clip into the mesh.")]
    public float rideHeightOffset = 0.08f;
    [Tooltip("How far forward the player keeps riding after touching the bus.")]
    public float rideDistance = 12.0f;

    public bool TryStartRide(CharacterInputController controller, Collider sourceCollider)
    {
        if (!rideableOnContact || controller == null)
            return false;

        controller.BeginRideSurface(null, CalculateRideHeight(controller, sourceCollider), rideDistance);
        return true;
    }

    float CalculateRideHeight(CharacterInputController controller, Collider sourceCollider)
    {
        float height = Mathf.Max(0.0f, rideHeight);

        if (useColliderTopAsRideHeight && sourceCollider != null)
        {
            float colliderHeight = sourceCollider.bounds.max.y - controller.transform.position.y + rideHeightOffset;
            height = Mathf.Max(height, colliderHeight);
        }

        return height;
    }

    public override IEnumerator Spawn(TrackSegment segment, float t)
    {
        Vector3 position;
        Quaternion rotation;
        segment.GetPointAt(t, out position, out rotation);

        GameObject obj = Instantiate(gameObject, position, rotation);
        obj.name = gameObject.name;

        int lane = laneIndex < 0 ? Random.Range(0, 3) : Mathf.Clamp(laneIndex, 0, 2);
        obj.transform.position += obj.transform.right * ((lane - 1) * segment.manager.laneOffset);
        obj.transform.position += obj.transform.TransformDirection(localOffset);
        obj.transform.SetParent(segment.objectRoot, true);

        //TODO : remove that hack related to #issue7
        Vector3 oldPos = obj.transform.position;
        obj.transform.position += Vector3.back;
        obj.transform.position = oldPos;

        yield break;
    }
}