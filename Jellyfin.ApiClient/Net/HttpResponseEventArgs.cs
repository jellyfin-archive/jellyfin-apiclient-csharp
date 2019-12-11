using System;
using System.Collections.Generic;
using System.Net;

namespace Jellyfin.ApiClient.Net
{
    /// <summary>
    /// HttpResponseEventArgs class
    /// </summary>
    class HttpResponseEventArgs
    {
        /// <summary>
        /// Gets or sets the URL.
        /// </summary>
        /// <value>The URL.</value>
        public Uri Url { get; set; }

        /// <summary>
        /// Gets or sets the status code.
        /// </summary>
        /// <value>The status code.</value>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the headers.
        /// </summary>
        /// <value>The headers.</value>
        public Dictionary<string, string> Headers { get; set; }

        public HttpResponseEventArgs()
        {
            Headers = new Dictionary<string, string>();
        }
    }
}
