﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.SignalR
{
    public static class DynamicDataHubContextExtensions
    {
        
        public static async void ItemsAddedExternallyGroupOverride<THub,TObject,TKey,TContext>(
            this IHubContext<THub> hubContext,
            IEnumerable<TObject> items,
            Func<TObject, TKey> keySelector,
            string groupIdentifier)
            where THub : Hub
            where TContext: DbContext
            where TObject : class
        {
            try
            {
                var changeAwareCache = new ChangeAwareCache<TObject, TKey>();
                foreach (var item in items)
                {
                    var key = keySelector.Invoke(item);
                    changeAwareCache.AddOrUpdate(item, key);
                }

                  var changes = changeAwareCache.CaptureChanges();
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());
                await hubContext.Clients.Group(groupIdentifier).SendAsync("Changes", json);

            }
            catch
            {
                var f = 3;
            }
        }
    }
}
