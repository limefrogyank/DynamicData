using DynamicData.Annotations;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.SignalR
{
    [PublicAPI]
    public static class SignalRObservableCacheEx
    {

        public static ISignalRObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this ISignalRObservableCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new AnonymousSignalRObservableCache<TObject, TKey>(source);
        }

        public static Task AddOrUpdateAsync<TObject, TKey>(this ISignalRSourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.GetType().Name.StartsWith("SignalR")) throw new NotSupportedException("Async calls can only be made with SignalRSourceCache.");
            return (source as ISignalRSourceCache<TObject, TKey>).EditAsync(updater => updater.AddOrUpdate(items));
        }

        public static Task AddOrUpdateAsync<TObject, TKey>(this ISignalRSourceCache<TObject, TKey> source, TObject item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.GetType().Name.StartsWith("SignalRSourceCache")) throw new NotSupportedException("Async calls can only be made with SignalRSourceCache.");
            return (source as ISignalRSourceCache<TObject, TKey>).EditAsync(updater => updater.AddOrUpdate(item));
        }

        public static Task RemoveAsync<TObject, TKey>(this ISignalRSourceCache<TObject, TKey> source, TObject item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.GetType().Name.StartsWith("SignalRSourceCache")) throw new NotSupportedException("Async calls can only be made with SignalRSourceCache.");
            return (source as ISignalRSourceCache<TObject, TKey>).EditAsync(updater => updater.Remove(item));
        }

        public static Task RemoveAsync<TObject, TKey>(this ISignalRSourceCache<TObject, TKey> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.GetType().Name.StartsWith("SignalRSourceCache")) throw new NotSupportedException("Async calls can only be made with SignalRSourceCache.");
            return (source as ISignalRSourceCache<TObject, TKey>).EditAsync(updater => updater.Remove(key));
        }

        public static Task ClearAsync<TObject, TKey>(this ISignalRSourceCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.GetType().Name.StartsWith("SignalRSourceCache")) throw new NotSupportedException("Async calls can only be made with SignalRSourceCache.");
            return (source as ISignalRSourceCache<TObject, TKey>).EditAsync(updater => updater.Clear());
        }
    }
}