## You must use netcore v3 (preview) for this to work! ##

See the sample aspnetcore app for an example of how to setup SignalR and use the generic hub, `DynamicDataHub<TObject,TKey,TContext>`.  You must be using EntityFrameworkCore and some kind of database (any will do).  The sample uses Sqlite for convenience.


## The way SignalR works with DynamicData: ##
(I'm open to suggestions about all of this)

1.  DynamicData will cache the data that already exists on the server.  You can use a predicate to limit what is initially pulled down.
2.  Once your cache is established, any filters you apply will work on your cached data.
3.  When you make an edit to your `SignalRSourceCache`, that change is propagated through every connection on SignalR.  If you are using 'DynamicDataPredicateHub`, you can taylor who gets those messages.
4.  Initialization of SignalR is an asynchronous process... therefore I had to put some async code into DynamicData.SignalR.  You should use `EditAsync` and the other corresponding actions (i.e. `AddOrUpdateAsync`, etc).  The extensions are not all implemented yet.
5.  `Clear` or `ClearAsync` will WIPE OUT YOUR SERVER DATA!  However, it will only wipe out the data that was cached on your `SignalRSourceCache`.  If you use a predicate to `Connect` and limit the inital dataset that you cache, when you use `Clear`, it will only wipe out that data set.  
6.  You can't use `Func<TObject,string>` for a key selector!  This is because you can't serialize it.  You must write an `Expression` that can be serialized.  It will be compiled into a `Func<TObject,string>` later.  
7.  Primary Keys must be created by the client.  When you add an object to the `SignalRSourceCache`, it is always going to assume a Primary Key is already there.  You can't have EntityFrameworkCore create it for you and return it. 
8.  I am not at all sure how to allow the readonly `IObservableCache` (from .AsObservableCache) to get a `Connect` overload that allows `Expression` predicates (without major changes to DynamicData).   For now, I'm just going to have to work with exposed `SignalRSourceCache`s everywhere.  (They are still `ISourceCache` but can easily be casted back to the original class.)

## Create a SignalR Hub two ways

### `DynamicDataHub<TObject,TKey,TContext>`
Just put this into your `Startup.cs` where you map your SignalR hubs like so:
```
 app.UseSignalR(routes =>
 {
     routes.MapHub<DynamicData.SignalR.DynamicDataCacheHub<CustomModel, string, DatabaseContext>>("/CustomHub");
 });
```
`TObject` is your model, `TKey` is your cache key AND your database primary key, and `TContext` is your EntityFrameworkCore `DbContext` class.  That's all that's needed.

### `abstract DynamicDataPredicateHub<TObject,TKey,TContext>` 
This is the hub you'll need if you want to use Authentication of some kind.  You'll have to subclass it so that you can create the logic that determines what the user is allowed to access from the database.  SignalR creates a new Hub instance for each method call, so you have to store two variables in the `Context.Items` dictionary.

`Context.Items["GroupIdentifier"]` -> this is a string that will limit who gets `ChangeSet` messages.  The most likely scenario is to restrict those messages to a particular logged in user.  If that user has more than one `SignalRSourceCache` connecting to this hub, while they have different ConnectionIds, by setting the GroupIdentifier as an authenticated userId, only your `SignalRSourceCache`s will get those messages.

`Context.Items["WherePredicate"]` -> You need to tell the hub how to limit queries to the database for each user.  The most likely scenarios is one where your models have an `OwnerId` field.  You will want to limit all queries for a user to items that they own.

An example might look like this:
```
public class CourseHub : DynamicData.SignalR.DynamicDataPredicateHub<Course, string, DatabaseContext>
    {
        public CourseHub(DatabaseContext databaseContext): base(databaseContext)
        { }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            Context.Items["GroupIdentifier"] = userId;
            Context.Items["WherePredicate"] = (Func<Course,bool>)( (course) => course.OwnerId == userId);
                    
            await base.OnConnectedAsync();
        }

        string GetUserId()
        {
            string userId = "";
            if (Context.User.Identity.IsAuthenticated)
            {
                var issClaim = Context.User.Claims.FirstOrDefault(x => x.Type == "iss");
                if (issClaim != null)
                {
                    if (issClaim.Value.Contains("microsoftonline"))
                    {
                        var userIdClaim = Context.User.Claims.FirstOrDefault(x => x.Type == "http://schemas.microsoft.com/identity/claims/objectidentifier");
                        if (userIdClaim != null)
                            userId= userIdClaim.Value;
                    }

                }
            }
            return userId;
        }
    }
```


## Additional dependencies ##
In addition to the expected AspNetCore stuff, there is a dependency on a library called `Serialize.Linq`.   This is the library that serializes the `Expression`.  

I just discovered `System.Linq.Dynamic.Core` uses string based predicates... maybe that is a better way to go...? 
