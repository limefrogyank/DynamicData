# THIS PACKAGE IS FOR BLAZOR! #  
It has only been tested on Blazor Server-side (formerly Razor Components, formerly Blazor Server-side again)

# DEMO
https://dynamicdatasignalrjsinteroptest.azurewebsites.net/

## Quirks unique to this package: ##
1.  The Blazor library is NOT imported into your Blazor app properly yet.  In particular, the javascript file is not loaded.  Even the awesome https://github.com/SQL-MisterMagoo/BlazorEmbedLibrary doesn't quite work in this case.  The javascript file doesn't load quickly enough.  You have to manually copy the embedded JS files into your Blazor app and statically link them in your \_Host.cshtml page.
2.  Instructions for use are the same as `DynamicData.SignalR` except that you must also provide an instance of `IJSRuntime` to each `SignalRSourceCache` you create.  It must be the _post-rendered_ version of `IJSRuntime`.  
3.  If you happen to use this on a client-side Blazor app in combination with `ReactiveUI`, use a hacked version that doesn't throw exceptions due to the project being dotnet standard-based.  The regular `ReactiveUI` nuget package works fine for server-side Blazor (since it's a dotnetcore app).

see https://github.com/limefrogyank/DynamicData/blob/master/DynamicData.SignalR/README.md for more information...