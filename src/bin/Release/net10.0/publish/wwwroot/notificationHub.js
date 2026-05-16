/**
 * Notification Hub Manager - Refactored to use baseHub.js
 * Handles real-time notifications with SignalR
 * 
 * Dependencies: baseHub.js
 */

window.notificationHub = {
    /**
     * Initialize notification hub connection
     * @param {object} dotnetHelper - DotNetObjectReference for JS interop
     */
    start: function (dotnetHelper) {
        // console.log("Notification Hub: Initializing...");
        
        // Use baseHub to create or reuse connection
        const connection = window.baseHub.createOrReuseConnection(
            "/notificationHub",              // hubUrl
            "notification",                  // hubName
            dotnetHelper,                    // dotnetHelper
            {
                enableCrossTabSync: false    // No cross-tab sync needed for notifications
            }
        );

        // Register event handlers only if not already registered
        if (!connection._handlersRegistered) {
            this._registerEventHandlers(connection);
            connection._handlersRegistered = true;
        }

        window._notificationConnection = connection;
        // console.log("Notification Hub: Initialization complete");
    },

    /**
     * Register all SignalR event handlers
     * @private
     * @param {object} connection - The SignalR connection
     */
    _registerEventHandlers: function(connection) {
        // console.log("Notification Hub: Registering event handlers...");

        // Register ReceiveNotification handler
        window.baseHub.registerEventHandler("notification", "ReceiveNotification", async (helper, notificationId) => {
            await helper.invokeMethodAsync("OnNotificationReceived", notificationId)
                .catch(err => {
                    window.hcLogger.error("Notification Hub: Error calling OnNotificationReceived:", err);
                    // Disposal handled by baseHub
                });
        });

        // Register UnreadCountChanged handler
        window.baseHub.registerEventHandler("notification", "UnreadCountChanged", async (helper) => {
            await helper.invokeMethodAsync("OnUnreadCountChanged")
                .catch(err => {
                    window.hcLogger.error("Notification Hub: Error calling OnUnreadCountChanged:", err);
                    // Disposal handled by baseHub
                });
        });

        // console.log("Notification Hub: All event handlers registered");
    },

    /**
     * Stop notification hub connection and cleanup resources
     */
    stop: function () {
        // console.log("Notification Hub: Stopping...");
        
        // Clear helper references FIRST (don't dispose from JS, let .NET handle it)
        if (window._notificationConnection && window._notificationConnection._dotnetHelpers) {
            // console.log("Notification Hub: Clearing helper references...");
            window._notificationConnection._dotnetHelpers = [];
        }
        
        // Stop connection via baseHub
        window.baseHub.stopConnection("notification");
        
        // Clear global reference
        window._notificationConnection = null;
        
        // console.log("Notification Hub: Stopped successfully");
    },

    /**
     * Get current connection status (for debugging)
     */
    getStatus: function() {
        return window.baseHub.getConnectionStatus("notification");
    }
};

// Log on load
window.hcLogger.log("Notification Hub module loaded successfully");
