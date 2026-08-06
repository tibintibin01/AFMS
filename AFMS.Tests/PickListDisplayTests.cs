using AFMS.Core;

namespace AFMS.Tests;

/// <summary>
/// Anything shown in a dropdown must read as plain text. A C# record's generated
/// ToString prints every field, which leaks onto the screen as text like
/// "ItemOption { Id = 00000000-... }" wherever a control falls back to it.
/// This slipped into a release once, so it is pinned here.
/// </summary>
public sealed class PickListDisplayTests
{
    [Fact]
    public void An_item_option_reads_as_code_and_name()
    {
        var option = new ItemOption(Guid.NewGuid(), "AF51", "Local receipt", "booklet", true, 50);

        Assert.Equal("AF51 - Local receipt", option.Display);
        Assert.Equal(option.Display, option.ToString());
    }

    [Fact]
    public void A_recipient_option_reads_as_code_and_name()
    {
        var option = new RecipientOption(Guid.NewGuid(), "AU", "Marigold L. Dela Cruz");

        Assert.Equal("AU - Marigold L. Dela Cruz", option.Display);
        Assert.Equal(option.Display, option.ToString());
    }

    [Theory]
    [MemberData(nameof(EverythingShownInADropdown))]
    public void Nothing_in_a_dropdown_leaks_its_field_names(object option)
    {
        var text = option.ToString();

        Assert.False(string.IsNullOrWhiteSpace(text));

        // The record ToString format is "TypeName { Prop = value, ... }".
        Assert.DoesNotContain("{", text);
        Assert.DoesNotContain("Id =", text);
        Assert.DoesNotContain(option.GetType().Name, text);
    }

    public static TheoryData<object> EverythingShownInADropdown() =>
    [
        new ItemOption(Guid.NewGuid(), "PAPER", "Bond paper", "ream", false, 10),
        new ItemOption(Guid.Empty, "ALL", "All items", string.Empty, false, 0),
        new RecipientOption(Guid.NewGuid(), "SMV", "Shella May I. Velasco"),
        new RecipientOption(Guid.Empty, "ALL", "Everyone")
    ];
}
