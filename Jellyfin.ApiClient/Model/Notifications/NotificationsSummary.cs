using System;
using System.Collections.Generic;
using System.Text;

namespace Jellyfin.ApiClient.Model.Notifications
{
    public class NotificationsSummary
    {
        public int UnreadCount { get; set; }
        public NotificationLevel MaxUnreadNotificationLevel { get; set; }
    }
}
