﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Jellyfin.ApiClient.WebSocket
{
    /// <summary>
    /// Enum WebSocketMessageType
    /// </summary>
    public enum WebSocketMessageType
    {
        /// <summary>
        /// The text
        /// </summary>
        Text,
        /// <summary>
        /// The binary
        /// </summary>
        Binary,
        /// <summary>
        /// The close
        /// </summary>
        Close
    }
}
