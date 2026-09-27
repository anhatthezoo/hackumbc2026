using UnityEngine;

public class AudioController : MonoBehaviour
{
    public static AudioController audioController {get; private set; }
     private AudioSource audioSource;
     [SerializeField] private AudioClip backgroundAudio;
    private void Awake()
    {
        if (audioController != null && audioController != this)
        {
            Destroy(gameObject);
            return;
        }

        audioController = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.generator = backgroundAudio;
        audioSource.loop = true;
        audioSource.Play();
    }

}
