## You must use netcore v4 (preview) for this to work! ##

See the sample aspnetcore app for an example of how to setup SignalR and use one of the two generic hubs listed below.  You must be using EntityFrameworkCore and some kind of database (any will do).  The sample uses Sqlite for convenience.

If you want to use this project in Blazor, see https://github.com/limefrogyank/DynamicData/tree/master/DynamicData.SignalR.JSInterop for a version that can communicate using the javascript version of SignalR.  

## Demo (using Blazor)

https://dynamicdatasignalrjsinteroptest.azurewebsites.net/

## The way SignalR works with DynamicData: ##
(I'm open to suggestions about all of this)

1.  DynamicData will cache the data that already exists on the server.  You can use a predicate to limit what is initially pulled down.  Or you can use server-side logic to limit access by user (see authentication).
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
### Customizing EntityFrameworkCore query even further
If you want to `Include` foreign key relationships into your regular class, you can do that by subclassing either of the two hubs above and adding some string array parameters to the `Context.Items` dictionary under the key `IncludeChain`.

For example, if your `DbContext` would be setup like this, where there is a one-to-many relationship between `Owner` and `Course` (i.e. one owner can have many courses):

```
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Course>()
        .HasOne(c => c.Owner)
        .WithMany(u => u.Courses)
        .HasForeignKey(c => c.OwnerId)
        .HasConstraintName("FK_Course_User")
        .IsRequired();
    base.OnModelCreating(modelBuilder); 
}
```
To get EntityFrameworkCore to actually include the `Owner` model in your `Course` query, you have to specifically add `.Include(course=>course.Owner)`.  Instead of rewriting the `DynamicDataHub`, just override `OnConnectedAsync` and add this:

```
Context.Items["IncludeChain"] = new List<string>() { "Owner" };
```

That's it.  You can add multiple string paths to the properties you want to include.

#### Quirks and things to avoid

While you can use child entities in your main entity for each cache, there are going to be problems if you actually try to add a new entity that contains a child entity (like a parent class) that already exists in the database.  Due to the generic nature of the Hub in this repo, there's no way to automatically tell it to update the child entities, rather then try to add them (along with the main one).  If your child entites are `null` when you add them, there's no problem.  But if you need them for some predicate logic, you should override the `AddOrUpdateAsync` method in your subclassed `DynamicDataPredicateHub` and tell EntityFrameworkCore to ignore the child objects.  The foreign keys themselves are fine, just not the objects they link to.

```
public override Task AddOrUpdateObjects(IEnumerable<AttendanceItem> items)
{
    //set child entities to unchanged
    foreach (var item in items)
    {
        if (item.RollCallParent != null)
            _dbContext.Entry(item.RollCallParent).State = EntityState.Unchanged;
        if (item.UserParent != null)
            _dbContext.Entry(item.UserParent).State = EntityState.Unchanged;
    }

    return base.AddOrUpdateObjects(items);
}
```

## Using the `SignalRSourceCache<TObject,TKey>`

For the most part, this is used very similarly to the standard `SourceCache<TObject,TKey>`.  
```
var param = Expression.Parameter(typeof(Course), "course");
var body = Expression.PropertyOrField(param, "Id");
var lambda = Expression.Lambda<Func<Course, string>>(body, param);
courseCache = new DynamicData.SignalR.SignalRSourceCache<Course, string>("/CourseHub", lambda);
```
**This immediately creates a connection asynchronously!**  This may be important when using the authentication overload...
```
courseCache = new DynamicData.SignalR.SignalRSourceCache<Course, string>("/CourseHub", lambda, authService.accessToken);
```
You *can* use the `AsObservableCache()` extension to create a read-only copy, but then you will lose the ability (for now) to `Connect()` with a predicate.  The current overload of using a `Func<TObject,bool>` will **not** work since it needs to be an `Expression` like the sample above.  An alternative might be to put the cache behind a service and only expose a custom Connect method.

Since the connection is created asynchronously, to be sure that you are ready to edit the `SignalRSourceCache`, you should use the `AddOrUpdateAsync`, `EditAsync`, etc extensions in `DynamicData.SignalR`.  You *might* be able to get away with the synchronous versions if there is a guaranteed signficant timespan between the cache creation and an edit.
```
courseCache.AddOrUpdateAsync(new Course()
{
    Id = Guid.NewGuid().ToString(),
    DisplayName = courseName,
    OwnerId = authService.UserId
});
```


## Additional dependencies ##
In addition to the expected AspNetCore stuff, there is a dependency on a library called `Serialize.Linq`.   This is the library that serializes the `Expression`.  

I just discovered `System.Linq.Dynamic.Core` uses string based predicates... maybe that is a better way to go...? 
