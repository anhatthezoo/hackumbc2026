using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RoyaltyBoat.Settings
{
    public enum VisualAntiAliasing
    {
        Off,
        Fxaa,
        Smaa
    }

    public static class VisualSettings
    {
        private const string FullscreenPreference = "settings.fullscreen";
        private const string ResolutionWidthPreference = "settings.resolution-width";
        private const string ResolutionHeightPreference = "settings.resolution-height";
        private const string VSyncPreference = "settings.vsync";
        private const string AntiAliasingPreference = "settings.anti-aliasing";
        private const string CameraShakePreference = "settings.camera-shake";

        private static readonly List<Vector2Int> resolutionOptions = new List<Vector2Int>();

        public static bool Fullscreen { get; private set; }
        public static bool VSync { get; private set; }
        public static VisualAntiAliasing AntiAliasing { get; private set; }
        public static float CameraShakeIntensity { get; private set; }
        public static string ResolutionLabel => $"{CurrentResolution.x} × {CurrentResolution.y}";
        public static Vector2Int CurrentResolution { get; private set; }
        public static int ResolutionCount => resolutionOptions.Count;
        public static int AntiAliasingCount => 3;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            resolutionOptions.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            BuildResolutionOptions();

            Fullscreen = PlayerPrefs.GetInt(
                FullscreenPreference,
                Screen.fullScreen ? 1 : 0) != 0;
            VSync = PlayerPrefs.GetInt(VSyncPreference, QualitySettings.vSyncCount > 0 ? 1 : 0) != 0;
            AntiAliasing = (VisualAntiAliasing)Mathf.Clamp(
                PlayerPrefs.GetInt(AntiAliasingPreference, (int)VisualAntiAliasing.Smaa),
                0,
                (int)VisualAntiAliasing.Smaa);
            CameraShakeIntensity = Mathf.Clamp01(
                PlayerPrefs.GetFloat(CameraShakePreference, 0.65f));
            CurrentResolution = ResolveSavedResolution();

            ApplyDisplayMode();
            QualitySettings.vSyncCount = VSync ? 1 : 0;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public static void SetFullscreen(bool enabled)
        {
            Fullscreen = enabled;
            PlayerPrefs.SetInt(FullscreenPreference, enabled ? 1 : 0);
            ApplyDisplayMode();
            Save();
        }

        public static string GetResolutionLabel(int index)
        {
            return index >= 0 && index < resolutionOptions.Count
                ? $"{resolutionOptions[index].x} × {resolutionOptions[index].y}"
                : string.Empty;
        }

        public static int GetCurrentResolutionIndex()
        {
            return Mathf.Max(0, resolutionOptions.IndexOf(CurrentResolution));
        }

        public static string GetAntiAliasingLabel(int index)
        {
            return index >= 0 && index < AntiAliasingCount
                ? ((VisualAntiAliasing)index).ToString().ToUpperInvariant()
                : string.Empty;
        }

        public static void SetResolution(int index)
        {
            if (index < 0 || index >= resolutionOptions.Count)
            {
                return;
            }

            CurrentResolution = resolutionOptions[index];
            PlayerPrefs.SetInt(ResolutionWidthPreference, CurrentResolution.x);
            PlayerPrefs.SetInt(ResolutionHeightPreference, CurrentResolution.y);
            ApplyDisplayMode();
            Save();
        }

        public static void SetVSync(bool enabled)
        {
            VSync = enabled;
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            PlayerPrefs.SetInt(VSyncPreference, enabled ? 1 : 0);
            Save();
        }

        public static void SetAntiAliasing(int index)
        {
            if (index < 0 || index >= AntiAliasingCount)
            {
                return;
            }

            AntiAliasing = (VisualAntiAliasing)index;
            PlayerPrefs.SetInt(AntiAliasingPreference, (int)AntiAliasing);
            ApplyAntiAliasing();
            Save();
        }

        public static void SetCameraShake(float intensity)
        {
            CameraShakeIntensity = Mathf.Clamp01(intensity);
            PlayerPrefs.SetFloat(CameraShakePreference, CameraShakeIntensity);
            Save();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyAntiAliasing();
        }

        private static void ApplyAntiAliasing()
        {
            AntialiasingMode mode = AntiAliasing switch
            {
                VisualAntiAliasing.Fxaa => AntialiasingMode.FastApproximateAntialiasing,
                VisualAntiAliasing.Smaa => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                _ => AntialiasingMode.None
            };

            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera camera in cameras)
            {
                UniversalAdditionalCameraData cameraData =
                    camera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData != null)
                {
                    cameraData.antialiasing = mode;
                }
            }
        }

        private static void BuildResolutionOptions()
        {
            resolutionOptions.Clear();
            HashSet<Vector2Int> unique = new HashSet<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions)
            {
                Vector2Int size = new Vector2Int(resolution.width, resolution.height);
                if (size.x >= 1024 && size.y >= 576 && unique.Add(size))
                {
                    resolutionOptions.Add(size);
                }
            }

            if (resolutionOptions.Count == 0)
            {
                resolutionOptions.Add(new Vector2Int(Screen.width, Screen.height));
            }
        }

        private static Vector2Int ResolveSavedResolution()
        {
            Vector2Int saved = new Vector2Int(
                PlayerPrefs.GetInt(ResolutionWidthPreference, Screen.width),
                PlayerPrefs.GetInt(ResolutionHeightPreference, Screen.height));
            if (resolutionOptions.Contains(saved))
            {
                return saved;
            }

            return resolutionOptions[resolutionOptions.Count - 1];
        }

        private static void ApplyDisplayMode()
        {
#if UNITY_EDITOR
            ApplyEditorGameViewResolution();
#else
            FullScreenMode mode = Fullscreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
            Screen.SetResolution(CurrentResolution.x, CurrentResolution.y, mode);
#endif
        }

#if UNITY_EDITOR
        private static void ApplyEditorGameViewResolution()
        {
            System.Reflection.Assembly editorAssembly =
                typeof(UnityEditor.EditorWindow).Assembly;
            System.Type gameViewType = editorAssembly.GetType("UnityEditor.GameView");
            System.Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
            System.Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
            System.Type sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
            if (gameViewType == null || sizesType == null || sizeType == null ||
                sizeKindType == null)
            {
                return;
            }

            System.Type singletonType = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singletonType.GetProperty("instance")?.GetValue(null);
            object group = sizesType.GetProperty("currentGroup")?.GetValue(sizes);
            if (group == null)
            {
                return;
            }

            System.Type groupType = group.GetType();
            System.Reflection.MethodInfo getCount = groupType.GetMethod("GetTotalCount");
            System.Reflection.MethodInfo getSize = groupType.GetMethod("GetGameViewSize");
            System.Reflection.MethodInfo addSize = groupType.GetMethod("AddCustomSize");
            System.Reflection.PropertyInfo widthProperty = sizeType.GetProperty("width");
            System.Reflection.PropertyInfo heightProperty = sizeType.GetProperty("height");
            int count = (int)getCount.Invoke(group, null);
            int selectedIndex = -1;

            for (int index = 0; index < count; ++index)
            {
                object candidate = getSize.Invoke(group, new object[] { index });
                int width = (int)widthProperty.GetValue(candidate);
                int height = (int)heightProperty.GetValue(candidate);
                if (width == CurrentResolution.x && height == CurrentResolution.y)
                {
                    selectedIndex = index;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                object fixedResolution = System.Enum.Parse(sizeKindType, "FixedResolution");
                object customSize = System.Activator.CreateInstance(
                    sizeType,
                    fixedResolution,
                    CurrentResolution.x,
                    CurrentResolution.y,
                    "Royalty Boat");
                addSize.Invoke(group, new[] { customSize });
                selectedIndex = count;
            }

            UnityEditor.EditorWindow gameView = UnityEditor.EditorWindow.GetWindow(gameViewType);
            gameViewType.GetProperty("selectedSizeIndex")?.SetValue(gameView, selectedIndex);
            gameView.maximized = Fullscreen;
            gameView.Repaint();
        }
#endif

        private static void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
