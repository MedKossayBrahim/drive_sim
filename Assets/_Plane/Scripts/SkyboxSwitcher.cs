using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Plane
{
    public enum SkyPresetId { Noon, Sunset, Night, Storm }

    /// <summary>Ciel mondial : deux panoramas fondus, lumière et brouillard interpolés, sans modifier les assets.</summary>
    [DisallowMultipleComponent]
    public sealed class SkyboxSwitcher : MonoBehaviour
    {
        [SerializeField] private SkyPresetId selectedPreset = SkyPresetId.Noon;
        [SerializeField] private SkyPreset noon = null, sunset = null, night = null, storm = null;
        [SerializeField] private Light directionalLight = null;
        [SerializeField, Tooltip("Touches 1–4 actives uniquement dans l'éditeur.")] private bool enableDebugKeys = true;
        [SerializeField] private Shader transitionShader = null;
        [SerializeField, Min(0f)] private float transitionDuration = 1f;
        [SerializeField, InspectorName("Cycle automatique")] private bool automaticCycle = false;
        [SerializeField, Min(3f), Tooltip("Durée d'un tour Noon > Sunset > Night > Noon, en secondes de jeu.")]
        private float cycleDuration = 600f;
        [SerializeField, Min(0.5f)] private float environmentRefreshInterval = 2f;
        [Header("Soleil et lune — toujours dans la partie haute du ciel")]
        [SerializeField, Range(30f,80f)] private float minimumSunElevation = 45f;
        [SerializeField, Range(.1f,3f)] private float sunAngularRadius = .65f;
        [SerializeField, Range(1f,15f)] private float originalSunMaskRadius = 7f;
        [SerializeField, Min(0f)] private float sunBrightness = 24f;
        [SerializeField, Tooltip("Direction du point lumineux dans le panorama non tourné ; calibration des HDRI actuels.")]
        private Vector3 noonSourceSunDirection = new Vector3(.523252f,.761202f,-.383116f);
        [SerializeField] private Vector3 sunsetSourceSunDirection = new Vector3(.802462f,.104122f,-.587549f);
        [SerializeField] private Vector3 nightSourceSunDirection = new Vector3(.711419f,.006136f,-.702742f);
        private struct State
        {
            public Color LightColor, FogColor;
            public float Intensity, Density, Exposure;
            public Quaternion Rotation;
        }
        private State from, to;
        private Material runtimeSky, previousSky;
        private Light previousSun, controlledSun;
        private State previous;
        private bool captured, previousFog, blending, observedCycle;
        private FogMode previousFogMode;
        private SkyPreset renderedPreset, destination;
        private SkyPresetId observedPreset;
        private float blendTime, cyclePhase, nextEnvironmentUpdate, cycleAngleOffset;
        private int cycleLeg = -1;
        public SkyPresetId SelectedPreset => selectedPreset;
        public string CurrentName => selectedPreset.ToString();
        public bool AutomaticCycle => automaticCycle;
        public bool IsTransitioning => blending;
        public float CyclePhase => cyclePhase;
        public event Action<SkyPresetId> OnPresetChanged;

        private SkyPreset Preset(SkyPresetId id) => id == SkyPresetId.Noon ? noon : id == SkyPresetId.Sunset ? sunset : id == SkyPresetId.Night ? night : storm;
        private static float BaseRotation(SkyPreset p) => (p.SkyMaterial.HasProperty("_Rotation") ? p.SkyMaterial.GetFloat("_Rotation") : 0f) + p.SkyRotationOffset;
        private static State TargetState(SkyPreset p) => new State
        { LightColor=p.DirectionalLightColor, Intensity=p.DirectionalLightIntensity, Rotation=Quaternion.Euler(p.SunEuler),
          FogColor=p.FogColor, Density=p.FogEnabled?p.FogDensity:0f, Exposure=p.Exposure };
        private static State Lerp(State a, State b, float t) => new State
        { LightColor=Color.Lerp(a.LightColor,b.LightColor,t), Intensity=Mathf.Lerp(a.Intensity,b.Intensity,t),
          Rotation=Quaternion.Slerp(a.Rotation,b.Rotation,t), FogColor=Color.Lerp(a.FogColor,b.FogColor,t),
          Density=Mathf.Lerp(a.Density,b.Density,t), Exposure=Mathf.Lerp(a.Exposure,b.Exposure,t) };
        private void OnEnable()
        {
            if(!Application.isPlaying)return;
            observedPreset=selectedPreset;
            if(!Prepare())return;
            renderedPreset=Preset(selectedPreset);
            if(renderedPreset==null||renderedPreset.SkyMaterial==null)return;
            BindSkies(renderedPreset,renderedPreset);Write(TargetState(renderedPreset),0f,0f);RefreshEnvironment(true);
            observedCycle=automaticCycle;
            if(automaticCycle)StartCycle();
        }
        private bool Prepare()
        {
            if(captured)return runtimeSky!=null;
            if(directionalLight!=null)controlledSun=directionalLight;
            else if(RenderSettings.sun!=null&&RenderSettings.sun.gameObject.scene==gameObject.scene)controlledSun=RenderSettings.sun;
            else
                foreach(var root in gameObject.scene.GetRootGameObjects())
                {
                    var candidates=root.GetComponentsInChildren<Light>().WhereDirectional();
                    foreach(var light in candidates)
                    { if(controlledSun!=null){Debug.LogWarning("[Sky Courier] Assigner une seule Directional Light au ciel.",this);return false;}controlledSun=light; }
                }
            if(transitionShader==null)transitionShader=Shader.Find("Sky Courier/Panoramic Blend");
            if(transitionShader==null){Debug.LogWarning("[Sky Courier] Exécuter Setup Mobile UI pour assigner le shader du ciel.",this);return false;}
            previousSky=RenderSettings.skybox;previousSun=RenderSettings.sun;previousFog=RenderSettings.fog;previousFogMode=RenderSettings.fogMode;
            previous=new State{LightColor=controlledSun!=null?controlledSun.color:Color.white,Intensity=controlledSun!=null?controlledSun.intensity:0f,
                Rotation=controlledSun!=null?controlledSun.transform.rotation:Quaternion.identity,FogColor=RenderSettings.fogColor,Density=RenderSettings.fogDensity,Exposure=1f};
            runtimeSky=new Material(transitionShader){name="Sky Courier — ciel mobile",hideFlags=HideFlags.DontSave};
            RenderSettings.skybox=runtimeSky;if(controlledSun!=null)RenderSettings.sun=controlledSun;captured=true;return true;
        }
        private void Update()
        {
            if(!Application.isPlaying)return;
            // Aucun raccourci clavier dans le jeu Android, même si un clavier externe est connecté.
            if(Application.isEditor&&enableDebugKeys&&Keyboard.current!=null)
            {
                var keys=Keyboard.current;
                if(keys.digit1Key.wasPressedThisFrame)Select(SkyPresetId.Noon);
                else if(keys.digit2Key.wasPressedThisFrame)Select(SkyPresetId.Sunset);
                else if(keys.digit3Key.wasPressedThisFrame)Select(SkyPresetId.Night);
                else if(keys.digit4Key.wasPressedThisFrame)Select(SkyPresetId.Storm);
            }
            if(observedPreset!=selectedPreset)ApplySelected();
            if(automaticCycle&&!observedCycle)StartCycle();
            observedCycle=automaticCycle;
            if(automaticCycle)TickCycle();
            else if(blending)
            {
                blendTime+=Time.unscaledDeltaTime;
                float t=transitionDuration<=0f?1f:Mathf.Clamp01(blendTime/transitionDuration);
                float smooth=t*t*(3f-2f*t);Write(Lerp(from,to,smooth),smooth,0f);
                if(t>=1f){blending=false;renderedPreset=destination;RefreshEnvironment(true);}
            }
            if(captured)RefreshEnvironment(false);
        }
        public void NextPreset() => Select((SkyPresetId)(((int)selectedPreset+1)%4));
        public void Select(SkyPresetId id){selectedPreset=id;ApplySelected();}
        public void ApplySelected(){observedPreset=selectedPreset;Apply(Preset(selectedPreset));}
        public void Apply(SkyPreset preset)
        {
            if(!Application.isPlaying||preset==null||preset.SkyMaterial==null)return;
            if(!Prepare())return;
            automaticCycle=observedCycle=false;cycleLeg=-1;
            for(int i=0;i<4;i++)if(Preset((SkyPresetId)i)==preset)selectedPreset=(SkyPresetId)i;
            observedPreset=selectedPreset;
            from=new State{LightColor=controlledSun!=null?controlledSun.color:previous.LightColor,Intensity=controlledSun!=null?controlledSun.intensity:0,
                Rotation=controlledSun!=null?controlledSun.transform.rotation:Quaternion.identity,FogColor=RenderSettings.fogColor,
                Density=RenderSettings.fog?RenderSettings.fogDensity:0f,Exposure=runtimeSky.GetFloat("_Exposure")};
            // Un nouvel appui remplace la destination ; aucune coroutine de transition ne s'empile.
            bool visibleB=runtimeSky.GetFloat("_Blend")>=.5f;
            float outgoingRotation=runtimeSky.GetFloat("_Rotation")+runtimeSky.GetFloat(visibleB?"_RotationB":"_RotationA");
            var source=blending&&visibleB?destination:renderedPreset;
            if(source==null)source=preset;
            renderedPreset=source;destination=preset;to=TargetState(preset);BindSkies(source,preset);
            // Lorsqu'on quitte le cycle, conserve l'orientation visible du ciel sortant pendant le fondu.
            runtimeSky.SetFloat("_RotationA",outgoingRotation);
            blendTime=0;blending=true;Write(from,0f,0f);OnPresetChanged?.Invoke(selectedPreset);
        }
        private void StartCycle()
        {
            cyclePhase=selectedPreset==SkyPresetId.Sunset?1f/3f:selectedPreset==SkyPresetId.Night?2f/3f:0f;
            cycleAngleOffset=-cyclePhase*360f;cycleLeg=-1;blending=false;
        }
        private void TickCycle()
        {
            if(!Prepare()||noon==null||sunset==null||night==null)return;
            cyclePhase=Mathf.Repeat(cyclePhase+Time.deltaTime/Mathf.Max(3f,cycleDuration),1f);
            float position=cyclePhase*3;int leg=Mathf.Min(2,(int)position);float t=position-leg;t=t*t*(3-2*t);
            var a=leg==0?noon:leg==1?sunset:night;var b=leg==0?sunset:leg==1?night:noon;
            if(leg!=cycleLeg){BindSkies(a,b);cycleLeg=leg;renderedPreset=a;destination=b;selectedPreset=(SkyPresetId)leg;observedPreset=selectedPreset;}
            State state=Lerp(TargetState(a),TargetState(b),t);
            // Même angle mondial ajouté au soleil et aux deux panoramas : aucune rotation liée à la caméra.
            float sharedYaw=cyclePhase*360f+cycleAngleOffset;
            state.Rotation=Quaternion.AngleAxis(sharedYaw,Vector3.up)*state.Rotation;
            Write(state,t,sharedYaw);
        }
        private void BindSkies(SkyPreset a,SkyPreset b)
        {
            runtimeSky.SetTexture("_TexA",a.SkyMaterial.GetTexture("_MainTex"));runtimeSky.SetTexture("_TexB",b.SkyMaterial.GetTexture("_MainTex"));
            runtimeSky.SetColor("_TintA",a.SkyMaterial.GetColor("_Tint"));runtimeSky.SetColor("_TintB",b.SkyMaterial.GetColor("_Tint"));
            runtimeSky.SetFloat("_RotationA",BaseRotation(a));runtimeSky.SetFloat("_RotationB",BaseRotation(b));
            BindSun("_OriginalSunA",a);BindSun("_OriginalSunB",b);
            float mask=originalSunMaskRadius*Mathf.Deg2Rad;
            runtimeSky.SetVector("_SunParams",new Vector4(Mathf.Cos(mask),Mathf.Cos(mask*.55f),
                Mathf.Cos((sunAngularRadius+.25f)*Mathf.Deg2Rad),Mathf.Cos(sunAngularRadius*Mathf.Deg2Rad)));
            runtimeSky.SetVector("_SunSamples",new Vector4(Mathf.Cos(mask*1.3f),Mathf.Sin(mask*1.3f),Mathf.Cos(5f*Mathf.Deg2Rad),0f));
        }
        private void BindSun(string property,SkyPreset preset)
        {
            Vector3 direction=preset==noon?noonSourceSunDirection:preset==sunset?sunsetSourceSunDirection:nightSourceSunDirection;
            direction.Normalize();runtimeSky.SetVector(property,new Vector4(direction.x,direction.y,direction.z,preset==storm?0f:1f));
        }
        private Quaternion RaiseSun(Quaternion rotation)
        {
            Vector3 direction=-(rotation*Vector3.forward);
            Vector3 horizontal=Vector3.ProjectOnPlane(direction,Vector3.up);
            if(horizontal.sqrMagnitude<.0001f)horizontal=Vector3.forward;horizontal.Normalize();
            float elevation=Mathf.Max(minimumSunElevation*Mathf.Deg2Rad,Mathf.Asin(Mathf.Clamp(direction.y,-1f,1f)));
            direction=horizontal*Mathf.Cos(elevation)+Vector3.up*Mathf.Sin(elevation);
            return Quaternion.LookRotation(-direction,Vector3.up);
        }
        private void Write(State state,float blend,float sharedYaw)
        {
            runtimeSky.SetFloat("_Blend",blend);runtimeSky.SetFloat("_Exposure",state.Exposure);runtimeSky.SetFloat("_Rotation",sharedYaw);
            state.Rotation=RaiseSun(state.Rotation);
            runtimeSky.SetVector("_SunDirection",-(state.Rotation*Vector3.forward));
            runtimeSky.SetColor("_SunColor",state.LightColor*Mathf.Max(.02f,state.Intensity)*sunBrightness);
            if(controlledSun!=null){controlledSun.color=state.LightColor;controlledSun.intensity=state.Intensity;controlledSun.transform.rotation=state.Rotation;}
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=state.FogColor;RenderSettings.fogDensity=state.Density;
        }
        private void RefreshEnvironment(bool force)
        {
            if(!force&&Time.unscaledTime<nextEnvironmentUpdate)return;
            nextEnvironmentUpdate=Time.unscaledTime+Mathf.Max(.5f,environmentRefreshInterval);DynamicGI.UpdateEnvironment();
        }
        private void OnDisable()
        {
            if(captured&&RenderSettings.skybox==runtimeSky)
            {
                RenderSettings.skybox=previousSky;RenderSettings.sun=previousSun;RenderSettings.fog=previousFog;RenderSettings.fogMode=previousFogMode;
                RenderSettings.fogColor=previous.FogColor;RenderSettings.fogDensity=previous.Density;
                if(controlledSun!=null){controlledSun.color=previous.LightColor;controlledSun.intensity=previous.Intensity;controlledSun.transform.rotation=previous.Rotation;}
                DynamicGI.UpdateEnvironment();
            }
            if(runtimeSky!=null)Destroy(runtimeSky);runtimeSky=null;captured=blending=false;
        }
    }
    internal static class SkyLightFilter
    {
        public static System.Collections.Generic.IEnumerable<Light> WhereDirectional(this Light[] lights)
        {foreach(var light in lights)if(light.type==LightType.Directional&&light.isActiveAndEnabled)yield return light;}
    }
}
