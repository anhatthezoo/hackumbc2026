using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RoyaltyBoat.Audio
{
    /// <summary>
    /// Central runtime sound service. Clips live in Resources so generated scenes and
    /// code-created obstacles can use the same mix without prefab references.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private enum Sound
        {
            IceCrash,
            IcebergBreak,
            Fire,
            Splash,
            Rain,
            Lightning,
            Click,
            Success,
            Failure,
            Ocean,
            BlockPlace
        }

        private readonly struct SoundDefinition
        {
            public SoundDefinition(string resource, float volume, float startTime)
            {
                Resource = resource;
                Volume = volume;
                StartTime = startTime;
            }

            public string Resource { get; }
            public float Volume { get; }
            public float StartTime { get; }
        }

        private const string ResourceRoot = "Audio/SFX/";
        private static readonly Dictionary<Sound, SoundDefinition> Definitions =
            new Dictionary<Sound, SoundDefinition>
            {
                { Sound.IceCrash, new SoundDefinition("IceCrash", 0.65f, 0.59f) },
                { Sound.IcebergBreak, new SoundDefinition("IcebergBreak", 1f, 0.29f) },
                { Sound.Fire, new SoundDefinition("FireLoop", 0.36f, 0f) },
                { Sound.Splash, new SoundDefinition("WaterSplash", 0.7f, 0.18f) },
                { Sound.Rain, new SoundDefinition("RainLoop", 0.85f, 0.66f) },
                { Sound.Lightning, new SoundDefinition("LightningCrash", 0.35f, 0.01f) },
                { Sound.Click, new SoundDefinition("ButtonClick", 0.75f, 0.05f) },
                { Sound.Success, new SoundDefinition("Success", 0.38f, 0.07f) },
                { Sound.Failure, new SoundDefinition("Failure", 0.38f, 0f) },
                { Sound.Ocean, new SoundDefinition("OceanLoop", 1f, 0f) },
                { Sound.BlockPlace, new SoundDefinition("BlockPlace", 0.42f, 0f) }
            };

        private static readonly HashSet<VisualElement> BoundUiRoots =
            new HashSet<VisualElement>();
        private static GameAudio instance;

        private readonly Dictionary<Sound, AudioClip> clips =
            new Dictionary<Sound, AudioClip>();
        private readonly Dictionary<Object, AudioSource> fireSources =
            new Dictionary<Object, AudioSource>();
        private readonly HashSet<Object> rainOwners = new HashSet<Object>();
        private readonly HashSet<Object> oceanOwners = new HashSet<Object>();
        private AudioSource rainSource;
        private AudioSource oceanSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static void PlayIceCrash(Vector3 position) =>
            EnsureInstance().PlayWorldOneShot(Sound.IceCrash, position);

        public static void PlayIcebergBreak(Vector3 position) =>
            EnsureInstance().PlayWorldOneShot(Sound.IcebergBreak, position);

        public static void PlaySplash(Vector3 position) =>
            EnsureInstance().PlayWorldOneShot(Sound.Splash, position);

        public static void PlayLightning(Vector3 position) =>
            EnsureInstance().PlayWorldOneShot(Sound.Lightning, position);

        public static void PlaySuccess() => EnsureInstance().PlayUiOneShot(Sound.Success);
        public static void PlayFailure() => EnsureInstance().PlayUiOneShot(Sound.Failure);

        public static void PlayBlockPlaced(Vector3 position) =>
            EnsureInstance().PlayWorldOneShot(
                Sound.BlockPlace,
                position,
                Random.Range(0.94f, 1.07f));

        public static void SetOcean(Object owner, bool active)
        {
            if (owner == null)
            {
                return;
            }

            GameAudio audio = EnsureInstance();
            if (active)
            {
                audio.oceanOwners.Add(owner);
            }
            else
            {
                audio.oceanOwners.Remove(owner);
            }

            audio.RefreshOcean();
        }

        public static void SetRain(Object owner, bool active)
        {
            if (owner == null)
            {
                return;
            }

            GameAudio audio = EnsureInstance();
            if (active)
            {
                audio.rainOwners.Add(owner);
            }
            else
            {
                audio.rainOwners.Remove(owner);
            }

            audio.RefreshRain();
        }

        public static void SetFire(Object owner, Transform followTarget, bool active)
        {
            if (owner == null)
            {
                return;
            }

            GameAudio audio = EnsureInstance();
            if (!active || followTarget == null)
            {
                if (audio.fireSources.TryGetValue(owner, out AudioSource oldSource))
                {
                    if (oldSource != null)
                    {
                        Destroy(oldSource.gameObject);
                    }

                    audio.fireSources.Remove(owner);
                }

                return;
            }

            if (audio.fireSources.TryGetValue(owner, out AudioSource existing) &&
                existing != null)
            {
                return;
            }

            AudioSource source = audio.CreateLoopSource(
                Sound.Fire,
                "Oil Fire Audio",
                followTarget,
                1f,
                8f,
                55f);
            audio.fireSources[owner] = source;
        }

        public static void BindUi(VisualElement root)
        {
            if (root == null || !BoundUiRoots.Add(root))
            {
                return;
            }

            root.RegisterCallback<ClickEvent>(OnUiClicked, TrickleDown.TrickleDown);
        }

        private static void OnUiClicked(ClickEvent clickEvent)
        {
            VisualElement clickedElement = clickEvent.target as VisualElement;
            Button button = clickedElement as Button ??
                            clickedElement?.GetFirstAncestorOfType<Button>();
            if (button != null && button.enabledInHierarchy)
            {
                EnsureInstance().PlayUiOneShot(Sound.Click);
            }
        }

        private static GameAudio EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindAnyObjectByType<GameAudio>();
            if (instance != null)
            {
                return instance;
            }

            GameObject audioObject = new GameObject("Game Audio");
            instance = audioObject.AddComponent<GameAudio>();
            DontDestroyOnLoad(audioObject);
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void RefreshRain()
        {
            if (rainOwners.Count > 0)
            {
                if (rainSource == null)
                {
                    rainSource = CreateLoopSource(
                        Sound.Rain,
                        "Rain Audio",
                        transform,
                        0f,
                        1f,
                        500f);
                }
            }
            else if (rainSource != null)
            {
                Destroy(rainSource.gameObject);
                rainSource = null;
            }
        }

        private void RefreshOcean()
        {
            if (oceanOwners.Count > 0)
            {
                if (oceanSource == null)
                {
                    oceanSource = CreateLoopSource(
                        Sound.Ocean,
                        "Ocean Ambience",
                        transform,
                        0f,
                        1f,
                        500f);
                }
            }
            else if (oceanSource != null)
            {
                Destroy(oceanSource.gameObject);
                oceanSource = null;
            }
        }

        private void PlayWorldOneShot(
            Sound sound,
            Vector3 position,
            float pitch = 1f)
        {
            AudioClip clip = GetClip(sound);
            if (clip == null)
            {
                return;
            }

            GameObject soundObject = new GameObject(sound + " Audio");
            soundObject.transform.position = position;
            AudioSource source = soundObject.AddComponent<AudioSource>();
            ConfigureSource(source, Definitions[sound], 1f, 7f, 75f);
            source.pitch = pitch;
            source.clip = clip;
            PlayFromOffset(source, Definitions[sound].StartTime);
            float remainingDuration = clip.length - Definitions[sound].StartTime;
            Destroy(
                soundObject,
                Mathf.Max(0.1f, remainingDuration / Mathf.Abs(pitch) + 0.15f));
        }

        private void PlayUiOneShot(Sound sound)
        {
            AudioClip clip = GetClip(sound);
            if (clip == null)
            {
                return;
            }

            GameObject soundObject = new GameObject(sound + " Audio");
            soundObject.transform.SetParent(transform, false);
            AudioSource source = soundObject.AddComponent<AudioSource>();
            ConfigureSource(source, Definitions[sound], 0f, 1f, 500f);
            source.clip = clip;
            PlayFromOffset(source, Definitions[sound].StartTime);
            Destroy(soundObject, Mathf.Max(0.1f, clip.length - Definitions[sound].StartTime + 0.15f));
        }

        private AudioSource CreateLoopSource(
            Sound sound,
            string objectName,
            Transform parent,
            float spatialBlend,
            float minDistance,
            float maxDistance)
        {
            AudioClip clip = GetClip(sound);
            if (clip == null)
            {
                return null;
            }

            GameObject soundObject = new GameObject(objectName);
            soundObject.transform.SetParent(parent, false);
            AudioSource source = soundObject.AddComponent<AudioSource>();
            ConfigureSource(source, Definitions[sound], spatialBlend, minDistance, maxDistance);
            source.clip = clip;
            source.loop = true;
            PlayFromOffset(source, Definitions[sound].StartTime);
            return source;
        }

        private static void ConfigureSource(
            AudioSource source,
            SoundDefinition definition,
            float spatialBlend,
            float minDistance,
            float maxDistance)
        {
            source.playOnAwake = false;
            source.volume = definition.Volume;
            source.spatialBlend = spatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
        }

        private static void PlayFromOffset(AudioSource source, float startTime)
        {
            source.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, source.clip.length - 0.01f));
            source.Play();
        }

        private AudioClip GetClip(Sound sound)
        {
            if (clips.TryGetValue(sound, out AudioClip clip))
            {
                return clip;
            }

            clip = Resources.Load<AudioClip>(ResourceRoot + Definitions[sound].Resource);
            clips[sound] = clip;
            if (clip == null)
            {
                Debug.LogWarning($"Missing audio clip: {Definitions[sound].Resource}", this);
            }

            return clip;
        }
    }
}
