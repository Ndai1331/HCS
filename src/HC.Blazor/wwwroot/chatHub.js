window.chatHub = {
    _broadcastChannel: null,

    start: function (dotnetHelper) {
        // Prevent duplicate connections - check if connection already exists
        if (window._chatConnection) {
            console.log("Chat SignalR connection already exists, reusing...");
            // Store helper in array to support multiple components
            if (!window._chatConnection._dotnetHelpers) {
                window._chatConnection._dotnetHelpers = [];
            }
            // Check if this helper already exists
            if (!window._chatConnection._dotnetHelpers.includes(dotnetHelper)) {
                window._chatConnection._dotnetHelpers.push(dotnetHelper);
            }
            return;
        }

        // Initialize BroadcastChannel for cross-tab communication
        if (typeof BroadcastChannel !== 'undefined') {
            this._broadcastChannel = new BroadcastChannel('chat-messages');
            console.log("Chat SignalR: BroadcastChannel initialized for cross-tab sync");

            // Listen for messages from other tabs
            this._broadcastChannel.onmessage = (event) => {
                console.log("Chat SignalR: Received message from another tab:", event.data);
                if (event.data.type === 'chat-message') {
                    // Forward to all local helpers using HandleCrossTabMessage
                    if (window._chatConnection && window._chatConnection._dotnetHelpers) {
                        window._chatConnection._dotnetHelpers.forEach((helper, index) => {
                            if (helper) {
                                console.log(`Chat SignalR: Forwarding cross-tab message to helper ${index}`);
                                helper.invokeMethodAsync("HandleCrossTabMessageJson", event.data.messageData)
                                    .then(() => console.log("Chat SignalR: Cross-tab message forwarded successfully"))
                                    .catch(err => console.error("Error forwarding cross-tab message:", err));
                            }
                        });
                    }
                }
            };
        } else {
            console.warn("Chat SignalR: BroadcastChannel not supported, cross-tab sync disabled");
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/chatHub")
            .withAutomaticReconnect()
            .build();

        // Store dotnetHelper references in array to support multiple components
        connection._dotnetHelpers = [dotnetHelper];

        // Register event handlers only once
        connection.on("ReceiveMessage", function (messageData) {
            console.log("Chat SignalR: Received message", messageData);
            console.log("Chat SignalR: Available helpers:", connection._dotnetHelpers ? connection._dotnetHelpers.length : 0);

            if (connection._dotnetHelpers) {
                connection._dotnetHelpers.forEach((helper, index) => {
                    console.log(`Chat SignalR: Helper ${index}:`, helper);
                    if (helper) {
                        // Call method in ChatHubConnectionService using JsonElement-based method
                        console.log("Chat SignalR: Calling HandleSignalRMessageJson for helper");
                        helper.invokeMethodAsync("HandleSignalRMessageJson", messageData)
                            .then(() => console.log("Chat SignalR: HandleSignalRMessageJson call completed"))
                            .catch(err => {
                                console.error("Error calling HandleSignalRMessageJson:", err);
                            });
                    } else {
                        console.log("Chat SignalR: Helper is null");
                    }
                });
            } else {
                console.log("Chat SignalR: No dotnetHelpers available");
            }

            // Broadcast message to other tabs for cross-tab sync
            if (window.chatHub._broadcastChannel) {
                console.log("Chat SignalR: Broadcasting message to other tabs");
                window.chatHub._broadcastChannel.postMessage({
                    type: 'chat-message',
                    messageData: messageData,
                    timestamp: Date.now()
                });
            }
        });

        connection.on("MessageDeleted", function (messageId) {
            if (connection._dotnetHelpers) {
                connection._dotnetHelpers.forEach(helper => {
                    if (helper) {
                        helper.invokeMethodAsync("OnMessageDeleted", messageId)
                            .catch(err => console.error("Error calling OnMessageDeleted:", err));
                    }
                });
            }
        });

        connection.on("ConversationDeleted", function (userId) {
            if (connection._dotnetHelpers) {
                connection._dotnetHelpers.forEach(helper => {
                    if (helper) {
                        helper.invokeMethodAsync("OnConversationDeleted", userId)
                            .catch(err => console.error("Error calling OnConversationDeleted:", err));
                    }
                });
            }
        });

        connection.start()
            .then(() => {
                console.log("Chat SignalR connected successfully");
                console.log("Chat SignalR: Connection established, listening for messages...");
            })
            .catch(err => {
                console.error("Chat SignalR connection error:", err);
                console.error("Chat SignalR: Make sure the hub URL is correct and server is running");
            });

        window._chatConnection = connection;
    },

    stop: function () {
        if (window._chatConnection) {
            // Dispose all helpers
            if (window._chatConnection._dotnetHelpers) {
                window._chatConnection._dotnetHelpers.forEach(helper => {
                    if (helper && helper.dispose) {
                        helper.dispose();
                    }
                });
            }
            window._chatConnection.stop()
                .then(() => console.log("Chat SignalR disconnected"))
                .catch(err => console.error("Chat SignalR disconnect error:", err));
            window._chatConnection = null;
        }

        // Cleanup BroadcastChannel
        if (this._broadcastChannel) {
            this._broadcastChannel.close();
            this._broadcastChannel = null;
            console.log("Chat SignalR: BroadcastChannel cleaned up");
        }
    }
};