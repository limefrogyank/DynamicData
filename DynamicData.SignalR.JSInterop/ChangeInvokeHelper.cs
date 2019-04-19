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
            //var changeSet = Newtonsoft.Json.JsonConvert.DeserializeObject<ChangeSet<TObject, TKey>>(changeSetJson, new ChangeSetConverter<TObject, TKey>());
            _callback?.Invoke(changeSetJson);
        }
      
    }
}
