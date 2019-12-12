using MediaBrowser.Model.Dlna;
using System;
using System.Collections.Generic;
using System.Text;

namespace Jellyfin.ApiClient.Playback
{
    public class PlaybackException : Exception
    {
        public PlaybackErrorCode ErrorCode { get; set; }
    }
}
