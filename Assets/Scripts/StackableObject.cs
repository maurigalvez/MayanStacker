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

    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem landingParticleEffect;

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

    // Components
    private Rigidbody2D rb;
    private Collider2D col;
    // State
    private bool isDropped = false;
    private bool hasLanded = false;
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
        if (!isDropped || hasLanded) return;

        // Check if we landed on another stackable object or the ground
        if (collision.gameObject.CompareTag("Stackable") || collision.gameObject.CompareTag("Ground"))
        {
            LandOnObject(collision);
        }
    }

    private void LandOnObject(Collision2D collision)
    {
        hasLanded = true;

        // Calculate landing accuracy based on how centered we are
        if (collision.gameObject.CompareTag("Stackable"))
        {
            StackableObject otherObject = collision.gameObject.GetComponent<StackableObject>();
            if (otherObject != null)
            {
                CalculateLandingAccuracy(otherObject);
            }
        }
        else
        {
            // Landed on ground - perfect landing
            landingAccuracy = 1f;
        }

        // Visual feedback based on landing accuracy
        UpdateVisualFeedback();

        // Calculate and award score with combo multiplier
        int baseScore = CalculateScore();
        var gameManager = DependencyRegistry.Find<GameManager>();
        if (gameManager != null)
        {
            // Use new combo-aware scoring method
            gameManager.AddScoreWithCombo(baseScore, landingAccuracy);
        }

        // Add this object to the stack
        var stackManager = DependencyRegistry.Find<StackManager>();
        if (stackManager == null)
        {
            // Create StackManager if it doesn't exist
            GameObject stackManagerGO = new GameObject("StackManager");
            stackManager = stackManagerGO.AddComponent<StackManager>();
        }
        stackManager.AddObjectToStack(this);

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
        float centerDistance = Mathf.Abs(transform.position.x - otherObject.transform.position.x);

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


    private void OnGameRestart()
    {
        // Reset object state
        isDropped = false;
        hasLanded = false;
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
