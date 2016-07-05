using System;
using System.Collections;
using System.Collections.Generic;

namespace DynamicData
{
    internal class VirtualChangeSet<T> : IVirtualChangeSet<T>
    {
        private readonly IChangeSet<T> _virtualChangeSet;

        public VirtualChangeSet(IChangeSet<T> virtualChangeSet, IVirtualResponse response)
        {
            if (virtualChangeSet == null) throw new ArgumentNullException(nameof(virtualChangeSet));
            if (response == null) throw new ArgumentNullException(nameof(response));
            _virtualChangeSet = virtualChangeSet;

            Response = response;
        }

        public IVirtualResponse Response { get; }

        #region Delegating members


        int IChangeSet.Adds => _virtualChangeSet.Adds;

        int IChangeSet.Removes => _virtualChangeSet.Removes;

        int IChangeSet.Moves => _virtualChangeSet.Moves;

        int IChangeSet.Count => _virtualChangeSet.Count;

        int IChangeSet.Capacity
        {
            get { return _virtualChangeSet.Capacity; }
            set { _virtualChangeSet.Capacity = value; }
        }

        int IChangeSet<T>.Replaced => _virtualChangeSet.Replaced;

        int IChangeSet<T>.TotalChanges => _virtualChangeSet.TotalChanges;

        #endregion

        public IEnumerator<Change<T>> GetEnumerator()
        {
            return _virtualChangeSet.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }
}