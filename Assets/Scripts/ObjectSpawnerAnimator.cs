using UnityEngine;

/// <summary>
/// Helper script that handles animations for the ObjectSpawner.
/// Plays animations when objects are spawned or dropped from the spawner.
/// </summary>
[RequireComponent(typeof(Animator))]
public class ObjectSpawnerAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private Animator animator;
    [SerializeField] private string spawnAnimationTrigger = "Spawn";
    [SerializeField] private string dropAnimationTrigger = "Drop";
    [SerializeField] private bool enableAnimations = true;

    [Header("Animation Variance (for Drop)")]
    [SerializeField] private bool enableAnimationVariance = true;
    [SerializeField] private Vector2 animationSpeedRange = new Vector2(0.9f, 1.1f);
    [SerializeField] private Vector2 animationStartTimeRange = new Vector2(0f, 0.2f);
    [Tooltip("Optional: Layer index for the drop animation state. -1 to use base layer.")]
    [SerializeField] private int dropAnimationLayer = 0;
    [Tooltip("Optional: Name of the drop animation state to apply time offset.")]
    [SerializeField] private string dropAnimationStateName = "";

    [Header("Optional Visual Feedback")]
    [SerializeField] private bool useScaleEffect = false;
    [SerializeField] private float scaleEffectDuration = 0.2f;
    [SerializeField] private float scaleEffectAmount = 1.1f;
    [SerializeField] private Vector2 scaleEffectAmountRange = new Vector2(1.05f, 1.15f);

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private bool enableAudio = true;
    [SerializeField] private Vector2 audioPitchRange = new Vector2(0.95f, 1.05f);

    private ObjectSpawner objectSpawner;
    private Vector3 originalScale;
    private Coroutine scaleCoroutine;

    private void Awake()
    {
        // Get or add Animator component
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = gameObject.AddComponent<Animator>();
            }
        }

        // Get or add AudioSource if audio is enabled
        if (enableAudio && audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Store original scale for scale effects
        originalScale = transform.localScale;
    }

    private void Start()
    {
        // Get ObjectSpawner from DependencyRegistry
        objectSpawner = DependencyRegistry.Find<ObjectSpawner>();

        // Reset all triggers to avoid unwanted transitions
        ResetAllTriggers();

        // Subscribe to ObjectSpawner events
        if (objectSpawner != null)
        {
            objectSpawner.OnObjectSpawned += OnObjectSpawned;
            objectSpawner.OnObjectDropped += OnObjectDropped;
        }
        else
        {
            Debug.LogWarning("ObjectSpawnerAnimator: No ObjectSpawner component found on this GameObject!");
        }
    }

    private void OnObjectSpawned(GameObject spawnedObject)
    {
        if (!enableAnimations) return;

        // Trigger spawn animation
        if (animator != null)
        {
            // Reset drop trigger before setting spawn trigger
            if (!string.IsNullOrEmpty(dropAnimationTrigger))
            {
                animator.ResetTrigger(dropAnimationTrigger);
            }

            if (!string.IsNullOrEmpty(spawnAnimationTrigger))
            {
                // Reset animation speed to normal for spawn
                animator.speed = 1f;

                animator.ResetTrigger(spawnAnimationTrigger);
                animator.SetTrigger(spawnAnimationTrigger);
            }
        }

        // Play spawn sound at normal pitch
        if (enableAudio && audioSource != null && spawnSound != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(spawnSound);
        }

        // Optional scale effect
        if (useScaleEffect)
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }
            scaleCoroutine = StartCoroutine(ScaleEffect());
        }
    }

    private void OnObjectDropped(GameObject droppedObject)
    {
        if (!enableAnimations) return;

        // Trigger drop animation with variance
        if (animator != null)
        {
            // Reset spawn trigger before setting drop trigger
            if (!string.IsNullOrEmpty(spawnAnimationTrigger))
            {
                animator.ResetTrigger(spawnAnimationTrigger);
            }

            if (!string.IsNullOrEmpty(dropAnimationTrigger))
            {
                animator.ResetTrigger(dropAnimationTrigger);

                // Apply animation variance
                if (enableAnimationVariance)
                {
                    ApplyAnimationVariance();
                }

                animator.SetTrigger(dropAnimationTrigger);
            }
        }

        // Play drop sound with random pitch variance
        if (enableAudio && audioSource != null && dropSound != null)
        {
            if (enableAnimationVariance)
            {
                audioSource.pitch = Random.Range(audioPitchRange.x, audioPitchRange.y);
            }
            audioSource.PlayOneShot(dropSound);
        }

        // Optional scale effect with variance
        if (useScaleEffect)
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }
            scaleCoroutine = StartCoroutine(ScaleEffect());
        }
    }

    private System.Collections.IEnumerator ScaleEffect()
    {
        float elapsed = 0f;
        float halfDuration = scaleEffectDuration * 0.5f;

        // Apply variance to scale amount if enabled
        float finalScaleAmount = scaleEffectAmount;
        if (enableAnimationVariance && scaleEffectAmountRange.x > 0 && scaleEffectAmountRange.y > 0)
        {
            finalScaleAmount = Random.Range(scaleEffectAmountRange.x, scaleEffectAmountRange.y);
        }

        Vector3 targetScale = originalScale * finalScaleAmount;

        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        elapsed = 0f;

        // Scale back down
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        // Ensure we end at exactly the original scale
        transform.localScale = originalScale;
        scaleCoroutine = null;
    }

    private void ApplyAnimationVariance()
    {
        if (animator == null) return;

        // Randomize animation speed
        float randomSpeed = Random.Range(animationSpeedRange.x, animationSpeedRange.y);
        animator.speed = randomSpeed;

        // Optionally offset the start time of the animation to add more variance
        // This requires the animation state name to be set
        if (!string.IsNullOrEmpty(dropAnimationStateName))
        {
            // Start a coroutine to set the normalized time after the trigger is processed
            StartCoroutine(SetAnimationStartTime());
        }
    }

    private System.Collections.IEnumerator SetAnimationStartTime()
    {
        // Wait one frame for the animation to start playing
        yield return null;

        // Set a random start time offset
        float randomStartTime = Random.Range(animationStartTimeRange.x, animationStartTimeRange.y);

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(dropAnimationLayer);

        // Check if we're in the right state
        if (!string.IsNullOrEmpty(dropAnimationStateName))
        {
            if (stateInfo.IsName(dropAnimationStateName))
            {
                animator.Play(dropAnimationStateName, dropAnimationLayer, randomStartTime);
            }
        }
    }

    private void ResetAllTriggers()
    {
        if (animator == null) return;

        // Reset spawn and drop triggers to avoid unwanted transitions
        if (!string.IsNullOrEmpty(spawnAnimationTrigger))
        {
            animator.ResetTrigger(spawnAnimationTrigger);
        }

        if (!string.IsNullOrEmpty(dropAnimationTrigger))
        {
            animator.ResetTrigger(dropAnimationTrigger);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (objectSpawner != null)
        {
            objectSpawner.OnObjectSpawned -= OnObjectSpawned;
            objectSpawner.OnObjectDropped -= OnObjectDropped;
        }
    }

    // Public methods to manually trigger animations if needed
    public void PlaySpawnAnimation()
    {
        OnObjectSpawned(null);
    }

    public void PlayDropAnimation()
    {
        OnObjectDropped(null);
    }

    // Public setters for runtime control
    public void SetEnableAnimations(bool enable)
    {
        enableAnimations = enable;
    }

    public void SetEnableAudio(bool enable)
    {
        enableAudio = enable;
    }

    public void SetUseScaleEffect(bool use)
    {
        useScaleEffect = use;
    }
}

