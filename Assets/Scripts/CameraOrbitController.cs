using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraOrbitController : MonoBehaviour
{
    [Header("Orbit Target")]
    [SerializeField] private Transform orbitTarget;
    [SerializeField] private Vector3 focusOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Orbit Controls")]
    [SerializeField, Min(0.01f)] private float orbitSensitivity = 0.2f;
    [SerializeField, Min(0.01f)] private float zoomSensitivity = 0.01f;
    [SerializeField] private float minimumPitch = -75f;
    [SerializeField] private float maximumPitch = 85f;
    [Tooltip("Starting orthographic camera size. Lower values appear closer.")]
    [SerializeField, Min(0.1f)] private float startingOrthographicSize = 9f;
    [SerializeField, Min(1f)] private float minimumZoom = 4f;
    [SerializeField, Min(1f)] private float maximumZoom = 12f;

    private Camera orbitCamera;
    private float yaw;
    private float pitch;
    private float distance;

    private Vector3 FocusPosition =>
        (orbitTarget != null ? orbitTarget.position : Vector3.zero) + focusOffset;

    public Transform OrbitTarget
    {
        get => orbitTarget;
        set
        {
            orbitTarget = value;

            if (isActiveAndEnabled)
            {
                InitializeFromCurrentView();
            }
        }
    }

    private void Awake()
    {
        orbitCamera = GetComponent<Camera>();

        if (orbitCamera.orthographic)
        {
            orbitCamera.orthographicSize = Mathf.Clamp(
                startingOrthographicSize,
                minimumZoom,
                maximumZoom);
        }

        InitializeFromCurrentView();
    }

    private void OnValidate()
    {
        maximumZoom = Mathf.Max(maximumZoom, minimumZoom);
        startingOrthographicSize = Mathf.Clamp(
            startingOrthographicSize,
            minimumZoom,
            maximumZoom);

        Camera cameraComponent = GetComponent<Camera>();

        if (!Application.isPlaying
            && cameraComponent != null
            && cameraComponent.orthographic)
        {
            cameraComponent.orthographicSize = startingOrthographicSize;
        }
    }

    private void LateUpdate()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        if (mouse.rightButton.isPressed)
        {
            Vector2 pointerDelta = mouse.delta.ReadValue();
            yaw += pointerDelta.x * orbitSensitivity;
            pitch = Mathf.Clamp(
                pitch - pointerDelta.y * orbitSensitivity,
                minimumPitch,
                maximumPitch);
        }

        float scroll = mouse.scroll.ReadValue().y;

        if (!Mathf.Approximately(scroll, 0f))
        {
            if (orbitCamera.orthographic)
            {
                orbitCamera.orthographicSize = Mathf.Clamp(
                    orbitCamera.orthographicSize - scroll * zoomSensitivity,
                    minimumZoom,
                    maximumZoom);
            }
            else
            {
                distance = Mathf.Clamp(
                    distance - scroll * zoomSensitivity,
                    minimumZoom,
                    maximumZoom);
            }
        }

        ApplyOrbit();
    }

    private void InitializeFromCurrentView()
    {
        Vector3 offset = transform.position - FocusPosition;
        float currentDistance = offset.magnitude;
        distance = Mathf.Clamp(
            currentDistance,
            minimumZoom,
            maximumZoom);

        if (currentDistance < 0.001f)
        {
            yaw = 0f;
            pitch = 45f;
            return;
        }

        yaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
        float verticalDirection = Mathf.Clamp(
            offset.y / currentDistance,
            -1f,
            1f);
        pitch = Mathf.Asin(verticalDirection) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);
    }

    private void ApplyOrbit()
    {
        if (!float.IsFinite(distance)
            || !float.IsFinite(yaw)
            || !float.IsFinite(pitch))
        {
            InitializeFromCurrentView();
        }

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = FocusPosition + orbitRotation * new Vector3(0f, 0f, -distance);
        transform.LookAt(FocusPosition, Vector3.up);
    }
}
