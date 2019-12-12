using Jellyfin.ApiClient.Model;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.ApiClient.Net
{
    public interface INetworkConnection
    {
        /// <summary>
        /// Occurs when [network changed].
        /// </summary>
        event EventHandler<EventArgs> NetworkChanged;

        /// <summary>
        /// Sends the wake on lan.
        /// </summary>
        /// <param name="macAddress">The mac address.</param>
        /// <param name="ipAddress">The ip address.</param>
        /// <param name="port">The port.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        Task SendWakeOnLan(string macAddress, string ipAddress, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the wake on lan.
        /// </summary>
        /// <param name="macAddress">The mac address.</param>
        /// <param name="port">The port.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        Task SendWakeOnLan(string macAddress, int port, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the network status.
        /// </summary>
        /// <returns>NetworkStatus.</returns>
        NetworkStatus GetNetworkStatus();

#if WINDOWS_UWP
        bool HasUnmeteredConnection();
#endif
    }
}
