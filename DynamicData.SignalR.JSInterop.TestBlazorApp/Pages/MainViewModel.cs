using DynamicData.Binding;
using DynamicData.SignalR.TestModel;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DynamicData.SignalR.JSInterop.TestBlazorApp.Pages
{
    public class MainViewModel
    {
        private readonly IJSRuntime jsRuntime;

        private DynamicData.SignalR.JSInterop.SignalRSourceCache<Person, string> firstCache;

        private ObservableCollectionExtended<Person> people = new ObservableCollectionExtended<Person>();
        public IObservableCollection<Person> People => people;

        public MainViewModel(IJSRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime;

            var param = Expression.Parameter(typeof(Person), "person");
            var body = Expression.PropertyOrField(param, "Id");
            var lambda = Expression.Lambda<Func<Person, string>>(body, param);
            firstCache = new SignalRSourceCache<Person, string>(jsRuntime, "/TestHub", lambda);
                        
            firstCache.Connect()
                .Bind(people)
                .Subscribe();
        }

       
    }
}
