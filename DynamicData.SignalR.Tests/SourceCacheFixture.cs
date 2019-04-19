using System;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.SignalR.TestModel;
using DynamicData.Tests;
using FluentAssertions;
using Xunit;

namespace DynamicData.SignalR.Tests
{

    public class SourceCacheFixture : IDisposable
    {
        private readonly ChangeSetAggregator<Person, string> _results;
        private readonly SignalRSourceCache<Person, string> _source;

        public SourceCacheFixture()
        {
            var param = Expression.Parameter(typeof(Person), "person");
            var body = Expression.PropertyOrField(param, "Id");
            var lambda = Expression.Lambda<Func<Person, string>>(body, param);

            _source = new SignalRSourceCache<Person, string>("https://localhost:44304/TestHub", lambda);
            _results = _source.Connect().AsAggregator();
        }

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Fact]
        public async void CanHandleABatchOfUpdates()
        {
            //Thread.Sleep(2000);

            await _source.EditAsync(updater =>
            {
                var torequery = new Person("Adult1", 44);

                updater.AddOrUpdate(torequery);
                torequery.Age = 41;
                updater.AddOrUpdate(torequery);
                torequery.Age = 42;
                updater.AddOrUpdate(torequery);
                torequery.Age = 43;
                updater.AddOrUpdate(torequery);
                updater.Refresh(torequery);
                updater.Remove(torequery);
            });

            //await Task.Delay(2000);  //changes come in asynchronously now... need to wait...

            _results.Summary.Overall.Count.Should().Be(6, "Should be  6 updates");
            _results.Messages.Count.Should().Be(2, "Should be 2 messages (1st is initial load, 2nd is edit)");
            _results.Messages[1].Adds.Should().Be(1, "Should be 1 add");
            _results.Messages[1].Updates.Should().Be(3, "Should be 3 updates");
            _results.Messages[1].Removes.Should().Be(1, "Should be  1 remove");
            _results.Messages[1].Refreshes.Should().Be(1, "Should be 1 refresh");

            _results.Data.Count.Should().Be(0, "Should be 0 items in the cache");
        }

        [Fact]
        public void CountChangedShouldAlwaysInvokeUponeSubscription()
        {
            int? result = null;
            var subscription = _source.CountChanged.Subscribe(count => result = count);

            result.HasValue.Should().BeTrue();
            result.Value.Should().Be(0, "Count should be zero");

            subscription.Dispose();
        }

        [Fact]
        public async void CountChangedShouldReflectContentsOfCacheInvokeUponSubscription()
        {
            var generator = new RandomPersonGenerator();
            int? result = null;
            var subscription = _source.CountChanged.Subscribe(count => result = count);

            await _source.AddOrUpdateAsync(generator.Take(100));

            result.HasValue.Should().BeTrue();
            result.Value.Should().Be(100, "Count should be 100");

            await _source.ClearAsync();
            result.Value.Should().Be(0, "Count should be 0");
            
            subscription.Dispose();
        }

        [Fact]
        public async void SubscribesDisposesCorrectly()
        {
            bool called = false;
            bool errored = false;
            bool completed = false;
            var subscription = _source.Connect()
                .Finally(() => completed = true)
                .Subscribe(updates => { called = true; }, ex => errored = true, () => completed = true);
            await _source.AddOrUpdateAsync(new Person("Adult1", 40));

            await _source.ClearAsync();

            subscription.Dispose();
            _source.Dispose();

            errored.Should().BeFalse();
            called.Should().BeTrue();
            completed.Should().BeTrue();
            
        }

        [Fact]
        public async void CountChanged()
        {
            int count = 0;
            int invoked = 0;
            using (_source.CountChanged.Subscribe(c =>
                        {
                            count = c;
                            invoked++;
                        }))
            {
                invoked.Should().Be(1);
                count.Should().Be(0);

                var persons = new RandomPersonGenerator().Take(100);
                await _source.AddOrUpdateAsync(persons);
                            
                invoked.Should().Be(2);
                count.Should().Be(100);

                await _source.ClearAsync();
                
                invoked.Should().Be(3);
                count.Should().Be(0);
            }
        }
    }
}
