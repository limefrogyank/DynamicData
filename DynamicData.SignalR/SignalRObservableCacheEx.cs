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
        public static Task AddOrUpdateAsync<TObject, TKey>(this SignalRSourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.EditAsync(updater => updater.AddOrUpdate(items));
        }

        public static Task AddOrUpdateAsync<TObject, TKey>(this SignalRSourceCache<TObject, TKey> source, TObject item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.EditAsync(updater => updater.AddOrUpdate(item));
        }

        public static Task ClearAsync<TObject, TKey>(this SignalRSourceCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.EditAsync(updater => updater.Clear());
        }
    }
}