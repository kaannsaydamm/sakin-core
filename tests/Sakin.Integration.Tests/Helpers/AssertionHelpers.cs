using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using StackExchange.Redis;

namespace Sakin.Integration.Tests.Helpers;

public static class AssertionHelpers
{
    public static async Task<T?> WaitForMessageAsync<T>(
        IConsumer<string, T> consumer,
        TimeSpan? timeout = null,
        int maxMessages = 100) where T : class
    {
        timeout ??= TimeSpan.FromSeconds(30);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var messagesConsumed = 0;

        try
        {
            while (stopwatch.Elapsed < timeout && messagesConsumed < maxMessages)
            {
                var message = consumer.Consume(TimeSpan.FromSeconds(1));

                if (message != null && message.Message.Value != null)
                {
                    consumer.Commit(message);
                    return message.Message.Value;
                }

                messagesConsumed++;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }

        return null;
    }

    public static async Task<List<T>> WaitForMessagesAsync<T>(
        IConsumer<string, T> consumer,
        int expectedCount,
        TimeSpan? timeout = null) where T : class
    {
        timeout ??= TimeSpan.FromSeconds(30);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var messages = new List<T>();

        try
        {
            while (stopwatch.Elapsed < timeout && messages.Count < expectedCount)
            {
                var message = consumer.Consume(TimeSpan.FromSeconds(1));

                if (message != null && message.Message.Value != null)
                {
                    messages.Add(message.Message.Value);
                    consumer.Commit(message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }

        return messages;
    }

    public static async Task AssertMessageExistsInKafkaAsync<T>(
        IConsumer<string, T> consumer,
        Func<T, bool> predicate,
        string failureMessage = "Expected message not found") where T : class
    {
        var message = await WaitForMessageAsync(consumer, timeout: TimeSpan.FromSeconds(30));

        message.Should().NotBeNull(failureMessage);
        predicate(message!).Should().BeTrue($"Message predicate failed: {failureMessage}");
    }

    public static void AssertRedisKeyExists(
        IConnectionMultiplexer redis,
        string key)
    {
        var db = redis.GetDatabase();
        var exists = db.KeyExists(key);

        exists.Should().BeTrue($"Redis key '{key}' should exist");
    }

    public static void AssertRedisKeyNotExists(
        IConnectionMultiplexer redis,
        string key)
    {
        var db = redis.GetDatabase();
        var exists = db.KeyExists(key);

        exists.Should().BeFalse($"Redis key '{key}' should not exist");
    }

    public static async Task<T?> GetRedisValueAsync<T>(
        IConnectionMultiplexer redis,
        string key) where T : class
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(key);

        if (!value.HasValue)
            return null;

        return System.Text.Json.JsonSerializer.Deserialize<T>(value.ToString());
    }

    public static async Task<bool> WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan? timeout = null,
        int pollIntervalMs = 100)
    {
        timeout ??= TimeSpan.FromSeconds(30);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(pollIntervalMs);
        }

        return false;
    }

    public static void AssertNoDataLoss<T>(
        List<T> sent,
        List<T> received,
        string identifier = "event")
    {
        received.Should().HaveCount(
            sent.Count,
            $"All {identifier}s should be received without loss. Sent: {sent.Count}, Received: {received.Count}");
    }

    public static void AssertMessageOrdering<T>(
        List<T> messages,
        Func<T, int> sequenceSelector)
    {
        var sequences = messages.Select(sequenceSelector).ToList();
        var sortedSequences = sequences.OrderBy(s => s).ToList();

        sequences.Should().Equal(sortedSequences, "Messages should be in correct order");
    }

    public static void AssertIdempotency<T>(
        T original,
        T replay,
        Func<T, string> serialize)
    {
        serialize(original).Should().Equal(
            serialize(replay),
            "Replayed event should produce identical result (idempotency)");
    }
}
