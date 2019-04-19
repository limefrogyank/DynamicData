THIS PACKAGE IS FOR BLAZOR!  It has only been tested on Blazor Server-side (formerly Razor Components, formerly Blazor Server-side again)

Quirks unique to this package:
1.  The Blazor library is imported into your Blazor app properly yet.  In particular, the javascript file is not imported properly.  Even the awesome https://github.com/SQL-MisterMagoo/BlazorEmbedLibrary doesn't quite work in this case.  The javascript file doesn't load quickly enough.  You have to manually copy the embedded JS files into your Blazor app and statically link them in your Index.cshtml page.


# Copied from `DynamicData.SignalR` #
You must use netcore v3 (preview) for this to work!

See the sample aspnetcore app for an example of how to setup SignalR and use the generic hub, `DynamicDataHub<TObject,TKey,TContext>`.  You must be using EntityFrameworkCore and some kind of database (any will do).  The sample uses Sqlite for convenience.

Authentication/Authorization is not implemented yet, but it will be.  It might be a little tricky.

(I'm open to suggestions about this next part)
The way SignalR works with DynamicData:

1.  DynamicData will cache the data that already exists on the server.  You can use a predicate to limit what is initially pulled down.
2.  Once your cache is established, any filters you apply will work on your cached data.
3.  When you make an edit to your `SignalRSourceCache`, that change is propagated through every connection on SignalR.  (Tricky part for Authorization.)
4.  Initialization of SignalR is an asynchronous process... therefore I had to put some async code into DynamicData.SignalR.  You should use `EditAsync` and the other corresponding actions (i.e. `AddOrUpdateAsync`, etc).  The extensions are not all implemented yet.
5.  `Clear` or `ClearAsync` will WIPE OUT YOUR SERVER DATA!  However, it will only wipe out the data that was cached on your `SignalRSourceCache`.  If you use a predicate to `Connect` and limit the inital dataset that you cache, when you use `Clear`, it will only wipe out that data set.  
6.  You can't use `Func<TObject,string>` for a key selector!  This is because you can't serialize it.  You must write an `Expression` that can be serialized.  It will be compiled into a `Func<TObject,string>` later.  
7.  Primary Keys must be created by the client.  When you add an object to the `SignalRSourceCache`, it is always going to assume a Primary Key is already there.  You can't have EntityFrameworkCore create it for you and return it. 
8.  I am not at all sure how to allow the readonly `IObservableCache` (from .AsObservableCache) to get a `Connect` overload that allows `Expression` predicates (without major changes to DynamicData).   For now, I'm just going to have to work with exposed `SignalRSourceCache`s everywhere.  (They are still `ISourceCache` but can easily be casted back to the original class.)

In addition to the expected AspNetCore stuff, there is a dependency on a library called `Serialize.Linq`.   This is the library that serializes the `Expression`.  
