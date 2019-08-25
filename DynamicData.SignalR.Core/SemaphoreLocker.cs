using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicData.SignalR.Core
{
    public class SemaphoreLocker
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public void Lock(Action action)
        {
            _semaphore.Wait();
            try
            {
                action();
            }
            finally
            {
                _semaphore.Release();
            }
        }


        public T Lock<T>(Func<T> worker)
        {
            T result = default;
            _semaphore.Wait();
            try
            {
                result = worker();
            }
            finally
            {
                _semaphore.Release();
            }
            return result;
        }

        public async Task LockAsync(Func<Task> worker)
        {
            await _semaphore.WaitAsync();
            try
            {
                await worker();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<T> LockAsync<T>(Func<Task<T>> worker)
        {
            await _semaphore.WaitAsync();

            T result = default;
            try
            {
                result = await worker();
            }
            finally
            {
                _semaphore.Release();

            }
            return result;
        }

        //public async Task<ChangeSet<TObject, TKey>> LockAsync(Func<Task<ChangeSet<TObject, TKey>>> worker)
        //{
        //    await _semaphore.WaitAsync();

        //    ChangeSet<TObject, TKey> result = ChangeSet<TObject, TKey>.Empty;
        //    try
        //    {
        //        result = await worker();
        //    }
        //    finally
        //    {
        //        _semaphore.Release();

        //    }
        //    return result;
        //}

        //public async Task<IObservable<ChangeSet<TObject, TKey>>> LockAsync(Func<Task<IObservable<ChangeSet<TObject, TKey>>>> worker)
        //{
        //    await _semaphore.WaitAsync();

        //    IObservable<ChangeSet<TObject, TKey>> result = Observable.Empty<ChangeSet<TObject, TKey>>();
        //    try
        //    {
        //        result = await worker();
        //    }
        //    finally
        //    {
        //        _semaphore.Release();

        //    }
        //    return result;
        //}
    }
}
