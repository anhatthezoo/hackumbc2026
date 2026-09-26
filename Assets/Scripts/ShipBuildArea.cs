using UnityEngine;

public sealed class ShipBuildArea : MonoBehaviour
{
    [SerializeField] private Transform platform;
    [SerializeField] private AdaptiveBuildGrid buildGrid;
    [SerializeField] private Ship startingShip;

    public Transform Platform => platform;
    public AdaptiveBuildGrid BuildGrid => buildGrid;
    public Ship StartingShip => startingShip;

    private void Awake()
    {
        ConfigureCamera(Camera.main);
    }

    public void ConfigureCamera(Camera targetCamera)
    {
        if (targetCamera == null || platform == null)
        {
            return;
        }

        BlockDragController dragController =
            targetCamera.GetComponent<BlockDragController>();

        if (dragController == null)
        {
            dragController =
                targetCamera.gameObject.AddComponent<BlockDragController>();
        }

        dragController.PlacementCenter = platform;

        CameraOrbitController orbitController =
            targetCamera.GetComponent<CameraOrbitController>();

        if (orbitController == null)
        {
            orbitController =
                targetCamera.gameObject.AddComponent<CameraOrbitController>();
        }

        orbitController.OrbitTarget = platform;

        if (buildGrid != null)
        {
            buildGrid.CenterTarget = platform;
            buildGrid.ViewCamera = targetCamera;
        }
    }
}
