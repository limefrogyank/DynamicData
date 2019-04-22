using DynamicData.Kernel;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Text;

namespace DynamicData.SignalR.JSInterop
{
    public class ChangeInvokeHelper
    {
        Action<string> _callback;

        public ChangeInvokeHelper()
        {
            
        }

        public void Initialize(Action<string> callback)
        {
            _callback = callback;
        }

        [JSInvokable]
        public void OnChanges(string changeSetJson)
        {
            _callback?.Invoke(changeSetJson);
        }
      
    }
}
