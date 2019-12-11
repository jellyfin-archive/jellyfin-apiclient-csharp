using Jellyfin.ApiClient.Model;
using MediaBrowser.Model.Devices;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.ApiClient
{
    public class Device : IDevice
    {
        public string DeviceName { get; set; }
        public string DeviceId { get; set; }
    }
}
