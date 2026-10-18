using System;
using UnityEngine;

/// <summary>
/// Dual-cradle passive cooling station for removable disk pack cartridges (ЕС-5061).
/// Allows extracted hot cartridges to cool down naturally (from ~90°C to 30°C in ~25-30s)
/// and enables an infinite, authentic rotating swap cycle between Cartridge #1 and Cartridge #2.
/// </summary>
public class CartridgeCoolingRack : MonoBehaviour
{
    public static CartridgeCoolingRack Instance { get; private set; }

    [Serializable]
    public class CradleSlot
    {
        public int cartridgeId;              // 1 or 2
        public string cradleName;            // "Ячейка 1"
        public bool isOccupied;
        public float currentTemp = 30f;      // In Celsius
        public GameObject cartridgeVisual;
        public Renderer statusLamp;
        public TextMesh labelTextMesh;
        public WorldInteractable interactable;
    }

    [Header("Cradle Slots")]
    [SerializeField] private CradleSlot cradle1 = new CradleSlot { cartridgeId = 1, cradleName = "Ячейка 1" };
    [SerializeField] private CradleSlot cradle2 = new CradleSlot { cartridgeId = 2, cradleName = "Ячейка 2" };

    [Header("Cooling Tuning")]
    [Tooltip("Passive cooling rate in °C per second")]
    [SerializeField] private float passiveCoolRate = 2.2f;
    [Tooltip("Safe temperature threshold to allow inserting into server")]
    [SerializeField] private float readyTempThreshold = 35f;
    [Tooltip("Ambient minimum temperature")]
    [SerializeField] private float ambientTemp = 30f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    private AudioClip placeClip;
    private AudioClip pickClip;

    [Header("Materials")]
    [SerializeField] private Material lampGreenMat;
    [SerializeField] private Material lampRedMat;
    [SerializeField] private Material lampOffMat;

    private void Awake()
    {
        Instance = this;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 0.85f;
        audioSource.minDistance = 0.5f;
        audioSource.maxDistance = 6f;

        CreateMaterials();
        GenerateAudioClips();
    }

    private void Start()
    {
        // Initial setup:
        // Cradle 1 is EMPTY (Cartridge #1 starts inside the server drive bay)
        cradle1.isOccupied = false;
        cradle1.currentTemp = ambientTemp;
        if (cradle1.cartridgeVisual != null) cradle1.cartridgeVisual.SetActive(false);

        // Cradle 2 is OCCUPIED with cold Cartridge #2 (30°C, ready to swap)
        cradle2.isOccupied = true;
        cradle2.currentTemp = ambientTemp;
        if (cradle2.cartridgeVisual != null) cradle2.cartridgeVisual.SetActive(true);

        UpdateCradleDisplay(cradle1);
        UpdateCradleDisplay(cradle2);
    }

    private void Update()
    {
        UpdateCooling(cradle1);
        UpdateCooling(cradle2);
    }

    private void UpdateCooling(CradleSlot cradle)
    {
        if (cradle.isOccupied && cradle.currentTemp > ambientTemp)
        {
            cradle.currentTemp = Mathf.Max(ambientTemp, cradle.currentTemp - passiveCoolRate * Time.deltaTime);
        }

        UpdateCradleDisplay(cradle);
    }

    private void UpdateCradleDisplay(CradleSlot cradle)
    {
        if (cradle.interactable == null) return;

        var bay = DiskDriveBay.Instance;
        bool hasExtractedCartridge = bay != null && bay.State == DiskDriveBay.DriveBayState.CartridgeExtracted;
        int extractedId = bay != null ? bay.ExtractedCartridgeId : 0;

        cradle.interactable.IsInteractable = true;

        if (!cradle.isOccupied)
        {
            // Empty cradle: can we place the extracted cartridge here?
            if (hasExtractedCartridge && extractedId == cradle.cartridgeId)
            {
                cradle.interactable.PromptText = $"[E] Положить горячий пакет №{cradle.cartridgeId} на охлаждение";
            }
            else
            {
                cradle.interactable.PromptText = $"[ {cradle.cradleName} пуста — сначала извлеките диск из сервера ]";
            }

            if (cradle.statusLamp != null) cradle.statusLamp.sharedMaterial = lampOffMat;
            if (cradle.labelTextMesh != null)
            {
                cradle.labelTextMesh.text = $"{cradle.cradleName} [ ПАКЕТ №{cradle.cartridgeId} ]\n<color=#888888>[ ПУСТО ]</color>";
            }
        }
        else
        {
            // Occupied cradle: is it hot or cooled?
            bool isReady = cradle.currentTemp <= readyTempThreshold;

            if (isReady)
            {
                // Can only take if server slot is empty (CartridgeExtracted)
                if (hasExtractedCartridge)
                {
                    cradle.interactable.PromptText = $"[E] Взять охлажденный пакет №{cradle.cartridgeId} ({cradle.currentTemp:F0}°C)";
                }
                else
                {
                    cradle.interactable.PromptText = $"[ Пакет №{cradle.cartridgeId} готов ({cradle.currentTemp:F0}°C) — сначала извлеките горячий диск ]";
                }

                if (cradle.statusLamp != null) cradle.statusLamp.sharedMaterial = lampGreenMat;
                if (cradle.labelTextMesh != null)
                {
                    cradle.labelTextMesh.text = $"{cradle.cradleName} [ ПАКЕТ №{cradle.cartridgeId} ]\n<color=#00FFAA>[ {cradle.currentTemp:F0}°C // ГОТОВ К ЗАМЕНЕ ]</color>";
                }
            }
            else
            {
                // Still cooling down
                cradle.interactable.PromptText = $"[ Пакет №{cradle.cartridgeId} остывает ({cradle.currentTemp:F0}°C) — дождитесь 35°C ]";

                if (cradle.statusLamp != null) cradle.statusLamp.sharedMaterial = lampRedMat;
                if (cradle.labelTextMesh != null)
                {
                    cradle.labelTextMesh.text = $"{cradle.cradleName} [ ПАКЕТ №{cradle.cartridgeId} ]\n<color=#FF8833>[ ОХЛАЖДЕНИЕ: {cradle.currentTemp:F0}°C ]</color>";
                }
            }
        }
    }

    /// <summary>
    /// Player interacts with Cradle 1 (calls via WorldInteractable).
    /// </summary>
    public void InteractCradle1()
    {
        HandleCradleInteraction(cradle1);
    }

    /// <summary>
    /// Player interacts with Cradle 2 (calls via WorldInteractable).
    /// </summary>
    public void InteractCradle2()
    {
        HandleCradleInteraction(cradle2);
    }

    private void HandleCradleInteraction(CradleSlot cradle)
    {
        var bay = DiskDriveBay.Instance;
        if (bay == null) return;

        if (!cradle.isOccupied)
        {
            // Place extracted cartridge
            if (bay.State == DiskDriveBay.DriveBayState.CartridgeExtracted && bay.ExtractedCartridgeId == cradle.cartridgeId)
            {
                cradle.isOccupied = true;
                cradle.currentTemp = bay.Temperature;
                if (cradle.cartridgeVisual != null) cradle.cartridgeVisual.SetActive(true);

                PlayPlaceSound();
                bay.NotifyCartridgePlacedOnRack();

                MainframeBootQuest.Instance?.ShowNotification(
                    $"<color=#FFFF66>[ПОЛКА ОХЛАЖДЕНИЯ]</color> Пакет №{cradle.cartridgeId} установлен на охлаждение ({cradle.currentTemp:F0}°C).");
            }
            else
            {
                MainframeBootQuest.Instance?.ShowNotification(
                    $"<color=#FFAA00>[ЯЧЕЙКА ПУСТА]</color> Сначала извлеките горячий пакет дисков №{cradle.cartridgeId} из сервера!");
            }
        }
        else
        {
            // Take cold cartridge
            if (cradle.currentTemp <= readyTempThreshold)
            {
                if (bay.State == DiskDriveBay.DriveBayState.CartridgeExtracted)
                {
                    cradle.isOccupied = false;
                    if (cradle.cartridgeVisual != null) cradle.cartridgeVisual.SetActive(false);

                    PlayPickSound();
                    bay.MountColdCartridge(cradle.cartridgeId, cradle.currentTemp);

                    MainframeBootQuest.Instance?.ShowNotification(
                        $"<color=#00FFAA>[КАРТРИДЖ НА РЕЛЬСАХ]</color> Пакет №{cradle.cartridgeId} установлен на рельсы слота. Задвиньте его мышью.");
                }
                else
                {
                    MainframeBootQuest.Instance?.ShowNotification("Сначала извлеките горячий картридж из сервера!");
                }
            }
            else
            {
                MainframeBootQuest.Instance?.ShowNotification(
                    $"<color=#FFAA00>[ОСТОРОЖНО: ГОРЯЧО]</color> Пакет №{cradle.cartridgeId} еще остывает ({cradle.currentTemp:F0}°C). Дождитесь 35°C!");
            }
        }

        UpdateCradleDisplay(cradle);
    }

    private void CreateMaterials()
    {
#if UNITY_EDITOR
        if (lampGreenMat == null) lampGreenMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_LedGreen.mat");
        if (lampRedMat == null) lampRedMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_LedRed.mat");
        if (lampOffMat == null) lampOffMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_LedOff.mat");
#endif
        var toonShader = Shader.Find("Custom/SovietToonLit");
        if (toonShader == null) toonShader = Shader.Find("Universal Render Pipeline/Lit");

        if (lampGreenMat == null)
        {
            lampGreenMat = new Material(toonShader);
            lampGreenMat.SetColor("_BaseColor", new Color(0.15f, 1.0f, 0.35f));
            lampGreenMat.EnableKeyword("_EMISSION");
            lampGreenMat.SetColor("_EmissionColor", new Color(0.15f, 1.0f, 0.35f) * 1.5f);
        }
        if (lampRedMat == null)
        {
            lampRedMat = new Material(toonShader);
            lampRedMat.SetColor("_BaseColor", new Color(1.0f, 0.15f, 0.15f));
            lampRedMat.EnableKeyword("_EMISSION");
            lampRedMat.SetColor("_EmissionColor", new Color(1.0f, 0.15f, 0.15f) * 1.5f);
        }
        if (lampOffMat == null)
        {
            lampOffMat = new Material(toonShader);
            lampOffMat.SetColor("_BaseColor", new Color(0.15f, 0.18f, 0.16f));
        }
    }

    private void GenerateAudioClips()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.08f);
        float[] clickData = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            clickData[i] = Mathf.Sin(2f * Mathf.PI * 350f * ((float)i / sampleRate)) * (1f - t);
        }
        placeClip = AudioClip.Create("CartridgePlace", samples, 1, sampleRate, false);
        placeClip.SetData(clickData, 0);

        pickClip = AudioClip.Create("CartridgePick", samples, 1, sampleRate, false);
        pickClip.SetData(clickData, 0);
    }

    private void PlayPlaceSound()
    {
        if (audioSource != null && placeClip != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(placeClip, 0.7f);
        }
    }

    private void PlayPickSound()
    {
        if (audioSource != null && pickClip != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(pickClip, 0.7f);
        }
    }
}
