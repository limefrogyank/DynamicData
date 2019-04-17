using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using Microsoft.AspNetCore.SignalR.Client;
using Serialize.Linq.Serializers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Security;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicData.SignalR
{
    internal sealed class SignalRReaderWriter<TObject, TKey>
    {
        private readonly Expression<Func<TObject, TKey>> _keySelectorExpression;
        Func<TObject, TKey> _keySelector;
        private Dictionary<TKey, TObject> _data = new Dictionary<TKey, TObject>(); //could do with priming this on first time load
        private SignalRRemoteUpdater<TObject, TKey> _remoteUpdater;


        private Subject<ChangeSet<TObject, TKey>> _onChanges;
        public IObservable<ChangeSet<TObject, TKey>> Changes => _onChanges.AsObservable();

        string baseUrl;
        private HubConnection _connection;
        private string _selectorString;

        //private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly SemaphoreLocker _slocker = new SemaphoreLocker();
        //private readonly object _locker = new object();

        public SignalRReaderWriter(HubConnection connection, Expression<Func<TObject, TKey>> keySelectorExpression = null)
        {
            _keySelectorExpression = keySelectorExpression;
            _onChanges = new Subject<ChangeSet<TObject, TKey>>();

            _connection = connection;

            _keySelector = _keySelectorExpression.Compile();


            //setting contract resolver on ConnectionBuilder is throwing an exception... solve it later, just deserialize manually
            _connection.On("Changes", (string changeSetJson) =>
            {

                var changeSet = Newtonsoft.Json.JsonConvert.DeserializeObject<ChangeSet<TObject, TKey>>(changeSetJson, new ChangeSetConverter<TObject, TKey>());
                var localChangeSet = ReplaceInstancesWithCachedInstances(changeSet);
                foreach (var change in changeSet)
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                            _data.Add(change.Key, change.Current);
                            break;
                        case ChangeReason.Remove:
                            // Binding adaptor tries to remove by the object instance... which isn't the same since we just created it from deserialization
                            // Need to replace it with the item cached here using the key.
                            _data.Remove(change.Key);
                            break;
                        case ChangeReason.Update:
                            _data[change.Key] = change.Current;
                            break;
                    }

                }
                
                _onChanges.OnNext(localChangeSet);
            });


        }

        private ChangeSet<TObject,TKey> ReplaceInstancesWithCachedInstances(ChangeSet<TObject,TKey> deserializedChanges)
        {
            var localChangeSet = new ChangeSet<TObject,TKey>();
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

        private ChangeSet<TObject, TKey> DoUpdate(Action<SignalRRemoteUpdater<TObject, TKey>> updateAction, Action<ChangeSet<TObject, TKey>> previewHandler, bool collectChanges)
        {
            //lock (_locker)
            return _slocker.Lock(() =>
            {
                if (previewHandler != null)
                {
                    var copy = new Dictionary<TKey, TObject>(_data);
                    var changeAwareCache = new ChangeAwareCache<TObject, TKey>(_data);

                    _remoteUpdater = new SignalRRemoteUpdater<TObject, TKey>(_connection, changeAwareCache, _keySelectorExpression);
                    updateAction(_remoteUpdater);

                    _remoteUpdater = null;

                    var changes = changeAwareCache.CaptureChanges();

                    InternalEx.Swap(ref copy, ref _data);
                    previewHandler(changes);
                    InternalEx.Swap(ref copy, ref _data);

                    return changes;
                }
                else
                {
                    if (collectChanges)
                    {
                        var changeAwareCache = new ChangeAwareCache<TObject, TKey>(_data);

                        _remoteUpdater = new SignalRRemoteUpdater<TObject, TKey>(_connection, changeAwareCache, _keySelectorExpression);
                        updateAction(_remoteUpdater);
                        Debug.Assert(_data.Count > 0);
                        _remoteUpdater = null;

                        return changeAwareCache.CaptureChanges();
                    }
                    else
                    {
                        _remoteUpdater = new SignalRRemoteUpdater<TObject, TKey>(_connection, _data, _keySelectorExpression);
                        updateAction(_remoteUpdater);
                        _remoteUpdater = null;

                        return ChangeSet<TObject, TKey>.Empty;
                    }
                }
            });
        }

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

        public async Task<ChangeSet<TObject, TKey>> GetInitialUpdates(Expression<Func<TObject, bool>> filterExpression = null)
        {
            //lock (_locker)
            var result = await _slocker.LockAsync(async () =>
            {
                Func<TObject, bool> filter = null;

                if (filterExpression == null)
                    _data = await _connection.InvokeAsync<Dictionary<TKey, TObject>>("GetKeyValuePairs");
                else
                {
                    filter = filterExpression.Compile();
                    var serializer = new ExpressionSerializer(new JsonSerializer());
                    var expressionString = serializer.SerializeText(filterExpression);
                    _data = await _connection.InvokeAsync<Dictionary<TKey, TObject>>("GetKeyValuePairsFiltered", expressionString);
                }

                var dictionary = _data;

                if (dictionary.Count == 0)
                    return ChangeSet<TObject, TKey>.Empty;

                var changes = filter == null
                    ? new ChangeSet<TObject, TKey>(dictionary.Count)
                    : new ChangeSet<TObject, TKey>();

                foreach (var kvp in dictionary)
                {
                    if (filter == null || filter(kvp.Value))
                        changes.Add(new Change<TObject, TKey>(ChangeReason.Add, kvp.Key, kvp.Value));
                }
                var converter = new DynamicData.SignalR.ChangeSetConverter<TObject, TKey>();
                var test = Newtonsoft.Json.JsonConvert.SerializeObject(changes, converter);
                Debug.WriteLine(test);
                return changes;

            });
            _onChanges.OnNext(result);
            return result;
        }

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
                return _slocker.Lock<int>(() =>
                {
                    //(_locker)
                    return _data.Count;
                });
            }
        }

        #endregion
    }
}
