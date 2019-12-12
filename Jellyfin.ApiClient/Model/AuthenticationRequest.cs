using System;
using System.Collections.Generic;
using System.Text;

namespace Jellyfin.ApiClient.Model
{
    class AuthenticationRequest
    {
        public string Username { get; set; }
        public string Pw { get; set; }
    }
}
