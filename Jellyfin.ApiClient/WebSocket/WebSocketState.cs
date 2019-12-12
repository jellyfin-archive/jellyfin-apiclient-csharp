using System;
using System.Collections.Generic;
using System.Text;

namespace Jellyfin.ApiClient.WebSocket
{
    /// <summary>
    /// Enum WebSocketState
    /// </summary>
    public enum WebSocketState
    {
        /// <summary>
        /// The none
        /// </summary>
        None,
        /// <summary>
        /// The connecting
        /// </summary>
        Connecting,
        /// <summary>
        /// The open
        /// </summary>
        Open,
        /// <summary>
        /// The close sent
        /// </summary>
        CloseSent,
        /// <summary>
        /// The close received
        /// </summary>
        CloseReceived,
        /// <summary>
        /// The closed
        /// </summary>
        Closed,
        /// <summary>
        /// The aborted
        /// </summary>
        Aborted
    }
}
