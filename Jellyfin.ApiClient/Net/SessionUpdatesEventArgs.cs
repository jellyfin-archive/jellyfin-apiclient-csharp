using Jellyfin.ApiClient.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Jellyfin.ApiClient.Net
{
    /// <summary>	
    /// Class SessionUpdatesEventArgs	
    /// </summary>	
    public class SessionUpdatesEventArgs
    {
        public SessionInfoDto[] Sessions { get; set; }
    }
}
