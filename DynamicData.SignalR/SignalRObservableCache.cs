using System;
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
using DynamicData.SignalR.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Serialize.Linq.Serializers;

namespace DynamicData.SignalR
{
    internal sealed class SignalRObservableCache<TObject, TKey> : SignalRObservableCacheBase<TObject,TKey>
    {
        private HubConnection _connection;        

       
        public SignalRObservableCache(string baseUrl, Expression<Func<TObject, TKey>> keySelectorExpression, string accessToken)
            :base(baseUrl, keySelectorExpression)
        {            
            _connection = new HubConnectionBuilder()

                 //.AddNewtonsoftJsonProtocol()
                 
                 .AddNewtonsoftJsonProtocol(options =>
                 {
                     options.PayloadSerializerSettings.PreserveReferencesHandling = Newtonsoft.Json.PreserveReferencesHandling.Objects;
                 })
                 .WithUrl($"{_baseUrl}", options =>
                 {
                     if (accessToken != null)
                         options.AccessTokenProvider = () => Task.FromResult(accessToken);
                 
                     //options.HttpMessageHandlerFactory = (handler) =>
                     //{
                     //    if (handler is HttpClientHandler clientHandler)
                     //    {
                     //        clientHandler.ServerCertificateCustomValidationCallback = ValidateCertificate;
                     //    }
                     //    return handler;
                     //};
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

        public override IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate = null)
        {
            if (predicate != null) throw new Exception("For ApiSourceCache, you can't have predicates in the connect method.  Use Expression<Func<TObject,bool>> overload instead.");

            return Observable.Defer<IChangeSet<TObject,TKey>>(async () =>
            {
                var task = GetInitialUpdatesAsync(null);
                
                return _changes;
            });
        }

        public override IObservable<IChangeSet<TObject, TKey>> Connect(Expression<Func<TObject, bool>> predicateExpression = null)
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

       
    }
}
