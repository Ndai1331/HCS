/**
 * Chat Hub Manager - Refactored to use baseHub.js
 * Handles real-time chat messaging with SignalR
 * 
 * Dependencies: baseHub.js
 */

window.chatHub = {
    /**
     * Initialize chat hub connection
     * @param {object} dotnetHelper - DotNetObjectReference for JS interop
     */
    start: function (dotnetHelper) {
        console.log("Chat Hub: Initializing...");

        const connection = window.baseHub.createOrReuseConnection(
            "/chatHub",                    // hubUrl
            "chat",                         // hubName
            dotnetHelper,                   // dotnetHelper
            {
                enableCrossTabSync: true,
                channelName: "chat-messages",
                onReceivingMessage: async (helper, messageData) => {
                    // Handle cross-tab messages
                    await helper.invokeMethodAsync("HandleCrossTabMessageJson", messageData);
                }
            },
            this._registerEventHandlers.bind(this)  // Register handlers BEFORE connection starts
        );

        window._chatConnection = connection;
        console.log("Chat Hub: Initialization complete");
    },

    /**
     * Register all SignalR event handlers
     * @private
     * @param {object} connection - The SignalR connection
     */
    _registerEventHandlers: function(connection) {
        console.log("Chat Hub: Registering event handlers...");

        // Register ReceiveMessage handler
        window.baseHub.registerEventHandler("chat", "ReceiveMessage", async (helper, messageData) => {
            // Determine helper type
            const isNotificationBarHelper = helper._isNotificationBarHelper === true;
            const isNotificationToastHelper = window._chatNotificationHelper && helper === window._chatNotificationHelper;
            const isChatHubServiceHelper = !isNotificationBarHelper && !isNotificationToastHelper;

            // Only call HandleSignalRMessageJson for ChatHubConnectionService (from Chat1.razor)
            if (isChatHubServiceHelper) {
                console.log("Chat Hub: Calling HandleSignalRMessageJson for ChatHubConnectionService helper");
                await helper.invokeMethodAsync("HandleSignalRMessageJson", messageData)
                    .then(() => console.log("Chat Hub: HandleSignalRMessageJson call completed"))
                    .catch(err => {
                        console.error("Chat Hub: Error calling HandleSignalRMessageJson:", err);
                    });
            }

            // Only call OnChatMessageReceived for NotificationToast helper
            if (isNotificationToastHelper) {
                console.log("Chat Hub: Calling OnChatMessageReceived for NotificationToast helper");
                const messageJson = JSON.stringify(messageData);
                await helper.invokeMethodAsync("OnChatMessageReceived", messageJson)
                    .then(() => console.log("Chat Hub: OnChatMessageReceived for NotificationToast completed"))
                    .catch(err => {
                        console.error("Chat Hub: Error calling OnChatMessageReceived for NotificationToast:", err);
                        if (err.message && err.message.includes("DotNetObjectReference instance was already disposed")) {
                            console.log("Chat Hub: NotificationToast helper was disposed, cleaning up...");
                            window._chatNotificationHelper = null;
                        }
                    });
            }

            // Broadcast message to other tabs for cross-tab sync
            window.baseHub.broadcastCrossTab("chat", "chat-message", messageData);
        });

        // Register MessageDeleted handler
        window.baseHub.registerEventHandler("chat", "MessageDeleted", async (helper, messageId) => {
            await helper.invokeMethodAsync("OnMessageDeleted", messageId)
                .catch(err => {
                    console.error("Chat Hub: Error calling OnMessageDeleted:", err);
                    // Disposal handled by baseHub
                });
        });

        // Register ConversationDeleted handler
        window.baseHub.registerEventHandler("chat", "ConversationDeleted", async (helper, userId) => {
            await helper.invokeMethodAsync("OnConversationDeleted", userId)
                .catch(err => {
                    console.error("Chat Hub: Error calling OnConversationDeleted:", err);
                    // Disposal handled by baseHub
                });
        });

        // Register ConversationCreated handler
        window.baseHub.registerEventHandler("chat", "ConversationCreated", async (helper, conversationData) => {
            console.log("Chat Hub: ConversationCreated event received", conversationData);
            
            // Only call OnConversationCreated if this is NOT the notification helper
            if (helper !== window._chatNotificationHelper) {
                await helper.invokeMethodAsync("OnConversationCreated", conversationData)
                    .catch(err => {
                        console.error("Chat Hub: Error calling OnConversationCreated:", err);
                        // Disposal handled by baseHub
                    });
            }
            
            // Only call NotificationToast helper if this IS the notification helper
            if (window._chatNotificationHelper && helper === window._chatNotificationHelper) {
                console.log("Chat Hub: Calling OnConversationCreated for NotificationToast helper");
                const conversationJson = JSON.stringify(conversationData);
                await helper.invokeMethodAsync("OnConversationCreated", conversationJson)
                    .then(() => console.log("Chat Hub: OnConversationCreated for NotificationToast completed"))
                    .catch(err => {
                        console.error("Chat Hub: Error calling OnConversationCreated for NotificationToast:", err);
                        if (err.message && err.message.includes("DotNetObjectReference instance was already disposed")) {
                            console.log("Chat Hub: Notification helper was disposed, cleaning up...");
                            window._chatNotificationHelper = null;
                        }
                    });
            }
        });

        // Register ChatUnreadCountChanged handler - notifies listeners when chat unread count changes
        window.baseHub.registerEventHandler("chat", "ChatUnreadCountChanged", async (helper) => {
            console.log("Chat Hub: ChatUnreadCountChanged event received");

            // Determine helper type
            const isNotificationBarHelper = helper._isNotificationBarHelper === true;
            const isNotificationToastHelper = window._chatNotificationHelper && helper === window._chatNotificationHelper;
            const isChatHubServiceHelper = !isNotificationBarHelper && !isNotificationToastHelper;

            // Call OnChatUnreadCountChanged for Notification.razor and ChatHubConnectionService
            // Skip NotificationToast helper as it doesn't need to handle unread count changes
            if (isNotificationBarHelper || isChatHubServiceHelper) {
                await helper.invokeMethodAsync("OnChatUnreadCountChanged")
                    .then(() => console.log("Chat Hub: OnChatUnreadCountChanged completed"))
                    .catch(err => {
                        console.error("Chat Hub: Error calling OnChatUnreadCountChanged:", err);
                        if (err.message && err.message.includes("DotNetObjectReference instance was already disposed")) {
                            console.log("Chat Hub: Helper was disposed, cleaning up...");
                            if (helper === window._chatNotificationHelper) {
                                window._chatNotificationHelper = null;
                            }
                        }
                    });
            }
        });

        console.log("Chat Hub: All event handlers registered");
    },

    /**
     * Initialize chat hub for NotificationToast component
     * @param {object} dotnetHelper - DotNetObjectReference for JS interop
     */
    startForNotifications: function (dotnetHelper) {
        console.log("Chat Hub: startForNotifications called for NotificationToast");

        // Store notification helper
        if (!window._chatNotificationHelper) {
            window._chatNotificationHelper = dotnetHelper;
            console.log("Chat Hub: Notification helper registered");
        }

        // Reuse existing connection if available
        if (!window._chatConnection) {
            console.log("Chat Hub: No existing connection, creating new one for notifications...");

            // Create connection with the notification helper
            const connection = window.baseHub.createOrReuseConnection(
                "/chatHub",
                "chat",
                dotnetHelper,
                {
                    enableCrossTabSync: false
                },
                this._registerEventHandlers.bind(this)  // Register handlers BEFORE start
            );

            window._chatConnection = connection;
            console.log("Chat Hub: Connection created for notifications");
        } else {
            console.log("Chat Hub: Reusing existing connection for notifications");
            // Add notification helper to existing connection's helper array
            const connection = window._chatConnection;

            // Only add if not already in array
            if (!connection._dotnetHelpers.includes(dotnetHelper)) {
                connection._dotnetHelpers.push(dotnetHelper);
                console.log("Chat Hub: Notification helper added to existing connection");
            }

            // Ensure handlers are registered on existing connection
            if (!connection._handlersRegistered) {
                console.log("Chat Hub: Registering handlers on existing connection for notifications");
                this._registerEventHandlers(connection);
                connection._handlersRegistered = true;
            }
        }
    },

    /**
     * Initialize chat hub for NotificationBar component (top bar with unread badge)
     * @param {object} dotnetHelper - DotNetObjectReference for JS interop
     */
    startForNotificationBar: function (dotnetHelper) {
        console.log("Chat Hub: startForNotificationBar called for Notification.razor");

        // Mark this helper as notification bar helper
        dotnetHelper._isNotificationBarHelper = true;

        // Reuse existing connection if available
        if (!window._chatConnection) {
            console.log("Chat Hub: No existing connection, creating new one for notification bar...");

            // Create connection with the notification bar helper
            const connection = window.baseHub.createOrReuseConnection(
                "/chatHub",
                "chat",
                dotnetHelper,
                {
                    enableCrossTabSync: false
                },
                this._registerEventHandlers.bind(this)  // Register handlers BEFORE start
            );

            window._chatConnection = connection;
            console.log("Chat Hub: Connection created for notification bar");
        } else {
            console.log("Chat Hub: Reusing existing connection for notification bar");
            // Add notification bar helper to existing connection's helper array
            const connection = window._chatConnection;

            // Only add if not already in array
            if (!connection._dotnetHelpers.includes(dotnetHelper)) {
                connection._dotnetHelpers.push(dotnetHelper);
                console.log("Chat Hub: Notification bar helper added to existing connection");
            }

            // Ensure handlers are registered on existing connection
            if (!connection._handlersRegistered) {
                console.log("Chat Hub: Registering handlers on existing connection for notification bar");
                this._registerEventHandlers(connection);
                connection._handlersRegistered = true;
            }
        }
    },

    /**
     * Cleanup notification helper reference
     */
    stopForNotifications: function () {
        console.log("Chat Hub: stopForNotifications called");
        
        // Clean up notification helper reference FIRST before disposing
        const helper = window._chatNotificationHelper;
        window._chatNotificationHelper = null;
        
        if (helper) {
            try {
                // Don't dispose here - let .NET handle disposal
                // Just remove reference so JS won't try to call it
                console.log("Chat Hub: Notification helper reference cleared");
            } catch (err) {
                console.error("Chat Hub: Error cleaning notification helper:", err);
            }
        }
        
        // Note: We don't stop the connection here because it might still be used by Chat1 page
        // The connection will be stopped when all helpers are removed
    },

    /**
     * Cleanup notification bar helper reference
     */
    stopForNotificationBar: function () {
        console.log("Chat Hub: stopForNotificationBar called");
        
        // Remove notification bar helper from connection
        if (window._chatConnection && window._chatConnection._dotnetHelpers) {
            window._chatConnection._dotnetHelpers = window._chatConnection._dotnetHelpers.filter(
                helper => !helper._isNotificationBarHelper
            );
            console.log("Chat Hub: Notification bar helper removed from connection");
        }
        
        // Note: We don't stop the connection here because it might still be used by other components
    },

    /**
     * Broadcast ChatUnreadCountChanged event locally
     * Called when user clicks on a conversation to reset unread count
     * This updates the notification icon without server roundtrip
     */
    broadcastUnreadCountChanged: function() {
        console.log("Chat Hub: Broadcasting ChatUnreadCountChanged locally...");

        if (!window._chatConnection || !window._chatConnection._dotnetHelpers) {
            console.warn("Chat Hub: No connection or helpers available for broadcast");
            return;
        }

        // Create a local event to update notification icons
        const helpers = [...window._chatConnection._dotnetHelpers];
        console.log(`Chat Hub: Broadcasting unread count changed to ${helpers.length} helpers`);

        helpers.forEach(async (helper, index) => {
            try {
                // Only call for Notification.razor helpers
                if (helper._isNotificationBarHelper === true) {
                    console.log(`Chat Hub: Calling OnChatUnreadCountChanged for helper ${index}`);
                    await helper.invokeMethodAsync("OnChatUnreadCountChanged")
                        .then(() => console.log("Chat Hub: OnChatUnreadCountChanged call completed"))
                        .catch(err => {
                            console.error("Chat Hub: Error calling OnChatUnreadCountChanged:", err);
                        });
                }
            } catch (err) {
                console.error(`Chat Hub: Error broadcasting to helper ${index}:`, err);
            }
        });
    },

    /**
     * Stop chat hub connection and cleanup resources
     */
    stop: function () {
        console.log("Chat Hub: Stopping...");
        
        // Cleanup notification helper
        if (window._chatNotificationHelper) {
            window._chatNotificationHelper = null;
        }

        // Stop connection via baseHub
        window.baseHub.stopConnection("chat");
        
        // Clear global reference
        window._chatConnection = null;
        
        console.log("Chat Hub: Stopped successfully");
    },

    /**
     * Get current connection status (for debugging)
     */
    getStatus: function() {
        return window.baseHub.getConnectionStatus("chat");
    }
};

// Log on load
console.log("Chat Hub module loaded successfully");
