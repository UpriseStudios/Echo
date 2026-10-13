using Echo.Core.Net;
using Echo.Core.Reels;
using Echo.Server;

namespace EchoCloud
{
    /// <summary>
    /// The server's replay (version 2.0: the server is authoritative). It decodes the client's packet, refuses one
    /// made for different content, and replays it with the game's own code to find the real result.
    /// </summary>
    public sealed class ReplayService
    {
        readonly GameContent _content;

        public ReplayService(GameContent content)
        {
            _content = content;
        }

        public ReplayCheckResponse Check(string packet)
        {
            var response = new ReplayCheckResponse { ServerContentVersion = _content.ContentVersion };
            if (!ReelPacket.TryDecode(packet, out Reel reel))
            {
                response.Error = "malformed";
                return response;
            }
            response.ClaimedDistance = reel.Distance;
            if (reel.ContentVersion != _content.ContentVersion)
            {
                response.Error = "content-version";
                return response;
            }
            ReplayResult result = RunReplay.Replay(reel, _content.CourseFor(reel.Seed), _content.Config);
            response.Accepted = true;
            response.Distance = result.Distance;
            response.Score = result.Score;
            response.DeathTick = result.DeathTick;
            response.Finished = result.Finished;
            return response;
        }
    }
}
