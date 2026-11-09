using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class AndroidPermissionRequester : MonoBehaviour
{
    private bool permissionRequested = false;
    
    void Start()
    {
        #if UNITY_ANDROID
        // Check Android version - permission required on Android 12+ (API 31+)
        if (!Permission.HasUserAuthorizedPermission("android.permission.BODY_SENSORS"))
        {
            Debug.Log("[AndroidPermissionRequester] Requesting BODY_SENSORS permission...");
            Permission.RequestUserPermission("android.permission.BODY_SENSORS");
            permissionRequested = true;
        }
        else
        {
            Debug.Log("[AndroidPermissionRequester] BODY_SENSORS permission already granted");
        }
        #else
        Debug.Log("[AndroidPermissionRequester] Not running on Android - no permission needed");
        #endif
    }
    
    void Update()
    {
        #if UNITY_ANDROID
        if (permissionRequested)
        {
            // Check if permission was granted
            if (Permission.HasUserAuthorizedPermission("android.permission.BODY_SENSORS"))
            {
                Debug.Log("[AndroidPermissionRequester] ✓ BODY_SENSORS permission GRANTED");
                permissionRequested = false;
                
                // Notify GyroCarController to try initializing again
                GyroCarController gyroController = FindObjectOfType<GyroCarController>();
                if (gyroController != null)
                {
                    gyroController.OnPermissionGranted();
                }
            }
        }
        #endif
    }
}
