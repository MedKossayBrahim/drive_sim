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
        private LandingVictoryView victoryView;
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
                mission.Arrival.GetApproachGuidance(out _, out string instruction);
                objective.text = $"3 / 3 · APPROCHE\n{instruction}";
            }
            else if (!mission.HasTakenOff)
            {
                objective.text = input != null && !input.IsMissionStarted
                    ? "1 / 3 · DÉCOLLAGE\nAPPUYEZ SUR DÉCOLLER"
                    : "1 / 3 · DÉCOLLAGE\nMONTEZ LES GAZ · CABREZ DOUCEMENT";
            }
            else if (mission.Settings.RequireAllRings && mission.Score.CoinsCollected >= mission.Settings.RequiredCoins)
            {
                objective.text = $"2 / 3 · PARCOURS\nANNEAUX {mission.RingsPassed}/{mission.RingCount}";
            }
            else
            {
                objective.text = $"2 / 3 · COLLECTE\nPIÈCES {mission.Score.CoinsCollected}/{mission.Settings.RequiredCoins} · SUIVEZ LA FLÈCHE";
            }
            bool showResult=terminal&&(mission.State==FlightMissionState.Won||Time.unscaledTime-mission.EndedAt>=explosionPresentationDelay);
            resultPanel.SetActive(showResult);
            if (!showResult) return;

            bool won = mission.State == FlightMissionState.Won;
            endTitle.transform.parent.gameObject.SetActive(!won);
            if (won)
            {
                if (victoryView == null)
                    victoryView = LandingVictoryView.Create(resultPanel.transform, endTitle.font, mission, Restart, Quit);
                victoryView.gameObject.SetActive(true);
            }
            else
            {
                if (victoryView != null) victoryView.gameObject.SetActive(false);
                endTitle.text = "CRASH — MISSION ÉCHOUÉE";
                endDetails.text = $"SCORE {mission.Score.Score}\nPIÈCES {mission.Score.CoinsCollected}\nÉvitez le relief et posez-vous sur la piste avant les montagnes.";
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = !Application.isMobilePlatform;
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
