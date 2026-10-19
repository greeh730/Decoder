using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Physical rotary potentiometer knob for DECODER.
/// Allows the player to click and drag the mouse or use the scroll wheel to physically
/// turn the 3D dial with analog precision, real-time continuous feedback,
/// procedural detent micro-clicks, and tactile responsiveness.
/// </summary>
public class PotentiometerKnob : MonoBehaviour
{
    [Header("Knob Transforms")]
    [Tooltip("The rotating dial cylinder (will rotate around its local Y axis)")]
    [SerializeField] private Transform dialTransform;

    [Header("Rotation Settings")]
    [Tooltip("Local Y rotation angle at 0% (Counter-clockwise / Min)")]
    [SerializeField] private float minAngle = -65f;

    [Tooltip("Local Y rotation angle at 100% (Clockwise / Max)")]
    [SerializeField] private float maxAngle = 65f;

    [Tooltip("Mouse drag sensitivity")]
    [SerializeField] private float dragSensitivity = 0.0035f;

    [Tooltip("Mouse scroll wheel step size")]
    [SerializeField] private float scrollStep = 0.04f;

    [Header("Value State")]
    [Range(0f, 1f)]
    [SerializeField] private float currentValue = 1.0f;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    private AudioClip detentClickClip;
    private AudioClip endStopClip;
    private float lastClickValue = 1.0f;
    private const float ClickStepInterval = 0.05f; // Click sound every 5% rotation

    [Header("Events")]
    public UnityEvent<float> onValueChanged = new UnityEvent<float>();
    public UnityEvent onDragEnd = new UnityEvent();

    private bool isDragging = false;

    public bool IsDragging => isDragging;
    public float CurrentValue => currentValue;
    public Transform DialTransform
    {
        get => dialTransform;
        set => dialTransform = value;
    }

    private void Awake()
    {
        if (dialTransform == null)
        {
            var d = transform.Find("Dial");
            if (d != null) dialTransform = d;
            else dialTransform = transform;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.spatialBlend = 1.0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 0.5f;
        audioSource.maxDistance = 6f;

        GenerateAudioClips();
        UpdateDialRotation();
    }

    public void StartDrag()
    {
        isDragging = true;
        lastClickValue = currentValue;
    }

    public void UpdateDrag(Vector2 mouseDelta)
    {
        if (!isDragging) return;

        // Both horizontal (right/left) and vertical (up/down) mouse movement contribute naturally
        float change = (mouseDelta.x + mouseDelta.y) * dragSensitivity;
        if (Mathf.Abs(change) > 0.00001f)
        {
            ApplyValueDelta(change);
        }
    }

    public void ApplyScroll(float scrollDelta)
    {
        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            ApplyValueDelta(Mathf.Sign(scrollDelta) * scrollStep);
            onDragEnd?.Invoke();
        }
    }

    public void EndDrag()
    {
        if (!isDragging) return;
        isDragging = false;
        onDragEnd?.Invoke();
    }

    public void SetValue(float val, bool triggerEvents = true)
    {
        float prev = currentValue;
        currentValue = Mathf.Clamp01(val);
        UpdateDialRotation();

        // Check for click threshold
        CheckClickAudio(prev, currentValue);

        if (triggerEvents)
        {
            onValueChanged?.Invoke(currentValue);
        }
    }

    private void ApplyValueDelta(float delta)
    {
        float prev = currentValue;
        float next = Mathf.Clamp01(currentValue + delta);

        if (Mathf.Abs(next - prev) > 0.0001f)
        {
            currentValue = next;
            UpdateDialRotation();
            CheckClickAudio(prev, currentValue);
            onValueChanged?.Invoke(currentValue);
        }
        else if ((delta > 0f && prev >= 0.999f) || (delta < 0f && prev <= 0.001f))
        {
            // Hit physical end stop
            PlayEndStopSound();
        }
    }

    private void UpdateDialRotation()
    {
        if (dialTransform != null)
        {
            float angle = Mathf.Lerp(minAngle, maxAngle, currentValue);
            dialTransform.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    private void CheckClickAudio(float prev, float curr)
    {
        if (Mathf.Abs(curr - lastClickValue) >= ClickStepInterval)
        {
            lastClickValue = curr;
            PlayDetentClick();
        }
    }

    private void PlayDetentClick()
    {
        if (audioSource != null && detentClickClip != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.08f);
            audioSource.PlayOneShot(detentClickClip, 0.40f);
        }
    }

    private void PlayEndStopSound()
    {
        if (audioSource != null && endStopClip != null)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.pitch = Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(endStopClip, 0.50f);
            }
        }
    }

    private void GenerateAudioClips()
    {
        int sampleRate = 44100;

        // 1. Detent micro-click (hollow rotary plastic/metal ball detent click)
        int clickSamples = (int)(sampleRate * 0.028f);
        float[] clickData = new float[clickSamples];
        for (int i = 0; i < clickSamples; i++)
        {
            float t = (float)i / clickSamples;
            float tone1 = Mathf.Sin(2f * Mathf.PI * 1150f * ((float)i / sampleRate));
            float tone2 = Mathf.Sin(2f * Mathf.PI * 2300f * ((float)i / sampleRate)) * 0.45f;
            float noise = (Random.value * 2f - 1f) * 0.35f;
            float env = Mathf.Exp(-t * 36f);
            clickData[i] = (tone1 + tone2 + noise) * env * 0.55f;
        }
        detentClickClip = AudioClip.Create("KnobDetentClick", clickSamples, 1, sampleRate, false);
        detentClickClip.SetData(clickData, 0);

        // 2. End-stop bump (physical metal stopper stop)
        int bumpSamples = (int)(sampleRate * 0.045f);
        float[] bumpData = new float[bumpSamples];
        for (int i = 0; i < bumpSamples; i++)
        {
            float t = (float)i / bumpSamples;
            float thud = Mathf.Sin(2f * Mathf.PI * 180f * ((float)i / sampleRate)) * 0.75f;
            float click = Mathf.Sin(2f * Mathf.PI * 850f * ((float)i / sampleRate)) * 0.3f;
            float env = Mathf.Exp(-t * 26f);
            bumpData[i] = (thud + click) * env * 0.6f;
        }
        endStopClip = AudioClip.Create("KnobEndStop", bumpSamples, 1, sampleRate, false);
        endStopClip.SetData(bumpData, 0);
    }
}
