using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DynamicData.Kernel;
using DynamicData.SignalR.Core;

namespace DynamicData.SignalR
{
    internal sealed class AnonymousSignalRObservableCache<TObject, TKey> : ISignalRObservableCache<TObject, TKey>
    {
        private readonly ISignalRObservableCache<TObject, TKey> _cache;
        
        public AnonymousSignalRObservableCache(ISignalRObservableCache<TObject, TKey> cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public IObservable<int> CountChanged => _cache.CountChanged;

        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return _cache.Watch(key);
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null)
        {
            return _cache.Connect(predicate);
        }

        public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool> predicate = null)
        {
            return _cache.Preview(predicate);
        }

        public IEnumerable<TKey> Keys => _cache.Keys;

        public IEnumerable<TObject> Items => _cache.Items;

        public int Count => _cache.Count;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

        public Optional<TObject> Lookup(TKey key)
        {
            return _cache.Lookup(key);
        }

        public void Dispose()
        {
            _cache.Dispose();
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Expression<Func<TObject, bool>> predicateExpression = null)
        {
            return _cache.Connect(predicateExpression);
        }
    }
}
