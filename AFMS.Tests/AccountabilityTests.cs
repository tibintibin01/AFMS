using AFMS.Core;
using AFMS.Core.Domain;

namespace AFMS.Tests;

/// <summary>Covers "what does this person still hold?" and the balances behind it.</summary>
public sealed class AccountabilityTests
{
    [Fact]
    public async Task Outstanding_is_what_was_released_minus_what_came_back()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper A4", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 100);
        await app.ReleaseAsync(person.Id, paper.Id, 30);
        await app.ReceiveAsync(paper.Id, 10, source: StockInSource.Returned, returnedBy: person.Id);

        var held = await app.Accountability.GetForRecipientAsync(person.Id);

        Assert.NotNull(held);
        var row = Assert.Single(held.Items);
        Assert.Equal(30, row.Released);
        Assert.Equal(10, row.Returned);
        Assert.Equal(20, row.Outstanding);
        Assert.Equal(20, held.TotalQuantity);
    }

    [Fact]
    public async Task Someone_who_returned_everything_drops_off_the_outstanding_list()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 100);
        await app.ReleaseAsync(person.Id, paper.Id, 30);
        await app.ReceiveAsync(paper.Id, 30, source: StockInSource.Returned, returnedBy: person.Id);

        Assert.Empty(await app.Accountability.ListOutstandingAsync());
    }

    [Fact]
    public async Task Outstanding_lists_the_exact_serials_a_person_holds()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(form.Id, 100, 1001, 1100);
        await app.ReleaseAsync(person.Id, form.Id, 50, 1001, 1050);
        await app.ReleaseAsync(person.Id, form.Id, 20, 1081, 1100);
        await app.ReceiveAsync(
            form.Id, 25, 1001, 1025,
            source: StockInSource.Returned, returnedBy: person.Id);

        var held = await app.Accountability.GetForRecipientAsync(person.Id);

        var row = Assert.Single(held!.Items);
        Assert.Equal(45, row.Outstanding);
        Assert.Equal("1026-1050, 1081-1100", row.SerialsHeld);
    }

    [Fact]
    public async Task One_persons_return_does_not_clear_anothers_accountability()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var first = await app.AddPersonAsync("A", "First");
        var second = await app.AddPersonAsync("B", "Second");

        await app.ReceiveAsync(paper.Id, 100);
        await app.ReleaseAsync(first.Id, paper.Id, 20);
        await app.ReleaseAsync(second.Id, paper.Id, 20);
        await app.ReceiveAsync(paper.Id, 20, source: StockInSource.Returned, returnedBy: first.Id);

        var outstanding = await app.Accountability.ListOutstandingAsync();

        var remaining = Assert.Single(outstanding);
        Assert.Equal("Second", remaining.RecipientName);
        Assert.Equal(20, remaining.TotalQuantity);
    }

    [Fact]
    public async Task A_voided_release_no_longer_counts_as_accountability()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 100);
        var release = await app.ReleaseAsync(person.Id, paper.Id, 30);

        await app.Releases.VoidAsync(release.Id, "Wrong person encoded.");

        Assert.Empty(await app.Accountability.ListOutstandingAsync());
    }

    [Fact]
    public async Task A_person_holding_stock_cannot_be_hidden()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(paper.Id, 100);
        await app.ReleaseAsync(person.Id, paper.Id, 30);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.Recipients.SetActiveAsync(person.Id, false));

        Assert.Contains("still holds", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task A_return_must_say_who_returned_it()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper", unit: "ream");

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.ReceiveAsync(paper.Id, 10, source: StockInSource.Returned));

        Assert.Contains("who returned", string.Join(" ", error.Errors));
    }
}
