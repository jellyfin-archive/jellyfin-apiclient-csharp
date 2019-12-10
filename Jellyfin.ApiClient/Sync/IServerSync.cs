using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.ApiClient.Model;
using MediaBrowser.Model.ApiClient;

namespace Jellyfin.ApiClient.Sync
{
    public interface IServerSync
    {
        Task Sync(ServerInfo server, bool enableCameraUpload, IProgress<double> progress, CancellationToken cancellationToken = default(CancellationToken));
    }
}