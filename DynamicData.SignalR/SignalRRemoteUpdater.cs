using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using DynamicData.SignalR.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Serialize.Linq.Serializers;

namespace DynamicData.SignalR
{
    public class SignalRRemoteUpdater<TObject, TKey> : SignalRRemoteUpdaterBase<TObject,TKey>
    {
        private readonly HubConnection _connection;
        //private readonly ICache<TObject, TKey> _cache;
        //private readonly Expression<Func<TObject, TKey>> _keySelectorExpression;
        //private readonly Func<TObject, TKey> _keySelector;
        //private readonly string _selectorString;

        public SignalRRemoteUpdater(HubConnection connection, ICache<TObject, TKey> cache, Expression<Func<TObject, TKey>> keySelectorExpression = null)
            :base(cache, keySelectorExpression)
        {
            _connection = connection;
            
        }

        public SignalRRemoteUpdater(HubConnection connection, Dictionary<TKey, TObject> data, Expression<Func<TObject, TKey>> keySelectorExpression = null)
            :base(data, keySelectorExpression)
        {
            _connection = connection;
        }
               
        public override void AddOrUpdate(IEnumerable<TObject> items)
        {
            base.AddOrUpdate(items);
            //if (items == null) throw new ArgumentNullException(nameof(items));
            //if (_keySelector == null)
            //    throw new KeySelectorException("A key selector must be specified");

            //if (items is IList<TObject> list)
            //{
            //    //zero allocation enumerator
            //    var enumerable = EnumerableIList.Create(list);
            //    foreach (var item in enumerable)
            //        _cache.AddOrUpdate(item, _keySelector(item));
            //}
            //else
            //{
            //    foreach (var item in items)
            //        _cache.AddOrUpdate(item, _keySelector(item));
            //}

            _connection.InvokeAsync("AddOrUpdateObjects", items);
        }

        public override void AddOrUpdate(TObject item)
        {
            base.AddOrUpdate(item);
            _connection.InvokeAsync("AddOrUpdateObjects", new[] { item });
        }

        public override void AddOrUpdate(TObject item, IEqualityComparer<TObject> comparer)
        {
            throw new NotImplementedException();
        }

        public override void AddOrUpdate(IEnumerable<KeyValuePair<TKey, TObject>> keyValuePairs)
        {
            base.AddOrUpdate(keyValuePairs);
            _connection.InvokeAsync("AddOrUpdateObjects", keyValuePairs.Select(x => x.Value));
        }

        public override void AddOrUpdate(KeyValuePair<TKey, TObject> item)
        {
            base.AddOrUpdate(item);
            _connection.InvokeAsync("AddOrUpdateObjects", new[] { item.Value });
        }

        public override void AddOrUpdate(TObject item, TKey key)
        {
            base.AddOrUpdate(item, key);
            _connection.InvokeAsync("AddOrUpdateObjects", new[] { item });
        }

        public override void Clear()
        {
            var items = _cache.Items.ToList();
            base.Clear();
            _connection.InvokeAsync("RemoveItems", items);
        }
        
        public override void Clone(IChangeSet<TObject, TKey> changes)
        {
            base.Clone(changes);

            var changesString = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());
            _connection.InvokeAsync("Clone", changesString);
        }


        public override void Refresh(TObject item)
        {
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            var key = _keySelector(item);
            _cache.Refresh(key);

            _connection.InvokeAsync("RefreshKeys", new List<TKey>() { key });
        }

        public override void Refresh()
        {
            base.Refresh();
            _connection.InvokeAsync("RefreshKeys", _cache.Keys.ToList());
        }
      
        //public void Refresh(IEnumerable<TKey> keys)
        //{
        //    if (keys == null) throw new ArgumentNullException(nameof(keys));
        //    if (keys is IList<TKey> list)
        //    {
        //        //zero allocation enumerator
        //        var enumerable = EnumerableIList.Create(list);
        //        foreach (var item in enumerable)
        //            Refresh(item);
        //    }
        //    else
        //    {
        //        foreach (var key in keys)
        //            Refresh(key);
        //    }

        //    _connection.InvokeAsync("RefreshKeys", keys);
        //}

        
        public override void Refresh(TKey key)
        {
            base.Refresh(key);
            _connection.InvokeAsync("RefreshKeys", new List<TKey>() { key });
        }

        //public void Remove(IEnumerable<TObject> items)
        //{
        //    if (items == null) throw new ArgumentNullException(nameof(items));

        //    if (items is IList<TObject> list)
        //    {
        //        //zero allocation enumerator
        //        var enumerable = EnumerableIList.Create(list);
        //        foreach (var item in enumerable)
        //            Remove(item);
        //    }
        //    else
        //    {
        //        foreach (var item in items)
        //            Remove(item);
        //    }
        //    _connection.InvokeAsync("RemoveItems", items);

        //}

        public override void Remove(TObject item)
        {
            base.Remove(item);
            _connection.InvokeAsync("RemoveItems", new[] { item });
        }

        //public void Remove(IEnumerable<TKey> keys)
        //{
        //    if (keys == null) throw new ArgumentNullException(nameof(keys));
        //    if (keys is IList<TKey> list)
        //    {
        //        //zero allocation enumerator
        //        var enumerable = EnumerableIList.Create(list);
        //        foreach (var key in enumerable)
        //            Remove(key);
        //    }
        //    else
        //    {
        //        foreach (var key in keys)
        //            Remove(key);
        //    }
        //    _connection.InvokeAsync("RemoveKeys", keys);
        //}

        public override void Remove(TKey key)
        {
            base.Remove(key);
            _connection.InvokeAsync("RemoveKeys", new[] { key });
        }

        //public void Remove(IEnumerable<KeyValuePair<TKey, TObject>> items)
        //{
        //    _connection.InvokeAsync("RemoveKeyValuePairs", items);
        //}

        public override void Remove(KeyValuePair<TKey, TObject> item)
        {
            base.Remove(item);
            _connection.InvokeAsync("RemoveKeys", new[] { item.Key });
        }


        public override void Update(IChangeSet<TObject, TKey> changes)
        {
            base.Update(changes);
            var changesString = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());
            _connection.InvokeAsync("Clone", changesString);
        }
    }
}
