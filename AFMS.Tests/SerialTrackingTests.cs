using AFMS.Core;
using AFMS.Core.Domain;

namespace AFMS.Tests;

/// <summary>
/// The behaviour the office depends on: never release a receipt serial that is
/// still out with somebody, and say who has it.
/// </summary>
public sealed class SerialTrackingTests
{
    [Fact]
    public async Task Releasing_the_same_serial_twice_is_blocked_and_names_the_holder()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");
        var marigold = await app.AddPersonAsync("AU", "Marigold L. Dela Cruz");
        var shella = await app.AddPersonAsync("SMV", "Shella May I. Velasco");

        await app.ReceiveAsync(form.Id, 100, 1001, 1100);
        await app.ReleaseAsync(marigold.Id, form.Id, 50, 1001, 1050);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.ReleaseAsync(shella.Id, form.Id, 50, 1001, 1050));

        var message = string.Join(" ", error.Errors);
        Assert.Contains("1001-1050", message);
        Assert.Contains("Marigold L. Dela Cruz", message);
    }

    [Fact]
    public async Task A_partly_overlapping_serial_range_is_also_blocked()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF56", "RPT receipt", tracksSerials: true, unit: "booklet");
        var first = await app.AddPersonAsync("A", "First Collector");
        var second = await app.AddPersonAsync("B", "Second Collector");

        await app.ReceiveAsync(form.Id, 100, 1, 100);
        await app.ReleaseAsync(first.Id, form.Id, 50, 1, 50);

        // 40-60 straddles the edge of what is already out.
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.ReleaseAsync(second.Id, form.Id, 21, 40, 60));

        Assert.Contains("40-50", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task A_free_serial_range_is_allowed()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");
        var first = await app.AddPersonAsync("A", "First Collector");
        var second = await app.AddPersonAsync("B", "Second Collector");

        await app.ReceiveAsync(form.Id, 100, 1001, 1100);
        await app.ReleaseAsync(first.Id, form.Id, 50, 1001, 1050);

        var release = await app.ReleaseAsync(second.Id, form.Id, 50, 1051, 1100);

        Assert.Equal(50, release.TotalQuantity);
        Assert.Equal("1051-1100", release.Lines.Single().SerialLabel);
    }

    [Fact]
    public async Task A_returned_serial_range_can_be_released_again()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");
        var marigold = await app.AddPersonAsync("AU", "Marigold L. Dela Cruz");
        var shella = await app.AddPersonAsync("SMV", "Shella May I. Velasco");

        await app.ReceiveAsync(form.Id, 50, 1001, 1050);
        await app.ReleaseAsync(marigold.Id, form.Id, 50, 1001, 1050);

        // Marigold hands the unused booklet back.
        await app.ReceiveAsync(
            form.Id, 50, 1001, 1050,
            source: StockInSource.Returned, returnedBy: marigold.Id);

        // It is now free to go to somebody else.
        var release = await app.ReleaseAsync(shella.Id, form.Id, 50, 1001, 1050);

        Assert.Equal("1001-1050", release.Lines.Single().SerialLabel);
    }

    [Fact]
    public async Task Only_the_unreturned_part_of_a_range_stays_blocked()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");
        var marigold = await app.AddPersonAsync("AU", "Marigold");
        var shella = await app.AddPersonAsync("SMV", "Shella");

        await app.ReceiveAsync(form.Id, 50, 1001, 1050);
        await app.ReleaseAsync(marigold.Id, form.Id, 50, 1001, 1050);

        // Only the first half comes back.
        await app.ReceiveAsync(
            form.Id, 25, 1001, 1025,
            source: StockInSource.Returned, returnedBy: marigold.Id);

        // The returned half is reusable.
        await app.ReleaseAsync(shella.Id, form.Id, 25, 1001, 1025);

        // The half Marigold still holds is not.
        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.ReleaseAsync(shella.Id, form.Id, 25, 1026, 1050));
        Assert.Contains("1026-1050", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task Voiding_a_release_frees_its_serials()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");
        var first = await app.AddPersonAsync("A", "First");
        var second = await app.AddPersonAsync("B", "Second");

        await app.ReceiveAsync(form.Id, 50, 1001, 1050);
        var wrong = await app.ReleaseAsync(first.Id, form.Id, 50, 1001, 1050);

        await app.Releases.VoidAsync(wrong.Id, "Encoded against the wrong collector.");

        var corrected = await app.ReleaseAsync(second.Id, form.Id, 50, 1001, 1050);
        Assert.Equal("1001-1050", corrected.Lines.Single().SerialLabel);
    }

    [Fact]
    public async Task Editing_a_release_does_not_clash_with_its_own_serials()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");
        var person = await app.AddPersonAsync("A", "Collector");

        await app.ReceiveAsync(form.Id, 100, 1001, 1100);
        var release = await app.ReleaseAsync(person.Id, form.Id, 50, 1001, 1050);

        // Re-saving the same release with a wider range must not trip over itself.
        var updated = await app.Releases.SaveAsync(new SaveReleaseRequest(
            release.Id,
            release.ReleaseDate,
            person.Id,
            "Corrected quantity",
            "Test Secretary",
            "Collector",
            null,
            [new StockLineInput(form.Id, 60, 1001, 1060, null)]));

        Assert.Equal("1001-1060", updated.Lines.Single().SerialLabel);
    }

    [Fact]
    public async Task The_same_delivery_cannot_be_entered_twice()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");

        await app.ReceiveAsync(form.Id, 50, 1001, 1050);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.ReceiveAsync(form.Id, 50, 1001, 1050));

        Assert.Contains("already received", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task Two_lines_of_one_release_cannot_share_a_serial()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");
        var person = await app.AddPersonAsync("A", "Collector");

        await app.ReceiveAsync(form.Id, 100, 1001, 1100);

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
                    new StockLineInput(form.Id, 50, 1001, 1050, null),
                    new StockLineInput(form.Id, 50, 1025, 1074, null)
                ])));

        Assert.Contains("another line", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task The_serial_count_must_match_the_quantity()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.ReceiveAsync(form.Id, 40, 1001, 1050));

        var message = string.Join(" ", error.Errors);
        Assert.Contains("50 piece", message);
        Assert.Contains("40", message);
    }

    [Fact]
    public async Task A_serial_tracked_item_requires_a_serial_range()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.ReceiveAsync(form.Id, 50));

        Assert.Contains("enter the serial range", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task A_plain_item_rejects_serial_numbers()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var paper = await app.AddItemAsync("PAPER", "Bond paper A4", tracksSerials: false, unit: "ream");

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => app.ReceiveAsync(paper.Id, 10, 1, 10));

        Assert.Contains("does not use serial numbers", string.Join(" ", error.Errors));
    }

    [Fact]
    public async Task The_live_check_warns_before_saving()
    {
        await using var app = await InventoryFixture.CreateAsync();
        var form = await app.AddItemAsync("AF51", "Local receipt", tracksSerials: true, unit: "booklet");
        var person = await app.AddPersonAsync("AU", "Marigold");

        await app.ReceiveAsync(form.Id, 100, 1001, 1100);
        await app.ReleaseAsync(person.Id, form.Id, 50, 1001, 1050);

        var taken = await app.Releases.CheckSerialAvailabilityAsync(form.Id, 1001, 1050);
        Assert.NotNull(taken);
        Assert.Contains("Marigold", taken);

        var free = await app.Releases.CheckSerialAvailabilityAsync(form.Id, 1051, 1100);
        Assert.Null(free);
    }
}
