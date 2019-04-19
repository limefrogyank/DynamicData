using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace DynamicData.SignalR
{
    public interface ISignalRObservableCache<TObject,TKey> : IObservableCache<TObject, TKey>
    {
        IObservable<IChangeSet<TObject, TKey>> Connect(Expression<Func<TObject, bool>> predicateExpression = null);
    }
}
