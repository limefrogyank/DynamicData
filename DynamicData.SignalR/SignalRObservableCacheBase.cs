using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.SignalR
{
    public abstract class SignalRObservableCacheBase<TObject, TKey> : IObservableCache<TObject, TKey>
    {

        public IEnumerable<TKey> Keys => _readerWriter.Keys;

        public IEnumerable<TObject> Items => _readerWriter.Items;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _readerWriter.KeyValues;

        public int Count => _readerWriter.Count;

        public IObservable<int> CountChanged => _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null)
        {
            if (predicate != null) throw new Exception("For ApiSourceCache, you can't have predicates in the connect method.  Use Expression<Func<TObject,bool>> overload instead.");

            return Observable.Defer<IChangeSet<TObject, TKey>>(async () =>
            {
                //lock (_locker)
                // var firstConnect = await _slocker.LockAsync(async () =>
                //{
                //    //var initial = await GetInitialUpdatesAsync(null);
                //    var changes = _changes;

                //    return changes.NotEmpty();
                //});

                //_slocker.Lock(() =>
                //{
                var task = GetInitialUpdatesAsync(null);
                //var changes = Observable.Return(initial).Concat(_changes);
                //return changes.NotEmpty();
                //});

                //await _slocker.LockAsync(() => { return null; });

                return _changes;
            });
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Expression<Func<TObject, bool>> predicateExpression = null)
        {

            return Observable.Defer(async () =>
            {
                var result = await _slocker.LockAsync(async () =>
                {
                    var initial = await GetInitialUpdatesAsync(predicateExpression);
                    var changes = Observable.Return(initial).Concat(_changes);

                    Func<TObject, bool> predicate = null;
                    if (predicateExpression != null)
                        predicate = predicateExpression.Compile();

                    return (predicateExpression == null ? changes : changes.Filter(predicate)).NotEmpty();
                });
                return result;
            });
        }

        public void Dispose() => _cleanUp.Dispose();

        public Optional<TObject> Lookup(TKey key) => _readerWriter.Lookup(key);

        public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool> predicate = null)
        {
            return predicate == null ? _changesPreview : _changesPreview.Filter(predicate);
        }

        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return Observable.Create<Change<TObject, TKey>>
            (
                observer =>
                {
                    //lock (_locker)
                    var result = _slocker.Lock(() =>
                    {
                        var initial = _readerWriter.Lookup(key);
                        if (initial.HasValue)
                            observer.OnNext(new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value));

                        return _changes.Finally(observer.OnCompleted).Subscribe(changes =>
                        {
                            foreach (var change in changes)
                            {
                                var match = EqualityComparer<TKey>.Default.Equals(change.Key, key);
                                if (match)
                                    observer.OnNext(change);
                            }
                        });
                    });
                    return result;
                });
        }
    }
}
