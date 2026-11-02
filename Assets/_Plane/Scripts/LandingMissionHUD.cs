using System.Linq;
using TMPro;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Objectif permanent et écran de fin indépendants des branches reconstruites par Build HUD.</summary>
    [DisallowMultipleComponent]
    public sealed class LandingMissionHUD : MonoBehaviour
    {
        [SerializeField] private LandingMission mission;
        [SerializeField] private RectTransform safeArea,objectivePanel;
        [SerializeField] private TMP_Text objective,endTitle,endDetails;
        [SerializeField] private GameObject resultPanel;
        [SerializeField, Min(0f)] private float explosionPresentationDelay=.9f;
        private PlaneMenu menu;
        private void LateUpdate()
        {
            if(mission==null)return;
            var input=mission.Input;bool terminal=mission.IsTerminal;
            bool visible=input!=null&&!input.IsPaused&&!terminal;
            objectivePanel.gameObject.SetActive(visible);
            float width=safeArea.rect.width,left=22f+Mathf.Min(720f,width*.42f)+16f,right=width-22f-546f-16f;
            objectivePanel.anchoredPosition=new Vector2((left+right)*.5f,-16f);
            objectivePanel.sizeDelta=new Vector2(Mathf.Max(220f,right-left),140f);
            if(mission.CanLand)
            {
                float distance=Vector3.Distance(input.transform.position,mission.Arrival.AimPoint);
                objective.text=$"PIÈCES {mission.Score.CoinsCollected}/{mission.Settings.RequiredCoins}\nATTERRISSEZ À L’ARRIVÉE\n{distance:0} m · COUPEZ LES GAZ AU POSÉ";
            }
            else objective.text=$"OBJECTIF : PIÈCES {mission.Score.CoinsCollected}/{mission.Settings.RequiredCoins}\nPUIS ATTERRISSEZ À L’ARRIVÉE";
            bool showResult=terminal&&(mission.State==FlightMissionState.Won||Time.unscaledTime-mission.EndedAt>=explosionPresentationDelay);
            resultPanel.SetActive(showResult);
            if(showResult)
            {
                endTitle.text=mission.State==FlightMissionState.Won?"MISSION RÉUSSIE":"CRASH — MISSION ÉCHOUÉE";
                endDetails.text=$"SCORE {mission.Score.Score}\nPIÈCES {mission.Score.CoinsCollected}\n"+
                    (mission.State==FlightMissionState.Won?"Atterrissage sûr à l’aéroport d’arrivée.":"Évitez le relief et posez-vous sur la piste avant les montagnes.");
                Cursor.lockState=CursorLockMode.None;Cursor.visible=!Application.isMobilePlatform;
            }
        }
        private PlaneMenu Menu()
        {
            if(menu==null)menu=gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlaneMenu>(true)).FirstOrDefault();
            return menu;
        }
        public void Restart()=>Menu()?.Restart();
        public void Quit()=>Menu()?.Quit();
#if UNITY_EDITOR
        public void Configure(LandingMission owner,RectTransform safe,RectTransform panel,TMP_Text label,GameObject results,TMP_Text title,TMP_Text details)
        {mission=owner;safeArea=safe;objectivePanel=panel;objective=label;resultPanel=results;endTitle=title;endDetails=details;}
#endif
    }
}
