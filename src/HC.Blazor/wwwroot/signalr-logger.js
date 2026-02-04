/**
 * SignalR Conditional Logger
 * 
 * Provides conditional logging for SignalR hubs to reduce overhead in production
 * Logs are only enabled in:
 * - Development environment (localhost/127.0.0.1)
 * - When explicitly enabled via sessionStorage.setItem('debugSignalR', 'true')
 * 
 * Usage:
 *   hcLogger.log("Message", ...args);    // Only logs in development/debug mode
 *   hcLogger.warn("Warning", ...args);   // Only logs in development/debug mode
 *   hcLogger.error("Error", ...args);    // Always logs regardless of environment
 *   hcLogger.trace("Trace", ...args);    // Only in debug mode with verbose logging
 */

window.hcLogger = (function() {
    'use strict';
    
    // Private: Check if logging should be enabled
    function isLoggingEnabled() {
        // Enable in development environment
        const isDev = window.location.hostname === 'localhost' || 
                      window.location.hostname === '127.0.0.1' ||
                      window.location.hostname === '::1';
        
        // Enable if explicitly turned on via sessionStorage
        const isDebugEnabled = sessionStorage.getItem('debugSignalR') === 'true';
        
        return isDev || isDebugEnabled;
    }
    
    // Private: Check if verbose (trace) logging is enabled
    function isTraceEnabled() {
        return sessionStorage.getItem('debugSignalRVerbose') === 'true';
    }
    
    // Private: Format log message with timestamp
    function formatMessage(level, message) {
        const timestamp = new Date().toISOString().split('T')[1].slice(0, -1);
        return `[${timestamp}] [SignalR ${level}] ${message}`;
    }
    
    // Public API
    return {
        /**
         * Check if debug logging is currently enabled
         */
        isEnabled: function() {
            return isLoggingEnabled();
        },
        
        /**
         * Enable debug logging (useful for production debugging)
         */
        enable: function() {
            sessionStorage.setItem('debugSignalR', 'true');
            console.info('[SignalR Logger] Debug logging ENABLED');
        },
        
        /**
         * Disable debug logging
         */
        disable: function() {
            sessionStorage.removeItem('debugSignalR');
            sessionStorage.removeItem('debugSignalRVerbose');
            console.info('[SignalR Logger] Debug logging DISABLED');
        },
        
        /**
         * Enable verbose (trace) logging
         */
        enableVerbose: function() {
            this.enable();
            sessionStorage.setItem('debugSignalRVerbose', 'true');
            console.info('[SignalR Logger] Verbose logging ENABLED');
        },
        
        /**
         * Log informational messages (only in development/debug mode)
         */
        log: function(message, ...args) {
            if (isLoggingEnabled()) {
                console.log(formatMessage('INFO', message), ...args);
            }
        },
        
        /**
         * Log warning messages (only in development/debug mode)
         */
        warn: function(message, ...args) {
            if (isLoggingEnabled()) {
                console.warn(formatMessage('WARN', message), ...args);
            }
        },
        
        /**
         * Log error messages (always logs, regardless of environment)
         */
        error: function(message, ...args) {
            console.error(formatMessage('ERROR', message), ...args);
        },
        
        /**
         * Log trace messages (only in verbose debug mode)
         */
        trace: function(message, ...args) {
            if (isLoggingEnabled() && isTraceEnabled()) {
                console.log(formatMessage('TRACE', message), ...args);
            }
        },
        
        /**
         * Log performance metrics (always logs but with minimal overhead)
         */
        performance: function(operation, durationMs) {
            if (isLoggingEnabled()) {
                console.log(formatMessage('PERF', `${operation} took ${durationMs}ms`));
            }
        },
        
        /**
         * Log with custom level (for advanced usage)
         */
        logCustom: function(level, message, ...args) {
            if (!isLoggingEnabled() && level !== 'ERROR') {
                return;
            }
            
            const formattedMsg = formatMessage(level.toUpperCase(), message);
            
            switch(level.toUpperCase()) {
                case 'ERROR':
                    console.error(formattedMsg, ...args);
                    break;
                case 'WARN':
                    console.warn(formattedMsg, ...args);
                    break;
                case 'TRACE':
                    if (isTraceEnabled()) {
                        console.log(formattedMsg, ...args);
                    }
                    break;
                default:
                    console.log(formattedMsg, ...args);
            }
        }
    };
})();

// Log initialization
if (window.hcLogger.isEnabled()) {
    console.info('[SignalR Logger] Initialized with logging ENABLED');
} else {
    console.info('[SignalR Logger] Initialized - Logging disabled (set sessionStorage.debugSignalR = "true" to enable)');
}

// Export for debugging (accessible from browser console)
window.hcLogger.isEnabled.toString = function() {
    return window.hcLogger.isEnabled() ? 'enabled' : 'disabled';
};
