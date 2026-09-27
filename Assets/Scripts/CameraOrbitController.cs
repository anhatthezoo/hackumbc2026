using System;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public enum BuildViewSide
{
    NearSide,
    FarSide
}

[RequireComponent(typeof(Camera))]
public class CameraOrbitController : MonoBehaviour
{
    [Header("Orbit Target")]
    [SerializeField] private Transform orbitTarget;
    [SerializeField] private Vector3 focusOffset = new Vector3(0f, 0.75f, 0f);

    [Header("Build Views")]
    [SerializeField] private float nearSideYaw = -35f;
    [SerializeField] private float farSideYaw = 145f;
    [SerializeField] private float topDownPitch = 32f;
    [SerializeField, Min(1f)] private float viewTransitionSpeed = 480f;

    [Header("Zoom")]
    [SerializeField, Min(0.01f)] private float zoomSensitivity = 0.01f;
    [SerializeField, Min(1f)] private float minimumZoom = 12f;
    [SerializeField, Min(1f)] private float maximumZoom = 42f;

    private Camera orbitCamera;
    private BuildViewSide currentSide = BuildViewSide.NearSide;
    private float yaw;
    private float pitch;
    private float targetYaw;
    private float targetPitch;
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
                SetBuildSide(currentSide, true);
            }
        }
    }

    public BuildViewSide CurrentSide => currentSide;

    private void Awake()
    {
        orbitCamera = GetComponent<Camera>();
        InitializeFromCurrentView();
        SetBuildSide(BuildViewSide.NearSide, true);
    }

    private void LateUpdate()
    {
        Mouse mouse = Mouse.current;
        float scroll = mouse == null ? 0f : mouse.scroll.ReadValue().y;

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

        float step = viewTransitionSpeed * Time.unscaledDeltaTime;
        yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, step);
        pitch = Mathf.MoveTowardsAngle(pitch, targetPitch, step);
        ApplyOrbit();
    }

    public void FlipBuildSide()
    {
        SetBuildSide(
            currentSide == BuildViewSide.NearSide
                ? BuildViewSide.FarSide
                : BuildViewSide.NearSide);
    }

    public void SetBuildSide(BuildViewSide side, bool instant = false)
    {
        currentSide = side;
        targetYaw = side == BuildViewSide.NearSide ? nearSideYaw : farSideYaw;
        targetPitch = topDownPitch;

        if (instant)
        {
            yaw = targetYaw;
            pitch = targetPitch;
            ApplyOrbit();
        }
    }

    private void InitializeFromCurrentView()
    {
        Vector3 offset = transform.position - FocusPosition;
        distance = Mathf.Max(offset.magnitude, minimumZoom);

        if (offset.sqrMagnitude < 0.001f)
        {
            yaw = nearSideYaw;
            pitch = topDownPitch;
        }
        else
        {
            yaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
            pitch = Mathf.Asin(offset.y / distance) * Mathf.Rad2Deg;
        }

        targetYaw = yaw;
        targetPitch = pitch;
    }

    private void ApplyOrbit()
    {
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position =
            FocusPosition + orbitRotation * new Vector3(0f, 0f, -distance);
        transform.LookAt(FocusPosition, Vector3.up);
    }
}
