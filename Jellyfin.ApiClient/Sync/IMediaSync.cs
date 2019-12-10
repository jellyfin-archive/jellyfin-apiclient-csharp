using Jellyfin.ApiClient.Model;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.ApiClient.Sync
{
    public interface IMediaSync
    {
        Task Sync(IApiClient apiClient,
            ServerInfo serverInfo,
            IProgress<double> progress,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}