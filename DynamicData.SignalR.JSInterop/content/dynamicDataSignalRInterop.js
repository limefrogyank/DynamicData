window.dynamicDataSignalR = {

    connection : null,
    idToken : "",

    createHubConnection: function (hubPath, withAuthentication) {
        console.log("Trying to connect to " + hubPath);
        if (withAuthentication) {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(hubPath, { accessTokenFactory: () => this.idToken }).build();
        } else {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(hubPath).build();
        }
        return true;
    },

    connect: function (dotNetRef) {

        this.connection.on("Changes", function (changesSerialized) {
            dotNetRef.invokeMethodAsync("OnChanges", changesSerialized);
        });

        return this.connection.start().then(function () {

        }).catch(function (err) {
            console.log("SignalR error connecting: " + err);
        });

    },

    //MAKE THESE ASYNC RETURN !!
    invoke: function (command, param1, param2, param3, param4, param5, param6) {
        if (param6 !== undefined) {
            return this.connection.invoke(command, param1, param2, param3, param4, param5, param6);
        }
        if (param5 !== undefined) {
            return this.connection.invoke(command, param1, param2, param3, param4, param5);
        }
        if (param4 !== undefined) {
            return this.connection.invoke(command, param1, param2, param3, param4);
        }
        if (param3 !== undefined) {
            return this.connection.invoke(command, param1, param2, param3);
        }
        if (param2 !== undefined) {
            return this.connection.invoke(command, param1, param2);
        }
        if (param1 !== undefined) {
            return this.connection.invoke(command, param1);
        }
        return this.connection.invoke(command);



    }

};


