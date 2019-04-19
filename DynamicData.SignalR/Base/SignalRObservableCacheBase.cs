using DynamicData.Kernel;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.SignalR
{
    public abstract class SignalRObservableCacheBase<TObject, TKey> : IObservableCache<TObject, TKey>
    {
        protected readonly Subject<ChangeSet<TObject, TKey>> _changes = new Subject<ChangeSet<TObject, TKey>>();
        protected readonly Subject<ChangeSet<TObject, TKey>> _changesPreview = new Subject<ChangeSet<TObject, TKey>>();
        protected readonly Lazy<ISubject<int>> _countChanged = new Lazy<ISubject<int>>(() => new Subject<int>());
        protected readonly object _writeLock = new object();
        protected readonly SemaphoreLocker _slocker = new SemaphoreLocker();
        
        protected SignalRReaderWriterBase<TObject, TKey> _readerWriter;
        protected IDisposable _cleanUp;

        protected int _editLevel; // The level of recursion in editing.

        protected string _baseUrl;
        protected Task initializationTask;
        protected readonly Expression<Func<TObject, TKey>> _keySelectorExpression;

        public SignalRObservableCacheBase(string baseUrl, Expression<Func<TObject, TKey>> keySelectorExpression)
        {
            _baseUrl = baseUrl;
            _keySelectorExpression = keySelectorExpression;


        }

        public async void UpdateFromSource(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            if (initializationTask != null)
                await initializationTask;
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));
            lock (_writeLock)
            {
                ChangeSet<TObject, TKey> changes = null;

                _editLevel++;
                if (_editLevel == 1)
                {
                    var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
                    changes = _readerWriter.Write(updateAction, previewHandler, _changes.HasObservers);
                }
                else
                {
                    //var task = _readerWriter.Write(updateAction, null, _changes.HasObservers);
                    _readerWriter.WriteNested(updateAction);
                }
                _editLevel--;

                if (_editLevel == 0)
                {
                    InvokeNext(changes);
                }
            }
        }

        public async Task UpdateFromSourceAsync(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            if (initializationTask != null)
                await initializationTask;

            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));
            lock (_writeLock)
            {
                ChangeSet<TObject, TKey> changes = null;

                _editLevel++;
                if (_editLevel == 1)
                {
                    var previewHandler = _changesPreview.HasObservers ? (Action<ChangeSet<TObject, TKey>>)InvokePreview : null;
                    changes = _readerWriter.Write(updateAction, previewHandler, _changes.HasObservers);
                }
                else
                {
                    _readerWriter.WriteNested(updateAction);
                }
                _editLevel--;

                if (_editLevel == 0)
                {
                    InvokeNext(changes);
                }
            }
        }



        protected void InvokePreview(ChangeSet<TObject, TKey> changes)
        {
            _slocker.Lock(() =>
            {
                if (changes.Count != 0)
                    _changesPreview.OnNext(changes);
            });
        }

        protected void InvokeNext(ChangeSet<TObject, TKey> changes)
        {
            //lock (_locker)
            _slocker.Lock(() =>
            {
                if (changes.Count != 0)
                    _changes.OnNext(changes);

                if (_countChanged.IsValueCreated)
                    _countChanged.Value.OnNext(_readerWriter.Count);
            });
        }

        protected async Task<ChangeSet<TObject, TKey>> GetInitialUpdatesAsync(Expression<Func<TObject, bool>> filterExpression = null)
        {
            await initializationTask;
            return await _readerWriter.GetInitialUpdates(filterExpression);
        }

        public IEnumerable<TKey> Keys => _readerWriter.Keys;

        public IEnumerable<TObject> Items => _readerWriter.Items;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _readerWriter.KeyValues;

        public int Count => _readerWriter.Count;

        public IObservable<int> CountChanged => _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();

        public abstract IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null);

        public abstract IObservable<IChangeSet<TObject, TKey>> Connect(Expression<Func<TObject, bool>> predicateExpression = null);

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
