using DynamicData.Kernel;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serialize.Linq.Serializers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DynamicData.SignalR
{
    public class DynamicDataCacheHub<TObject,TKey, TContext> : Hub 
        where TContext : DbContext 
        where TObject : class
    {
        private readonly TContext _dbContext;

        public DynamicDataCacheHub(TContext dbContext)
        {
            _dbContext = dbContext;
        }

        private Task SendChangesToOthersAsync(ChangeAwareCache<TObject,TKey> changeAwareCache)
        {
            var changes = changeAwareCache.CaptureChanges();
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());
            return Clients.Others.SendAsync("Changes", json);
        }

        public void Initialize(string keySelectorString)
        {
            var deserializer = new ExpressionSerializer(new JsonSerializer());
            var keySelectorExpression = (Expression<Func<TObject, TKey>>)deserializer.DeserializeText(keySelectorString);
            
            var keySelector = keySelectorExpression.Compile();

            Context.Items["KeySelector"] = keySelector;

        }


        public Dictionary<TKey, TObject> GetKeyValuePairs()
        {
            var keySelector = (Func<TObject,TKey>)Context.Items["KeySelector"];

            var data = _dbContext.Set<TObject>().ToDictionary((o) => keySelector.Invoke(o));
            return data;
        }

        public Task<Dictionary<TKey,TObject>> GetKeyValuePairsFiltered(string predicateFilterString)
        {
            var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];

            var deserializer = new ExpressionSerializer(new JsonSerializer());
            var filterExpression = (Expression<Func<TObject, bool>>)deserializer.DeserializeText(predicateFilterString);

            var data = _dbContext.Set<TObject>().Where(filterExpression).ToDictionary((o) => keySelector.Invoke(o));
            return Task.FromResult(data);
        }



        public async Task AddOrUpdateObjects(IEnumerable<TObject> items)
        {
            var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();
            
            foreach (var item in items)
            {
                var key = keySelector.Invoke(item);
                var found = _dbContext.Set<TObject>().Find(key);
                if (found != null)
                {
                    existing.Add(key, found);
                    _dbContext.Entry(found).CurrentValues.SetValues(item);
                }
                else
                    _dbContext.Add(item);
            }

            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
            foreach (var item in items)
            {
                var key = keySelector.Invoke(item);
                changeAwareCache.AddOrUpdate(item, key);
            }

            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task AddOrUpdateObject(TObject item)
        {
            var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();

            var key = keySelector.Invoke(item);
            var found = _dbContext.Set<TObject>().Find(key);
            if (found != null)
            {
                existing.Add(key, found);
                _dbContext.Entry(found).CurrentValues.SetValues(item);
            }
            else
                _dbContext.Add(item);

            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);            
            changeAwareCache.AddOrUpdate(item, key);
            
            var num = _dbContext.SaveChanges();

            await SendChangesToOthersAsync(changeAwareCache);
            //var changes = changeAwareCache.CaptureChanges();
            //var json = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());
            //await Clients.Others.SendAsync("Changes", json);
        }

        public async Task AddOrUpdateKeyValuePair(KeyValuePair<TKey, TObject> pair)
        {
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();

            var found = _dbContext.Set<TObject>().Find(pair.Key);
            if (found != null)
            {
                existing.Add(pair.Key, found);
                _dbContext.Update(pair.Value);
            }
            else
                _dbContext.Add(pair.Value);

            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
            changeAwareCache.AddOrUpdate(pair.Value, pair.Key);

            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task AddOrUpdateKeyValuePairs(IEnumerable<KeyValuePair<TKey, TObject>> pairs)
        {
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();

            foreach (var pair in pairs)
            {
                var found = _dbContext.Set<TObject>().Find(pair.Key);
                if (found != null)
                {
                    existing.Add(pair.Key, found);
                    _dbContext.Update(pair.Value);
                }
                else
                    _dbContext.Add(pair.Value);
            }
            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);

            foreach (var pair in pairs)
                changeAwareCache.AddOrUpdate(pair.Value, pair.Key);

            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task AddOrUpdateValueWithKey(TObject item, TKey key)
        {
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();

            var found = _dbContext.Set<TObject>().Find(key);
            if (found != null)
            {
                existing.Add(key, found);
                _dbContext.Update(item);
            }
            else
                _dbContext.Add(item);

            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
            changeAwareCache.AddOrUpdate(item, key);

            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task RemoveItems(IEnumerable<TObject> items)
        {
            var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();

            foreach (var item in items)
            {
                var key = keySelector.Invoke(item);
                var found = _dbContext.Set<TObject>().Find(key);
                if (found != null)
                {
                    existing.Add(key, item);
                    _dbContext.Remove(found);
                }
            }

            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
            foreach (var pair in existing)
            {                
                changeAwareCache.Remove(pair.Key);
            }

            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task RemoveItem(TObject item)
        {
            var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();
            
            var key = keySelector.Invoke(item);
            var found = _dbContext.Set<TObject>().Find(key);
            if (found != null)
            {
                existing.Add(key, item);
                _dbContext.Remove(found);
            }

            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);

            changeAwareCache.Remove(key);
            
            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task RemoveKeys(IEnumerable<TKey> keys)
        {
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();

            foreach (var key in keys)
            {
                var found = _dbContext.Set<TObject>().Find(key);
                if (found != null)
                {
                    existing.Add(key, found);
                    _dbContext.Remove(found);
                }
            }

            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
            foreach (var pair in existing)
            {
                changeAwareCache.Remove(pair.Key);
            }

            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task RemoveKey(TKey key)
        {
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();
            
            var found = _dbContext.Set<TObject>().Find(key);
            if (found != null)
            {
                existing.Add(key, found);
                _dbContext.Remove(found);
            }
            
            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
            changeAwareCache.Remove(key);


            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task RemoveKeyValuePairs(IEnumerable<KeyValuePair<TKey,TObject>> pairs)
        {
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();

            foreach (var pair in pairs)
            {
                var found = _dbContext.Set<TObject>().Find(pair.Key);
                if (found != null)
                {
                    existing.Add(pair.Key, found);
                    _dbContext.Remove(found);
                }
            }

            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
            foreach (var pair in existing)
            {
                changeAwareCache.Remove(pair.Key);
            }

            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task RemoveKeyValuePair(KeyValuePair<TKey, TObject> pair)
        {
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();

            var found = _dbContext.Set<TObject>().Find(pair.Key);
            if (found != null)
            {
                existing.Add(pair.Key, found);
                _dbContext.Remove(found);
            }
            
            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
            changeAwareCache.Remove(pair.Key);

            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task RefreshKeys(IEnumerable<TKey> keys)
        {
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();

            foreach (var key in keys)
            {
                var found = _dbContext.Set<TObject>().Find(key);
                if (found != null)
                {
                    existing.Add(key, found);  //do nothing to database
                }
            }

            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
            changeAwareCache.Refresh(existing.Select(x=>x.Key));

            await SendChangesToOthersAsync(changeAwareCache);
        }

        public async Task Clone(string changeSetString)
        {
            var changeSet = Newtonsoft.Json.JsonConvert.DeserializeObject<ChangeSet<TObject, TKey>>(changeSetString, new ChangeSetConverter<TObject, TKey>());

            var enumerable = changeSet.ToConcreteType();
            Dictionary<TKey, TObject> existing = new Dictionary<TKey, TObject>();

            foreach (var change in enumerable)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                    case ChangeReason.Update:
                        
                        var found = _dbContext.Set<TObject>().Find(change.Key);
                        if (found != null)
                        {
                            existing.Add(change.Key, found);
                            _dbContext.Update(change.Current);
                        }
                        else
                            _dbContext.Add(change.Current);
                        break;
                    case ChangeReason.Remove:
                        found = _dbContext.Set<TObject>().Find(change.Key);
                        if (found != null)
                        {
                            existing.Add(change.Key, found);
                            _dbContext.Remove(found);
                        }
                        break;
                    case ChangeReason.Refresh:
                        found = _dbContext.Set<TObject>().Find(change.Key);
                        if (found != null)
                        {
                            existing.Add(change.Key, found);  //do nothing to database
                        }
                        break;
                }
            }
            var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
            foreach (var change in enumerable)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                    case ChangeReason.Update:
                        changeAwareCache.AddOrUpdate(change.Current, change.Key);
                        break;
                    case ChangeReason.Remove:
                        changeAwareCache.Remove(change.Key);
                        break;
                    case ChangeReason.Refresh:
                        changeAwareCache.Refresh(change.Key);
                        break;
                }
                
            }

            _dbContext.SaveChanges();
            await SendChangesToOthersAsync(changeAwareCache);
        }

        public override Task OnConnectedAsync()
        {
            Debug.WriteLine("Connected");
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            Debug.WriteLine("Disconnected");
            return base.OnDisconnectedAsync(exception);
        }
    }
}
