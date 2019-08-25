using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using DynamicData;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using DynamicData.SignalR.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Serialize.Linq.Serializers;

namespace DynamicData.SignalR.JSInterop
{
    public class SignalRRemoteUpdater<TObject, TKey> : SignalRRemoteUpdaterBase<TObject, TKey>
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly string _connectionKey;

        //private readonly ICache<TObject, TKey> _cache;
        //private readonly Expression<Func<TObject, TKey>> _keySelectorExpression;
        //private readonly Func<TObject, TKey> _keySelector;
        //private readonly string _selectorString;

        public SignalRRemoteUpdater(IJSRuntime jsRuntime, string connectionKey, ICache<TObject, TKey> cache, Expression<Func<TObject, TKey>> keySelectorExpression = null)
            : base(cache, keySelectorExpression)
        {
            _jsRuntime = jsRuntime;
            _connectionKey = connectionKey;
            //_cache = cache ?? throw new ArgumentNullException(nameof(cache));
            //_keySelectorExpression = keySelectorExpression;

            //_keySelector = _keySelectorExpression.Compile();
            //var serializer = new ExpressionSerializer(new JsonSerializer());
            //_selectorString = serializer.SerializeText(_keySelectorExpression);  //string version for serialization on SignalR
        }

        public SignalRRemoteUpdater(IJSRuntime jsRuntime, string connectionKey, Dictionary<TKey, TObject> data, Expression<Func<TObject, TKey>> keySelectorExpression = null)
            : base(data, keySelectorExpression)
        {
            _jsRuntime = jsRuntime;
            _connectionKey = connectionKey;
            //if (data == null) throw new ArgumentNullException(nameof(data));
            //_cache = new Cache<TObject, TKey>(data);
            //_keySelectorExpression = keySelectorExpression;
            //_keySelector = _keySelectorExpression.Compile();
            //var serializer = new ExpressionSerializer(new JsonSerializer());
            //_selectorString = serializer.SerializeText(_keySelectorExpression);  //string version for serialization on SignalR
        }

        public override void AddOrUpdate(IEnumerable<TObject> items)
        {
            base.AddOrUpdate(items);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "AddOrUpdateObjects", items);
        }

        public override void AddOrUpdate(TObject item)
        {
            base.AddOrUpdate(item);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "AddOrUpdateObjects", new[] { item });
        }

        public override void AddOrUpdate(TObject item, IEqualityComparer<TObject> comparer)
        {
            base.AddOrUpdate(item, comparer);
        }

        public override void AddOrUpdate(IEnumerable<KeyValuePair<TKey, TObject>> keyValuePairs)
        { 
            base.AddOrUpdate(keyValuePairs);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "AddOrUpdateObjects", keyValuePairs.Select(x=>x.Value));
        }

        public override void AddOrUpdate(KeyValuePair<TKey, TObject> item)
        {
            base.AddOrUpdate(item);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "AddOrUpdateObjects", new[] { item.Value });
        }

        public override void AddOrUpdate(TObject item, TKey key)
        {
            base.AddOrUpdate(item, key);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "AddOrUpdateObjects", new[] { item });
        }


        public override void Clear()
        {
            var items = _cache.Items.ToList();
            base.Clear();
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "RemoveItems", items);
        }


        public override void Clone(IChangeSet<TObject, TKey> changes)
        {
            base.Clone(changes);
            var changesString = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "Clone", changesString);
        }






        public override void Refresh(TObject item)
        {
            base.Refresh(item);
            var key = _keySelector(item);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "RefreshKeys", new List<TKey>() { key });
        }

        public override void Refresh()
        {
            base.Refresh();
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "RefreshKeys", _cache.Keys.ToList());
        }

        public override void Refresh(IEnumerable<TKey> keys)
        {
            base.Refresh(keys);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "RefreshKeys", keys);
        }

        public override void Refresh(TKey key)
        {
            base.Refresh(key);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "RefreshKeys", new List<TKey>() { key });
        }




        public override void Remove(TObject item)
        {
            base.Remove(item);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "RemoveItems", new[] { item });
        }

        public override void Remove(TKey key)
        {
            base.Remove(key);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "RemoveKeys", new[] { key });
        }

        public override void Remove(KeyValuePair<TKey, TObject> item)
        {
            base.Remove(item);
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "RemoveKeys", new[] { item.Key });
        }


        public override void Update(IChangeSet<TObject, TKey> changes)
        {
            base.Update(changes);
            var changesString = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());
            _jsRuntime.InvokeAsync<object>("dynamicDataSignalR.invoke", _connectionKey, "Clone", changesString);
        }
    }
}
