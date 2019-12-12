using MediaBrowser.Model.ApiClient;
using MediaBrowser.Model.System;
using System;
using System.Collections.Generic;

namespace Jellyfin.ApiClient.Model
{
    public class ServerInfo
    {
        public String Name { get; set; }
        public String Id { get; set; }
        public Uri Address { get; set; }
        public Guid UserId { get; set; }
        public String AccessToken { get; set; }
        public DateTime DateLastAccessed { get; set; }

        public ServerInfo()
        {
        }

        public void ImportInfo(PublicSystemInfo systemInfo)
        {
            Name = systemInfo.ServerName;
            Id = systemInfo.Id;

            if (!string.IsNullOrEmpty(systemInfo.LocalAddress))
            {
                Address = new Uri(systemInfo.LocalAddress);
            }

            if (!string.IsNullOrEmpty(systemInfo.LocalAddress))
            {
                Address = new Uri(systemInfo.LocalAddress);
            }
        }
    }
}
