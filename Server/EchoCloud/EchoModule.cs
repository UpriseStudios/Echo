using System.Threading.Tasks;
using Echo.Server;
using Unity.Services.CloudCode.Core;

namespace EchoCloud
{
    /// <summary>
    /// Echo's Cloud Code endpoints (version 2.0). Phase 2 has only ReplayCheck, a development tool that shows a run
    /// replaying to the same distance on the server as on the device. The Daily Haunt and challenge endpoints follow.
    /// </summary>
    public class EchoModule
    {
        const string Development = "development";

        [CloudCodeFunction("ReplayCheck")]
        public Task<ReplayCheckResponse> ReplayCheck(IExecutionContext context, string packet)
        {
            if (context.EnvironmentName != Development)
                return Task.FromResult(new ReplayCheckResponse { Error = "development-only" });
            return Task.FromResult(new ReplayService(GameContent.Shared).Check(packet));
        }
    }
}
