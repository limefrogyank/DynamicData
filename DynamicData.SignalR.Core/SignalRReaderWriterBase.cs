using DynamicData.Kernel;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.SignalR.Core
{
    public abstract class SignalRReaderWriterBase<TObject, TKey>
    {
        protected readonly Expression<Func<TObject, TKey>> _keySelectorExpression;
        protected Func<TObject, TKey> _keySelector;

        protected Dictionary<TKey, TObject> _data = new Dictionary<TKey, TObject>(); //could do with priming this on first time load
        protected SignalRRemoteUpdaterBase<TObject, TKey> _remoteUpdater;

        protected Subject<ChangeSet<TObject, TKey>> _onChanges;
        public IObservable<ChangeSet<TObject, TKey>> Changes => _onChanges.AsObservable();

        protected string baseUrl;

        protected string _selectorString;

        protected readonly SemaphoreLocker _slocker = new SemaphoreLocker();

        public SignalRReaderWriterBase(Expression<Func<TObject, TKey>> keySelectorExpression = null)
        {
            _keySelectorExpression = keySelectorExpression;
            _onChanges = new Subject<ChangeSet<TObject, TKey>>();
            _keySelector = _keySelectorExpression.Compile();
        }


        protected ChangeSet<TObject, TKey> ReplaceInstancesWithCachedInstances(ChangeSet<TObject, TKey> deserializedChanges)
        {
            var localChangeSet = new ChangeSet<TObject, TKey>();
            foreach (var change in deserializedChanges)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        //this should be updated by key, so no need to get the original instance
                        localChangeSet.Add(change);
                        break;
                    case ChangeReason.Update:
                        //need to get original old item
                        var originalInstance = _data[_keySelector.Invoke(change.Previous.Value)];
                        var localChange = new Change<TObject, TKey>(change.Reason, change.Key, change.Current, Optional.Some(originalInstance));
                        localChangeSet.Add(localChange);
                        break;
                    case ChangeReason.Remove:
                    case ChangeReason.Refresh:
                        originalInstance = _data[change.Key];
                        localChange = new Change<TObject, TKey>(change.Reason, change.Key, originalInstance);
                        localChangeSet.Add(localChange);
                        break;
                    case ChangeReason.Moved:
                        // not used in ObservableCache
                        break;
                }
            }
            return localChangeSet;
        }

        #region Writers

        public ChangeSet<TObject, TKey> Write(IChangeSet<TObject, TKey> changes, Action<ChangeSet<TObject, TKey>> previewHandler, bool collectChanges)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            return DoUpdate(updater => updater.Clone(changes), previewHandler, collectChanges);
        }

        public ChangeSet<TObject, TKey> Write(Action<ICacheUpdater<TObject, TKey>> updateAction, Action<ChangeSet<TObject, TKey>> previewHandler, bool collectChanges)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));

            return DoUpdate(updateAction, previewHandler, collectChanges);
        }

        public ChangeSet<TObject, TKey> Write(Action<ISourceUpdater<TObject, TKey>> updateAction, Action<ChangeSet<TObject, TKey>> previewHandler, bool collectChanges)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));

            return DoUpdate(updateAction, previewHandler, collectChanges);
        }

        protected abstract ChangeSet<TObject, TKey> DoUpdate(Action<SignalRRemoteUpdaterBase<TObject, TKey>> updateAction, Action<ChangeSet<TObject, TKey>> previewHandler, bool collectChanges);

        internal void WriteNested(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            //lock (_locker)
            _slocker.Lock(() =>
            {
                if (_remoteUpdater == null)
                {
                    throw new InvalidOperationException("WriteNested can only be used if another write is already in progress.");
                }
                updateAction(_remoteUpdater);
                //return connection.SendAsync("DoUpdate", updateAction, null, true);
            });
        }

        #endregion

        #region Accessors

        public abstract Task<ChangeSet<TObject, TKey>> GetInitialUpdates(Expression<Func<TObject, bool>> filterExpression = null);

        public TKey[] Keys
        {
            get
            {
                //lock (_locker)
                return _slocker.Lock(() =>
                {
                    TKey[] result = new TKey[_data.Count];
                    _data.Keys.CopyTo(result, 0);
                    return result;
                });
            }
        }

        public KeyValuePair<TKey, TObject>[] KeyValues
        {
            get
            {
                //lock (_locker)
                return _slocker.Lock(() =>
                {
                    KeyValuePair<TKey, TObject>[] result = new KeyValuePair<TKey, TObject>[_data.Count];
                    int i = 0;
                    foreach (var kvp in _data)
                    {
                        result[i] = kvp;
                        i++;
                    }

                    return result;
                });
            }
        }

        public TObject[] Items
        {
            get
            {
                //lock (_locker)
                return _slocker.Lock(() =>
                {
                    TObject[] result = new TObject[_data.Count];
                    _data.Values.CopyTo(result, 0);
                    return result;
                });
            }
        }

        public Optional<TObject> Lookup(TKey key)
        {
            //lock (_locker)
            return _slocker.Lock(() =>
            {
                return _data.Lookup(key);
            });
        }

        public int Count
        {
            get
            {
                return _slocker.Lock(() =>
                {
                    //(_locker)
                    return _data.Count;
                });
            }
        }

        #endregion
    }
}
