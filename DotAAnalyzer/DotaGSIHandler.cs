using Dota2GSI;
using Dota2GSI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotAAnalyzer
{
    class DotaGSIHandler
    {
        private static DotaGSIHandler instance = null;
        GameStateListener gsl;
        private bool pickPhase = false;

        private DotaGSIHandler()
        {
            gsl = new GameStateListener("http://127.0.0.1:3009/");
            gsl.NewGameState += new NewGameStateHandler(OnNewGameState);

            if (!gsl.Start())
            {
                Console.WriteLine("GameStateListener could not start. Try running this program as Administrator.\r\nExiting.");
                Environment.Exit(0);
            }
            Console.WriteLine("Listening for game integration calls...");
        }

        public static DotaGSIHandler Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DotaGSIHandler();
                }
                return instance;
            }
        }




        void OnNewGameState(GameState gs)
        {
            try
            {
                if (!pickPhase && gs.Map.GameState == DOTA_GameState.DOTA_GAMERULES_STATE_HERO_SELECTION)
                {
                    pickPhase = true;
                    DotaAnalyzerAutosyncForm.Instance.StartTracking(gs.Player.SteamID);
                }
                else if (pickPhase && gs.Map.GameState == DOTA_GameState.DOTA_GAMERULES_STATE_STRATEGY_TIME)
                {
                    pickPhase = false;
                    DotaAnalyzerAutosyncForm.Instance.StopTracking();
                }
            } catch
            {

            }
            

        }

    }
}
