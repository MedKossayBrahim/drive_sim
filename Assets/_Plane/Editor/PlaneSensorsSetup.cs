#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Plane
{
    /// <summary>Ajoute la télémétrie et sa coupure moteur au seul prefab Plane, sans réécrire la scène.</summary>
    public static class PlaneSensorsSetup
    {
        private const string PrefabPath = "Assets/_Plane/Prefabs/Plane.prefab";
        [MenuItem("Sky Courier/8 - Add Sensors", false, 80)]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[Sky Courier] Quitter Play avant d'ajouter les capteurs."); return; }
            GameObject root = null;
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                    throw new InvalidOperationException("Plane.prefab absent.");
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
                var input = root.GetComponent<PlaneInput>();
                if (root.GetComponent<PlaneController>() == null || root.GetComponent<Rigidbody>() == null || input == null)
                    throw new InvalidOperationException("PlaneController, Rigidbody et PlaneInput sont requis sur Plane.");
                var sensors = root.GetComponent<PlaneSensors>();
                if (sensors == null)
                {
                    sensors = root.AddComponent<PlaneSensors>();
                    int ground = LayerMask.GetMask("Ground");
                    if (ground != 0) sensors.ConfigureGroundMask(ground);
                }
                if (root.GetComponent<EngineCutoff>() == null) root.AddComponent<EngineCutoff>();
                if (root.GetComponent<PlaneSensorsDebugOverlay>() == null) root.AddComponent<PlaneSensorsDebugOverlay>();
                // Liaison d'éditeur uniquement : évite un faux pic de G après Reset, sans toucher PlaneInput.cs.
                var field = typeof(PlaneInput).GetField("onReset", BindingFlags.Instance | BindingFlags.NonPublic);
                var resetEvent = field?.GetValue(input) as UnityEvent;
                if (resetEvent == null) throw new InvalidOperationException("Événement onReset introuvable.");
                bool registered = false;
                for (int i = resetEvent.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    if (resetEvent.GetPersistentTarget(i) != sensors || resetEvent.GetPersistentMethodName(i) != nameof(PlaneSensors.ResetMotionHistory)) continue;
                    if (registered) UnityEventTools.RemovePersistentListener(resetEvent, i);
                    else { registered = true; resetEvent.SetPersistentListenerState(i, UnityEventCallState.RuntimeOnly); }
                }
                if (!registered) UnityEventTools.AddPersistentListener(resetEvent, sensors.ResetMotionHistory);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new InvalidOperationException("Sauvegarde du prefab impossible.");
                AssetDatabase.SaveAssets();
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Debug.Log("[Sky Courier] Plane : un PlaneSensors, un EngineCutoff et un PlaneSensorsDebugOverlay. " +
                    "F3 affiche le bloc capteurs sous le diagnostic d'inputs. Manager recherché dans la même scène. " +
                    "AGL = layer Ground ; MSL = Y monde 0 ; G signé sur Y local ; consommation 0,08 L/s à plein gaz. " +
                    "Reset conserve le carburant. Aucun script existant ni autre objet de scène modifié.");
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
