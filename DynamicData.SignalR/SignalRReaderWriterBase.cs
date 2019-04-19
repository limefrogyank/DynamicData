using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.SignalR
{
    public abstract class SignalRReaderWriterBase<TObject, TKey>
    {

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
