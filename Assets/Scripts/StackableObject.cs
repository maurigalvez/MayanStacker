using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class StackableObject : MonoBehaviour
{
    [Header("Physics Settings")]
    [SerializeField] private float mass = 1f;
    [SerializeField] private float drag = 0.5f;
    [SerializeField] private float angularDrag = 0.5f;
    [SerializeField] private float bounciness = 0.1f;
    [SerializeField] private float friction = 0.6f;
    [SerializeField] private float gravityScale = 2f; // Double the falling speed

    [Header("Visual Settings")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color perfectLandingColor = Color.green;
    [SerializeField] private Color poorLandingColor = Color.red;
    [SerializeField] private bool enableDistanceCulling = true; // Disable rendering for blocks far below camera
    [SerializeField] private float cullingDistance = 15f; // Distance below camera to disable rendering

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem landingParticleEffect;

    [Header("Squash & Stretch")]
    [SerializeField] private bool enableSquashStretch = true;
    [SerializeField] private float squashAmount = 0.4f; // How much to squish vertically (0.4 = 60% height)
    [SerializeField] private float stretchAmount = 1.15f; // Overshoot on the way back
    [SerializeField] private float squashDuration = 0.08f;
    [SerializeField] private float reboundDuration = 0.35f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] landingAudioClips;
    [SerializeField] private bool enableLandingAudio = true;
    [SerializeField] private float pitchMin = 0.8f;
    [SerializeField] private float pitchMax = 1.2f;

    [Header("Scoring")]
    [SerializeField] private float perfectLandingThreshold = 0.1f; // How close to center for perfect score
    [SerializeField] private int perfectScore = 100;
    [SerializeField] private int goodScore = 50;
    [SerializeField] private int poorScore = 10;
    [SerializeField] private float rotationUnlockDelay = 0.15f; // Delay before unlocking rotation after landing (allows block to settle)

    // Components
    private Rigidbody2D rb;
    private Collider2D col;
    // State
    private bool isDropped = false;
    private bool hasLanded = false;
    private bool landedOnStackable = false; // Track if object initially landed on a stackable object
    private Vector3 originalPosition;
    private float landingAccuracy = 0f;

    // Events
    public System.Action<StackableObject, float> OnObjectLanded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        // Set up physics properties
        if (rb != null)
        {
            rb.mass = mass;
            rb.linearDamping = drag;
            rb.angularDamping = angularDrag;
            rb.gravityScale = gravityScale;
            rb.bodyType = RigidbodyType2D.Kinematic; // Start as kinematic until dropped
        }

        // Set up collider physics material
        if (col != null)
        {
            PhysicsMaterial2D physicsMaterial = new PhysicsMaterial2D("StackableMaterial");
            physicsMaterial.bounciness = bounciness;
            physicsMaterial.friction = friction;
            col.sharedMaterial = physicsMaterial;
        }

        // Set up audio source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Set initial color
        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }

        originalPosition = transform.position;
    }

    private void Start()
    {
        // Subscribe to game events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameRestart += OnGameRestart;
        }
    }

    private void Update()
    {
        // Update sprite renderer visibility based on distance from camera
        if (enableDistanceCulling && spriteRenderer != null && hasLanded)
        {
            UpdateRendererVisibility();
        }
    }

    /// <summary>
    /// Updates sprite renderer visibility based on distance from camera
    /// Disables rendering for blocks far below the camera to improve performance
    /// </summary>
    private void UpdateRendererVisibility()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        // Calculate distance below camera
        float cameraY = mainCamera.transform.position.y;
        float blockY = transform.position.y;
        float distanceBelowCamera = cameraY - blockY;

        // Disable renderer if block is far below camera
        bool shouldBeVisible = distanceBelowCamera <= cullingDistance;
        if (spriteRenderer.enabled != shouldBeVisible)
        {
            spriteRenderer.enabled = shouldBeVisible;
        }
    }

    public void Drop()
    {
        if (isDropped) return;

        isDropped = true;

        // Deparent the object so it can fall independently
        transform.SetParent(null);

        rb.bodyType = RigidbodyType2D.Dynamic;

        // Add a slight random horizontal force for more interesting gameplay
        float randomForce = Random.Range(-0.5f, 0.5f);
        rb.AddForce(new Vector2(randomForce, 0), ForceMode2D.Impulse);

        // Register with StackManager for fall detection monitoring
        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager != null)
        {
            stackManager.RegisterDroppedObject(this);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isDropped) return;

        // If we haven't landed yet, process initial landing
        if (!hasLanded)
        {
            // Check if we landed on another stackable object or the ground
            if (collision.gameObject.CompareTag("Stackable") || collision.gameObject.CompareTag("Ground"))
            {
                LandOnObject(collision);
            }
        }
        // If we already landed on a stackable object, check if we're now hitting the ground (fell off)
        else if (hasLanded && landedOnStackable && collision.gameObject.CompareTag("Ground"))
        {
            CheckFallOffStack();
        }
    }

    private void LandOnObject(Collision2D collision)
    {
        hasLanded = true;

        // Get references early for use throughout the method
        var stackManager = DependencyRegistry.Find<StackManager>();
        var gameManager = DependencyRegistry.Find<GameManager>();

        // Check if we landed on the ground (not on another stackable object)
        bool landedOnGround = collision.gameObject.CompareTag("Ground");
        landedOnStackable = collision.gameObject.CompareTag("Stackable");

        // Lock rotation immediately to prevent tilting from affecting accuracy calculation
        // This ensures the block is stable before we calculate landing accuracy
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            // Snap rotation to 0 to ensure accurate bounds calculation
            transform.rotation = Quaternion.identity;
        }

        // Calculate landing accuracy based on how centered we are
        if (landedOnStackable)
        {
            StackableObject otherObject = collision.gameObject.GetComponent<StackableObject>();
            if (otherObject != null)
            {
                CalculateLandingAccuracy(otherObject);
            }
        }
        else if (landedOnGround)
        {
            // Landed on ground - check if this is the first object
            if (stackManager != null && stackManager.GetStackCount() > 0)
            {
                // Not the first object - game over!
                Debug.Log("Game Over: Object landed on ground instead of the stack!");
                if (gameManager != null)
                {
                    gameManager.GameOver();
                    return; // Don't process landing further
                }
            }

            // First object landing on ground is fine - perfect landing
            landingAccuracy = 1f;
        }

        // Visual feedback based on landing accuracy
        UpdateVisualFeedback();

        // Calculate and award score with combo multiplier
        int baseScore = CalculateScore();
        if (gameManager != null)
        {
            // Use new combo-aware scoring method
            gameManager.AddScoreWithCombo(baseScore, landingAccuracy);
        }

        // Daily Challenge: Fragile Stack modifier ends the run on any sub-Good landing.
        if (gameManager != null && gameManager.CurrentGameMode == GameMode.DailyChallenge && landingAccuracy < gameManager.FragileStackFailThreshold)
        {
            var dailyMgr = DependencyRegistry.Find<DailyChallengeManager>();
            if (dailyMgr != null && dailyMgr.IsActive
                && dailyMgr.CurrentConfig.modifier == DailyChallengeModifier.FragileStack)
            {
                Debug.Log($"[DailyChallenge] FragileStack: misaligned landing ({landingAccuracy:F2}) ends the run.");
                gameManager.GameOver();
            }
        }

        // Unlock rotation after a short delay - allows block to settle before it can tilt/fall
        // This maintains the danger element while ensuring accurate scoring
        StartCoroutine(UnlockRotationAfterDelay());

        // Add this object to the stack
        if (stackManager == null)
        {
            // Create StackManager if it doesn't exist
            GameObject stackManagerGO = new GameObject("StackManager");
            stackManager = stackManagerGO.AddComponent<StackManager>();
        }
        stackManager.AddObjectToStack(this);

        // Trigger squash & stretch bounce
        if (enableSquashStretch)
        {
            StartCoroutine(SquashAndStretch());
        }

        // Trigger landing particle effect
        TriggerLandingParticleEffect();

        // Trigger landing audio
        TriggerLandingAudio();

        // Notify listeners
        OnObjectLanded?.Invoke(this, landingAccuracy);
    }


    private void CalculateLandingAccuracy(StackableObject otherObject)
    {
        // Calculate how centered this object is on top of the other
        // Rotation is already locked and snapped to 0, so bounds.center is now accurate
        // This gives us the actual center of the collider, accounting for any pivot offset
        Vector2 thisCenter = col.bounds.center;
        Vector2 otherCenter = otherObject.Collider.bounds.center;
        float centerDistance = Mathf.Abs(thisCenter.x - otherCenter.x);

        // Get collider sizes for accurate calculations using references
        BoxCollider2D thisCollider = col as BoxCollider2D;
        BoxCollider2D otherCollider = otherObject.Collider as BoxCollider2D;

        float thisWidth = thisCollider != null ? thisCollider.size.x : 1f;
        float otherWidth = otherCollider != null ? otherCollider.size.x : 1f;

        float maxDistance = (thisWidth + otherWidth) * 0.5f;

        // landingAccuracy: 1 = perfect center, 0 = completely off
        landingAccuracy = Mathf.Clamp01(1f - (centerDistance / maxDistance));
    }

    private int CalculateScore()
    {
        if (landingAccuracy >= 0.9f)
            return perfectScore;
        else if (landingAccuracy >= 0.6f)
            return goodScore;
        else
            return poorScore;
    }

    private void UpdateVisualFeedback()
    {
        if (spriteRenderer == null) return;

        // Color changing disabled - keep original color
        // if (landingAccuracy >= 0.9f)
        // {
        //     spriteRenderer.color = perfectLandingColor;
        // }
        // else if (landingAccuracy <= 0.3f)
        // {
        //     spriteRenderer.color = poorLandingColor;
        // }
        // else
        // {
        //     spriteRenderer.color = normalColor;
        // }
    }

    private void TriggerLandingParticleEffect()
    {
        if (landingParticleEffect == null) return;

        // Tint the particle effect based on the currently selected theme:
        // - Day:    keep the block's color (as-is)
        // - Sunset: smoke (grayscale range)
        // - Night:  colorful (random vibrant hue per burst)
        var themeManager = DependencyRegistry.Find<ThemeManager>();
        GameTheme theme = themeManager != null ? themeManager.GetSelectedTheme() : GameTheme.Day;

        var main = landingParticleEffect.main;
        switch (theme)
        {
            case GameTheme.Sunset:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.75f, 0.75f, 0.75f, 0.9f),
                    new Color(0.35f, 0.35f, 0.35f, 0.9f));
                break;
            case GameTheme.Night:
                main.startColor = new ParticleSystem.MinMaxGradient(
                    Color.HSVToRGB(Random.value, 0.9f, 1f),
                    Color.HSVToRGB(Random.value, 0.9f, 1f));
                break;
            case GameTheme.Day:
            default:
                if (spriteRenderer != null)
                {
                    main.startColor = spriteRenderer.color;
                }
                break;
        }

        landingParticleEffect.Play();
    }

    private void TriggerLandingAudio()
    {
        if (!enableLandingAudio || audioSource == null || landingAudioClips == null || landingAudioClips.Length == 0) return;

        // Select a random audio clip
        AudioClip randomClip = landingAudioClips[Random.Range(0, landingAudioClips.Length)];

        // Set random pitch within the specified range
        float randomPitch = Random.Range(pitchMin, pitchMax);
        audioSource.pitch = randomPitch;

        // Play the audio clip
        audioSource.PlayOneShot(randomClip);
    }

    /// <summary>
    /// Squashes the block vertically on impact then springs back with an elastic overshoot
    /// for a juicy, fun landing effect.
    /// </summary>
    private IEnumerator SquashAndStretch()
    {
        Vector3 baseScale = transform.localScale;
        // Preserve volume feel: squash Y, widen X
        Vector3 squashedScale = new Vector3(
            baseScale.x * (1f + (1f - squashAmount) * 0.5f),
            baseScale.y * squashAmount,
            baseScale.z);

        // Quick squash down
        float t = 0f;
        while (t < squashDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / squashDuration);
            transform.localScale = Vector3.Lerp(baseScale, squashedScale, k);
            yield return null;
        }
        transform.localScale = squashedScale;

        // Elastic rebound back to base scale (with stretch overshoot)
        Vector3 stretchedScale = new Vector3(
            baseScale.x * (2f - stretchAmount),
            baseScale.y * stretchAmount,
            baseScale.z);

        t = 0f;
        while (t < reboundDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / reboundDuration);
            // Damped oscillation: overshoots toward stretched, then settles at base
            float damp = Mathf.Exp(-5f * k);
            float osc = Mathf.Sin(k * Mathf.PI * 3f) * damp;
            transform.localScale = Vector3.Lerp(squashedScale, baseScale, k)
                + (stretchedScale - baseScale) * osc;
            yield return null;
        }
        transform.localScale = baseScale;
    }

    /// <summary>
    /// Unlocks rotation after a short delay to allow the block to settle before it can tilt/fall
    /// </summary>
    private IEnumerator UnlockRotationAfterDelay()
    {
        yield return new WaitForSeconds(rotationUnlockDelay);

        if (rb != null && hasLanded)
        {
            rb.constraints = RigidbodyConstraints2D.None;
        }
    }

    /// <summary>
    /// Check if object fell off the stack and hit the ground
    /// </summary>
    private void CheckFallOffStack()
    {
        var stackManager = DependencyRegistry.Find<StackManager>();
        var gameManager = DependencyRegistry.Find<GameManager>();

        if (stackManager == null || gameManager == null) return;

        // Get all objects in the stack
        var stackObjects = stackManager.GetStackObjects();
        int stackCount = stackObjects.Count;

        // If an object that landed on a stackable object hits the ground:
        // - If it's the first object (stackCount == 1 and this object is in it), it's fine
        // - If there are other objects in the stack, this object fell off → game over
        bool isFirstObject = stackCount == 1 && stackObjects.Contains(this);

        if (!isFirstObject && stackCount > 0)
        {
            Debug.Log("Game Over: Object fell off the stack and touched the ground!");
            gameManager.GameOver();
        }
    }


    private void OnGameRestart()
    {
        // Reset object state
        isDropped = false;
        hasLanded = false;
        landedOnStackable = false;
        landingAccuracy = 0f;

        // Reset physics
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Reset visual
        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
            spriteRenderer.enabled = true; // Re-enable renderer on restart
        }

        // Reset position and rotation
        transform.position = originalPosition;
        transform.rotation = Quaternion.identity;
    }

    private void OnDestroy()
    {
        // Remove from stack when destroyed
        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager != null)
        {
            stackManager.RemoveObjectFromStack(this);
        }

        // Unsubscribe from events
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnGameRestart -= OnGameRestart;
        }
    }

    // Public getters
    public bool IsDropped => isDropped;
    public bool HasLanded => hasLanded;
    public float LandingAccuracy => landingAccuracy;
    public Collider2D Collider => col;
    public SpriteRenderer SpriteRenderer => spriteRenderer;
}
