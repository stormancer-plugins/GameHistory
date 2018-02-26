using Nest;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Server.GameHistory
{
    public interface IGameHistoryService
    {
        Task AddGame(string id, List<GameHistoryPlayer> players, string winnningTeam, JObject gameData, DateTime creationDate);

        Task<GameHistorySearchResult> GetPlayerHistory(string playerId, int count);

        Task<GameHistorySearchResult> GetPlayerHistoryByCursor(string cursor);

        Task<IElasticClient> GetESClient(string historyType);
    }

    public class GameHistorySearchResult
    {
        public IEnumerable<GameHistoryRecord> Documents { get; set; }

        public string Previous { get; set; }

        public string Next { get; set; }
    }

}
