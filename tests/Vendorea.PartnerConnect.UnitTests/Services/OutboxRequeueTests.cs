using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Application.Services;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.UnitTests.Services;

/// <summary>
/// Tests for the manual retry/replay (requeue) admin operations on the outbox.
/// </summary>
public class OutboxRequeueTests
{
    private static OutboxService CreateService(Mock<IOutboxRepository> repo) =>
        new(repo.Object, new Mock<IOutboxMessageProcessor>().Object, NullLogger<OutboxService>.Instance);

    [Fact]
    public async Task RequeueAsync_FailedMessage_ResetsToPendingWithFreshBudget()
    {
        var id = Guid.NewGuid();
        var message = new OutboxMessage
        {
            Id = id,
            MessageType = "Merchant360OrderStatus",
            Status = OutboxMessageStatus.Failed,
            RetryCount = 5,
            NextRetryAt = DateTime.UtcNow,
            LastError = "M360 down"
        };

        var repo = new Mock<IOutboxRepository>();
        repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(message);
        OutboxMessage? updated = null;
        repo.Setup(r => r.UpdateAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<OutboxMessage, CancellationToken>((m, _) => updated = m)
            .Returns(Task.CompletedTask);

        var ok = await CreateService(repo).RequeueAsync(id);

        ok.Should().BeTrue();
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(OutboxMessageStatus.Pending);
        updated.RetryCount.Should().Be(0);
        updated.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public async Task RequeueAsync_NotFound_ReturnsFalse()
    {
        var repo = new Mock<IOutboxRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutboxMessage?)null);

        (await CreateService(repo).RequeueAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task RequeueAsync_DeliveredMessage_NotReplayable_ReturnsFalse()
    {
        var id = Guid.NewGuid();
        var repo = new Mock<IOutboxRepository>();
        repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutboxMessage { Id = id, Status = OutboxMessageStatus.Delivered });

        (await CreateService(repo).RequeueAsync(id)).Should().BeFalse();
        repo.Verify(r => r.UpdateAsync(It.IsAny<OutboxMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequeueAllFailedAsync_RequeuesEveryFailedMessage()
    {
        var failed = new List<OutboxMessage>
        {
            new() { Id = Guid.NewGuid(), Status = OutboxMessageStatus.Failed, RetryCount = 5 },
            new() { Id = Guid.NewGuid(), Status = OutboxMessageStatus.Failed, RetryCount = 5 }
        };

        var repo = new Mock<IOutboxRepository>();
        repo.Setup(r => r.GetByStatusAsync(OutboxMessageStatus.Failed, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failed);
        repo.Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<OutboxMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var count = await CreateService(repo).RequeueAllFailedAsync();

        count.Should().Be(2);
        failed.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending && m.RetryCount == 0);
    }
}
