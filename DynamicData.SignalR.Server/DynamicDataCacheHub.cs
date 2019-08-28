using DynamicData.Kernel;
using DynamicData.SignalR.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serialize.Linq.Serializers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DynamicData.SignalR.Server
{
    public class DynamicDataCacheHub<TObject, TKey, TContext> : Hub<IDynamicDataCacheClient<TObject, TKey>> where TContext : DbContext
        where TObject : class
    {
        protected readonly TContext _dbContext;

        protected List<string> IncludeChain { get; set; } = new List<string>();

        public DynamicDataCacheHub(TContext dbContext)
        {
            _dbContext = dbContext;
        }

        protected virtual Task SendChangesToOthersAsync(ChangeAwareCache<TObject, TKey> changeAwareCache)
        {
            var changes = changeAwareCache.CaptureChanges();
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());
                return Clients.Others.Changes(json);
            }
            catch
            {
                return Task.CompletedTask;
            }
        }

        protected Func<TObject,TKey> GetKeySelector()
        {
            var keySelectorString = (string)Context.Items["KeySelector"];
            var deserializer = new ExpressionSerializer(new JsonSerializer());
            var keySelectorExpression = (Expression<Func<TObject, TKey>>)deserializer.DeserializeText(keySelectorString);

            var keySelector = keySelectorExpression.Compile();
            return keySelector;
        }

        public void Initialize(string keySelectorString)
        {            
            Context.Items["KeySelector"] = keySelectorString;
        }

        protected IQueryable<TObject> ChainIncludes(IQueryable<TObject> query)
        {
            //var includeChain = (List<string>)Context.Items["IncludeChain"];
            //if (includeChain == null)
            //    return query;
            foreach (var includeString in IncludeChain)
                query = query.Include(includeString);
            return query;
        }

        protected IQueryable<TObject> StartQuery()
        {
            IQueryable<TObject> query = _dbContext.Set<TObject>();
            query = ChainIncludes(query);
            return query;
        }

        public virtual Dictionary<TKey, TObject> GetKeyValuePairs()
        {
            //var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            var keySelector = GetKeySelector();
            var data = StartQuery().ToDictionary((o) => keySelector.Invoke(o));
            return data;
        }

        public virtual Task<Dictionary<TKey, TObject>> GetKeyValuePairsFiltered(string predicateFilterString)
        {
            //var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            var keySelector = GetKeySelector();

            var deserializer = new ExpressionSerializer(new JsonSerializer());
            var filterExpression = (Expression<Func<TObject, bool>>)deserializer.DeserializeText(predicateFilterString);

            var data = StartQuery().Where(filterExpression).ToDictionary((o) => keySelector.Invoke(o));
            return Task.FromResult(data);
        }



        public virtual async Task AddOrUpdateObjects(IEnumerable<TObject> items)
        {
            try
            {
                //var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
                var keySelector = GetKeySelector();
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

                _dbContext.SaveChanges();

                var changeAwareCache = new ChangeAwareCache<TObject, TKey>(existing);
                foreach (var item in items)
                {
                    var key = keySelector.Invoke(item);
                    changeAwareCache.AddOrUpdate(item, key);
                }



                await SendChangesToOthersAsync(changeAwareCache);

            }
            catch
            {
                var f = 3;
            }
        }



        public virtual async Task RemoveItems(IEnumerable<TObject> items)
        {
            //var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            var keySelector = GetKeySelector(); 
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


        public virtual async Task RemoveKeys(IEnumerable<TKey> keys)
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



        public virtual async Task RefreshKeys(IEnumerable<TKey> keys)
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
            changeAwareCache.Refresh(existing.Select(x => x.Key));

            await SendChangesToOthersAsync(changeAwareCache);
        }

        public virtual async Task Clone(string changeSetString)
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
