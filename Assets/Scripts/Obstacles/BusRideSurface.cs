using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BusRideSurface : MonoBehaviour
{
    [Tooltip("Character local Y height while riding on this surface.")]
    public float rideHeight = 1.8f;
    [Tooltip("Use the touched mesh/collider bounds to calculate ride height. Good for mesh colliders.")]
    public bool useColliderTopAsRideHeight = true;
    [Tooltip("Small extra height above the touched collider top so the player does not clip into the mesh.")]
    public float rideHeightOffset = 0.08f;
    [Tooltip("How far forward the player can ride after entering this surface.")]
    public float rideDistance = 12.0f;
    [Tooltip("If enabled, touching an Obstacle-layer collider on this bus starts riding instead of taking damage.")]
    public bool startRideFromObstacleCollision = true;
    [Tooltip("End the ride when the player leaves this trigger. Usually keep this off for fast runners.")]
    public bool endOnExit = false;

    void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    public bool TryStartRideFromObstacle(CharacterInputController controller, Collider sourceCollider)
    {
        if (!startRideFromObstacleCollision)
            return false;

        StartRide(controller, sourceCollider);
        return true;
    }

    public void StartRide(CharacterInputController controller, Collider sourceCollider)
    {
        if (controller == null)
            return;

        controller.BeginRideSurface(this, CalculateRideHeight(controller, sourceCollider), rideDistance);
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

    void OnTriggerEnter(Collider other)
    {
        CharacterCollider characterCollider = other.GetComponent<CharacterCollider>();
        if (characterCollider != null && characterCollider.controller != null)
        {
            StartRide(characterCollider.controller, GetComponent<Collider>());
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!endOnExit)
            return;

        CharacterCollider characterCollider = other.GetComponent<CharacterCollider>();
        if (characterCollider != null && characterCollider.controller != null)
        {
            characterCollider.controller.EndRideSurface();
        }
    }
}