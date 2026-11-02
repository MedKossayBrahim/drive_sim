#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Project.Plane;
[InitializeOnLoad]
public static class TemporaryApplyFlightFix
{
    static TemporaryApplyFlightFix()
    {
        if(Application.isBatchMode||SessionState.GetBool("SkyCourier.FlightAndSunFix.Applied",false))return;
        EditorApplication.delayCall+=Apply;
    }
    static void Apply()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Apply;return;}
        SessionState.SetBool("SkyCourier.FlightAndSunFix.Applied",true);FlightBehaviorSetup.Setup();
    }
}
#endif
