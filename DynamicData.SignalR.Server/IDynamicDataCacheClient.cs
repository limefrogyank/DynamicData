using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DynamicData.SignalR.Server
{
    public interface IDynamicDataCacheClient<TObject, TKey> where TObject : class
    {

        //// Hub methods -> can't use strongly-types clients yet.
        //Task AddOrUpdateObjects(IEnumerable<TObject> items);
        //Task Clone(string changeSetString);
        //Dictionary<TKey, TObject> GetKeyValuePairs();
        //Task<Dictionary<TKey, TObject>> GetKeyValuePairsFiltered(string predicateFilterString);
        //void Initialize(string keySelectorString);
        //Task RefreshKeys(IEnumerable<TKey> keys);
        //Task RemoveItems(IEnumerable<TObject> items);
        //Task RemoveKeys(IEnumerable<TKey> keys);

        // Client methods
        Task Changes(string changeSetString);

    }
}