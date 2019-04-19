using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using Serialize.Linq.Serializers;

namespace DynamicData.SignalR
{
    public class SignalRRemoteUpdaterBase<TObject, TKey> : ISourceUpdater<TObject, TKey>
    {
        protected readonly ICache<TObject, TKey> _cache;
        protected readonly Expression<Func<TObject, TKey>> _keySelectorExpression;
        protected readonly Func<TObject, TKey> _keySelector;
        protected readonly string _selectorString;

        public SignalRRemoteUpdaterBase(ICache<TObject,TKey> cache, Expression<Func<TObject, TKey>> keySelectorExpression = null)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _keySelectorExpression = keySelectorExpression;

            _keySelector = _keySelectorExpression.Compile();
            var serializer = new ExpressionSerializer(new JsonSerializer());
            _selectorString = serializer.SerializeText(_keySelectorExpression);  //string version for serialization on SignalR
        }

        public SignalRRemoteUpdaterBase(Dictionary<TKey, TObject> data, Expression<Func<TObject, TKey>> keySelectorExpression = null)
        {
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

        public virtual void AddOrUpdate(IEnumerable<TObject> items)
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
           
        }

        public virtual void AddOrUpdate(TObject item)
        {
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            var key = _keySelector(item);
            _cache.AddOrUpdate(item, key);

        }

        public virtual void AddOrUpdate(TObject item, IEqualityComparer<TObject> comparer)
        {
            throw new NotImplementedException();
        }

        public virtual void AddOrUpdate(IEnumerable<KeyValuePair<TKey, TObject>> keyValuePairs)
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
        }

        public virtual void AddOrUpdate(KeyValuePair<TKey, TObject> item)
        {
            _cache.AddOrUpdate(item.Value, item.Key);
        }

        public virtual void AddOrUpdate(TObject item, TKey key)
        {
            _cache.AddOrUpdate(item, key);
        }

        public virtual void Clear()
        {          
            _cache.Clear();
        }

        public virtual void Clone(IChangeSet<TObject, TKey> changes)
        {
            _cache.Clone(changes);
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



        public void Refresh(IEnumerable<TObject> items)
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
        }

        public virtual void Refresh(TObject item)
        {
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            var key = _keySelector(item);
            _cache.Refresh(key);
        }

        public virtual void Refresh()
        {
            _cache.Refresh();
        }

        public virtual void Refresh(IEnumerable<TKey> keys)
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
        }

        public virtual void Refresh(TKey key)
        {
            _cache.Refresh(key);
        }



        public virtual void Remove(IEnumerable<TObject> items)
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
        }

        public virtual void Remove(TObject item)
        {
            if (_keySelector == null)
                throw new KeySelectorException("A key selector must be specified");

            var key = _keySelector(item);
            _cache.Remove(key);
        }

        public void Remove(IEnumerable<TKey> keys)
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
        }

        public virtual void Remove(TKey key)
        {
            _cache.Remove(key);
        }

        public void Remove(IEnumerable<KeyValuePair<TKey, TObject>> items)
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
        }

        public virtual void Remove(KeyValuePair<TKey, TObject> item)
        {
            Remove(item.Key);
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

        public virtual void Update(IChangeSet<TObject, TKey> changes)
        {
            _cache.Clone(changes);
        }
    }
}
