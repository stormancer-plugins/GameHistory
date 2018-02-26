using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Stormancer.Server.Components;
using Server.Database;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Nest;
using Server.Extensions;

namespace Stormancer.Server.GameHistory
{
    class GameHistoryService : IGameHistoryService
    {
        private readonly IEnvironment _environment;
        private const string _databaseName = "gamehistory";
        private readonly IESClientFactory _clientFactory;

        public GameHistoryService(
            IEnvironment environment,
            IESClientFactory clientFactory)
        {
            _environment = environment;
            _clientFactory = clientFactory;
        }

        private static readonly ConcurrentDictionary<string, bool> _initializedIndices = new ConcurrentDictionary<string, bool>();
        public async Task<Nest.IElasticClient> CreateClient(string historyType = "")
        {
            var result = await _clientFactory.CreateClient<GameHistoryRecord>(_databaseName, historyType);

            if (_initializedIndices.TryAdd("", true))
            {
                await CreateGameHistoryMapping(result);
            }
            return result;
        }

        private Task CreateGameHistoryMapping(Nest.IElasticClient client)
        {
            return client.MapAsync<GameHistoryRecord>(m =>
                m.DynamicTemplates(templates => templates
                    .DynamicTemplate("dates", t => t
                         .Match("CreatedOn")
                         .Mapping(ma => ma.Date(s => s))
                        )
                     .DynamicTemplate("score", t =>
                        t.MatchMappingType("string")
                         .Mapping(ma => ma.Keyword(s => s.Index()))
                         )
                    )
            );
        }

        public async Task AddGame(string id, List<GameHistoryPlayer> players, string winningTeam, JObject gameData, DateTime creationDate)
        {
            var client = await CreateClient();
            var record = new GameHistoryRecord { Id = id, CreatedOn = creationDate, GameData = gameData, players = players, WinningTeam = winningTeam };
            await client.IndexDocumentAsync(record);
        }

        public async Task<GameHistorySearchResult> GetPlayerHistory(string playerId, int count)
        {
            var client = await CreateClient();
            var result = await client.SearchAsync<GameHistoryRecord>(s => s
            .Query(q => q.Bool(b => b.Must(m => m.Term("players.id", playerId))))
            .Size(count + 1)
            .Sort(sort => sort.Descending(r => r.CreatedOn))
            );


            return new GameHistorySearchResult
            {
                Documents = result.Documents.Take(count),
                Next = result.Documents.Count() == count + 1 ? GetNextCursor(result.Documents, count) : "",
                Previous = ""
            };


        }

        public async Task<GameHistorySearchResult> GetPlayerHistoryByCursor(string cursor)
        {
            var cData = ReadCursor(cursor);
            var client = await CreateClient();

            var result = await client.SearchAsync<GameHistoryRecord>(s => s
                .Query(q => q.Bool(b => b.Must(
                    m => m.Term("players.id", cData.PlayerId),
                    m => m.DateRange(dr =>
                    {
                        switch (cData.Type)
                        {
                            case CursorType.Next:
                                dr = dr.LessThan(cData.PivotDate);
                                break;
                            case CursorType.Previous:
                                dr = dr.GreaterThan(cData.PivotDate);
                                break;
                            default:
                                break;
                        }
                        return dr;
                    }))))
                .Size(cData.Count + 1)
                .Sort(sort =>
                {
                    switch (cData.Type)
                    {
                        case CursorType.Next:
                            sort = sort.Descending(r => r.CreatedOn);
                            break;
                        case CursorType.Previous:
                            sort = sort.Ascending(r => r.CreatedOn);
                            break;
                    }
                    return sort;
                })
            );
            var documents = cData.Type == CursorType.Next ? result.Documents : result.Documents.Reverse();


            return new GameHistorySearchResult
            {
                Documents = documents.Take(cData.Count),
                Next = result.Documents.Count() == cData.Count + 1 ? GetNextCursor(result.Documents, cData.Count) : "",
                Previous = result.Documents.Count() == cData.Count + 1 ? GetPreviousCursor(result.Documents, cData.Count) : ""
            };

        }

        private enum CursorType
        {
            Previous,
            Next
        }
        private class Cursor
        {
            public CursorType Type { get; set; }

            public string PlayerId { get; set; }

            public DateTime PivotDate { get; set; }

            public int Count { get; set; }
        }

        /// <summary>
        /// Create previous cursor
        /// </summary>
        /// <param name="historySet"> set of results to build the cursor from, ordered by CreationDate descending</param>
        /// <returns></returns>
        private string GetPreviousCursor(IEnumerable<GameHistoryRecord> historySet, int count)
        {
            var first = historySet.FirstOrDefault();
            if (first == null)
            {
                return "";
            }

            var cursor = new Cursor { Count = count, PivotDate = first.CreatedOn, PlayerId = first.Id, Type = CursorType.Previous };

            var json = JsonConvert.SerializeObject(cursor);

            return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        }


        /// <summary>
        /// Create next cursor
        /// </summary>
        /// <param name="historySet"> set of results to build the cursor from, ordered by CreationDate descending</param>
        /// <returns></returns>
        private string GetNextCursor(IEnumerable<GameHistoryRecord> historySet, int count)
        {
            var first = historySet.LastOrDefault();
            if (first == null)
            {
                return "";
            }

            var cursor = new Cursor { Count = count, PivotDate = first.CreatedOn, PlayerId = first.Id, Type = CursorType.Next };

            var json = JsonConvert.SerializeObject(cursor);

            return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

        }

        private Cursor ReadCursor(string cursorString)
        {
            var json = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(cursorString));
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Cursor>(json);
        }

        public Task<IElasticClient> GetESClient(string historyType)
        {
            return CreateClient(historyType);
        }
    }
}
