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
    internal sealed class SignalRReaderWriter<TObject, TKey> : SignalRReaderWriterBase<TObject,TKey>
    {

        //private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private HubConnection _connection;
        private readonly SemaphoreLocker _slocker = new SemaphoreLocker();
        //private readonly object _locker = new object();

        public SignalRReaderWriter(HubConnection connection, Expression<Func<TObject, TKey>> keySelectorExpression = null)
            : base(keySelectorExpression)
        {
            _connection = connection;

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

        

       
        protected override  ChangeSet<TObject, TKey> DoUpdate(Action<SignalRRemoteUpdaterBase<TObject, TKey>> updateAction, Action<ChangeSet<TObject, TKey>> previewHandler, bool collectChanges)
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

        
        #region Accessors

        public override async Task<ChangeSet<TObject, TKey>> GetInitialUpdates(Expression<Func<TObject, bool>> filterExpression = null)
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
