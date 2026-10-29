using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace EchoCloud
{
    /// <summary>
    /// Rewards the server grants for competitive modes (modes doc): cosmetics and badges in the player's private
    /// "rewards" item, kept for good, granted once per settled event. Badges also go to the board index
    /// (TeamService.BadgesBucket) so a page of names needs one read.
    /// </summary>
    public static class RewardStore
    {
        public const string Key = "rewards";

        public sealed class PlayerRewards
        {
            public List<string> Cosmetics { get; set; } = new List<string>();
            public List<BadgeEntry> Badges { get; set; } = new List<BadgeEntry>();

            /// <summary>Events and days whose rewards have been worked out, so they're granted once.</summary>
            public List<string> Settled { get; set; } = new List<string>();
        }

        public static async Task<PlayerRewards> ReadAsync(IDailyStore players, string playerId)
        {
            string json = await players.GetPlayerAsync(playerId, Key);
            if (string.IsNullOrEmpty(json)) return new PlayerRewards();
            try
            {
                return JsonSerializer.Deserialize<PlayerRewards>(json) ?? new PlayerRewards();
            }
            catch (JsonException)
            {
                return new PlayerRewards();
            }
        }

        public static async Task<bool> SettledAsync(IDailyStore players, string playerId, string settleKey) =>
            (await ReadAsync(players, playerId)).Settled.Contains(settleKey);

        /// <summary>Grants once under <paramref name="settleKey"/>; returns false when it was already settled.</summary>
        public static async Task<bool> GrantAsync(IDayStore days, IDailyStore players, string playerId, string settleKey,
            IEnumerable<string> cosmetics, IEnumerable<BadgeEntry> badges)
        {
            PlayerRewards rewards = await ReadAsync(players, playerId);
            if (rewards.Settled.Contains(settleKey)) return false;
            rewards.Settled.Add(settleKey);
            foreach (string id in cosmetics)
                if (!rewards.Cosmetics.Contains(id)) rewards.Cosmetics.Add(id);
            var added = badges.ToList();
            rewards.Badges.AddRange(added);
            await players.SetPlayerAsync(playerId, Key, JsonSerializer.Serialize(rewards));
            if (added.Count > 0) await days.SetAsync(TeamService.BadgesBucket, playerId, JsonSerializer.Serialize(rewards.Badges));
            return true;
        }
    }
}
