using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.SignalR
{
    public interface ISourceCacheAsync<TObject, TKey>
    {
        Task EditAsync(Action<ISourceUpdater<TObject, TKey>> updateAction);

    }
}
