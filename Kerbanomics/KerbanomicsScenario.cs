using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using KSP;
using Kerbanomics;

namespace Kerbanomics
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.FLIGHT, GameScenes.SPACECENTER, })]
    class KerbanomicsScenario : ScenarioModule
    {
        //public static KerbanomicsScenario Instance;

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            KerbanomicsMain.Instance.LoadData();
            KerbanomicsMain.Instance.LoadSettings();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            Debug.Log("Saving Financial Data");
            KerbanomicsMain.Instance.SaveData();
            Debug.Log("Saving Settings");
            KerbanomicsMain.Instance.SaveSettings();

        }
    }
}
