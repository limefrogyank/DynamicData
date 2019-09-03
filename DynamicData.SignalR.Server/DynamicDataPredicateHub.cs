using DynamicData.SignalR.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serialize.Linq.Serializers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.SignalR.Server
{
    public abstract class DynamicDataPredicateHub<TObject, TKey, TContext> : DynamicDataCacheHub<TObject, TKey, TContext>
        where TContext : DbContext
        where TObject : class
    {
        abstract protected Func<TObject,bool> WherePredicate { get; }

        virtual protected List<Func<TObject, string>> GroupPredicates => new List<Func<TObject, string>>();

        public DynamicDataPredicateHub(TContext dbContext) : base(dbContext)
        {

        }

        //protected Func<TObject, bool> GetWherePredicate()
        //{
        //    var wherePredicateString = (string)Context.Items["WherePredicate"];
        //    var deserializer = new ExpressionSerializer(new JsonSerializer());
        //    var wherePredicateExpression = (Expression<Func<TObject, bool>>)deserializer.DeserializeText(wherePredicateString);

        //    var wherePredicate = wherePredicateExpression.Compile();
        //    return wherePredicate;
        //}

        //protected List<Func<TObject, string>> GetGroupPredicates()
        //{
        //    if (Context.Items.ContainsKey("GroupPredicates"))
        //    {
        //        var predicates = new List<Func<TObject, string>>();
        //        var groupPredicatesStrings = (List<string>)Context.Items["GroupPredicates"];
        //        var deserializer = new ExpressionSerializer(new JsonSerializer());
        //        foreach (var groupPredicateString in groupPredicatesStrings)
        //        {                    
        //            var groupPredicateExpression = (Expression<Func<TObject, string>>)deserializer.DeserializeText(groupPredicateString);

        //            var groupPredicate = groupPredicateExpression.Compile();
        //            predicates.Add(groupPredicate);
        //        }
        //        return predicates;
        //    }
        //    else
        //    {
        //        return new List<Func<TObject, string>>();
        //    }
        //}

        public override Task AddOrUpdateObjects(IEnumerable<TObject> items)
        {
            //var justOwned = items.Where((Func<TObject, bool>)Context.Items["WherePredicate"]).ToList();
            var justOwned = items.Where(WherePredicate).ToList();
            return base.AddOrUpdateObjects(justOwned);
        }

        public override Task Clone(string changeSetString)
        {
            return base.Clone(changeSetString);
        }



        public override Dictionary<TKey, TObject> GetKeyValuePairs()
        {
            //var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            var keySelector = GetKeySelector();

            IQueryable<TObject> query = _dbContext.Set<TObject>();
            query = ChainIncludes(query);
            var data = query.Where(WherePredicate).ToDictionary((o) => keySelector.Invoke(o));

            if (data == null)
            {
                data = new Dictionary<TKey, TObject>();
            }
            //_dbContext.Set<TObject>().Where((Func<TObject, bool>)Context.Items["WherePredicate"]).ToDictionary((o) => keySelector.Invoke(o));
            return data;
        }

        public override Task<Dictionary<TKey, TObject>> GetKeyValuePairsFiltered(string predicateFilterString)
        {
            try
            {
                //var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
                var keySelector = GetKeySelector(); 
                var deserializer = new ExpressionSerializer(new JsonSerializer());
                var filterExpression = (Expression<Func<TObject, bool>>)deserializer.DeserializeText(predicateFilterString);
                IQueryable<TObject> query = _dbContext.Set<TObject>();
                query = ChainIncludes(query);
                var data = query.Where(WherePredicate).Where(filterExpression.Compile()).ToDictionary((o) => keySelector.Invoke(o));

                //var data = _dbContext.Set<TObject>().Where((Func<TObject, bool>)Context.Items["WherePredicate"]).Where(filterExpression.Compile()).ToDictionary((o) => keySelector.Invoke(o));
                return Task.FromResult(data);
            }
            catch
            {
                return Task.FromResult<Dictionary<TKey, TObject>>(null);
            }
        }

        public override Task RefreshKeys(IEnumerable<TKey> keys)
        {
            //var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            var keySelector = GetKeySelector(); 
            Dictionary<TObject, TKey> existing = new Dictionary<TObject, TKey>();

            foreach (var key in keys)
            {
                var found = _dbContext.Set<TObject>().Find(key);
                if (found != null)
                {
                    existing.Add(found, key);
                }
            }
            var ownedObjects = existing.Select(x => x.Key).Where(WherePredicate).ToDictionary((x) => keySelector(x));

            return base.RefreshKeys(existing.Select(x => x.Value));
        }

        public override Task RemoveItems(IEnumerable<TObject> items)
        {
            var ownedItems = items.Where(WherePredicate);
            return base.RemoveItems(ownedItems);
        }

        public override async Task RemoveKeys(IEnumerable<TKey> keys)
        {
            //var keySelector = (Func<TObject, TKey>)Context.Items["KeySelector"];
            var keySelector = GetKeySelector(); 
            Dictionary<TObject, TKey> existing = new Dictionary<TObject, TKey>();

            foreach (var key in keys)
            {
                var found = await _dbContext.Set<TObject>().FindAsync(key);
                if (found != null)
                {
                    existing.Add(found, key);
                }
            }
            var ownedObjects = existing.Select(x => x.Key).Where(WherePredicate).ToDictionary((x) => keySelector(x));

            await base.RemoveKeys(existing.Select(x => x.Value));
        }

        protected override Task SendChangesToOthersAsync(ChangeAwareCache<TObject, TKey> changeAwareCache)
        {
            //var groupIdentifier = (string)Context.Items["GroupIdentifier"];
            //var groupPredicates = (List<Func<TObject, string>>)Context.Items["GroupPredicates"];
            //var groupPredicates = GetGroupPredicates();

            List<Task> tasks = new List<Task>();
            var changes = changeAwareCache.CaptureChanges();

            // send to current group
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());
            tasks.Add(Clients.OthersInGroup((string)Context.Items["GroupIdentifier"]).Changes(json));

            // send to other groups if defined
            //if (groupPredicates != null)
            //{
                foreach (var group in GroupPredicates)
                {
                    var groupedByIdentifier = changes.GroupBy(x => group.Invoke(x.Current));
                    foreach (var subGroup in groupedByIdentifier)
                    {
                        try
                        {
                            //new ChangeSet<TObject, TKey>(subGroup)
                            json = Newtonsoft.Json.JsonConvert.SerializeObject(new ChangeSet<TObject, TKey>(subGroup), new ChangeSetConverter<TObject, TKey>());
                            tasks.Add(Clients.OthersInGroup(subGroup.Key).Changes(json));
                        }
                        catch
                        {
                            var nothing = 0;
                        }
                    }
                }
            //}
            return Task.WhenAll(tasks);
        }

        /// <inheritdoc/>
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, (string)Context.Items["GroupIdentifier"]);//(string)Context.Items["GroupIdentifier"]);

            await base.OnConnectedAsync();
        }

        /// <inheritdoc/>
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, (string)Context.Items["GroupIdentifier"]);//(string)Context.Items["GroupIdentifier"]);
            await base.OnDisconnectedAsync(exception);
        }



    }
}
