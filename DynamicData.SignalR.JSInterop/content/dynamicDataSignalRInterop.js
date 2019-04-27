//function from https://stackoverflow.com/a/2117523/1938624
function uuidv4() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        var r = Math.random() * 16 | 0, v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

window.dynamicDataSignalR = {

    connectionDict: {},

    createHubConnection: function (hubPath, accessToken) {
        console.log("Trying to connect to " + hubPath);
        var connection;
        if (accessToken !== null) {
            connection = new signalR.HubConnectionBuilder()
                .withUrl(hubPath, { accessTokenFactory: () => accessToken })
                //.withHubProtocol(new signalR.protocols.msgpack.MessagePackHubProtocol())
                .configureLogging(signalR.LogLevel.Trace)
                .build();
        } else {
            connection = new signalR.HubConnectionBuilder()
                .withUrl(hubPath)
                //.withHubProtocol(new signalR.protocols.msgpack.MessagePackHubProtocol())
                .configureLogging(signalR.LogLevel.Trace)
                .build();
        }
        var key = uuidv4();
        window.dynamicDataSignalR.connectionDict[key] = connection;
        return key;
    },

    connect: function (key, dotNetRef) {
        var connection = window.dynamicDataSignalR.connectionDict[key];

        connection.on("Changes", function (changesSerialized) {
            dotNetRef.invokeMethodAsync("OnChanges", changesSerialized);
        });

        connection.onclose((err) => {
            console.log("CLOSED!");
            console.log(JSON.stringify(err));
        });

        return connection.start().then(function () {

        }).catch(function (err) {
            console.log("SignalR error connecting: " + err);
        });

    },

    invoke: function (key, command, param1, param2, param3, param4, param5, param6) {
        var connection = window.dynamicDataSignalR.connectionDict[key];

        if (param6 !== undefined) {
            return connection.invoke(command, param1, param2, param3, param4, param5, param6);
        }
        if (param5 !== undefined) {
            return connection.invoke(command, param1, param2, param3, param4, param5);
        }
        if (param4 !== undefined) {
            return connection.invoke(command, param1, param2, param3, param4);
        }
        if (param3 !== undefined) {
            return connection.invoke(command, param1, param2, param3);
        }
        if (param2 !== undefined) {
            return connection.invoke(command, param1, param2);
        }
        if (param1 !== undefined) {
            return connection.invoke(command, param1);
        }
        return connection.invoke(command);



    }

};


