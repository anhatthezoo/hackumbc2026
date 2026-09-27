using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class AudioController : MonoBehaviour
{
    private const string DefaultMusicPath = "Audio/Music/BackgroundMusic";

    public static AudioController audioController { get; private set; }

    [SerializeField] private AudioClip backgroundAudio;

    private AudioSource audioSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        audioController = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureControllerExists()
    {
        if (FindAnyObjectByType<AudioController>() != null)
        {
            return;
        }

        GameObject audioBlock = new GameObject("AudioBlock");
        audioBlock.AddComponent<AudioController>();
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioController != null && audioController != this)
        {
            AudioClip duplicateTrack = backgroundAudio != null
                ? backgroundAudio
                : audioSource.clip;
            audioController.AdoptTrackIfMissing(duplicateTrack);
            audioController.EnsureMusicIsPlaying();
            Destroy(gameObject);
            return;
        }

        audioController = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureMusicIsPlaying();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        EnsureMusicIsPlaying();
    }

    private void AdoptTrackIfMissing(AudioClip track)
    {
        if (backgroundAudio == null && track != null)
        {
            backgroundAudio = track;
        }
    }

    private void EnsureMusicIsPlaying()
    {
        audioSource ??= GetComponent<AudioSource>();
        backgroundAudio ??= audioSource.clip;
        backgroundAudio ??= Resources.Load<AudioClip>(DefaultMusicPath);

        if (backgroundAudio == null)
        {
            Debug.LogWarning("AudioBlock could not find its background music track.", this);
            return;
        }

        audioSource.clip = backgroundAudio;
        audioSource.loop = true;
        audioSource.playOnAwake = true;
        audioSource.spatialBlend = 0f;

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    private void OnDestroy()
    {
        if (audioController != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        audioController = null;
    }
}
