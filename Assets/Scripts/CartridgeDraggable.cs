using System;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls physical sliding of a removable disk pack cartridge in/out of the drive bay
/// using mouse dragging (holding LMB and pulling/pushing mouse).
/// </summary>
public class CartridgeDraggable : MonoBehaviour
{
    public enum SlideMode
    {
        PullOut, // Pulling hot cartridge out of drive slot
        PushIn   // Pushing cold cartridge into empty drive slot
    }

    [Header("Slide Configuration")]
    [SerializeField] private SlideMode mode = SlideMode.PullOut;
    [Tooltip("Local movement axis (usually forward or right)")]
    [SerializeField] private Vector3 slideLocalAxis = new Vector3(0f, 0f, -1f);
    [Tooltip("Maximum slide travel distance in meters")]
    [SerializeField] private float travelDistance = 0.42f;
    [Tooltip("Mouse drag sensitivity")]
    [SerializeField] private float dragSensitivity = 0.0035f;

    [Header("Current Progress")]
    [Range(0f, 1f)]
    [SerializeField] private float progress = 0f; // 0 = fully inside, 1 = fully outside

    [Header("Cartridge Info")]
    [SerializeField] private string cartridgeName = "Пакет дисков №1 (Основной)";
    [SerializeField] private bool isHot = true;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    private AudioClip slideClip;
    private AudioClip lockClip;

    [Header("Events")]
    public UnityEvent<float> onSlideProgress = new UnityEvent<float>();
    public UnityEvent onCompleted = new UnityEvent();

    private Vector3 initialLocalPos;
    private bool isDragging = false;
    private bool isCompleted = false;

    public bool IsDragging => isDragging;
    public bool IsCompleted => isCompleted;
    public float Progress => progress;
    public bool IsHot => isHot;
    public string CartridgeName => cartridgeName;
    public SlideMode Mode => mode;

    private void Awake()
    {
        initialLocalPos = transform.localPosition;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.spatialBlend = 0.9f;
        audioSource.minDistance = 0.5f;
        audioSource.maxDistance = 6f;

        GenerateAudioClips();
        UpdatePosition();
    }

    public void SetMode(SlideMode newMode, float startProgress, bool hot)
    {
        mode = newMode;
        progress = Mathf.Clamp01(startProgress);
        isHot = hot;
        isCompleted = false;
        UpdatePosition();
    }

    public void StartDrag()
    {
        if (isCompleted) return;
        isDragging = true;
    }

    public void UpdateDrag(Vector2 mouseDelta)
    {
        if (!isDragging || isCompleted) return;

        // In pull-out mode: pulling mouse backward/down (negative Y/X) increases progress (pulls out)
        // In push-in mode: pushing mouse forward/up (positive Y/X) decreases progress (pushes in to 0)
        float delta = (mouseDelta.x + mouseDelta.y) * dragSensitivity;

        if (mode == SlideMode.PullOut)
        {
            // Moving mouse down/left pulls out towards player
            progress = Mathf.Clamp01(progress - delta);
            UpdatePosition();
            onSlideProgress?.Invoke(progress);

            if (progress >= 0.98f)
            {
                progress = 1.0f;
                UpdatePosition();
                CompleteSlide();
            }
        }
        else // PushIn
        {
            // Moving mouse up/right pushes in into drive
            progress = Mathf.Clamp01(progress - delta);
            UpdatePosition();
            onSlideProgress?.Invoke(progress);

            if (progress <= 0.02f)
            {
                progress = 0.0f;
                UpdatePosition();
                CompleteSlide();
            }
        }
    }

    public void EndDrag()
    {
        isDragging = false;
    }

    private void CompleteSlide()
    {
        isCompleted = true;
        isDragging = false;
        PlayLockSound();
        onCompleted?.Invoke();
    }

    private void UpdatePosition()
    {
        transform.localPosition = initialLocalPos + slideLocalAxis * (progress * travelDistance);
    }

    private void PlayLockSound()
    {
        if (audioSource != null && lockClip != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(lockClip, 0.75f);
        }
    }

    private void GenerateAudioClips()
    {
        int sampleRate = 44100;

        // Mechanical solenoid lock clack
        int lockSamples = (int)(sampleRate * 0.09f);
        float[] lockData = new float[lockSamples];
        for (int i = 0; i < lockSamples; i++)
        {
            float t = (float)i / lockSamples;
            float thud = Mathf.Sin(2f * Mathf.PI * 110f * ((float)i / sampleRate)) * 0.75f;
            float metallic = Mathf.Sin(2f * Mathf.PI * 920f * ((float)i / sampleRate)) * 0.4f;
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.35f;
            float env = Mathf.Exp(-t * 24f);
            lockData[i] = (thud + metallic + noise) * env;
        }
        lockClip = AudioClip.Create("CartridgeLockClack", lockSamples, 1, sampleRate, false);
        lockClip.SetData(lockData, 0);
    }
}
