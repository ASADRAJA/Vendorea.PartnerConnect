using FluentAssertions;
using Vendorea.PartnerConnect.Domain.Entities;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests;

/// <summary>
/// Which feed polls are worth recording.
/// </summary>
/// <remarks>
/// The feed worker polls on a timer whether or not the partner has delivered anything, and
/// persisted a batch row every time. That produced 21,801 identical empty rows on the test
/// environment — one every five minutes — which would have buried the real batches the first time
/// SPR delivered a file.
///
/// The distinction that matters: "nothing was there" is discarded, "something went wrong" is kept.
/// </remarks>
public class PriceFeedBatchRecordsNoFileTests
{
    private static PriceFeedBatch Batch(
        FeedBatchStatus status, int totalItems = 0, int? documentId = null) => new()
    {
        TradingPartnerId = 1,
        Status = status,
        TotalItems = totalItems,
        PartnerDocumentId = documentId,
        ReceivedAt = DateTime.UtcNow
    };

    [Fact]
    public void A_completed_poll_that_found_no_file_records_nothing()
    {
        // Exactly the shape of all 21,801 rows: completed, zero items, no document.
        Batch(FeedBatchStatus.Completed).RecordsNoFile.Should().BeTrue();
    }

    [Fact]
    public void A_failed_poll_is_still_worth_keeping()
    {
        // A failure is a real event. Discarding it would hide an outage behind silence.
        Batch(FeedBatchStatus.Failed).RecordsNoFile.Should().BeFalse();
    }

    [Fact]
    public void A_poll_that_processed_items_is_kept()
    {
        Batch(FeedBatchStatus.Completed, totalItems: 46585, documentId: 12)
            .RecordsNoFile.Should().BeFalse();
    }

    [Fact]
    public void A_file_that_arrived_but_contained_nothing_is_kept()
    {
        // A document exists, so a file really was delivered — an empty file is a supplier problem
        // worth seeing, not a quiet no-op.
        Batch(FeedBatchStatus.Completed, totalItems: 0, documentId: 12)
            .RecordsNoFile.Should().BeFalse();
    }

    [Theory]
    [InlineData(FeedBatchStatus.Received)]
    [InlineData(FeedBatchStatus.Validating)]
    [InlineData(FeedBatchStatus.Processing)]
    [InlineData(FeedBatchStatus.PartiallyCompleted)]
    public void An_unfinished_or_partial_batch_is_kept(FeedBatchStatus status)
    {
        // Only a *completed* poll can be dismissed as "nothing there". Anything still in flight or
        // partially done describes work that happened.
        Batch(status).RecordsNoFile.Should().BeFalse();
    }
}
