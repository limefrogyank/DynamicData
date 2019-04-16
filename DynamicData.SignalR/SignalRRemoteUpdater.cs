using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using Microsoft.AspNetCore.SignalR.Client;
using Serialize.Linq.Serializers;

namespace DynamicData.SignalR
{
    public class SignalRRemoteUpdater<TObject, TKey> : ISourceUpdater<TObject, TKey>
    {
        private readonly HubConnection _connection;
        private readonly ICache<TObject, TKey> _cache;
        private readonly Expression<Func<TObject, TKey>> _keySelectorExpression;
        private readonly Func<TObject, TKey> _keySelector;
        private readonly string _selectorString;

        public SignalRRemoteUpdater(HubConnection connection, ICache<TObject, TKey> cache, Expression<Func<TObject, TKey>> keySelectorExpression = null)
        {
            _connection = connection;
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _keySelectorExpression = keySelectorExpression;
            
            _keySelector = _keySelectorExpression.Compile();
            var serializer = new ExpressionSerializer(new JsonSerializer());
            _selectorString = serializer.SerializeText(_keySelectorExpression);  //string version for serialization on SignalR
        }

        public SignalRRemoteUpdater(HubConnection connection, Dictionary<TKey, TObject> data, Expression<Func<TObject, TKey>> keySelectorExpression = null)
        {
            _connection = connection;
            if (data == null) throw new ArgumentNullException(nameof(data));
            _cache = new Cache<TObject, TKey>(data);
            _keySelectorExpression = keySelectorExpression;
            _keySelector = _keySelectorExpression.Compile();
            var serializer = new ExpressionSerializer(new JsonSerializer());
            _selectorString = serializer.SerializeText(_keySelectorExpression);  //string version for serialization on SignalR
        }
               
        public IEnumerable<TKey> Keys => _cache.Keys;

        public IEnumerable<TObject> Items => _cache.Items;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

        public int Count => _cache.Count;

        public async void AddOrUpdate(IEnumerable<TObject> items)
        {
            
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            if (items is IList<TObject> list)
            {
                //zero allocation enumerator
                var enumerable = EnumerableIList.Create(list);
                foreach (var item in enumerable)
                    _cache.AddOrUpdate(item, _keySelector(item));
            }
            else
            {
                foreach (var item in items)
                    _cache.AddOrUpdate(item, _keySelector(item));
            }

            await _connection.InvokeAsync("AddOrUpdateObjects", items);
        }

        public async void AddOrUpdate(TObject item)
        {
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            var key = _keySelector(item);
            _cache.AddOrUpdate(item, key);

            await _connection.InvokeAsync("AddOrUpdateObject", item);
        }

        public void AddOrUpdate(TObject item, IEqualityComparer<TObject> comparer)
        {
            throw new NotImplementedException();
        }

        public async void AddOrUpdate(IEnumerable<KeyValuePair<TKey, TObject>> keyValuePairs)
        {
            if (keyValuePairs is IList<KeyValuePair<TKey, TObject>> list)
            {
                //zero allocation enumerator
                var enumerable = EnumerableIList.Create(list);
                foreach (var item in enumerable)
                    _cache.AddOrUpdate(item.Value, item.Key);
            }
            else
            {
                foreach (var item in keyValuePairs)
                    _cache.AddOrUpdate(item.Value, item.Key);
            }
            await _connection.InvokeAsync("AddOrUpdateKeyValuePairs", keyValuePairs);
        }

        public async void AddOrUpdate(KeyValuePair<TKey, TObject> item)
        {
            _cache.AddOrUpdate(item.Value, item.Key);
            await _connection.InvokeAsync("AddOrUpdateKeyValuePair", item);
        }

        public async void AddOrUpdate(TObject item, TKey key)
        {
            _cache.AddOrUpdate(item, key);
            await _connection.InvokeAsync("AddOrUpdateValueWithKey", item, key);
        }

        public async void Clear()
        {
            var items = _cache.Items.ToList();
            _cache.Clear();
            await _connection.InvokeAsync("RemoveItems", items);
        }

        public async void Clone(IChangeSet<TObject, TKey> changes)
        {
            _cache.Clone(changes);

            var changesString = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());

            await _connection.InvokeAsync("Clone", changesString);
        }

        public void Evaluate(IEnumerable<TObject> items)
        {
            throw new NotImplementedException();
        }

        public void Evaluate(TObject item)
        {
            throw new NotImplementedException();
        }

        public void Evaluate()
        {
            throw new NotImplementedException();
        }

        public void Evaluate(IEnumerable<TKey> keys)
        {
            throw new NotImplementedException();
        }

        public void Evaluate(TKey key)
        {
            throw new NotImplementedException();
        }

        public TKey GetKey(TObject item)
        {
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            return _keySelector(item);
        }

        public IEnumerable<KeyValuePair<TKey, TObject>> GetKeyValues(IEnumerable<TObject> items)
        {
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            return items.Select(t => new KeyValuePair<TKey, TObject>(_keySelector(t), t));
        }


        //Very dangerous method... clears server items
        public void Load(IEnumerable<TObject> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            Clear();
            AddOrUpdate(items);
        }

        public Optional<TObject> Lookup(TKey key)
        {
            var item = _cache.Lookup(key);
            return item.HasValue ? item.Value : Optional.None<TObject>();
        }



        public async void Refresh(IEnumerable<TObject> items)
        {
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            if (items == null) throw new ArgumentNullException(nameof(items));

            if (items is IList<TObject> list)
            {
                //zero allocation enumerator
                var enumerable = EnumerableIList.Create(list);
                foreach (var item in enumerable)
                {
                    Refresh(item);
                }
            }
            else
            {
                foreach (var item in items)
                    Refresh(item);
            }

            await _connection.InvokeAsync("RefreshKeys", items.Select(x=>_keySelector.Invoke(x)).ToList());
        }

        public async void Refresh(TObject item)
        {
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            var key = _keySelector(item);
            _cache.Refresh(key);

            await _connection.InvokeAsync("RefreshKeys", new List<TKey>() { key });
        }

        public async void Refresh()
        {
            _cache.Refresh();

            await _connection.InvokeAsync("RefreshKeys", _cache.Keys.ToList());
        }

        public async void Refresh(IEnumerable<TKey> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys is IList<TKey> list)
            {
                //zero allocation enumerator
                var enumerable = EnumerableIList.Create(list);
                foreach (var item in enumerable)
                    Refresh(item);
            }
            else
            {
                foreach (var key in keys)
                    Refresh(key);
            }

            await _connection.InvokeAsync("RefreshKeys", keys);
        }

        public async void Refresh(TKey key)
        {
            _cache.Refresh(key);
            await _connection.InvokeAsync("RefreshKeys", new List<TKey>() { key });
        }



        public async void Remove(IEnumerable<TObject> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            if (items is IList<TObject> list)
            {
                //zero allocation enumerator
                var enumerable = EnumerableIList.Create(list);
                foreach (var item in enumerable)
                    Remove(item);
            }
            else
            {
                foreach (var item in items)
                    Remove(item);
            }
            await _connection.InvokeAsync("RemoveItems", items);

        }

        public async void Remove(TObject item)
        {
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            var key = _keySelector(item);
            _cache.Remove(key);
            await _connection.InvokeAsync("RemoveItem", item);
        }

        public async void Remove(IEnumerable<TKey> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            if (keys is IList<TKey> list)
            {
                //zero allocation enumerator
                var enumerable = EnumerableIList.Create(list);
                foreach (var key in enumerable)
                    Remove(key);
            }
            else
            {
                foreach (var key in keys)
                    Remove(key);
            }
            await _connection.InvokeAsync("RemoveKeys", keys);
        }

        public async void Remove(TKey key)
        {
            _cache.Remove(key);
            await _connection.InvokeAsync("RemoveKey", key);
        }

        public async void Remove(IEnumerable<KeyValuePair<TKey, TObject>> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            if (items is IList<TObject> list)
            {
                //zero allocation enumerator
                var enumerable = EnumerableIList.Create(list);
                foreach (var key in enumerable)
                    Remove(key);
            }
            else
            {
                foreach (var key in items)
                    Remove(key);
            }
            await _connection.InvokeAsync("RemoveKeyValuePairs", items);
        }

        public async void Remove(KeyValuePair<TKey, TObject> item)
        {
            Remove(item.Key);
            await _connection.InvokeAsync("RemoveKeyValuePair", item);
        }

        public void RemoveKey(TKey key)
        {
            Remove(key);
        }

        public void RemoveKeys(IEnumerable<TKey> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            Remove(keys);
        }

        public async void Update(IChangeSet<TObject, TKey> changes)
        {
            _cache.Clone(changes);

            var changesString = Newtonsoft.Json.JsonConvert.SerializeObject(changes, new ChangeSetConverter<TObject, TKey>());

            await _connection.InvokeAsync("Clone", changesString);
        }
    }
}
