#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Applique les réglages tactiles au prefab et au joystick, sans toucher aux réglages globaux.</summary>
    public static class TouchFlightSetup
    {
        [MenuItem("Sky Courier/Controls/Setup Responsive Joystick")]
        public static void Setup()
        {
            var scene=ModuleSceneUtility.Open();
            const string path="Assets/_Plane/Prefabs/Plane.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var serialized=new SerializedObject(root.GetComponent<PlaneInput>());
                serialized.FindProperty("touchAxisSmoothing").floatValue=16f;
                serialized.FindProperty("touchSensitivity").floatValue=1f;
                serialized.FindProperty("touchResponseExponent").floatValue=1.1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var joystick=ModuleSceneUtility.Only<VirtualJoystick>(scene);
            var settings=new SerializedObject(joystick);settings.FindProperty("radius").floatValue=85f;
            settings.FindProperty("deadZone").floatValue=.06f;settings.ApplyModifiedPropertiesWithoutUndo();
            ModuleSceneUtility.Save(scene);
            Debug.Log("[Sky Courier] Joystick réactif : cap direct et retour à plat, réglages de virage dans PlaneSettings.");
        }
    }
}
#endif
