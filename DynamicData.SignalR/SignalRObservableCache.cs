﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Security;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Serialize.Linq.Serializers;

namespace DynamicData.SignalR
{
    internal sealed class SignalRObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    {
        private readonly Subject<ChangeSet<TObject, TKey>> _changes = new Subject<ChangeSet<TObject, TKey>>();
        private readonly Subject<ChangeSet<TObject, TKey>> _changesPreview = new Subject<ChangeSet<TObject, TKey>>();
        private readonly Lazy<ISubject<int>> _countChanged = new Lazy<ISubject<int>>(() => new Subject<int>());
        private readonly SignalRReaderWriter<TObject, TKey> _readerWriter;
        private readonly IDisposable _cleanUp;
        //private readonly object _locker = new object();
        private readonly object _writeLock = new object();

        private HubConnection _connection;        

        private readonly SemaphoreLocker _slocker = new SemaphoreLocker();

        private int _editLevel; // The level of recursion in editing.

        private string _baseUrl;
        private Task initializationTask;
        private readonly Expression<Func<TObject, TKey>> _keySelectorExpression;

        public SignalRObservableCache(string baseUrl, Expression<Func<TObject, TKey>> keySelectorExpression)
        {
            _baseUrl = baseUrl;
            _keySelectorExpression = keySelectorExpression;

           
            _connection = new HubConnectionBuilder()
                 .WithUrl($"{_baseUrl}", options =>
                 {
                 
                     options.HttpMessageHandlerFactory = (handler) =>
                     {
                         if (handler is HttpClientHandler clientHandler)
                         {
                             clientHandler.ServerCertificateCustomValidationCallback = ValidateCertificate;
                         }
                         return handler;
                     };
                 })
                 .Build();
            

            _readerWriter = new SignalRReaderWriter<TObject, TKey>(_connection, keySelectorExpression);

            var changeSubscription = _readerWriter.Changes.Subscribe((changeSet) =>
            {
                _changes.OnNext(changeSet);
            });

            _cleanUp = Disposable.Create(() =>
            {
                changeSubscription.Dispose();
                _changes.OnCompleted();
                _changesPreview.OnCompleted();
                if (_countChanged.IsValueCreated)
                {
                    _countChanged.Value.OnCompleted();
                }
            });


            _connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await _connection.StartAsync();
            };
            

            initializationTask = InitializeSignalR();
        }

        internal Task InitializeSignalR()
        {
            var task = _slocker.LockAsync(async () =>
            {
                await _connection.StartAsync();

                var serializer = new ExpressionSerializer(new JsonSerializer());
                var expressionString = serializer.SerializeText(_keySelectorExpression);

                await _connection.InvokeAsync("Initialize", expressionString);

                Debug.WriteLine("Connection initialized");
            });
            return task;
        }

        private bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // TODO: You can do custom validation here, or just return true to always accept the certificate.
            // DO NOT use custom validation logic in a production application as it is insecure.
            return true;
        }


        internal async void UpdateFromSource(Action<ISourceUpdater<TObject, TKey>> updateAction)
        {
            if (initializationTask!= null)
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

        internal async Task UpdateFromSourceAsync(Action<ISourceUpdater<TObject, TKey>> updateAction)
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

        private void InvokePreview(ChangeSet<TObject, TKey> changes)
        {
            _slocker.Lock(() =>
            {

                //lock (_locker)
                //{
                if (changes.Count != 0)
                    _changesPreview.OnNext(changes);
                
                //}
            });
        }

        private void InvokeNext(ChangeSet<TObject, TKey> changes)
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

        internal async Task<ChangeSet<TObject, TKey>> GetInitialUpdatesAsync(Expression<Func<TObject, bool>> filterExpression = null)
        {
            await initializationTask;
            return await _readerWriter.GetInitialUpdates(filterExpression);
        }

        public IEnumerable<TKey> Keys => _readerWriter.Keys;

        public IEnumerable<TObject> Items => _readerWriter.Items;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _readerWriter.KeyValues;

        public int Count => _readerWriter.Count;

        public IObservable<int> CountChanged => _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null)
        {
            if (predicate != null) throw new Exception("For ApiSourceCache, you can't have predicates in the connect method.  Use Expression<Func<TObject,bool>> overload instead.");

            return Observable.Defer<IChangeSet<TObject,TKey>>(async () =>
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
                    var initial =  await GetInitialUpdatesAsync(predicateExpression);
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
