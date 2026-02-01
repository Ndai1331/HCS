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
            }
        );

        // Register event handlers only if not already registered
        if (!connection._handlersRegistered) {
            this._registerEventHandlers(connection);
            connection._handlersRegistered = true;
        }

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
            console.log("Chat Hub: Calling HandleSignalRMessageJson for helper");
            
            // Only call HandleSignalRMessageJson if this is NOT the notification helper
            if (helper !== window._chatNotificationHelper) {
                await helper.invokeMethodAsync("HandleSignalRMessageJson", messageData)
                    .then(() => console.log("Chat Hub: HandleSignalRMessageJson call completed"))
                    .catch(err => {
                        console.error("Chat Hub: Error calling HandleSignalRMessageJson:", err);
                        // Disposal handled by baseHub
                    });
            }

            // Only call NotificationToast helper if this IS the notification helper
            if (window._chatNotificationHelper && helper === window._chatNotificationHelper) {
                console.log("Chat Hub: Calling OnChatMessageReceived for NotificationToast helper");
                const messageJson = JSON.stringify(messageData);
                await helper.invokeMethodAsync("OnChatMessageReceived", messageJson)
                    .then(() => console.log("Chat Hub: OnChatMessageReceived for NotificationToast completed"))
                    .catch(err => {
                        console.error("Chat Hub: Error calling OnChatMessageReceived for NotificationToast:", err);
                        if (err.message && err.message.includes("DotNetObjectReference instance was already disposed")) {
                            console.log("Chat Hub: Notification helper was disposed, cleaning up...");
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

        // Register ChatUnreadCountChanged handler - notifies all listeners when chat unread count changes
        window.baseHub.registerEventHandler("chat", "ChatUnreadCountChanged", async (helper) => {
            console.log("Chat Hub: ChatUnreadCountChanged event received");
            
            // Only call if this is a notification helper (for Notification.razor)
            if (helper === window._chatNotificationHelper || helper._isNotificationBarHelper) {
                await helper.invokeMethodAsync("OnChatUnreadCountChanged")
                    .then(() => console.log("Chat Hub: OnChatUnreadCountChanged completed"))
                    .catch(err => {
                        console.error("Chat Hub: Error calling OnChatUnreadCountChanged:", err);
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
                }
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
                }
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
