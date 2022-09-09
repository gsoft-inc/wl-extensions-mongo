using ShareGate.Infra.Mongo.Threading;
using ShareGate.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace ShareGate.Infra.Mongo.Tests;

[Collection("threading")]
public sealed class MongoDistributedLockTests : BaseIntegrationTest<MongoFixture>
{
    public MongoDistributedLockTests(MongoFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task AcquireAsync_Single_Lock_Works()
    {
        var distributedLockFactory = this.Services.GetRequiredService<MongoDistributedLockFactory>();

        await using (var distributedLock = await distributedLockFactory.AcquireAsync("single", lifetime: 100, timeout: 100))
        {
            Assert.True(distributedLock.IsAcquired);
        }
    }

    [Theory]
    [InlineData(10, 500, 50, 2)]
    [InlineData(10, 500, 1000, 4)]
    [InlineData(10, 500, 5000, 10)]
    public async Task AcquireAsync_Only_Allows_One_Owner_At_A_Time(int taskCount, int lifetime, int timeout, int expectedAcquiredLockCount)
    {
        var distributedLockFactory = this.Services.GetRequiredService<MongoDistributedLockFactory>();

        var tasks = new Task[taskCount];
        var totalAcquiredLockCount = 0;

        var lockId1 = Guid.NewGuid().ToString();
        var lockId2 = Guid.NewGuid().ToString();

        for (var i = 0; i < tasks.Length; i++)
        {
            async Task AcquireAction(string lockId)
            {
                await using (var distributedLock = await distributedLockFactory.AcquireAsync(lockId, lifetime, timeout))
                {
                    if (distributedLock.IsAcquired)
                    {
                        Interlocked.Increment(ref totalAcquiredLockCount);
                        await Task.Delay(lifetime);
                    }
                }
            }

            var lockId = i % 2 == 0 ? lockId1 : lockId2;
            tasks[i] = Task.Factory.StartNew(() => AcquireAction(lockId).GetAwaiter().GetResult(), TaskCreationOptions.LongRunning);
        }

        await Task.WhenAll(tasks);

        // This test is flaky due to threading issues. Task.Delay might take longer than requested, or the computer running the test might be slow.
        // Ex: on a dev workstation, works well all the time, the precision is great. On a Azure DevOps built-in agent, we often acquire one more lock due Task.Delay's lack of precision.
        // Because of this, we assert that the actual value is in a range instead of a specific value.
        var rangeLowerBound = Math.Max(0, expectedAcquiredLockCount - 1);
        var rangeUpperBound = expectedAcquiredLockCount + 1;

        Assert.InRange(totalAcquiredLockCount, rangeLowerBound, rangeUpperBound);
    }

    [Theory]
    [InlineData(10, 2000, 2000, 200)]
    public async Task AcquireAsync_Throws_When_Cancellation_Is_Requested(int taskCount, int lifetime, int timeout, int cancelAfter)
    {
        var distributedLockFactory = this.Services.GetRequiredService<MongoDistributedLockFactory>();

        var tasks = new Task[taskCount];
        var ocexCount = 0;

        using var cts = new CancellationTokenSource(cancelAfter);

        for (var i = 0; i < tasks.Length; i++)
        {
            async Task AcquireAction(string lockId)
            {
                try
                {
                    await using (var distributedLock = await distributedLockFactory.AcquireAsync(lockId, lifetime, timeout, cts.Token))
                    {
                        if (distributedLock.IsAcquired)
                        {
                            await Task.Delay(lifetime);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref ocexCount);
                }
            }

            tasks[i] = Task.Factory.StartNew(() => AcquireAction("foo").GetAwaiter().GetResult(), TaskCreationOptions.LongRunning);
        }

        await Task.WhenAll(tasks);

        Assert.Equal(taskCount - 1, ocexCount);
    }
}