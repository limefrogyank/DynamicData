using DynamicData.Kernel;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.SignalR
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    public abstract class SignalRSourceCacheBase<TObject, TKey> : ISourceCache<TObject, TKey>
    {
        abstract protected SignalRObservableCacheBase<TObject, TKey> InnerCache { get; set; }

        public IEnumerable<TKey> Keys => InnerCache.Keys;

        public IEnumerable<TObject> Items => InnerCache.Items;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => InnerCache.KeyValues;

        public int Count => InnerCache.Count;

        public IObservable<int> CountChanged => InnerCache.CountChanged;

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null) => InnerCache.Connect(predicate);

        public IObservable<IChangeSet<TObject, TKey>> ConnectWithPredicate(Expression<Func<TObject, bool>> predicateExpression = null) => InnerCache.Connect(predicateExpression);

        public void Dispose() => InnerCache.Dispose();

        public abstract void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction) => InnerCache.UpdateFromSource(updateAction);

        public abstract Task EditAsync(Action<ISourceUpdater<TObject, TKey>> updateAction) => InnerCache.UpdateFromSourceAsync(updateAction);

        public Optional<TObject> Lookup(TKey key) => InnerCache.Lookup(key);

        public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool> predicate = null) => InnerCache.Preview(predicate);

        public IObservable<Change<TObject, TKey>> Watch(TKey key) => InnerCache.Watch(key);

    }

}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
