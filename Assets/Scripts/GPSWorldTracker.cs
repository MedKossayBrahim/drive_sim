using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Reads the device GPS, advances a virtual GPS point at a configurable
/// multiplier (default: 1 real metre = 1 virtual kilometre), reverse-geocodes
/// that virtual point with LocationIQ, and draws a GTA-style location banner at
/// the top of the screen.
///
/// HOW THE VIRTUAL GPS WORKS:
///     * The first accepted fix becomes the starting point.
///     * Every real-world metre travelled moves the virtual latitude/longitude
///       by "simulatedMetersPerRealMeter" metres along the real bearing.
///       With the default of 1000, walking 1 m moves the GPS point 1 km.
///     * The virtual point is what gets reverse-geocoded and shown in the HUD.
///     * The virtual point is also placed in the Unity world using the fixed
///       1 : 1000 compression (1 virtual kilometre = 1 Unity unit).
///
/// CONFLICT AVOIDANCE WITH GyroCarController:
///     GyroCarController owns the car's Rigidbody (MovePosition + linearVelocity).
///     This component NEVER writes Rigidbody velocity or forces. It either:
///       * moves a plain container Transform (no Rigidbody) as an additive world
///         offset on top of gyro physics movement, or
///       * if the target owns a Rigidbody, only repositions it with MovePosition
///         while the car is (almost) stationary, so it never fights the physics.
///     GyroCarController.cs is intentionally left unmodified.
///
/// IMPORTANT:
/// - Attach this component to a manager GameObject, not necessarily the car.
/// - Assign the object that should follow the GPS position to "movementTarget".
///   For a car driven by GyroCarController, prefer a parent container without a
///   Rigidbody so the two systems operate on different transforms.
/// - Add LocationIQ attribution to the UI: "© OpenStreetMap contributors / LocationIQ".
/// </summary>
public class GPSWorldTracker : MonoBehaviour
{
    [Header("LocationIQ")]
    [Tooltip("Enter your LocationIQ public access token in the Inspector. " +
             "For production apps, proxy this request through your backend.")]
    [SerializeField] private string locationIqApiKey = "pk.bc3d3f68e9c30032109748d7534ef20a";

    [Tooltip("Language requested for the returned address.")]
    [SerializeField] private string addressLanguage = "en";

    [Header("GPS Startup")]
    [Min(1f)]
    [SerializeField] private float desiredAccuracyInMeters = 10f;

    [Tooltip("Ask the device for a new fix after about this much real movement. " +
             "1 m keeps the 1 m -> 1 km virtual movement responsive.")]
    [Min(1f)]
    [SerializeField] private float gpsUpdateDistanceInMeters = 1f;

    [Min(1f)]
    [SerializeField] private float gpsStartupTimeoutSeconds = 20f;

    [Tooltip("GPS fixes with worse horizontal accuracy than this are discarded.")]
    [Min(1f)]
    [SerializeField] private float maximumAcceptedAccuracyMeters = 15f;

    [Header("Virtual GPS (1 m real = 1 km GPS)")]
    [Tooltip("Move the virtual GPS point faster than the real device. This is the " +
             "core 1 m real -> 1 km virtual behaviour.")]
    [SerializeField] private bool simulateGpsTravel = true;

    [Tooltip("Virtual metres added to the GPS latitude/longitude for every real " +
             "metre travelled. 1000 = 1 real metre becomes 1 virtual kilometre.")]
    [Min(1f)]
    [SerializeField] private float simulatedMetersPerRealMeter = 1000f;

    [Tooltip("Real movement below this many metres is ignored as GPS jitter.")]
    [Min(0f)]
    [SerializeField] private float minimumRealMovementMeters = 0.5f;

    [Tooltip("A single GPS fix that jumps farther than this is treated as noise " +
             "and ignored. Set to 0 to disable the guard.")]
    [Min(0f)]
    [SerializeField] private float maximumRealJumpMeters = 100f;

    [Header("World Mapping (1 : 1000)")]
    [Tooltip("Virtual metres represented by one Unity world unit. " +
             "1000 => 1 virtual kilometre becomes 1 Unity unit.")]
    [Min(1f)]
    [SerializeField] private float realMetersPerUnityUnit = 1000f;

    [Tooltip("The object whose world position should follow the GPS. Prefer a " +
             "container without a Rigidbody. If empty, a GyroCarController in the scene is used.")]
    [SerializeField] private Transform movementTarget;

    [Tooltip("Optional Rigidbody on movementTarget. Leave empty to move its Transform. " +
             "Auto-detected when movementTarget has one.")]
    [SerializeField] private Rigidbody targetRigidbody;

    [Tooltip("Higher values make the target catch up to GPS updates faster.")]
    [Min(0.1f)]
    [SerializeField] private float movementSmoothing = 4f;

    [Tooltip("Keep the target's current height instead of changing its Y position.")]
    [SerializeField] private bool preserveTargetHeight = true;

    [Header("Conflict Avoidance")]
    [Tooltip("When the target owns a Rigidbody, only correct its position while it is " +
             "(almost) stationary so GPS never fights gyro-controlled physics.")]
    [SerializeField] private bool onlyMoveWhenStationary = true;

    [Tooltip("Speed (Unity units/s) below which the target is treated as stationary.")]
    [Min(0f)]
    [SerializeField] private float stationarySpeedThreshold = 0.05f;

    [Header("Reverse Geocoding")]
    [Tooltip("Request a new address after the VIRTUAL point has moved at least " +
             "this many metres. Default 1000 = roughly one request per virtual km.")]
    [Min(1f)]
    [SerializeField] private float geocodeAfterMovementMeters = 1000f;

    [Tooltip("Minimum time between actual reverse-geocoding network requests. " +
             "Keeps the LocationIQ free tier happy.")]
    [Min(1f)]
    [SerializeField] private float geocodeMinIntervalSeconds = 3f;

    [Tooltip("A cached place name within this radius is reused instead of calling the API.")]
    [Min(1f)]
    [SerializeField] private float geocodeCacheRadiusMeters = 300f;

    [Min(1)]
    [SerializeField] private int geocodeCacheSize = 20;

    [Min(1)]
    [SerializeField] private int requestTimeoutSeconds = 15;

    [Header("Optional UI")]
    [SerializeField] private Text placeNameText;
    [SerializeField] private Text coordinatesText;
    [SerializeField] private Text gpsStatusText;

    [Header("GTA-Style HUD")]
    [Tooltip("Draw the location banner at the top of the screen with IMGUI.")]
    [SerializeField] private bool showScreenOverlay = true;

    [SerializeField] private float hudTopMargin = 14f;

    [SerializeField] private float hudMaxWidth = 620f;

    [Tooltip("Also show the real device coordinates in the HUD.")]
    [SerializeField] private bool showRealCoordinatesInHud = true;

    [Header("Desktop Preview")]
    [Tooltip("Editor/desktop only. Fakes a GPS fix so the HUD and simulation can " +
             "be tested without a phone. Android/iOS always use the real GPS.")]
    [SerializeField] private bool enableDesktopPreview = true;

    [Tooltip("Starting latitude used by the desktop preview.")]
    [SerializeField] private double desktopPreviewLatitude = 48.8566d;

    [Tooltip("Starting longitude used by the desktop preview.")]
    [SerializeField] private double desktopPreviewLongitude = 2.3522d;

    [Tooltip("Automatically walk the fake GPS so the 1 m -> 1 km behaviour is " +
             "visible in the editor. Turn off to avoid using LocationIQ quota.")]
    [SerializeField] private bool desktopPreviewAutoWalk = true;

    [Min(0.1f)]
    [SerializeField] private float desktopPreviewSpeedMetersPerSecond = 1f;

    [SerializeField] private float desktopPreviewHeadingDegrees = 90f;

    [Header("Debug")]
    [SerializeField] private bool logGpsUpdates = true;

    /// <summary>Fired after a successful reverse-geocoding response (or a cached lookup).</summary>
    public event Action<string, LocationIqAddress> PlaceUpdated;

    /// <summary>Virtual (simulated) latitude currently shown on the HUD.</summary>
    public double Latitude { get; private set; }

    /// <summary>Virtual (simulated) longitude currently shown on the HUD.</summary>
    public double Longitude { get; private set; }

    /// <summary>Latest real device latitude.</summary>
    public double RealLatitude { get; private set; }

    /// <summary>Latest real device longitude.</summary>
    public double RealLongitude { get; private set; }

    /// <summary>Latest accepted GPS accuracy in metres.</summary>
    public float AccuracyMeters { get; private set; }

    /// <summary>Latest LocationIQ display_name.</summary>
    public string CurrentPlaceName { get; private set; }

    /// <summary>Structured address for the latest reverse-geocoding result.</summary>
    public LocationIqAddress CurrentAddress { get; private set; }

    public bool IsGpsRunning { get; private set; }

    public bool IsSimulationActive => simulateGpsTravel;

    /// <summary>Virtual distance travelled since the starting fix, in metres.</summary>
    public double VirtualTravelMeters { get; private set; }

    /// <summary>Real distance travelled since the starting fix, in metres.</summary>
    public double RealTravelMeters { get; private set; }

    private bool hasWorldOrigin;
    private double originLatitude;
    private double originLongitude;
    private Vector3 originWorldPosition;

    // Accumulated virtual displacement, already converted to Unity world units.
    private Vector3 gpsWorldOffset;
    private Vector3 desiredWorldPosition;

    private bool hasGeocodedPosition;
    private double lastGeocodedLatitude;
    private double lastGeocodedLongitude;
    private float lastGeocodeRequestTime = -999f;
    private bool geocodeRequestInProgress;

    private bool hasAcceptedFix;
    private double lastAcceptedLatitude;
    private double lastAcceptedLongitude;

    private bool isDesktopPreview;

    private readonly List<GeocodeCacheEntry> geocodeCache = new List<GeocodeCacheEntry>();

    private GUIStyle hudTitleStyle;
    private GUIStyle hudSubtitleStyle;
    private GUIStyle hudCoordsStyle;
    private GUIStyle hudTripStyle;
    private GUIStyle hudStatusStyle;
    private GUIStyle hudBadgeStyle;
    private GUIStyle hudAttributionStyle;
    private Texture2D hudPanelTexture;
    private Texture2D hudAccentTexture;
    private Texture2D hudShadowTexture;
    private int hudStyledForHeight = -1;
    private float hudStyleScale = 1f;

    private string currentStatus = "Starting...";

#if UNITY_ANDROID
    private bool permissionRequestCompleted;
#endif

    [Serializable]
    private class LocationIqResponse
    {
        public string display_name;
        public LocationIqAddress address;
    }

    [Serializable]
    public class LocationIqAddress
    {
        public string house_number;
        public string road;
        public string neighbourhood;
        public string suburb;
        public string city;
        public string town;
        public string village;
        public string county;
        public string state;
        public string postcode;
        public string country;
        public string country_code;
    }

    [Serializable]
    private class GeocodeCacheEntry
    {
        public double latitude;
        public double longitude;
        public string displayName;
        public LocationIqAddress address;
    }

    private void Start()
    {
        ResolveMovementTarget();

        if (realMetersPerUnityUnit <= 0f)
            realMetersPerUnityUnit = 1000f;

        if (simulatedMetersPerRealMeter <= 0f)
            simulatedMetersPerRealMeter = 1000f;

        desiredWorldPosition = movementTarget != null
            ? movementTarget.position
            : transform.position;

        StartCoroutine(StartGps());
    }

    /// <summary>
    /// Crée le tracker seulement s'il y a déjà une voiture dans la scène.
    /// Sinon la carte GPS n'apparaît pas sur le login / hub (voir CarRide).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (FindAnyObjectByType<GPSWorldTracker>() != null)
            return;
        if (FindAnyObjectByType<GyroCarController>() == null)
            return;

        GameObject host = new GameObject("GPSWorldTracker (auto)");
        host.AddComponent<GPSWorldTracker>();
    }

    /// <summary>Appelé par CarRide au démarrage de la course voiture.</summary>
    public static void EnsureForCarRide()
    {
        if (FindAnyObjectByType<GPSWorldTracker>() != null)
            return;

        GameObject host = new GameObject("GPSWorldTracker (auto)");
        host.AddComponent<GPSWorldTracker>();
    }

    /// <summary>
    /// Wires the component to the player car when no explicit target is assigned.
    /// It only reads GyroCarController; its logic is never modified.
    /// </summary>
    private void ResolveMovementTarget()
    {
        if (movementTarget == null)
        {
            GyroCarController car = FindAnyObjectByType<GyroCarController>();

            if (car != null)
            {
                movementTarget = car.transform;
                Debug.Log("[GPSWorldTracker] No movementTarget assigned; using the GyroCarController car. " +
                          "For smoother results, parent the car under an empty container and assign that container.");
            }
            else
            {
                movementTarget = transform;
            }
        }

        if (targetRigidbody == null && movementTarget != null)
            targetRigidbody = movementTarget.GetComponent<Rigidbody>();

        if (targetRigidbody != null)
        {
            Debug.Log("[GPSWorldTracker] GPS target owns a Rigidbody. GPS corrections are applied " +
                      "without overwriting its velocity or forces" +
                      (onlyMoveWhenStationary ? " and only while the car is stationary." : "."));
        }
    }

    private IEnumerator StartGps()
    {
        SetStatus("Checking location services...");

        // The editor can be set to the Android build target (so UNITY_ANDROID is
        // defined) while still running on a PC without device GPS. Prefer the
        // preview there; a real Android/iOS build never takes this branch.
#if UNITY_EDITOR || UNITY_STANDALONE
        if (enableDesktopPreview)
        {
            yield return RunDesktopPreview();
            yield break;
        }
#endif

#if UNITY_ANDROID
        // Google Play compliant flow: ask the user before touching location APIs.
        // Requesting ACCESS_FINE_LOCATION also covers ACCESS_COARSE_LOCATION (the
        // Android 12+ dialog lets the user pick Precise or Approximate).
        if (!HasLocationPermission())
        {
            SetStatus("Requesting location permission...");
            yield return RequestLocationPermission();

            if (!HasLocationPermission())
            {
                SetStatus("Location permission denied. GPS tracking is disabled.");
                Debug.LogError("[GPSWorldTracker] ACCESS_FINE/COARSE_LOCATION permission was not granted.");
                yield break;
            }
        }
#endif

#if UNITY_ANDROID || UNITY_IOS
        if (!Input.location.isEnabledByUser)
        {
            SetStatus("Location is disabled. Enable GPS/location in device settings.");
            Debug.LogError("[GPSWorldTracker] Location services are disabled by the user.");
            yield break;
        }

        Input.location.Start(desiredAccuracyInMeters, gpsUpdateDistanceInMeters);

        float elapsed = 0f;
        while (Input.location.status == LocationServiceStatus.Initializing &&
               elapsed < gpsStartupTimeoutSeconds)
        {
            elapsed += 1f;
            SetStatus("Waiting for GPS fix...");
            yield return new WaitForSeconds(1f);
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            SetStatus("GPS failed: " + Input.location.status);
            Debug.LogError("[GPSWorldTracker] GPS failed to start: " + Input.location.status);
            yield break;
        }

        IsGpsRunning = true;
        SetStatus("GPS running. Waiting for an accurate fix...");
        Debug.Log("[GPSWorldTracker] GPS service started.");
#else
        if (enableDesktopPreview)
        {
            yield return RunDesktopPreview();
            yield break;
        }

        SetStatus("GPS is only available on a mobile device.");
        Debug.LogWarning("[GPSWorldTracker] Device GPS is unavailable on this platform. " +
                         "Enable Desktop Preview to test the HUD in the editor.");
#endif
    }

    /// <summary>
    /// Editor/desktop fallback. Feeds synthetic fixes through exactly the same
    /// pipeline as the real GPS, so the virtual multiplier and HUD can be tested
    /// without a phone.
    /// </summary>
    private IEnumerator RunDesktopPreview()
    {
        isDesktopPreview = true;
        IsGpsRunning = true;

        double realLatitude = desktopPreviewLatitude;
        double realLongitude = desktopPreviewLongitude;
        float accuracy = Mathf.Max(1f, desiredAccuracyInMeters);
        double headingRadians = desktopPreviewHeadingDegrees * Math.PI / 180d;

        SetStatus("Desktop preview | simulated GPS");
        ProcessFix(realLatitude, realLongitude, accuracy);

        while (isDesktopPreview)
        {
            if (!desktopPreviewAutoWalk)
            {
                yield return null;
                continue;
            }

            yield return new WaitForSeconds(1f);

            double stepMeters = Mathf.Max(0.1f, desktopPreviewSpeedMetersPerSecond);
            OffsetLatLon(
                realLatitude,
                realLongitude,
                stepMeters,
                headingRadians,
                out double nextLatitude,
                out double nextLongitude);

            realLatitude = nextLatitude;
            realLongitude = nextLongitude;

            ProcessFix(realLatitude, realLongitude, accuracy);
        }
    }

#if UNITY_ANDROID
    /// <summary>True when either precise or approximate location has been authorised.</summary>
    private static bool HasLocationPermission()
    {
        return Permission.HasUserAuthorizedPermission(Permission.FineLocation) ||
               Permission.HasUserAuthorizedPermission(Permission.CoarseLocation);
    }

    private IEnumerator RequestLocationPermission()
    {
        permissionRequestCompleted = false;

        PermissionCallbacks callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => permissionRequestCompleted = true;
        callbacks.PermissionDenied += _ => permissionRequestCompleted = true;
        callbacks.PermissionDeniedAndDontAskAgain += _ => permissionRequestCompleted = true;

        // Android adds ACCESS_COARSE_LOCATION automatically when fine location is
        // requested, so a single request covers both permissions.
        Permission.RequestUserPermission(Permission.FineLocation, callbacks);

        float elapsed = 0f;
        while (!permissionRequestCompleted && elapsed < gpsStartupTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
#endif

    private void Update()
    {
        if (isDesktopPreview)
            return;

        if (!IsGpsRunning || Input.location.status != LocationServiceStatus.Running)
            return;

        LocationInfo data = Input.location.lastData;
        ProcessFix(data.latitude, data.longitude, data.horizontalAccuracy);
    }

    /// <summary>
    /// Shared pipeline for real and preview fixes. The real fix is used to move
    /// the virtual GPS point; the virtual point drives the HUD and geocoding.
    /// </summary>
    private void ProcessFix(double realLatitude, double realLongitude, float accuracy)
    {
        if (realLatitude == 0d && realLongitude == 0d)
            return;

        // Discard low-precision fixes.
        if (accuracy < 0f || accuracy > maximumAcceptedAccuracyMeters)
        {
            SetStatus("GPS accuracy too low: " + accuracy.ToString("F0") + " m");
            return;
        }

        double movedMeters = 0d;
        double bearingRadians = 0d;

        if (hasAcceptedFix)
        {
            movedMeters = DistanceMeters(
                lastAcceptedLatitude,
                lastAcceptedLongitude,
                realLatitude,
                realLongitude);

            // Avoid repeatedly processing the exact same fix / tiny jitter.
            if (movedMeters < minimumRealMovementMeters)
                return;

            if (maximumRealJumpMeters > 0f && movedMeters > maximumRealJumpMeters)
            {
                Debug.LogWarning(
                    "[GPSWorldTracker] Ignored GPS jump of " +
                    movedMeters.ToString("F0") + " m (noise guard).");

                // Resync so the next fix is measured from the new position.
                lastAcceptedLatitude = realLatitude;
                lastAcceptedLongitude = realLongitude;
                return;
            }

            bearingRadians = BearingRadians(
                lastAcceptedLatitude,
                lastAcceptedLongitude,
                realLatitude,
                realLongitude);
        }

        RealLatitude = realLatitude;
        RealLongitude = realLongitude;
        AccuracyMeters = accuracy;

        if (!hasWorldOrigin)
        {
            EstablishWorldOrigin(realLatitude, realLongitude);
        }
        else
        {
            RealTravelMeters += movedMeters;

            // 1 real metre -> simulatedMetersPerRealMeter virtual metres.
            double virtualMeters = simulateGpsTravel
                ? movedMeters * simulatedMetersPerRealMeter
                : movedMeters;

            if (simulateGpsTravel)
            {
                OffsetLatLon(
                    Latitude,
                    Longitude,
                    virtualMeters,
                    bearingRadians,
                    out double nextLatitude,
                    out double nextLongitude);

                Latitude = nextLatitude;
                Longitude = nextLongitude;
                VirtualTravelMeters += virtualMeters;
            }
            else
            {
                Latitude = realLatitude;
                Longitude = realLongitude;
                VirtualTravelMeters += movedMeters;
            }

            // The virtual point is placed in the world with the fixed 1 : 1000
            // compression, so 1 virtual km becomes 1 Unity unit.
            double eastMeters = virtualMeters * Math.Sin(bearingRadians);
            double northMeters = virtualMeters * Math.Cos(bearingRadians);

            gpsWorldOffset += new Vector3(
                (float)(eastMeters / realMetersPerUnityUnit),
                0f,
                (float)(northMeters / realMetersPerUnityUnit));

            desiredWorldPosition = originWorldPosition + gpsWorldOffset;
        }

        hasAcceptedFix = true;
        lastAcceptedLatitude = realLatitude;
        lastAcceptedLongitude = realLongitude;

        UpdateCoordinatesUI();

        SetStatus(
            simulateGpsTravel
                ? "SIM GPS x" + simulatedMetersPerRealMeter.ToString("F0") +
                  " | Accuracy " + accuracy.ToString("F1") + " m"
                : "GPS OK | Accuracy: " + accuracy.ToString("F1") + " m");

        if (logGpsUpdates)
        {
            Debug.Log(
                "[GPSWorldTracker] REAL: " +
                realLatitude.ToString("F6") + ", " +
                realLongitude.ToString("F6") +
                " | VIRTUAL: " +
                Latitude.ToString("F6") + ", " +
                Longitude.ToString("F6") +
                " | real moved: " + movedMeters.ToString("F1") + " m" +
                " | virtual trip: " + (VirtualTravelMeters / 1000d).ToString("F2") + " km");
        }

        TryReverseGeocode(Latitude, Longitude);
    }

    private void EstablishWorldOrigin(double realLatitude, double realLongitude)
    {
        hasWorldOrigin = true;
        originLatitude = realLatitude;
        originLongitude = realLongitude;

        Latitude = realLatitude;
        Longitude = realLongitude;

        originWorldPosition = movementTarget != null
            ? movementTarget.position
            : transform.position;

        gpsWorldOffset = Vector3.zero;
        desiredWorldPosition = originWorldPosition;
        RealTravelMeters = 0d;
        VirtualTravelMeters = 0d;

        Debug.Log(
            "[GPSWorldTracker] Start location established at " +
            Latitude.ToString("F6") + ", " +
            Longitude.ToString("F6") +
            ". Virtual multiplier: x" + simulatedMetersPerRealMeter.ToString("F0") + ".");
    }

    private void FixedUpdate()
    {
        if (!hasWorldOrigin || movementTarget == null)
            return;

        bool hasBody = targetRigidbody != null;

        if (hasBody)
        {
            if (onlyMoveWhenStationary)
            {
                bool moving = targetRigidbody.linearVelocity.sqrMagnitude >
                              stationarySpeedThreshold * stationarySpeedThreshold;

                if (moving)
                {
                    // The car is being driven manually. Keep the GPS world anchor
                    // under it, otherwise the tracker drags the car back toward its
                    // old GPS position as soon as the player releases the gas.
                    originWorldPosition = targetRigidbody.position - gpsWorldOffset;
                    desiredWorldPosition = targetRigidbody.position;
                }
            }

            // A Rigidbody driven by GyroCarController must never also be moved
            // by the GPS interpolation path. Otherwise releasing the gas makes
            // the tracker pull the car toward its previous GPS target.
            return;
        }

        Vector3 currentPosition = hasBody ? targetRigidbody.position : movementTarget.position;

        // Smoothed interpolation removes jitter from inconsistent GPS readings.
        Vector3 targetPosition = Vector3.Lerp(
            currentPosition,
            desiredWorldPosition,
            1f - Mathf.Exp(-movementSmoothing * Time.fixedDeltaTime));

        if (preserveTargetHeight)
            targetPosition.y = currentPosition.y;

        if (hasBody)
        {
            // MovePosition changes position only. It never writes linearVelocity or
            // applied forces, so gyro-controlled physics keeps working untouched.
            targetRigidbody.MovePosition(targetPosition);
        }
        else
        {
            // Plain container: an additive world offset on top of gyro movement.
            movementTarget.position = targetPosition;
        }
    }

    private void TryReverseGeocode(double latitude, double longitude)
    {
        if (geocodeRequestInProgress)
            return;

        if (string.IsNullOrWhiteSpace(locationIqApiKey))
        {
            SetStatus("GPS OK, but LocationIQ API key is missing.");
            return;
        }

        bool intervalElapsed = Time.time - lastGeocodeRequestTime >= geocodeMinIntervalSeconds;
        bool shouldRequest;

        if (!hasGeocodedPosition)
        {
            // Need the first place name, but still obey the retry interval so a
            // failed request cannot spam the API on every GPS fix.
            shouldRequest = intervalElapsed;
        }
        else
        {
            double distance = DistanceMeters(
                lastGeocodedLatitude,
                lastGeocodedLongitude,
                latitude,
                longitude);

            // Both thresholds must be met: significant virtual displacement AND
            // a minimum interval since the last request.
            bool movedEnough = distance >= geocodeAfterMovementMeters;
            shouldRequest = movedEnough && intervalElapsed;
        }

        if (!shouldRequest)
            return;

        // Reuse a recent result to avoid redundant network calls.
        if (TryGetCachedGeocode(latitude, longitude, out string cachedName, out LocationIqAddress cachedAddress))
        {
            lastGeocodeRequestTime = Time.time;
            ApplyPlaceResult(cachedName, cachedAddress, latitude, longitude);
            return;
        }

        lastGeocodeRequestTime = Time.time;
        StartCoroutine(ReverseGeocode(latitude, longitude));
    }

    private IEnumerator ReverseGeocode(double latitude, double longitude)
    {
        geocodeRequestInProgress = true;

        string url =
            "https://us1.locationiq.com/v1/reverse" +
            "?key=" + UnityWebRequest.EscapeURL(locationIqApiKey) +
            "&lat=" + latitude.ToString(
                System.Globalization.CultureInfo.InvariantCulture) +
            "&lon=" + longitude.ToString(
                System.Globalization.CultureInfo.InvariantCulture) +
            "&format=json" +
            "&accept-language=" + UnityWebRequest.EscapeURL(addressLanguage);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = requestTimeoutSeconds;
            SetStatus("Getting place name...");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    "[GPSWorldTracker] LocationIQ request failed: " +
                    request.error + " | " + request.downloadHandler.text);

                SetStatus(request.responseCode == 429
                    ? "LocationIQ rate limit reached. Retrying shortly..."
                    : "GPS OK, reverse geocoding failed.");

                geocodeRequestInProgress = false;
                yield break;
            }

            LocationIqResponse response = null;

            try
            {
                response = JsonUtility.FromJson<LocationIqResponse>(
                    request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                Debug.LogError("[GPSWorldTracker] JSON parsing error: " + exception);
            }

            if (response == null || string.IsNullOrWhiteSpace(response.display_name))
            {
                Debug.LogWarning("[GPSWorldTracker] LocationIQ returned no display_name.");
                SetStatus("Location found, but no place name was returned.");
                geocodeRequestInProgress = false;
                yield break;
            }

            AddToCache(latitude, longitude, response.display_name, response.address);
            ApplyPlaceResult(response.display_name, response.address, latitude, longitude);

            Debug.Log("[GPSWorldTracker] Current place: " + response.display_name);
        }

        geocodeRequestInProgress = false;
    }

    private void ApplyPlaceResult(string placeName, LocationIqAddress address, double latitude, double longitude)
    {
        CurrentPlaceName = placeName;
        CurrentAddress = address;
        hasGeocodedPosition = true;
        lastGeocodedLatitude = latitude;
        lastGeocodedLongitude = longitude;

        if (placeNameText != null)
            placeNameText.text = ArabicTextSupport.PrepareForDisplay(placeName);

        SetStatus("Place: " + placeName);
        PlaceUpdated?.Invoke(placeName, address);
    }

    private bool TryGetCachedGeocode(double latitude, double longitude, out string placeName, out LocationIqAddress address)
    {
        for (int i = 0; i < geocodeCache.Count; i++)
        {
            GeocodeCacheEntry entry = geocodeCache[i];

            if (DistanceMeters(entry.latitude, entry.longitude, latitude, longitude) <= geocodeCacheRadiusMeters)
            {
                placeName = entry.displayName;
                address = entry.address;
                return true;
            }
        }

        placeName = null;
        address = null;
        return false;
    }

    private void AddToCache(double latitude, double longitude, string placeName, LocationIqAddress address)
    {
        GeocodeCacheEntry entry = new GeocodeCacheEntry
        {
            latitude = latitude,
            longitude = longitude,
            displayName = placeName,
            address = address
        };

        for (int i = 0; i < geocodeCache.Count; i++)
        {
            if (DistanceMeters(geocodeCache[i].latitude, geocodeCache[i].longitude, latitude, longitude) <=
                geocodeCacheRadiusMeters)
            {
                geocodeCache[i] = entry;
                return;
            }
        }

        geocodeCache.Add(entry);

        while (geocodeCache.Count > Mathf.Max(1, geocodeCacheSize))
            geocodeCache.RemoveAt(0);
    }

    private void UpdateCoordinatesUI()
    {
        if (coordinatesText == null)
            return;

        coordinatesText.text =
            "GPS: " + Latitude.ToString("F6") + ", " + Longitude.ToString("F6") +
            "\nReal: " + RealLatitude.ToString("F6") + ", " + RealLongitude.ToString("F6") +
            "\nVirtual trip: " + (VirtualTravelMeters / 1000d).ToString("F2") + " km";
    }

    private void SetStatus(string message)
    {
        currentStatus = message;

        if (gpsStatusText != null)
            gpsStatusText.text = ArabicTextSupport.PrepareForDisplay(message);
    }

    private static double DistanceMeters(
        double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000d;

        double dLat = (lat2 - lat1) * Math.PI / 180d;
        double dLon = (lon2 - lon1) * Math.PI / 180d;

        double a =
            Math.Sin(dLat / 2d) * Math.Sin(dLat / 2d) +
            Math.Cos(lat1 * Math.PI / 180d) *
            Math.Cos(lat2 * Math.PI / 180d) *
            Math.Sin(dLon / 2d) * Math.Sin(dLon / 2d);

        double c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return earthRadiusMeters * c;
    }

    private static double BearingRadians(
        double lat1, double lon1, double lat2, double lon2)
    {
        double lat1Rad = lat1 * Math.PI / 180d;
        double lat2Rad = lat2 * Math.PI / 180d;
        double dLon = (lon2 - lon1) * Math.PI / 180d;

        double y = Math.Sin(dLon) * Math.Cos(lat2Rad);
        double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                   Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);

        // 0 = north, increasing clockwise; Unity world +Z maps to north, +X to east.
        return Math.Atan2(y, x);
    }

    /// <summary>
    /// Moves a latitude/longitude point by a distance along a bearing. This is
    /// what turns "1 m real" into "1 km virtual" without accumulating Mercator
    /// projection errors.
    /// </summary>
    private static void OffsetLatLon(
        double latitude,
        double longitude,
        double distanceMeters,
        double bearingRadians,
        out double resultLatitude,
        out double resultLongitude)
    {
        const double earthRadiusMeters = 6371000d;

        double angularDistance = distanceMeters / earthRadiusMeters;
        double latitudeRadians = latitude * Math.PI / 180d;
        double longitudeRadians = longitude * Math.PI / 180d;

        double sinLatitude = Math.Sin(latitudeRadians);
        double cosLatitude = Math.Cos(latitudeRadians);
        double sinAngularDistance = Math.Sin(angularDistance);
        double cosAngularDistance = Math.Cos(angularDistance);

        double nextLatitudeRadians = Math.Asin(
            sinLatitude * cosAngularDistance +
            cosLatitude * sinAngularDistance * Math.Cos(bearingRadians));

        double nextLongitudeRadians = longitudeRadians + Math.Atan2(
            Math.Sin(bearingRadians) * sinAngularDistance * cosLatitude,
            cosAngularDistance - sinLatitude * Math.Sin(nextLatitudeRadians));

        resultLatitude = Math.Max(-85d, Math.Min(85d, nextLatitudeRadians * 180d / Math.PI));
        resultLongitude = NormalizeLongitude(nextLongitudeRadians * 180d / Math.PI);
    }

    private static double NormalizeLongitude(double longitude)
    {
        longitude = (longitude + 180d) % 360d;

        if (longitude < 0d)
            longitude += 360d;

        return longitude - 180d;
    }

    /// <summary>
    /// Call this from a UI button if you want to reset the local map origin and
    /// the virtual GPS point to the phone's current REAL GPS location.
    /// </summary>
    public void ResetWorldOrigin()
    {
        if (!hasAcceptedFix || movementTarget == null)
            return;

        Latitude = RealLatitude;
        Longitude = RealLongitude;

        originLatitude = RealLatitude;
        originLongitude = RealLongitude;
        originWorldPosition = movementTarget.position;
        gpsWorldOffset = Vector3.zero;
        desiredWorldPosition = originWorldPosition;
        hasWorldOrigin = true;

        RealTravelMeters = 0d;
        VirtualTravelMeters = 0d;

        hasGeocodedPosition = false;
        CurrentPlaceName = null;
        CurrentAddress = null;
        geocodeCache.Clear();

        SetStatus("Simulation reset to the current real GPS position.");
        UpdateCoordinatesUI();
        TryReverseGeocode(Latitude, Longitude);

        Debug.Log("[GPSWorldTracker] Virtual GPS reset to real position: " +
                  Latitude.ToString("F6") + ", " + Longitude.ToString("F6"));
    }

    /// <summary>Editor/testing helper: start or stop the fake GPS walk.</summary>
    public void SetDesktopPreviewAutoWalk(bool enabled)
    {
        desktopPreviewAutoWalk = enabled;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused || isDesktopPreview)
            return;

        if (IsGpsRunning && Input.location.status != LocationServiceStatus.Running)
        {
            IsGpsRunning = false;
            StartCoroutine(StartGps());
        }
    }

    private void OnGUI()
    {
        if (!showScreenOverlay)
            return;

        // Pas de carte GPS hors course voiture (login, hub, autres véhicules).
        if (FindAnyObjectByType<GyroCarController>() == null && FindAnyObjectByType<CarRide>() == null)
            return;

        EnsureHudResources();

        float scale = hudStyleScale;
        float margin = Mathf.Max(8f, 10f * scale);
        float panelWidth = Mathf.Min(hudMaxWidth * scale, Screen.width - margin * 2f);
        float padding = 16f * scale;
        float innerWidth = panelWidth - padding * 2f;

        string badgeText = simulateGpsTravel
            ? "SIM GPS  x" + simulatedMetersPerRealMeter.ToString("F0")
            : "REAL GPS";

        string placeTitle = BuildHudTitle();
        string placeSubtitle = BuildHudSubtitle();

        GUIContent titleContent = new GUIContent(placeTitle);
        GUIContent subtitleContent = new GUIContent(placeSubtitle);

        float titleHeight = hudTitleStyle.CalcHeight(titleContent, innerWidth);
        float subtitleHeight = string.IsNullOrEmpty(placeSubtitle)
            ? 0f
            : hudSubtitleStyle.CalcHeight(subtitleContent, innerWidth);

        float badgeHeight = hudBadgeStyle.fontSize + 10f * scale;
        float coordsHeight = hudCoordsStyle.fontSize * 1.5f;
        float tripHeight = hudTripStyle.fontSize * 1.5f;
        float attributionHeight = hudAttributionStyle.fontSize * 1.7f;

        float panelHeight =
            padding +
            badgeHeight + 6f * scale +
            titleHeight + 2f * scale +
            (subtitleHeight > 0f ? subtitleHeight + 8f * scale : 0f) +
            coordsHeight + 2f * scale +
            tripHeight + 4f * scale +
            attributionHeight +
            padding;

        // Top-right keeps the banner clear of GyroCarController's large top-left
        // debug overlay while still reading like a GTA-style location popup.
        float panelX = Screen.width - panelWidth - margin;
        float panelY = hudTopMargin * scale;
        Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);

        // Soft drop shadow keeps the banner readable over bright city scenes.
        GUI.color = new Color(1f, 1f, 1f, 0.35f);
        GUI.DrawTexture(
            new Rect(panelRect.x + 3f * scale, panelRect.y + 4f * scale, panelRect.width, panelRect.height),
            hudShadowTexture);
        GUI.color = Color.white;

        GUI.DrawTexture(panelRect, hudPanelTexture);
        GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, panelRect.width, 3f * scale), hudAccentTexture);
        GUI.DrawTexture(
            new Rect(panelRect.x, panelRect.y + panelRect.height - 1f * scale, panelRect.width, 1f * scale),
            hudAccentTexture);
        GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, 4f * scale, panelRect.height), hudAccentTexture);

        float cursorY = panelY + padding;
        float badgeWidth = 150f * scale;

        GUI.Label(
            new Rect(panelX + padding, cursorY, badgeWidth, badgeHeight),
            badgeText,
            hudBadgeStyle);

        hudStatusStyle.normal.textColor = IsGpsRunning
            ? new Color(0.72f, 1f, 0.72f)
            : new Color(1f, 0.65f, 0.45f);

        GUI.Label(
            new Rect(
                panelX + padding + badgeWidth + 8f * scale,
                cursorY,
                innerWidth - badgeWidth - 8f * scale,
                badgeHeight),
            ArabicTextSupport.PrepareForDisplay(GetHudStatus()),
            hudStatusStyle);

        cursorY += badgeHeight + 6f * scale;

        GUI.Label(
            new Rect(panelX + padding, cursorY, innerWidth, titleHeight),
            titleContent,
            hudTitleStyle);

        cursorY += titleHeight + 2f * scale;

        if (subtitleHeight > 0f)
        {
            GUI.Label(
                new Rect(panelX + padding, cursorY, innerWidth, subtitleHeight),
                subtitleContent,
                hudSubtitleStyle);

            cursorY += subtitleHeight + 8f * scale;
        }

        string coordinates =
            "LAT " + Latitude.ToString("F6") +
            "     LON " + Longitude.ToString("F6");

        GUI.Label(
            new Rect(panelX + padding, cursorY, innerWidth, coordsHeight),
            coordinates,
            hudCoordsStyle);

        cursorY += coordsHeight + 2f * scale;

        string trip =
            "REAL " + RealTravelMeters.ToString("F0") + " m" +
            "   >   VIRTUAL " + (VirtualTravelMeters / 1000d).ToString("F2") + " km";

        if (showRealCoordinatesInHud)
        {
            trip += "   |   START " +
                    originLatitude.ToString("F4") + ", " +
                    originLongitude.ToString("F4");
        }

        GUI.Label(
            new Rect(panelX + padding, cursorY, innerWidth, tripHeight),
            trip,
            hudTripStyle);

        cursorY += tripHeight + 4f * scale;

        GUI.Label(
            new Rect(panelX + padding, cursorY, innerWidth, attributionHeight),
            "© OpenStreetMap contributors / LocationIQ",
            hudAttributionStyle);
    }

    private string GetHudStatus()
    {
        if (!IsGpsRunning)
            return currentStatus;

        if (isDesktopPreview)
        {
            return desktopPreviewAutoWalk
                ? "PREVIEW  ±" + AccuracyMeters.ToString("F0") + " m"
                : "PREVIEW";
        }

        return AccuracyMeters > 0f
            ? "LIVE  ±" + AccuracyMeters.ToString("F0") + " m"
            : currentStatus;
    }

    private string BuildHudTitle()
    {
        if (CurrentAddress != null && !string.IsNullOrWhiteSpace(CurrentAddress.road))
        {
            string number = CurrentAddress.house_number;
            string title = string.IsNullOrWhiteSpace(number)
                ? CurrentAddress.road
                : number + " " + CurrentAddress.road;

            // Legacy IMGUI does not shape Arabic, so convert it to visual order
            // (connected presentation forms + RTL ordering) before drawing.
            return ArabicTextSupport.PrepareForDisplay(title.Trim().ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(CurrentPlaceName))
        {
            int commaIndex = CurrentPlaceName.IndexOf(',');
            string firstPart = commaIndex > 0
                ? CurrentPlaceName.Substring(0, commaIndex)
                : CurrentPlaceName;

            return ArabicTextSupport.PrepareForDisplay(firstPart.Trim().ToUpperInvariant());
        }

        return IsGpsRunning ? "ACQUIRING LOCATION..." : "GPS OFFLINE";
    }

    private string BuildHudSubtitle()
    {
        if (CurrentAddress == null)
            return IsGpsRunning ? "WAITING FOR GPS FIX" : currentStatus;

        List<string> parts = new List<string>(4);
        AddUniquePart(parts, CurrentAddress.neighbourhood);
        AddUniquePart(parts, CurrentAddress.suburb);
        AddUniquePart(parts, FirstNonEmpty(CurrentAddress.city, CurrentAddress.town, CurrentAddress.village));
        AddUniquePart(parts, CurrentAddress.county);
        AddUniquePart(parts, CurrentAddress.state);
        AddUniquePart(parts, CurrentAddress.country);

        while (parts.Count > 3)
            parts.RemoveAt(parts.Count - 1);

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(CurrentPlaceName))
        {
            int commaIndex = CurrentPlaceName.IndexOf(',');
            if (commaIndex > 0)
            {
                return ArabicTextSupport.PrepareForDisplay(
                    CurrentPlaceName.Substring(commaIndex + 1).Trim().ToUpperInvariant());
            }
        }

        return ArabicTextSupport.PrepareForDisplay(
            string.Join(" • ", parts.ToArray()).ToUpperInvariant());
    }

    private static void AddUniquePart(List<string> parts, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        string trimmed = value.Trim();

        for (int i = 0; i < parts.Count; i++)
        {
            if (string.Equals(parts[i], trimmed, StringComparison.OrdinalIgnoreCase))
                return;
        }

        parts.Add(trimmed);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return null;
    }

    private void EnsureHudResources()
    {
        if (hudPanelTexture == null)
            hudPanelTexture = CreateSolidTexture(new Color(0.035f, 0.04f, 0.055f, 0.86f));

        if (hudAccentTexture == null)
            hudAccentTexture = CreateSolidTexture(new Color(0.98f, 0.78f, 0.25f, 1f));

        if (hudShadowTexture == null)
            hudShadowTexture = CreateSolidTexture(new Color(0f, 0f, 0f, 0.7f));

        float scale = Mathf.Clamp(Screen.height / 1080f, 0.8f, 1.8f);

        if (hudStyledForHeight == Screen.height && Mathf.Approximately(hudStyleScale, scale))
            return;

        hudStyledForHeight = Screen.height;
        hudStyleScale = scale;

        hudTitleStyle = CreateHudStyle(
            Mathf.RoundToInt(30f * scale),
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            Color.white,
            true);

        hudSubtitleStyle = CreateHudStyle(
            Mathf.RoundToInt(13f * scale),
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color(0.98f, 0.80f, 0.32f),
            true);

        hudCoordsStyle = CreateHudStyle(
            Mathf.RoundToInt(15f * scale),
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            new Color(0.92f, 0.94f, 0.98f),
            false);

        hudTripStyle = CreateHudStyle(
            Mathf.RoundToInt(11f * scale),
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            new Color(0.70f, 0.74f, 0.80f),
            false);

        hudStatusStyle = CreateHudStyle(
            Mathf.RoundToInt(11f * scale),
            FontStyle.Bold,
            TextAnchor.MiddleRight,
            new Color(0.72f, 1f, 0.72f),
            false);

        hudAttributionStyle = CreateHudStyle(
            Mathf.RoundToInt(9f * scale),
            FontStyle.Normal,
            TextAnchor.MiddleRight,
            new Color(0.60f, 0.63f, 0.68f),
            false);

        hudBadgeStyle = CreateHudStyle(
            Mathf.RoundToInt(11f * scale),
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Color(0.03f, 0.03f, 0.04f),
            false);

        hudBadgeStyle.normal.background = hudAccentTexture;
        hudBadgeStyle.padding = new RectOffset(
            Mathf.RoundToInt(8f * scale),
            Mathf.RoundToInt(8f * scale),
            Mathf.RoundToInt(2f * scale),
            Mathf.RoundToInt(2f * scale));
    }

    private static GUIStyle CreateHudStyle(
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color,
        bool wordWrap)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = fontStyle,
            alignment = alignment,
            wordWrap = wordWrap,
            clipping = TextClipping.Clip,
            richText = false,
            padding = new RectOffset(0, 0, 0, 0)
        };

        style.normal.textColor = color;
        return style;
    }

    private static Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(2, 2);
        Color[] pixels = new Color[4];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        isDesktopPreview = false;
        IsGpsRunning = false;

        if (hudPanelTexture != null)
            Destroy(hudPanelTexture);

        if (hudAccentTexture != null)
            Destroy(hudAccentTexture);

        if (hudShadowTexture != null)
            Destroy(hudShadowTexture);

        if (Input.location.status == LocationServiceStatus.Running ||
            Input.location.status == LocationServiceStatus.Initializing)
        {
            Input.location.Stop();
        }
    }
}
