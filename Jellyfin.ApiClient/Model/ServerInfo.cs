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
        public List<WakeOnLanInfo> WakeOnLanInfos { get; set; }
        public DateTime DateLastAccessed { get; set; }

        public ServerInfo()
        {
            WakeOnLanInfos = new List<WakeOnLanInfo>();
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

            var fullSystemInfo = systemInfo as SystemInfo;

            if (fullSystemInfo != null)
            {
                WakeOnLanInfos = new List<WakeOnLanInfo>();

                if (!string.IsNullOrEmpty(fullSystemInfo.MacAddress))
                {
                    WakeOnLanInfos.Add(new WakeOnLanInfo
                    {
                        MacAddress = fullSystemInfo.MacAddress
                    });
                }
            }
        }
    }
}
