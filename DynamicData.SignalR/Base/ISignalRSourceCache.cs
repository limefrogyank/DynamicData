using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.SignalR
{
    public interface ISignalRSourceCache<TObject, TKey>: ISourceCache<TObject,TKey>
    {
        Task EditAsync(Action<ISourceUpdater<TObject, TKey>> updateAction);

    }
}
