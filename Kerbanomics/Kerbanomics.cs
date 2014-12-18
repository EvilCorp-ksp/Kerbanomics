using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using KSP;

namespace Kerbanomics
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class Kerbanomics : MonoBehaviour
    {
        double _interval;
        public static int _lastUpdate = 0;
        Game game;
        public ApplicationLauncherButton button;

        public static Kerbanomics Instance;

        public void Awake()
        {
            game = HighLogic.CurrentGame;
            if (GameSettings.KERBIN_TIME)
                _interval = 2300400;
            else
                _interval = 7884000;
            if (HighLogic.LoadedSceneHasPlanetarium)
                _lastUpdate = (int)Math.Floor(Planetarium.GetUniversalTime() / _interval);

        }

        public void Start()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;
            GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);
        }

        public void DestroyButtons()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
            ApplicationLauncher.Instance.RemoveModApplication(button);
        }

        void Update()
        {
            
            if (HighLogic.LoadedSceneHasPlanetarium)
            {
                int currentDay = (int)Math.Floor(Planetarium.GetUniversalTime() / _interval);
                if (currentDay > _lastUpdate)
                {
                    double multiplier = 106.5;
                    Debug.Log("_lastUpdate=" + _lastUpdate + ", currentDay=" + currentDay);
                    _lastUpdate = (currentDay);
                    StringBuilder message = new StringBuilder();
                    message.AppendLine("Payroll is processed.");
                    message.AppendLine("Current staff:");
                    foreach (ProtoCrewMember crewMember in game.CrewRoster.Crew)
                    {
                        double wage = 10;
                        double standbyWage = 5;
                        switch (crewMember.experienceLevel)
                        {
                            case 0:
                                wage = 10;
                                break;
                            case 1:
                                wage = 20;
                                break;
                            case 2:
                                wage = 40;
                                break;
                            case 3:
                                wage = 80;
                                break;
                            case 4:
                                wage = 140;
                                break;
                            case 5:
                                wage = 200;
                                break;
                            default:
                                wage = 10;
                                break;
                        }
                        standbyWage = wage / 2;
                        message.Append(crewMember.name);
                        if (crewMember.rosterStatus.Equals(ProtoCrewMember.RosterStatus.Assigned))
                        {
                            double paycheck = wage * multiplier;
                            message.AppendLine(", level" + crewMember.experienceLevel + ", is on mission. Wages paid = " + paycheck);
                            Funding.Instance.AddFunds(-paycheck, 0);
                        }
                        else if (crewMember.rosterStatus.Equals(ProtoCrewMember.RosterStatus.Available))
                        {
                            double paycheck = standbyWage * multiplier;
                            message.AppendLine(", level" + crewMember.experienceLevel + ", is available. Wages paid = " + paycheck);
                            Funding.Instance.AddFunds(-paycheck, 0);
                        }
                    }
                    double externalFunding = 2500 + 247.5 * Reputation.CurrentRep;
                    Funding.Instance.AddFunds(+externalFunding, 0);
                    message.AppendLine("Received Funding - " + externalFunding);
                    MessageSystem.Message m = new MessageSystem.Message(
                        "Processing Finances",
                        message.ToString(),
                        MessageSystemButton.MessageButtonColor.RED,
                        MessageSystemButton.ButtonIcons.ALERT);
                    MessageSystem.Instance.AddMessage(m);
                }
            }
        }

        public void OnDestroy()
        {
            DestroyButtons();
        }
        void OnGUIAppLauncherReady()
        {
            this.button = ApplicationLauncher.Instance.AddModApplication(
                onAppLauncherToggleOn,
                onAppLauncherToggleOff,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.SPACECENTER,
                (Texture)GameDatabase.Instance.GetTexture("EvilCorp/Textures/icon_button_stock", false));
        }
               
        //void onGUIApplicationLauncherReady()
        //{
            
        //}

        void onAppLauncherToggleOn()
        {
            Debug.Log("Toggled on");
        }

        void onAppLauncherToggleOff()
        {
            Debug.Log("Toggled off");
        }

        //void onAppLauncherHover()
        //{

        //}

        //void onAppLauncherHoverOut()
        //{

        //}


        //void onAppLauncherEnable()
        //{

        //}

        //void onAppLauncherDisable()
        //{

        //}
    }
}
