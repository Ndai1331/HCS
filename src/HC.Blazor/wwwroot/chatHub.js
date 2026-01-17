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

            // Call Chat1 page helpers (ChatHubConnectionService)
            if (connection._dotnetHelpers) {
                // Create a copy to iterate over, in case we need to remove disposed helpers
                const helpers = [...connection._dotnetHelpers];
                helpers.forEach((helper, index) => {
                    console.log(`Chat SignalR: Helper ${index}:`, helper);
                    if (helper) {
                        // Call method in ChatHubConnectionService using JsonElement-based method
                        console.log("Chat SignalR: Calling HandleSignalRMessageJson for helper");
                        helper.invokeMethodAsync("HandleSignalRMessageJson", messageData)
                            .then(() => console.log("Chat SignalR: HandleSignalRMessageJson call completed"))
                            .catch(err => {
                                console.error("Error calling HandleSignalRMessageJson:", err);
                                // If error is about disposed object, remove this helper from array
                                if (err.message && err.message.includes("DotNetObjectReference instance was already disposed")) {
                                    console.log("Chat SignalR: Removing disposed helper from array");
                                    const helperIndex = connection._dotnetHelpers.indexOf(helper);
                                    if (helperIndex > -1) {
                                        connection._dotnetHelpers.splice(helperIndex, 1);
                                    }
                                }
                            });
                    } else {
                        console.log("Chat SignalR: Helper is null");
                    }
                });
            } else {
                console.log("Chat SignalR: No dotnetHelpers available");
            }

            // Also call NotificationToast helper if registered
            if (window._chatNotificationHelper) {
                console.log("Chat SignalR: Also calling NotificationToast helper");
                const messageJson = JSON.stringify(messageData);
                window._chatNotificationHelper.invokeMethodAsync("OnChatMessageReceived", messageJson)
                    .then(() => console.log("Chat SignalR: OnChatMessageReceived for NotificationToast completed"))
                    .catch(err => console.error("Chat SignalR: Error calling OnChatMessageReceived for NotificationToast:", err));
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
                const helpers = [...connection._dotnetHelpers];
                helpers.forEach(helper => {
                    if (helper) {
                        helper.invokeMethodAsync("OnMessageDeleted", messageId)
                            .catch(err => {
                                console.error("Error calling OnMessageDeleted:", err);
                                if (err.message && err.message.includes("DotNetObjectReference instance was already disposed")) {
                                    const helperIndex = connection._dotnetHelpers.indexOf(helper);
                                    if (helperIndex > -1) {
                                        connection._dotnetHelpers.splice(helperIndex, 1);
                                    }
                                }
                            });
                    }
                });
            }
        });

        connection.on("ConversationDeleted", function (userId) {
            if (connection._dotnetHelpers) {
                const helpers = [...connection._dotnetHelpers];
                helpers.forEach(helper => {
                    if (helper) {
                        helper.invokeMethodAsync("OnConversationDeleted", userId)
                            .catch(err => {
                                console.error("Error calling OnConversationDeleted:", err);
                                if (err.message && err.message.includes("DotNetObjectReference instance was already disposed")) {
                                    const helperIndex = connection._dotnetHelpers.indexOf(helper);
                                    if (helperIndex > -1) {
                                        connection._dotnetHelpers.splice(helperIndex, 1);
                                    }
                                }
                            });
                    }
                });
            }
        });

        connection.on("ConversationCreated", function (conversationData) {
            console.log("Chat SignalR: ConversationCreated event received", conversationData);
            
            // Call Chat1 page helpers (ChatHubConnectionService)
            if (connection._dotnetHelpers) {
                const helpers = [...connection._dotnetHelpers];
                helpers.forEach(helper => {
                    if (helper) {
                        helper.invokeMethodAsync("OnConversationCreated", conversationData)
                            .catch(err => {
                                console.error("Error calling OnConversationCreated for Chat1:", err);
                                if (err.message && err.message.includes("DotNetObjectReference instance was already disposed")) {
                                    const helperIndex = connection._dotnetHelpers.indexOf(helper);
                                    if (helperIndex > -1) {
                                        connection._dotnetHelpers.splice(helperIndex, 1);
                                    }
                                }
                            });
                    }
                });
            }
            
            // Also call NotificationToast helper if registered
            if (window._chatNotificationHelper) {
                console.log("Chat SignalR: Also calling NotificationToast helper for ConversationCreated");
                const conversationJson = JSON.stringify(conversationData);
                window._chatNotificationHelper.invokeMethodAsync("OnConversationCreated", conversationJson)
                    .then(() => console.log("Chat SignalR: OnConversationCreated for NotificationToast completed"))
                    .catch(err => console.error("Chat SignalR: Error calling OnConversationCreated for NotificationToast:", err));
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

    startForNotifications: function (dotnetHelper) {
        console.log("Chat SignalR: startForNotifications called for NotificationToast");
        
        // Store notification helper
        if (!window._chatNotificationHelper) {
            window._chatNotificationHelper = dotnetHelper;
            console.log("Chat SignalR: Notification helper registered");
        }

        // Reuse existing connection if available
        if (!window._chatConnection) {
            console.log("Chat SignalR: No existing connection, creating new one for notifications...");
            
            const connection = new signalR.HubConnectionBuilder()
                .withUrl("/chatHub")
                .withAutomaticReconnect()
                .build();

            // Register ReceiveMessage handler
            connection.on("ReceiveMessage", function (messageData) {
                console.log("Chat SignalR: ReceiveMessage received", messageData);
                
                // Call NotificationToast's OnChatMessageReceived
                if (window._chatNotificationHelper) {
                    const messageJson = JSON.stringify(messageData);
                    window._chatNotificationHelper.invokeMethodAsync("OnChatMessageReceived", messageJson)
                        .then(() => console.log("Chat SignalR: OnChatMessageReceived called successfully"))
                        .catch(err => console.error("Chat SignalR: Error calling OnChatMessageReceived:", err));
                }
            });

            // Register ConversationCreated handler
            connection.on("ConversationCreated", function (conversationData) {
                console.log("Chat SignalR: ConversationCreated received", conversationData);
                
                if (window._chatNotificationHelper) {
                    const conversationJson = JSON.stringify(conversationData);
                    window._chatNotificationHelper.invokeMethodAsync("OnConversationCreated", conversationJson)
                        .then(() => console.log("Chat SignalR: OnConversationCreated called successfully"))
                        .catch(err => console.error("Chat SignalR: Error calling OnConversationCreated:", err));
                }
            });

            connection.start()
                .then(() => {
                    console.log("Chat SignalR: Connection started for notifications");
                })
                .catch(err => {
                    console.error("Chat SignalR: Connection error for notifications:", err);
                });

            window._chatConnection = connection;
        } else {
            console.log("Chat SignalR: Reusing existing connection for notifications");
            // Connection already exists, notification helper is already registered
            // The main ReceiveMessage handler in start() will handle chat messages for Chat1 page
            // We don't need to add duplicate handlers here
            // The NotificationToast will use the existing connection through the main handlers
        }
    },

    stopForNotifications: function () {
        console.log("Chat SignalR: stopForNotifications called");
        
        if (window._chatNotificationHelper) {
            // Dispose notification helper
            if (window._chatNotificationHelper.dispose) {
                window._chatNotificationHelper.dispose();
            }
            window._chatNotificationHelper = null;
            console.log("Chat SignalR: Notification helper disposed");
        }
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