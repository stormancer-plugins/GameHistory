using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.GameHistory
{
    public class GameHistoryRecord
    {
        public string Id { get; set; }

        public List<GameHistoryPlayer> players { get; set; }

        public DateTime CreatedOn { get; set; }

        public string WinningTeam { get; set; }

        public JObject GameData { get; set; }
    }

    public struct GameHistoryPlayer
    {
        public string Id { get; set; }

        public string Team { get; set; }
    }


}
