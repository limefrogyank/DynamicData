using DynamicData.Binding;
using DynamicData.SignalR.TestModel;
using Microsoft.JSInterop;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace DynamicData.SignalR.JSInterop.TestBlazorApp.Pages
{
    public class MainViewModel : ReactiveObject
    {
        private readonly IJSRuntime jsRuntime;

        private DynamicData.SignalR.JSInterop.SignalRSourceCache<Person, string> firstCache;

        private ObservableCollectionExtended<Person> people = new ObservableCollectionExtended<Person>();
        private int selectedSort = 0;


        public IObservableCollection<Person> People => people;
        public int SelectedSort { get => selectedSort; set => this.RaiseAndSetIfChanged(ref selectedSort, value); }


        public MainViewModel(IJSRuntime jsRuntime)
        {
            this.jsRuntime = jsRuntime;

            var param = Expression.Parameter(typeof(Person), "person");
            var body = Expression.PropertyOrField(param, "Id");
            var lambda = Expression.Lambda<Func<Person, string>>(body, param);
            firstCache = new SignalRSourceCache<Person, string>(jsRuntime, "/TestHub", lambda);

            var sortObservable = this.WhenAnyValue(x => x.SelectedSort).Select(x =>
              {
                  if (x == 0)
                      return SortExpressionComparer<Person>.Ascending(person => person.Name);
                  else if (x == 1)
                      return SortExpressionComparer<Person>.Descending(person => person.Name);
                  else if (x == 2)
                      return SortExpressionComparer<Person>.Ascending(person => person.Age);
                  else if (x == 3)
                      return SortExpressionComparer<Person>.Descending(person => person.Age);
                  else
                      return SortExpressionComparer<Person>.Ascending(person => person.Name);
              });

            firstCache.Connect()
                .Sort(sortObservable)
                .Bind(people)
                .Subscribe();

          

        }

        public Task AddFiveRandom()
        {
            var generator = new RandomPersonGenerator();
            int? result = null;
            var subscription = firstCache.CountChanged.Subscribe(count => result = count);

            return firstCache.AddOrUpdateAsync(generator.Take(5));
        }

        public Task ClearAll()
        {            
            return firstCache.ClearAsync();
        }

    }
}
