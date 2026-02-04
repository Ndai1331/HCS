/**
 * Base Hub Manager - Provides common SignalR connection management
 * Eliminates code duplication between chatHub and notificationHub
 * 
 * Features:
 * - Automatic reconnection
 * - Helper management (multiple components per hub)
 * - Disposal safety
 * - Error handling
 * - BroadcastChannel for cross-tab sync (optional)
 */

window.baseHub = {
    // Store all hub connections
    _connections: {},
    _broadcastChannels: {},

    /**
     * Create or reuse a SignalR connection
     * @param {string} hubUrl - The URL for the SignalR hub (e.g., "/chatHub")
     * @param {string} hubName - Unique name for this hub (e.g., "chat")
     * @param {object} dotnetHelper - DotNetObjectReference for JS interop
     * @param {object} options - Configuration options
     * @param {function} registerHandlersFn - Function to register event handlers (called before connection starts for new connections)
     * @returns {object} - The SignalR connection object
     */
    createOrReuseConnection: function(hubUrl, hubName, dotnetHelper, options = {}, registerHandlersFn = null) {
        const {
            enableCrossTabSync = false,
            channelName = null,
            onReceivingMessage = null,
            onConnectionLost = null,
            onReconnected = null
        } = options;

        // Reuse existing connection if available
        if (this._connections[hubName]) {
            window.hcLogger.log(`${hubName} Hub: Connection already exists, reusing...`);

            const connection = this._connections[hubName];

            // Initialize helpers array if not exists
            if (!connection._dotnetHelpers) {
                connection._dotnetHelpers = [];
            }

            // Add helper if not already in array
            if (!connection._dotnetHelpers.includes(dotnetHelper)) {
                connection._dotnetHelpers.push(dotnetHelper);
                window.hcLogger.log(`${hubName} Hub: Added new helper, total helpers: ${connection._dotnetHelpers.length}`);
            }

            return connection;
        }

        window.hcLogger.log(`${hubName} Hub: Creating new connection...`);

        // Initialize BroadcastChannel for cross-tab sync if enabled
        if (enableCrossTabSync && typeof BroadcastChannel !== 'undefined' && channelName) {
            this._broadcastChannels[hubName] = new BroadcastChannel(channelName);
            window.hcLogger.log(`${hubName} Hub: BroadcastChannel initialized for cross-tab sync`);

            // Listen for messages from other tabs
            this._broadcastChannels[hubName].onmessage = (event) => {
                window.hcLogger.log(`${hubName} Hub: Received message from another tab:`, event.data);

                if (event.data.type === 'hub-message' && onReceivingMessage) {
                    // Forward to all local helpers
                    const connection = this._connections[hubName];
                    if (connection && connection._dotnetHelpers) {
                        connection._dotnetHelpers.forEach((helper, index) => {
                            if (helper) {
                                window.hcLogger.log(`${hubName} Hub: Forwarding cross-tab message to helper ${index}`);
                                onReceivingMessage(helper, event.data.messageData)
                                    .catch(err => window.hcLogger.error(`${hubName} Hub: Error forwarding cross-tab message:`, err));
                            }
                        });
                    }
                }
            };
        } else if (enableCrossTabSync) {
            window.hcLogger.warn(`${hubName} Hub: BroadcastChannel not supported, cross-tab sync disabled`);
        }

        // Create new SignalR connection
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: (retryContext) => {
                    // Custom retry strategy: 0s, 2s, 10s, 30s, then 60s
                    if (retryContext.previousRetryCount === 0) {
                        return 0;
                    }
                    if (retryContext.previousRetryCount < 3) {
                        return 2000 * retryContext.previousRetryCount;
                    }
                    return 30000;
                }
            })
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Store helpers array
        connection._dotnetHelpers = [dotnetHelper];
        connection._hubName = hubName;
        connection._hubUrl = hubUrl;

        // IMPORTANT: Store connection in registry FIRST
        // This allows registerEventHandler to find it when called from registerHandlersFn
        this._connections[hubName] = connection;
        this._connections[hubName]._options = options;

        // IMPORTANT: Register event handlers BEFORE starting connection
        // This prevents race conditions where messages arrive before handlers are ready
        if (registerHandlersFn && typeof registerHandlersFn === 'function') {
            window.hcLogger.log(`${hubName} Hub: 🎯 Registering event handlers BEFORE connection starts...`);
            registerHandlersFn(connection);
            connection._handlersRegistered = true;
            window.hcLogger.log(`${hubName} Hub: ✅ Event handlers registered successfully`);
        } else {
            window.hcLogger.warn(`${hubName} Hub: ⚠️ No registerHandlersFn provided, handlers may not be registered!`);
        }

        // Connection lifecycle event handlers
        connection.onreconnecting(error => {
            window.hcLogger.log(`${hubName} Hub: Connection lost, reconnecting...`, error);
            if (onConnectionLost) {
                onConnectionLost(error);
            }
        });

        connection.onreconnected(connectionId => {
            window.hcLogger.log(`${hubName} Hub: Reconnected with connectionId: ${connectionId}`);
            if (onReconnected) {
                onReconnected(connectionId);
            }
        });

        connection.onclose(error => {
            window.hcLogger.log(`${hubName} Hub: Connection closed`, error);
        });

        // Start the connection AFTER handlers are registered
        window.hcLogger.log(`${hubName} Hub: 🚀 Starting connection...`);
        connection.start()
            .then(() => {
                window.hcLogger.log(`${hubName} Hub: ✅ Connected successfully`);
            })
            .catch(err => {
                window.hcLogger.error(`${hubName} Hub: ❌ Connection error:`, err);
                window.hcLogger.error(`${hubName} Hub: Make sure the hub URL is correct and server is running`);
            });

        return connection;
    },

    /**
     * Register an event handler for a specific SignalR event
     * @param {string} hubName - The hub name
     * @param {string} eventName - The SignalR event name
     * @param {function} handler - The handler function
     */
    registerEventHandler: function(hubName, eventName, handler) {
        const connection = this._connections[hubName];
        if (!connection) {
            window.hcLogger.error(`${hubName} Hub: Connection not found`);
            return;
        }

        window.hcLogger.log(`${hubName} Hub: 📝 Registering handler for event: ${eventName}`);
        connection.on(eventName, async (data) => {
            window.hcLogger.log(`${hubName} Hub: 🔔 Received event '${eventName}'`, data);

            if (connection._dotnetHelpers && connection._dotnetHelpers.length > 0) {
                // Create a copy to avoid modification during iteration
                const helpers = [...connection._dotnetHelpers];
                window.hcLogger.log(`${hubName} Hub: 👥 Processing event for ${helpers.length} helpers`);

                for (let i = 0; i < helpers.length; i++) {
                    const helper = helpers[i];

                    if (!helper) {
                        window.hcLogger.warn(`${hubName} Hub: ⚠️ Helper ${i} is null, skipping`);
                        continue;
                    }

                    try {
                        window.hcLogger.log(`${hubName} Hub: → Calling handler for helper ${i}`);
                        await handler(helper, data, i);
                    } catch (err) {
                        window.hcLogger.error(`${hubName} Hub: ❌ Error in ${eventName} handler for helper ${i}:`, err);

                        // Check if error is due to disposed helper
                        if (err.message &&
                            (err.message.includes("DotNetObjectReference instance was already disposed") ||
                             err.message.includes("does not contain a public invokable method"))) {

                            window.hcLogger.log(`${hubName} Hub: 🗑️ Helper ${i} is disposed or invalid, removing from array...`);

                            const helperIndex = connection._dotnetHelpers.indexOf(helper);
                            if (helperIndex > -1) {
                                connection._dotnetHelpers.splice(helperIndex, 1);
                                window.hcLogger.log(`${hubName} Hub: ✅ Removed disposed/invalid helper. Remaining: ${connection._dotnetHelpers.length}`);
                            }
                        }
                    }
                }

                if (connection._dotnetHelpers.length === 0) {
                    window.hcLogger.warn(`${hubName} Hub: ⚠️ No valid helpers remaining for event '${eventName}'`);
                }
            } else {
                window.hcLogger.log(`${hubName} Hub: ℹ️ No helpers available for event '${eventName}'`);
            }
        });
        window.hcLogger.log(`${hubName} Hub: ✅ Handler registered for '${eventName}'`);
    },

    /**
     * Handle disposed DotNetObjectReference helpers
     * @param {object} connection - The SignalR connection
     * @param {object} helper - The DotNetObjectReference helper
     * @param {Error} error - The error that occurred
     * @param {number} index - The helper index
     */
    handleDisposedHelper: function(connection, helper, error, index) {
        if (error.message && error.message.includes("DotNetObjectReference instance was already disposed")) {
            window.hcLogger.log(`${connection._hubName} Hub: Helper ${index} was disposed, removing from array...`);
            
            const helperIndex = connection._dotnetHelpers.indexOf(helper);
            if (helperIndex > -1) {
                connection._dotnetHelpers.splice(helperIndex, 1);
                window.hcLogger.log(`${connection._hubName} Hub: Removed disposed helper. Remaining: ${connection._dotnetHelpers.length}`);
            }
        }
    },

    /**
     * Broadcast message to other tabs for cross-tab sync
     * @param {string} hubName - The hub name
     * @param {string} messageType - The message type
     * @param {object} messageData - The message data to broadcast
     */
    broadcastCrossTab: function(hubName, messageType, messageData) {
        const broadcastChannel = this._broadcastChannels[hubName];
        if (broadcastChannel) {
            window.hcLogger.log(`${hubName} Hub: Broadcasting message to other tabs`);
            broadcastChannel.postMessage({
                type: messageType,
                messageData: messageData,
                timestamp: Date.now()
            });
        }
    },

    /**
     * Remove a specific helper from a hub connection
     * @param {string} hubName - The hub name
     * @param {object} dotnetHelper - The DotNetObjectReference to remove
     */
    removeHelper: function(hubName, dotnetHelper) {
        const connection = this._connections[hubName];
        if (!connection || !connection._dotnetHelpers) {
            return;
        }

        const helperIndex = connection._dotnetHelpers.indexOf(dotnetHelper);
        if (helperIndex > -1) {
            connection._dotnetHelpers.splice(helperIndex, 1);
            window.hcLogger.log(`${hubName} Hub: Helper removed. Remaining: ${connection._dotnetHelpers.length}`);
        }
    },

    /**
     * Stop and cleanup a specific hub connection
     * @param {string} hubName - The hub name to cleanup
     */
    stopConnection: function(hubName) {
        const connection = this._connections[hubName];
        if (!connection) {
            window.hcLogger.log(`${hubName} Hub: No connection to stop`);
            return;
        }

        window.hcLogger.log(`${hubName} Hub: Stopping connection...`);

        // Clear helper references FIRST (don't dispose from JS, let .NET handle it)
        if (connection._dotnetHelpers) {
            window.hcLogger.log(`${hubName} Hub: Clearing helper references...`);
            connection._dotnetHelpers = [];
        }

        // Stop connection
        connection.stop()
            .then(() => {
                window.hcLogger.log(`${hubName} Hub: Connection stopped successfully`);
            })
            .catch(err => {
                window.hcLogger.error(`${hubName} Hub: Error stopping connection:`, err);
            });

        // Cleanup BroadcastChannel
        if (this._broadcastChannels[hubName]) {
            this._broadcastChannels[hubName].close();
            this._broadcastChannels[hubName] = null;
            window.hcLogger.log(`${hubName} Hub: BroadcastChannel cleaned up`);
        }

        // Remove from connections storage
        delete this._connections[hubName];
    },

    /**
     * Stop all hub connections
     */
    stopAllConnections: function() {
        window.hcLogger.log('Base Hub: Stopping all connections...');
        
        Object.keys(this._connections).forEach(hubName => {
            this.stopConnection(hubName);
        });

        // Verify cleanup
        if (Object.keys(this._connections).length === 0) {
            window.hcLogger.log('Base Hub: All connections stopped successfully');
        } else {
            window.hcLogger.warn('Base Hub: Some connections may not have stopped properly');
        }
    },

    /**
     * Get connection status
     * @param {string} hubName - The hub name
     * @returns {object} - Connection status info
     */
    getConnectionStatus: function(hubName) {
        const connection = this._connections[hubName];
        
        if (!connection) {
            return {
                exists: false,
                state: 'not_initialized'
            };
        }

        return {
            exists: true,
            state: connection.state,
            connectionId: connection.connectionId,
            helpersCount: connection._dotnetHelpers?.length || 0,
            baseUrl: connection.baseUrl,
            hasCrossTabSync: !!this._broadcastChannels[hubName]
        };
    },

    /**
     * Log all active connections (for debugging)
     */
    logActiveConnections: function() {
        window.hcLogger.log('=== Base Hub: Active Connections ===');
        
        const hubNames = Object.keys(this._connections);
        
        if (hubNames.length === 0) {
            window.hcLogger.log('No active connections');
            return;
        }

        hubNames.forEach(hubName => {
            const status = this.getConnectionStatus(hubName);
            window.hcLogger.log(`${hubName}:`, status);
        });
        
        window.hcLogger.log('=====================================');
    }
};

// Log on load
window.hcLogger.log('Base Hub module loaded successfully');
