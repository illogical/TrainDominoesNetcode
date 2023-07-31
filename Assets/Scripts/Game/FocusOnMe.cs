using UnityEngine;

public class FocusOnMe : MonoBehaviour
{
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
        Focus();
    }

    public void Focus()
    {
        Bounds objectBounds = GetComponent<Renderer>().bounds;
        float distance = CalculateDesiredDistance(objectBounds.size.x, objectBounds.size.y, objectBounds.size.z);

        // Set the camera's position to the adjusted distance
        Vector3 targetPosition = mainCamera.transform.position + distance * mainCamera.transform.forward;
        transform.position = targetPosition;
        Debug.Log($"Sent to position {targetPosition}");
    }

    /// <summary>
    /// This will work with a line. Pass in all dimensions and it will decide which is the longest and zoom to it
    /// </summary>
    /// <param name="sizeX"></param>
    /// <param name="sizeY"></param>
    /// <param name="sizeZ"></param>
    /// <returns></returns>
    private float CalculateDesiredDistance(float sizeX, float sizeY, float sizeZ)
    {
        // Calculate the desired distance based on the object's size
        float objectSize = Mathf.Max(sizeX, sizeY, sizeZ);
        float desiredDistance = objectSize / (2.0f * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));

        return desiredDistance;
    }
}
