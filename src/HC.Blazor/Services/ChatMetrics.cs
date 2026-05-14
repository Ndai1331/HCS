using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace HC.Blazor.Services
{
    /// <summary>
    /// Metrics collection for chat and notification system
    /// Tracks performance, errors, and usage patterns
    /// </summary>
    public interface IChatMetrics
    {
        void RecordMessageSent(bool success, long durationMs);
        void RecordMessageReceived(bool success);
        void RecordNotificationSent(bool success, int recipientCount);
        void RecordError(string operation, Exception exception);
        void RecordConnectionEvent(string hub, string eventType);
        ChatMetricsSnapshot GetSnapshot();
        Task ResetAsync();
    }

    /// <summary>
    /// Implementation of chat metrics collection
    /// </summary>
    public class ChatMetrics : IChatMetrics, IDisposable
    {
        private readonly ILogger<ChatMetrics> _logger;
        private readonly Timer _snapshotTimer;
        /// <summary>Protects list/dictionary fields and max/recent duration updates only.</summary>
        private readonly object _listsLock = new object();

        // Metrics storage (Interlocked for hot counters — avoids SemaphoreSlim.Wait on thread pool)
        private long _messagesSent;
        private long _messagesSentFailed;
        private long _messagesReceived;
        private long _messagesReceivedFailed;
        private long _notificationsSent;
        private long _notificationsSentFailed;
        private long _totalRecipients;

        // Performance metrics
        private long _totalSendDurationMs;
        private long _maxSendDurationMs;
        private readonly List<long> _recentSendDurations = new List<long>();

        // Error tracking
        private readonly Dictionary<string, long> _errorCounts = new Dictionary<string, long>();
        private readonly List<ErrorEvent> _recentErrors = new List<ErrorEvent>();

        // Connection metrics
        private int _activeConnections;
        private readonly Dictionary<string, long> _connectionEvents = new Dictionary<string, long>();

        public ChatMetrics(ILogger<ChatMetrics> logger)
        {
            _logger = logger;

            // Log metrics snapshot every minute
            _snapshotTimer = new Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
            _snapshotTimer.Elapsed += async (sender, e) => await LogSnapshotAsync();
            _snapshotTimer.Start();
        }

        public void RecordMessageSent(bool success, long durationMs)
        {
            if (success)
            {
                Interlocked.Increment(ref _messagesSent);
                Interlocked.Add(ref _totalSendDurationMs, durationMs);
                lock (_listsLock)
                {
                    _maxSendDurationMs = Math.Max(_maxSendDurationMs, durationMs);
                    _recentSendDurations.Add(durationMs);
                    if (_recentSendDurations.Count > 100)
                    {
                        _recentSendDurations.RemoveAt(0);
                    }
                }
            }
            else
            {
                Interlocked.Increment(ref _messagesSentFailed);
            }
        }

        public void RecordMessageReceived(bool success)
        {
            if (success)
            {
                Interlocked.Increment(ref _messagesReceived);
            }
            else
            {
                Interlocked.Increment(ref _messagesReceivedFailed);
            }
        }

        public void RecordNotificationSent(bool success, int recipientCount)
        {
            if (success)
            {
                Interlocked.Increment(ref _notificationsSent);
                Interlocked.Add(ref _totalRecipients, recipientCount);
            }
            else
            {
                Interlocked.Increment(ref _notificationsSentFailed);
            }
        }

        public void RecordError(string operation, Exception exception)
        {
            lock (_listsLock)
            {
                if (!_errorCounts.ContainsKey(operation))
                {
                    _errorCounts[operation] = 0;
                }
                _errorCounts[operation]++;

                _recentErrors.Add(new ErrorEvent
                {
                    Operation = operation,
                    ExceptionType = exception.GetType().Name,
                    Message = exception.Message,
                    Timestamp = DateTime.UtcNow
                });

                while (_recentErrors.Count > 50)
                {
                    _recentErrors.RemoveAt(0);
                }
            }

            _logger.LogError(
                exception,
                "Error recorded in metrics: {Operation}, Type: {ExceptionType}",
                operation,
                exception.GetType().Name);
        }

        public void RecordConnectionEvent(string hub, string eventType)
        {
            lock (_listsLock)
            {
                var key = $"{hub}_{eventType}";
                if (!_connectionEvents.ContainsKey(key))
                {
                    _connectionEvents[key] = 0;
                }
                _connectionEvents[key]++;

                if (eventType == "connected")
                {
                    _activeConnections++;
                }
                else if (eventType == "disconnected" && _activeConnections > 0)
                {
                    _activeConnections--;
                }
            }

            _logger.LogDebug("Connection event: {Hub} - {EventType}", hub, eventType);
        }

        public ChatMetricsSnapshot GetSnapshot()
        {
            lock (_listsLock)
            {
                var messagesSent = Interlocked.Read(ref _messagesSent);
                var avgSendDuration = messagesSent > 0
                    ? Interlocked.Read(ref _totalSendDurationMs) / messagesSent
                    : 0;

                var p95SendDuration = CalculateP95();

                return new ChatMetricsSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    MessagesSent = messagesSent,
                    MessagesSentFailed = Interlocked.Read(ref _messagesSentFailed),
                    MessagesReceived = Interlocked.Read(ref _messagesReceived),
                    MessagesReceivedFailed = Interlocked.Read(ref _messagesReceivedFailed),
                    NotificationsSent = Interlocked.Read(ref _notificationsSent),
                    NotificationsSentFailed = Interlocked.Read(ref _notificationsSentFailed),
                    TotalRecipients = Interlocked.Read(ref _totalRecipients),
                    AverageSendDurationMs = avgSendDuration,
                    MaxSendDurationMs = _maxSendDurationMs,
                    P95SendDurationMs = p95SendDuration,
                    ActiveConnections = _activeConnections,
                    ErrorCounts = new Dictionary<string, long>(_errorCounts),
                    RecentErrors = _recentErrors.Take(10).ToList(),
                    ConnectionEvents = new Dictionary<string, long>(_connectionEvents)
                };
            }
        }

        public Task ResetAsync()
        {
            lock (_listsLock)
            {
                Interlocked.Exchange(ref _messagesSent, 0);
                Interlocked.Exchange(ref _messagesSentFailed, 0);
                Interlocked.Exchange(ref _messagesReceived, 0);
                Interlocked.Exchange(ref _messagesReceivedFailed, 0);
                Interlocked.Exchange(ref _notificationsSent, 0);
                Interlocked.Exchange(ref _notificationsSentFailed, 0);
                Interlocked.Exchange(ref _totalRecipients, 0);
                Interlocked.Exchange(ref _totalSendDurationMs, 0);
                Interlocked.Exchange(ref _maxSendDurationMs, 0);
                _recentSendDurations.Clear();
                _errorCounts.Clear();
                _recentErrors.Clear();
                _connectionEvents.Clear();
                _activeConnections = 0;

                _logger.LogInformation("Chat metrics reset");
            }

            return Task.CompletedTask;
        }

        private long CalculateP95()
        {
            if (_recentSendDurations.Count == 0)
            {
                return 0;
            }

            var sorted = _recentSendDurations.OrderBy(x => x).ToList();
            var index = (int)Math.Ceiling(0.95 * sorted.Count) - 1;
            return sorted[Math.Max(0, index)];
        }

        private async Task LogSnapshotAsync()
        {
            var snapshot = GetSnapshot();

            _logger.LogInformation(
                "Chat Metrics Snapshot - " +
                "Sent: {Sent} (Failed: {SentFailed}), " +
                "Received: {Received} (Failed: {ReceivedFailed}), " +
                "Notifications: {Notifications} (Failed: {NotifFailed}), " +
                "Avg Latency: {AvgLatency}ms, " +
                "P95 Latency: {P95Latency}ms, " +
                "Active Connections: {Connections}",
                snapshot.MessagesSent,
                snapshot.MessagesSentFailed,
                snapshot.MessagesReceived,
                snapshot.MessagesReceivedFailed,
                snapshot.NotificationsSent,
                snapshot.NotificationsSentFailed,
                snapshot.AverageSendDurationMs,
                snapshot.P95SendDurationMs,
                snapshot.ActiveConnections);

            // Log warnings if metrics are degraded
            if (snapshot.MessagesSent > 0 && snapshot.MessagesSentFailed > snapshot.MessagesSent * 0.05) // >5% failure rate
            {
                _logger.LogWarning(
                    "High message send failure rate: {FailureRate:F2}%",
                    (double)snapshot.MessagesSentFailed / snapshot.MessagesSent * 100);
            }

            if (snapshot.AverageSendDurationMs > 1000) // >1 second average
            {
                _logger.LogWarning(
                    "High message send latency: {Latency}ms average",
                    snapshot.AverageSendDurationMs);
            }

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _snapshotTimer?.Stop();
            _snapshotTimer?.Dispose();
        }
    }

    /// <summary>
    /// Snapshot of metrics at a point in time
    /// </summary>
    public class ChatMetricsSnapshot
    {
        public DateTime Timestamp { get; set; }
        public long MessagesSent { get; set; }
        public long MessagesSentFailed { get; set; }
        public long MessagesReceived { get; set; }
        public long MessagesReceivedFailed { get; set; }
        public long NotificationsSent { get; set; }
        public long NotificationsSentFailed { get; set; }
        public long TotalRecipients { get; set; }
        public long AverageSendDurationMs { get; set; }
        public long MaxSendDurationMs { get; set; }
        public long P95SendDurationMs { get; set; }
        public int ActiveConnections { get; set; }
        public Dictionary<string, long> ErrorCounts { get; set; }
        public List<ErrorEvent> RecentErrors { get; set; }
        public Dictionary<string, long> ConnectionEvents { get; set; }

        /// <summary>
        /// Calculate success rate for messages
        /// </summary>
        public double GetMessageSuccessRate()
        {
            var total = MessagesSent + MessagesSentFailed;
            return total > 0 ? (double)MessagesSent / total * 100 : 100;
        }

        /// <summary>
        /// Calculate success rate for notifications
        /// </summary>
        public double GetNotificationSuccessRate()
        {
            var total = NotificationsSent + NotificationsSentFailed;
            return total > 0 ? (double)NotificationsSent / total * 100 : 100;
        }

        /// <summary>
        /// Get total error count
        /// </summary>
        public long GetTotalErrors()
        {
            return ErrorCounts?.Values.Sum() ?? 0;
        }
    }

    /// <summary>
    /// Represents an error event
    /// </summary>
    public class ErrorEvent
    {
        public string Operation { get; set; }
        public string ExceptionType { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Health check result for the chat system
    /// </summary>
    public class ChatSystemHealth
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
        public ChatMetricsSnapshot Metrics { get; set; }

        public static ChatSystemHealth CheckHealth(ChatMetricsSnapshot snapshot)
        {
            var health = new ChatSystemHealth
            {
                Metrics = snapshot
            };

            // Check message success rate
            if (snapshot.GetMessageSuccessRate() < 95)
            {
                health.Issues.Add($"Low message success rate: {snapshot.GetMessageSuccessRate():F1}%");
            }

            // Check notification success rate
            if (snapshot.GetNotificationSuccessRate() < 95)
            {
                health.Issues.Add($"Low notification success rate: {snapshot.GetNotificationSuccessRate():F1}%");
            }

            // Check latency
            if (snapshot.P95SendDurationMs > 2000)
            {
                health.Issues.Add($"High P95 latency: {snapshot.P95SendDurationMs}ms");
            }

            // Check error rate
            if (snapshot.GetTotalErrors() > 100)
            {
                health.Issues.Add($"High error count: {snapshot.GetTotalErrors()}");
            }

            // Check active connections
            if (snapshot.ActiveConnections == 0 && snapshot.MessagesSent > 0)
            {
                health.Issues.Add("No active connections but messages were sent");
            }

            health.IsHealthy = health.Issues.Count == 0;
            health.Status = health.IsHealthy ? "Healthy" : "Degraded";

            return health;
        }
    }
}
