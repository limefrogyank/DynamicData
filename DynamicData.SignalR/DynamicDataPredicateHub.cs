using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serialize.Linq.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.SignalR
{
    public abstract class DynamicDataPredicateHub<TObject, TKey, TContext> : DynamicDataCacheHub<TObject, TKey, TContext>
        where TContext : DbContext
        where TObject : class
    {
        public DynamicDataPredicateHub(TContext dbContext) : base(dbContext)
        {

        }

        public override Task AddOrUpdateObjects(IEnumerable<TObject> items)
        {
            var justOwned = items.Where((Func<TObject, bool>)Context.Items["WherePredicate"]).ToList();
            return base.AddOrUpdateObjects(justOwned);
        }

        public override Task Clone(string changeSetString)
        {
            return base.Clone(changeSetString);
        }



        public override Dictionary<TKey, TObject> GetKeyValuePairs()
        {
            var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];

            IQueryable<TObject> query = _dbContext.Set<TObject>();
            query = ChainIncludes(query);
            var data = query.Where((Func<TObject, bool>)Context.Items["WherePredicate"]).ToDictionary((o) => keySelector.Invoke(o));
            //_dbContext.Set<TObject>().Where((Func<TObject, bool>)Context.Items["WherePredicate"]).ToDictionary((o) => keySelector.Invoke(o));
            return data;
        }

        public override Task<Dictionary<TKey, TObject>> GetKeyValuePairsFiltered(string predicateFilterString)
        {
            var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            var deserializer = new ExpressionSerializer(new JsonSerializer());
            var filterExpression = (Expression<Func<TObject, bool>>)deserializer.DeserializeText(predicateFilterString);
            IQueryable<TObject> query = _dbContext.Set<TObject>();
            query = ChainIncludes(query);
            var data = query.Where((Func<TObject, bool>)Context.Items["WherePredicate"]).Where(filterExpression.Compile()).ToDictionary((o) => keySelector.Invoke(o));

            //var data = _dbContext.Set<TObject>().Where((Func<TObject, bool>)Context.Items["WherePredicate"]).Where(filterExpression.Compile()).ToDictionary((o) => keySelector.Invoke(o));
            return Task.FromResult(data);
        }

        public override Task RefreshKeys(IEnumerable<TKey> keys)
        {
            var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            Dictionary<TObject, TKey> existing = new Dictionary<TObject, TKey>();

            foreach (var key in keys)
            {
                var found = _dbContext.Set<TObject>().Find(key);
                if (found != null)
                {
                    existing.Add(found, key);
                }
            }
            var ownedObjects = existing.Select(x => x.Key).Where((Func<TObject, bool>)Context.Items["WherePredicate"]).ToDictionary((x) => keySelector(x));

            return base.RefreshKeys(existing.Select(x => x.Value));
        }

        public override Task RemoveItems(IEnumerable<TObject> items)
        {
            var ownedItems = items.Where((Func<TObject, bool>)Context.Items["WherePredicate"]);
            return base.RemoveItems(ownedItems);
        }

        public override async Task RemoveKeys(IEnumerable<TKey> keys)
        {
            var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            Dictionary<TObject, TKey> existing = new Dictionary<TObject, TKey>();

            foreach (var key in keys)
            {
                var found = await _dbContext.Set<TObject>().FindAsync(key);
                if (found != null)
                {
                    existing.Add(found, key);
                }
            }
            var ownedObjects = existing.Select(x => x.Key).Where((Func<TObject, bool>)Context.Items["WherePredicate"]).ToDictionary((x) => keySelector(x));

            await base.RemoveKeys(existing.Select(x => x.Value));
        }

        protected override Task SendChangesToOthersAsync(ChangeAwareCache<TObject, TKey> changeAwareCache)
        {
            //var groupIdentifier = (string)Context.Items["GroupIdentifier"];
            var groupPredicates = (List<Func<TObject, string>>)Context.Items["GroupPredicates"];

            List<Task> tasks = new List<Task>();
            var changes = changeAwareCache.CaptureChanges();

            // send to current group
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());
            tasks.Add(Clients.OthersInGroup((string)Context.Items["GroupIdentifier"]).Changes(json));

            // send to other groups if defined
            if (groupPredicates != null)
            {
                foreach (var group in groupPredicates)
                {
                    var groupedByIdentifier = changes.GroupBy(x => group.Invoke(x.Current));
                    foreach (var subGroup in groupedByIdentifier)
                    {
                        json = Newtonsoft.Json.JsonConvert.SerializeObject(subGroup, new ChangeSetConverter<TObject, TKey>());
                        tasks.Add(Clients.OthersInGroup(subGroup.Key).Changes(json));
                    }
                }
            }
            return Task.WhenAll(tasks);
        }

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, (string)Context.Items["GroupIdentifier"]);//(string)Context.Items["GroupIdentifier"]);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, (string)Context.Items["GroupIdentifier"]);//(string)Context.Items["GroupIdentifier"]);
            await base.OnDisconnectedAsync(exception);
        }

       

    }
}
