﻿using Elfo.Wardein.Abstractions.Configuration.Models;
using Elfo.Wardein.Abstractions.Services;
using Elfo.Wardein.Abstractions.Watchers;
using Elfo.Wardein.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Warden.Watchers;

namespace Elfo.Wardein.Watchers
{
    public abstract class WardeinWatcherWithResolution<T> : WardeinWatcher<T> where T : IAmConfigurationModelWithResolution
    {
        private readonly IAmNotificationService notificationService;

        protected WardeinWatcherWithResolution(string name, T config, string group = null) : base(name, config, group)
        {
            notificationService = ServicesContainer.NotificationService(config.NotificationType);
        }

        protected virtual string GetLoggingDisplayName => $"WatcherConfigurationId: {base.Config.WatcherConfigurationId}";

        protected virtual async Task PerformActionOnServiceDown(WatcherStatusResult watcherStatusResult, Func<T, Task> howToActInCaseOfError)
        {
            if (IsFailureCountEqualToMaxRetyrCount() || IsMultipleOfReminderRetryCount())
            {
                log.Warn($"Sending Fail Notification for {GetLoggingDisplayName}");
                await notificationService.SendNotificationAsync(Config.RecipientAddresses, Config.FailureMessage,
                        $"Attention: {GetLoggingDisplayName} is down on {Config.ApplicationHostname}");
            }

            if (howToActInCaseOfError != null)
            {
                log.Info($"trying to restore {GetLoggingDisplayName} on {Config.ApplicationHostname}");
                await howToActInCaseOfError(Config);
                log.Info($"{GetLoggingDisplayName} was restarted");
            }

            #region Local Functions

            bool IsFailureCountEqualToMaxRetyrCount() => watcherStatusResult.FailureCount == Config.MaxRetryCount;

            bool IsMultipleOfReminderRetryCount() => watcherStatusResult.FailureCount % Config.SendReminderEmailAfterRetryCount == 0;

            #endregion
        }

        protected virtual async Task PerformActionOnServiceAlive(WatcherStatusResult watcherStatusResult)
        {
            try
            {
                log.Info($"{GetLoggingDisplayName} is active");
                if (!watcherStatusResult.PreviousStatus)
                {
                    log.Info($"Send Restored Notification for {GetLoggingDisplayName}");
                    await notificationService.SendNotificationAsync(Config.RecipientAddresses, Config.RestoredMessage,
                          $"Good news: {GetLoggingDisplayName} has been restored succesfully");
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Unable to send email from {GetLoggingDisplayName} watcher");
            }
        }
    }
}
