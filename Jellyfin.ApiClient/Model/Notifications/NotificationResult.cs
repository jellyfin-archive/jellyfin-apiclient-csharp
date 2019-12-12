﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Jellyfin.ApiClient.Model.Notifications
{
    public class NotificationResult
    {
        public Notification[] Notifications { get; set; }
        public int TotalRecordCount { get; set; }
    }
}
