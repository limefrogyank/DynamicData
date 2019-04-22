using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Kernel;
using Microsoft.JSInterop;

namespace DynamicData.SignalR.JSInterop
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class SignalRSourceCache<TObject, TKey> : ISignalRSourceCache<TObject, TKey>, ISignalRObservableCache<TObject, TKey>
    {
        private readonly SignalRObservableCacheBase<TObject, TKey> _innerCache;
        private readonly IJSRuntime _jsRuntime;
        private readonly string _baseUrl;

        public SignalRSourceCache(IJSRuntime jsRuntime, string baseUrl, Expression<Func<TObject, TKey>> keySelectorExpression, string accessToken = null)
        {
            _jsRuntime = jsRuntime;
            _baseUrl = baseUrl;
            if (keySelectorExpression == null) throw new ArgumentNullException(nameof(keySelectorExpression));
            _innerCache = new SignalRObservableCache<TObject, TKey>(jsRuntime, baseUrl, keySelectorExpression, accessToken);
            
        }

        // May need to delay initialization, will try after render method
       
        public IEnumerable<TKey> Keys => _innerCache.Keys;

        public IEnumerable<TObject> Items => _innerCache.Items;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _innerCache.KeyValues;

        public int Count => _innerCache.Count;

        public IObservable<int> CountChanged => _innerCache.CountChanged;

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null) => _innerCache.Connect(predicate);

        public IObservable<IChangeSet<TObject, TKey>> Connect(Expression<Func<TObject, bool>> predicateExpression) => _innerCache.Connect(predicateExpression);

        public void Dispose() => _innerCache.Dispose();

        public void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction) => _innerCache.UpdateFromSource(updateAction);

        public Task EditAsync(Action<ISourceUpdater<TObject, TKey>> updateAction) => _innerCache.UpdateFromSourceAsync(updateAction);

        public Optional<TObject> Lookup(TKey key) => _innerCache.Lookup(key);

        public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool> predicate = null) => _innerCache.Preview(predicate);

        public IObservable<Change<TObject, TKey>> Watch(TKey key) => _innerCache.Watch(key);


    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
