using AFMS.Core;
using AFMS.Core.Domain;

namespace AFMS.Tests;

/// <summary>
/// Balances, the stock card running total, and the guards that keep quantity on
/// hand from going negative.
/// </summary>
public sealed class StockBalanceTests
{
    [Fact]
    public async Task On_hand_is_received_minus_released()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 100);
        await app.ReceiveAsync(paper.Id, 50);
        await app.ReleaseAsync(person.Id, paper.Id, 30);

        var item = await app.Items.GetAsync(paper.Id);

        Assert.Equal(150, item!.QuantityIn);
        Assert.Equal(30, item.QuantityOut);
        Assert.Equal(120, item.OnHand);
    }

    [Fact]
    public async Task Releasing_more_than_is_on_hand_is_blocked()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 10);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.ReleaseAsync(person.Id, paper.Id, 11));

        Assert.Contains("only 10 ream on hand", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task Two_lines_for_one_item_are_totalled_before_the_stock_check()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 10);

        // Each line alone would pass; together they exceed what is available.
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.Releases.SaveAsync(new SaveReleaseRequest(
                null,
                DateOnly.FromDateTime(DateTime.Today),
                person.Id,
                "Test",
                "Secretary",
                "Collector",
                null,
                [
                    new StockLineInput(paper.Id, 6, null, null, null),
                    new StockLineInput(paper.Id, 6, null, null, null)
                ])));

        Assert.Contains("12 is being released", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task A_voided_stock_in_stops_counting_towards_on_hand()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");

        var entry = await app.ReceiveAsync(paper.Id, 100);
        await app.StockIn.VoidAsync(entry.Id, "Duplicate encoding of the same delivery.");

        var item = await app.Items.GetAsync(paper.Id);
        Assert.Equal(0, item!.OnHand);
    }

    [Fact]
    public async Task A_stock_in_cannot_be_voided_if_the_stock_is_already_out()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        var entry = await app.ReceiveAsync(paper.Id, 100);
        await app.ReleaseAsync(person.Id, paper.Id, 80);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.StockIn.VoidAsync(entry.Id, "Trying to remove stock that is already released."));

        Assert.Contains("below zero", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task Editing_a_release_frees_its_own_quantity_for_the_stock_check()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 10);
        var release = await app.ReleaseAsync(person.Id, paper.Id, 10);

        // All 10 are already out on this very release, so re-saving 10 must be fine.
        var updated = await app.Releases.SaveAsync(new SaveReleaseRequest(
            release.Id,
            release.ReleaseDate,
            person.Id,
            "Corrected purpose",
            "Secretary",
            "Collector",
            null,
            [new StockLineInput(paper.Id, 10, null, null, null)]));

        Assert.Equal(10, updated.TotalQuantity);
    }

    [Fact]
    public async Task The_stock_card_shows_a_running_balance()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        var day1 = new DateOnly(2026, 1, 5);
        var day2 = new DateOnly(2026, 1, 10);
        var day3 = new DateOnly(2026, 1, 15);

        await app.ReceiveAsync(paper.Id, 100, date: day1);
        await app.ReleaseAsync(person.Id, paper.Id, 30, date: day2);
        await app.ReceiveAsync(paper.Id, 20, date: day3);

        var card = await app.History.GetStockCardAsync(paper.Id);

        Assert.Equal(3, card.Count);
        Assert.Equal(100, card[0].Balance);
        Assert.Equal(70, card[1].Balance);
        Assert.Equal(90, card[2].Balance);
    }

    [Fact]
    public async Task A_date_filtered_stock_card_still_opens_from_the_true_balance()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 100, date: new DateOnly(2026, 1, 5));
        await app.ReleaseAsync(person.Id, paper.Id, 30, date: new DateOnly(2026, 2, 10));

        // Only February is requested, but the balance must account for January.
        var card = await app.History.GetStockCardAsync(
            paper.Id, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));

        var row = Assert.Single(card);
        Assert.Equal(30, row.QuantityOut);
        Assert.Equal(70, row.Balance);
    }

    [Fact]
    public async Task History_combines_incoming_and_outgoing_lines()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 100);
        await app.ReleaseAsync(person.Id, paper.Id, 30);

        var history = await app.History.ListAsync(new HistoryFilter());

        Assert.Equal(2, history.Count);
        Assert.Contains(history, entry => entry.Direction == MovementDirection.In && entry.Quantity == 100);
        Assert.Contains(history, entry =>
            entry.Direction == MovementDirection.Out && entry.PartyName == "Marigold");
    }

    [Fact]
    public async Task History_can_be_filtered_to_one_person()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var first = await app.AddPersonAsync("A", "First");
        var second = await app.AddPersonAsync("B", "Second");

        await app.ReceiveAsync(paper.Id, 100);
        await app.ReleaseAsync(first.Id, paper.Id, 10);
        await app.ReleaseAsync(second.Id, paper.Id, 20);

        var history = await app.History.ListAsync(new HistoryFilter(RecipientId: first.Id));

        var entry = Assert.Single(history);
        Assert.Equal("First", entry.PartyName);
        Assert.Equal(10, entry.Quantity);
    }

    [Fact]
    public async Task Reference_numbers_run_in_sequence_per_prefix_and_year()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        var first = await app.ReceiveAsync(paper.Id, 10, date: new DateOnly(2026, 3, 1));
        var second = await app.ReceiveAsync(paper.Id, 10, date: new DateOnly(2026, 4, 1));
        var release = await app.ReleaseAsync(person.Id, paper.Id, 5, date: new DateOnly(2026, 4, 2));

        Assert.Equal("IN-2026-0001", first.ReferenceNumber);
        Assert.Equal("IN-2026-0002", second.ReferenceNumber);
        Assert.Equal("OUT-2026-0001", release.ReferenceNumber);
    }

    [Fact]
    public async Task Low_stock_is_flagged_at_or_below_the_warning_level()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", reorderLevel: 20, unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 100);
        await app.ReleaseAsync(person.Id, paper.Id, 80);

        var summary = await app.Dashboard.GetAsync();

        Assert.Equal(1, summary.LowStockCount);
        Assert.Equal(20, summary.TotalOnHand);
        Assert.Equal(80, summary.OutstandingQuantity);
        Assert.Equal(1, summary.PeopleHoldingItems);
    }

    [Fact]
    public async Task A_duplicate_item_code_is_rejected()
    {
        await using var app = await InventoryFixture.CreateAsync();
        await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.AddItemAsync("paper", "Another paper", unit: "ream"));

        Assert.Contains("already used", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task Serial_tracking_cannot_be_switched_off_once_serials_exist()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");
        await app.ReceiveAsync(form.Id, 50, 1001, 1050);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.Items.SaveAsync(new SaveItemRequest(
                form.Id, "AF51", "Local receipt", "booklet", null, false, 0, null, true)));

        Assert.Contains("cannot be switched off", string.Join(" ", error.Errors));
    }
}
