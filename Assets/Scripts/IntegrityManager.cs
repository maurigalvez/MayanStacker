using System;
using System.Collections;
using Google.Play.Integrity;
using UnityEngine;

/// <summary>
/// Manages Google Play Integrity API checks for detecting app tampering, unauthorized installations, and emulator usage.
/// Persists across scenes to maintain session state and cached token providers.
/// </summary>
public class IntegrityManager : MonoBehaviour
{
    [Header("Integrity Settings")]
    [SerializeField] private bool enableIntegrityChecks = true;
    [SerializeField] private long cloudProjectNumber = 0; // Optional for Play Store apps
    [Tooltip("Enable detailed logging of integrity check results")]
    [SerializeField] private bool verboseLogging = true;

    // Integrity API instances
    private Google.Play.Integrity.IntegrityManager classicIntegrityManager;
    private StandardIntegrityManager standardIntegrityManager;
    
    // Cached Standard Integrity token provider (expensive to prepare, reuse throughout session)
    private StandardIntegrityTokenProvider cachedStandardTokenProvider;
    private bool isStandardTokenProviderReady = false;
    private bool isPreparingStandardTokenProvider = false;

    // Session state
    private bool hasPerformedStartupCheck = false;
    private IntegrityCheckResult lastStartupCheckResult;
    private IntegrityCheckResult lastGameSessionCheckResult;

    // Events
    public event Action<IntegrityCheckResult> OnIntegrityCheckCompleted;

    // Properties
    public bool IsIntegrityChecksEnabled => enableIntegrityChecks;
    public bool HasPerformedStartupCheck => hasPerformedStartupCheck;
    public IntegrityCheckResult LastStartupCheckResult => lastStartupCheckResult;

    private void Awake()
    {
        // Register with DependencyRegistry
        DependencyRegistry.Register<IntegrityManager>(this);

        // Persist across scenes
        DontDestroyOnLoad(gameObject);

        // Initialize integrity managers
        if (enableIntegrityChecks)
        {
            InitializeIntegrityManagers();
        }
        else
        {
            Debug.LogWarning("[IntegrityManager] Integrity checks are disabled in settings.");
        }
    }

    private void Start()
    {
        // Prepare Standard Integrity token provider on startup (asynchronous, cached for session)
        if (enableIntegrityChecks)
        {
            StartCoroutine(PrepareStandardIntegrityTokenProvider());
        }
    }

    private void InitializeIntegrityManagers()
    {
        try
        {
            classicIntegrityManager = new Google.Play.Integrity.IntegrityManager();
            standardIntegrityManager = new StandardIntegrityManager();
            
            if (verboseLogging)
            {
                Debug.Log("[IntegrityManager] Integrity managers initialized successfully.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[IntegrityManager] Failed to initialize integrity managers: {e.Message}");
        }
    }

    /// <summary>
    /// Prepare Standard Integrity token provider once and cache it for the session.
    /// This is an expensive operation and should only be done once.
    /// </summary>
    private IEnumerator PrepareStandardIntegrityTokenProvider()
    {
        if (isPreparingStandardTokenProvider || isStandardTokenProviderReady)
        {
            yield break;
        }

        isPreparingStandardTokenProvider = true;

        if (verboseLogging)
        {
            Debug.Log("[IntegrityManager] Preparing Standard Integrity token provider...");
        }

        var prepareRequest = new PrepareIntegrityTokenRequest(cloudProjectNumber);
        var prepareOperation = standardIntegrityManager.PrepareIntegrityToken(prepareRequest);

        yield return prepareOperation;

        if (prepareOperation.Error != StandardIntegrityErrorCode.NoError)
        {
            Debug.LogWarning($"[IntegrityManager] Failed to prepare Standard Integrity token provider: {prepareOperation.Error}");
            isPreparingStandardTokenProvider = false;
            yield break;
        }

        cachedStandardTokenProvider = prepareOperation.GetResult();
        isStandardTokenProviderReady = true;
        isPreparingStandardTokenProvider = false;

        if (verboseLogging)
        {
            Debug.Log("[IntegrityManager] Standard Integrity token provider prepared and cached successfully.");
        }
    }

    /// <summary>
    /// Request a Classic Integrity token for critical operations (e.g., leaderboard submissions).
    /// This performs a full integrity check and should be used sparingly.
    /// </summary>
    /// <param name="nonce">A unique nonce for this request (should be generated server-side or using secure random)</param>
    /// <param name="onComplete">Callback with the integrity check result</param>
    public void RequestClassicIntegrityToken(string nonce, Action<IntegrityCheckResult> onComplete)
    {
        if (!enableIntegrityChecks)
        {
            onComplete?.Invoke(new IntegrityCheckResult
            {
                Success = true,
                Token = null,
                ErrorMessage = "Integrity checks disabled",
                CheckType = IntegrityCheckType.Classic
            });
            return;
        }

        StartCoroutine(RequestClassicIntegrityTokenCoroutine(nonce, onComplete));
    }

    private IEnumerator RequestClassicIntegrityTokenCoroutine(string nonce, Action<IntegrityCheckResult> onComplete)
    {
        if (verboseLogging)
        {
            Debug.Log($"[IntegrityManager] Requesting Classic Integrity token with nonce: {nonce.Substring(0, Math.Min(8, nonce.Length))}...");
        }

        var tokenRequest = new IntegrityTokenRequest(nonce, cloudProjectNumber);
        var requestOperation = classicIntegrityManager.RequestIntegrityToken(tokenRequest);

        yield return requestOperation;

        var result = new IntegrityCheckResult
        {
            CheckType = IntegrityCheckType.Classic,
            Timestamp = DateTime.UtcNow
        };

        if (requestOperation.Error != IntegrityErrorCode.NoError)
        {
            result.Success = false;
            result.ErrorCode = requestOperation.Error.ToString();
            result.ErrorMessage = $"Classic Integrity check failed: {requestOperation.Error}";
            
            Debug.LogWarning($"[IntegrityManager] {result.ErrorMessage}");
        }
        else
        {
            var tokenResponse = requestOperation.GetResult();
            result.Success = true;
            result.Token = tokenResponse.Token;

            if (verboseLogging)
            {
                Debug.Log($"[IntegrityManager] Classic Integrity token received (length: {result.Token?.Length ?? 0})");
            }
        }

        OnIntegrityCheckCompleted?.Invoke(result);
        onComplete?.Invoke(result);
    }

    /// <summary>
    /// Request a Standard Integrity token for frequent operations (e.g., app startup, game session start).
    /// Uses the cached token provider for better performance.
    /// </summary>
    /// <param name="requestHash">Optional hash of the request for tampering protection</param>
    /// <param name="onComplete">Callback with the integrity check result</param>
    public void RequestStandardIntegrityToken(string requestHash, Action<IntegrityCheckResult> onComplete)
    {
        if (!enableIntegrityChecks)
        {
            onComplete?.Invoke(new IntegrityCheckResult
            {
                Success = true,
                Token = null,
                ErrorMessage = "Integrity checks disabled",
                CheckType = IntegrityCheckType.Standard
            });
            return;
        }

        StartCoroutine(RequestStandardIntegrityTokenCoroutine(requestHash, onComplete));
    }

    private IEnumerator RequestStandardIntegrityTokenCoroutine(string requestHash, Action<IntegrityCheckResult> onComplete)
    {
        // Wait for token provider to be ready if it's still preparing
        while (isPreparingStandardTokenProvider)
        {
            yield return null;
        }

        if (!isStandardTokenProviderReady)
        {
            Debug.LogWarning("[IntegrityManager] Standard Integrity token provider not ready. Attempting to prepare now...");
            yield return PrepareStandardIntegrityTokenProvider();

            if (!isStandardTokenProviderReady)
            {
                var failureResult = new IntegrityCheckResult
                {
                    Success = false,
                    ErrorMessage = "Failed to prepare Standard Integrity token provider",
                    CheckType = IntegrityCheckType.Standard,
                    Timestamp = DateTime.UtcNow
                };
                onComplete?.Invoke(failureResult);
                yield break;
            }
        }

        if (verboseLogging)
        {
            Debug.Log($"[IntegrityManager] Requesting Standard Integrity token with requestHash: {requestHash?.Substring(0, Math.Min(8, requestHash?.Length ?? 0))}...");
        }

        var tokenRequest = new StandardIntegrityTokenRequest(requestHash);
        var requestOperation = cachedStandardTokenProvider.Request(tokenRequest);

        yield return requestOperation;

        var result = new IntegrityCheckResult
        {
            CheckType = IntegrityCheckType.Standard,
            Timestamp = DateTime.UtcNow
        };

        if (requestOperation.Error != StandardIntegrityErrorCode.NoError)
        {
            result.Success = false;
            result.ErrorCode = requestOperation.Error.ToString();
            result.ErrorMessage = $"Standard Integrity check failed: {requestOperation.Error}";
            
            Debug.LogWarning($"[IntegrityManager] {result.ErrorMessage}");
        }
        else
        {
            var tokenResponse = requestOperation.GetResult();
            result.Success = true;
            result.Token = tokenResponse.Token;

            if (verboseLogging)
            {
                Debug.Log($"[IntegrityManager] Standard Integrity token received (length: {result.Token?.Length ?? 0})");
            }
        }

        OnIntegrityCheckCompleted?.Invoke(result);
        onComplete?.Invoke(result);
    }

    /// <summary>
    /// Perform startup integrity check (Standard API - lightweight).
    /// Should be called once when the app starts.
    /// </summary>
    public void PerformStartupCheck(Action<IntegrityCheckResult> onComplete = null)
    {
        if (hasPerformedStartupCheck)
        {
            if (verboseLogging)
            {
                Debug.Log("[IntegrityManager] Startup check already performed this session. Returning cached result.");
            }
            onComplete?.Invoke(lastStartupCheckResult);
            return;
        }

        string requestHash = GenerateRequestHash("app_startup");
        RequestStandardIntegrityToken(requestHash, (result) =>
        {
            hasPerformedStartupCheck = true;
            lastStartupCheckResult = result;

            if (result.Success)
            {
                Debug.Log("[IntegrityManager] Startup integrity check passed.");
            }
            else
            {
                Debug.LogWarning($"[IntegrityManager] Startup integrity check failed: {result.ErrorMessage}");
            }

            onComplete?.Invoke(result);
        });
    }

    /// <summary>
    /// Perform game session integrity check (Standard API - lightweight).
    /// Can be called at the start of each game session.
    /// </summary>
    public void PerformGameSessionCheck(Action<IntegrityCheckResult> onComplete = null)
    {
        string requestHash = GenerateRequestHash($"game_session_{DateTime.UtcNow.Ticks}");
        RequestStandardIntegrityToken(requestHash, (result) =>
        {
            lastGameSessionCheckResult = result;

            if (result.Success)
            {
                Debug.Log("[IntegrityManager] Game session integrity check passed.");
            }
            else
            {
                Debug.LogWarning($"[IntegrityManager] Game session integrity check failed: {result.ErrorMessage}");
            }

            onComplete?.Invoke(result);
        });
    }

    /// <summary>
    /// Generate a simple request hash for Standard Integrity API.
    /// In production, consider using more sophisticated hashing based on request context.
    /// </summary>
    private string GenerateRequestHash(string context)
    {
        // Simple hash generation - in production, use proper cryptographic hashing
        return $"{context}_{DateTime.UtcNow.Ticks}_{UnityEngine.Random.Range(1000, 9999)}";
    }

    /// <summary>
    /// Generate a nonce for Classic Integrity API.
    /// In production, nonces should be generated server-side and verified server-side.
    /// </summary>
    public static string GenerateNonce(int length = 32)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var nonce = new char[length];
        for (int i = 0; i < length; i++)
        {
            nonce[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        }
        return new string(nonce);
    }

    private void OnDestroy()
    {
        // Unregister from DependencyRegistry
        DependencyRegistry.Unregister<IntegrityManager>(this);
    }
}

/// <summary>
/// Result of an integrity check
/// </summary>
[Serializable]
public class IntegrityCheckResult
{
    public bool Success;
    public string Token;
    public string ErrorCode;
    public string ErrorMessage;
    public IntegrityCheckType CheckType;
    public DateTime Timestamp;
}

/// <summary>
/// Type of integrity check performed
/// </summary>
public enum IntegrityCheckType
{
    Classic,  // Full integrity check for critical operations
    Standard  // Lightweight check for frequent operations
}

