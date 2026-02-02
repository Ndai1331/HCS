using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Services
{
    /// <summary>
    /// Retry policy with exponential backoff for resilient operations
    /// </summary>
    public class RetryPolicy
    {
        private readonly ILogger _logger;
        private readonly int _maxRetries;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _maxDelay;
        private readonly double _backoffMultiplier;

        public RetryPolicy(
            ILogger logger,
            int maxRetries = 3,
            TimeSpan? initialDelay = null,
            TimeSpan? maxDelay = null,
            double backoffMultiplier = 2.0)
        {
            _logger = logger;
            _maxRetries = maxRetries;
            _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
            _maxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
            _backoffMultiplier = backoffMultiplier;
        }

        /// <summary>
        /// Execute an operation with retry policy
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            string operationName)
        {
            var retryCount = 0;
            var delay = _initialDelay;

            while (true)
            {
                try
                {
                    var result = await operation();
                    
                    if (retryCount > 0)
                    {
                        _logger.LogInformation(
                            "Operation {OperationName} succeeded after {RetryCount} retries",
                            operationName, retryCount);
                    }
                    
                    return result;
                }
                catch (Exception ex) when (retryCount < _maxRetries)
                {
                    retryCount++;
                    
                    var currentDelay = CalculateDelay(retryCount, delay);
                    delay = TimeSpan.FromMilliseconds(
                        Math.Min(
                            currentDelay.TotalMilliseconds * _backoffMultiplier,
                            _maxDelay.TotalMilliseconds));

                    _logger.LogWarning(
                        ex,
                        "Operation {OperationName} failed (attempt {Attempt}/{MaxAttempts}). " +
                        "Retrying in {Delay}s... Error: {ErrorMessage}",
                        operationName,
                        retryCount,
                        _maxRetries + 1,
                        currentDelay.TotalSeconds.ToString("F1"),
                        ex.Message);

                    await Task.Delay(currentDelay);
                }
            }
        }

        /// <summary>
        /// Execute an async operation with retry policy (no return value)
        /// </summary>
        public async Task ExecuteAsync(
            Func<Task> operation,
            string operationName)
        {
            var retryCount = 0;
            var delay = _initialDelay;

            while (true)
            {
                try
                {
                    await operation();
                    
                    if (retryCount > 0)
                    {
                        _logger.LogInformation(
                            "Operation {OperationName} succeeded after {RetryCount} retries",
                            operationName, retryCount);
                    }
                    
                    return;
                }
                catch (Exception ex) when (retryCount < _maxRetries)
                {
                    retryCount++;
                    
                    var currentDelay = CalculateDelay(retryCount, delay);
                    delay = TimeSpan.FromMilliseconds(
                        Math.Min(
                            currentDelay.TotalMilliseconds * _backoffMultiplier,
                            _maxDelay.TotalMilliseconds));

                    _logger.LogWarning(
                        ex,
                        "Operation {OperationName} failed (attempt {Attempt}/{MaxAttempts}). " +
                        "Retrying in {Delay}s... Error: {ErrorMessage}",
                        operationName,
                        retryCount,
                        _maxRetries + 1,
                        currentDelay.TotalSeconds.ToString("F1"),
                        ex.Message);

                    await Task.Delay(currentDelay);
                }
            }
        }

        private TimeSpan CalculateDelay(int retryCount, TimeSpan previousDelay)
        {
            // Progressive delay: 1s, 2s, 4s, 8s, etc.
            var newDelay = TimeSpan.FromMilliseconds(
                previousDelay.TotalMilliseconds * _backoffMultiplier);
            
            return newDelay > _maxDelay ? _maxDelay : newDelay;
        }
    }

    /// <summary>
    /// Circuit breaker pattern to prevent cascading failures
    /// </summary>
    public class CircuitBreaker
    {
        private readonly ILogger _logger;
        private readonly int _failureThreshold;
        private readonly TimeSpan _openTimeout;
        private readonly TimeSpan _halfOpenTimeout;

        private int _failureCount;
        private DateTime? _lastFailureTime;
        private CircuitState _state = CircuitState.Closed;
        private DateTime? _openedTime;

        public CircuitBreaker(
            ILogger logger,
            int failureThreshold = 5,
            TimeSpan? openTimeout = null,
            TimeSpan? halfOpenTimeout = null)
        {
            _logger = logger;
            _failureThreshold = failureThreshold;
            _openTimeout = openTimeout ?? TimeSpan.FromMinutes(1);
            _halfOpenTimeout = halfOpenTimeout ?? TimeSpan.FromSeconds(30);
        }

        public enum CircuitState
        {
            Closed,    // Normal operation
            Open,      // Failing, reject requests
            HalfOpen   // Testing if service recovered
        }

        public CircuitState State => _state;

        /// <summary>
        /// Execute operation through circuit breaker
        /// </summary>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation,
            string operationName)
        {
            // Check if circuit should transition from Open to HalfOpen
            if (_state == CircuitState.Open && 
                _openedTime.HasValue && 
                DateTime.UtcNow - _openedTime.Value > _openTimeout)
            {
                _state = CircuitState.HalfOpen;
                _logger.LogInformation(
                    "Circuit breaker transitioning to HalfOpen for {OperationName}",
                    operationName);
            }

            // Reject if circuit is Open
            if (_state == CircuitState.Open)
            {
                _logger.LogWarning(
                    "Circuit breaker is OPEN for {OperationName}. Rejecting request.",
                    operationName);
                
                throw new CircuitBreakerOpenException(
                    $"Circuit breaker is open for {operationName}. " +
                    $"Too many failures ({_failureCount}/{_failureThreshold}). " +
                    $"Try again in {(_openTimeout - (DateTime.UtcNow - _openedTime.Value)).TotalSeconds:F0}s.");
            }

            try
            {
                // Execute operation
                var result = await operation();

                // Success - reset failure count and close circuit
                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Closed;
                    _logger.LogInformation(
                        "Circuit breaker closed again for {OperationName} after successful test",
                        operationName);
                }

                _failureCount = 0;
                _lastFailureTime = null;

                return result;
            }
            catch (Exception ex)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;

                _logger.LogError(
                    ex,
                    "Operation {OperationName} failed. Failure count: {FailureCount}/{FailureThreshold}",
                    operationName,
                    _failureCount,
                    _failureThreshold);

                // Open circuit if threshold reached
                if (_failureCount >= _failureThreshold)
                {
                    _state = CircuitState.Open;
                    _openedTime = DateTime.UtcNow;
                    
                    _logger.LogError(
                        "Circuit breaker OPENED for {OperationName} after {FailureCount} failures",
                        operationName,
                        _failureCount);
                }

                throw;
            }
        }

        /// <summary>
        /// Execute operation through circuit breaker (no return value)
        /// </summary>
        public async Task ExecuteAsync(
            Func<Task> operation,
            string operationName)
        {
            // Check if circuit should transition from Open to HalfOpen
            if (_state == CircuitState.Open && 
                _openedTime.HasValue && 
                DateTime.UtcNow - _openedTime.Value > _openTimeout)
            {
                _state = CircuitState.HalfOpen;
                _logger.LogInformation(
                    "Circuit breaker transitioning to HalfOpen for {OperationName}",
                    operationName);
            }

            // Reject if circuit is Open
            if (_state == CircuitState.Open)
            {
                _logger.LogWarning(
                    "Circuit breaker is OPEN for {OperationName}. Rejecting request.",
                    operationName);
                
                throw new CircuitBreakerOpenException(
                    $"Circuit breaker is open for {operationName}. " +
                    $"Too many failures ({_failureCount}/{_failureThreshold}). " +
                    $"Try again in {(_openTimeout - (DateTime.UtcNow - _openedTime.Value)).TotalSeconds:F0}s.");
            }

            try
            {
                // Execute operation
                await operation();

                // Success - reset failure count and close circuit
                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Closed;
                    _logger.LogInformation(
                        "Circuit breaker closed again for {OperationName} after successful test",
                        operationName);
                }

                _failureCount = 0;
                _lastFailureTime = null;
            }
            catch (Exception ex)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;

                _logger.LogError(
                    ex,
                    "Operation {OperationName} failed. Failure count: {FailureCount}/{FailureThreshold}",
                    operationName,
                    _failureCount,
                    _failureThreshold);

                // Open circuit if threshold reached
                if (_failureCount >= _failureThreshold)
                {
                    _state = CircuitState.Open;
                    _openedTime = DateTime.UtcNow;
                    
                    _logger.LogError(
                        "Circuit breaker OPENED for {OperationName} after {FailureCount} failures",
                        operationName,
                        _failureCount);
                }

                throw;
            }
        }

        /// <summary>
        /// Reset circuit breaker manually
        /// </summary>
        public void Reset()
        {
            _failureCount = 0;
            _state = CircuitState.Closed;
            _openedTime = null;
            _lastFailureTime = null;
            
            _logger.LogInformation("Circuit breaker reset manually");
        }
    }

    /// <summary>
    /// Exception thrown when circuit breaker is open
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message)
        {
        }

        public CircuitBreakerOpenException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Dead letter queue for failed messages
    /// </summary>
    public interface IDeadLetterQueue
    {
        Task AddAsync<T>(T item, string reason, Exception exception = null);
        Task<T> GetAsync<T>(string id);
        Task<IEnumerable<T>> GetAllAsync<T>();
        Task RemoveAsync<T>(string id);
        Task ClearAsync();
    }

    /// <summary>
    /// In-memory dead letter queue implementation
    /// For production, use persistent storage like Redis or database
    /// </summary>
    public class InMemoryDeadLetterQueue : IDeadLetterQueue
    {
        private readonly Dictionary<string, DeadLetterItem> _items = new();
        private readonly ILogger<InMemoryDeadLetterQueue> _logger;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public InMemoryDeadLetterQueue(ILogger<InMemoryDeadLetterQueue> logger)
        {
            _logger = logger;
        }

        public async Task AddAsync<T>(T item, string reason, Exception exception = null)
        {
            await _lock.WaitAsync();
            try
            {
                var id = Guid.NewGuid().ToString();
                _items[id] = new DeadLetterItem
                {
                    Id = id,
                    Item = item,
                    ItemType = typeof(T).Name,
                    Reason = reason,
                    Exception = exception?.Message,
                    StackTrace = exception?.StackTrace,
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogWarning(
                    "Item added to dead letter queue. Type: {ItemType}, Reason: {Reason}, Id: {Id}",
                    typeof(T).Name,
                    reason,
                    id);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<T> GetAsync<T>(string id)
        {
            await _lock.WaitAsync();
            try
            {
                if (_items.TryGetValue(id, out var item) && item.ItemType == typeof(T).Name)
                {
                    _items.Remove(id);
                    return (T)item.Item;
                }

                return default(T);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>()
        {
            await _lock.WaitAsync();
            try
            {
                return _items.Values
                    .Where(item => item.ItemType == typeof(T).Name)
                    .Select(item => (T)item.Item)
                    .ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RemoveAsync<T>(string id)
        {
            await _lock.WaitAsync();
            try
            {
                if (_items.TryGetValue(id, out var item) && item.ItemType == typeof(T).Name)
                {
                    _items.Remove(id);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ClearAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var count = _items.Count;
                _items.Clear();
                _logger.LogInformation("Dead letter queue cleared. Removed {Count} items", count);
            }
            finally
            {
                _lock.Release();
            }
        }

        private class DeadLetterItem
        {
            public string Id { get; set; }
            public object Item { get; set; }
            public string ItemType { get; set; }
            public string Reason { get; set; }
            public string Exception { get; set; }
            public string StackTrace { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
