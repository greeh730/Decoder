using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to any object in the world that the player can interact with.
/// When the player looks at it and is in range, a prompt is shown,
/// and pressing the interact key invokes the OnInteract event.
/// </summary>
public class WorldInteractable : MonoBehaviour
{
    [Header("Prompt & Interaction")]
    [Tooltip("Text shown to the player when looking at this object")]
    [SerializeField] private string promptText = "[E] Использовать";

    [Tooltip("Maximum distance from player camera to interact")]
    [SerializeField] private float maxDistance = 2.4f;

    [Tooltip("Whether this object is currently interactable")]
    [SerializeField] private bool isInteractable = true;

    [Header("Events")]
    public UnityEvent onInteract = new UnityEvent();

    public string PromptText
    {
        get => LocalizationManager.TranslatePrompt(promptText);
        set => promptText = value;
    }

    public float MaxDistance
    {
        get => maxDistance;
        set => maxDistance = value;
    }
    public bool IsInteractable
    {
        get => isInteractable;
        set => isInteractable = value;
    }

    public void Interact()
    {
        if (!isInteractable) return;
        onInteract?.Invoke();
    }
}
