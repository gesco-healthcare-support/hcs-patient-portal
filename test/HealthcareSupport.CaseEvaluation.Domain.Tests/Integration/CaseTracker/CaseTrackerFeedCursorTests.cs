using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>The feed cursor's wire form (#927): strict, opaque, and a lossless round trip.</summary>
public class CaseTrackerFeedCursorTests
{
    [Theory]
    [InlineData(0L, "0000000000000000")]
    [InlineData(2003L, "00000000000007D3")]
    [InlineData(long.MaxValue, "7FFFFFFFFFFFFFFF")]
    public void Encode_WritesSixteenUpperCaseHexDigits(long position, string expected)
    {
        CaseTrackerFeedCursor.Encode(position).ShouldBe(expected);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(123456789L)]
    [InlineData(long.MaxValue)]
    public void Decode_OfEncode_IsTheIdentity(long position)
    {
        CaseTrackerFeedCursor.TryDecode(CaseTrackerFeedCursor.Encode(position), out var decoded).ShouldBeTrue();
        decoded.ShouldBe(position);
    }

    [Fact]
    public void TryDecode_AcceptsLowerCaseHex()
    {
        // Only the digits matter; a consumer that lower-cases a string it was told is opaque is still right.
        CaseTrackerFeedCursor.TryDecode("00000000000007d3", out var decoded).ShouldBeTrue();
        decoded.ShouldBe(2003);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7D3")] // too short
    [InlineData("000000000000007D3")] // too long
    [InlineData(" 00000000000007D3")] // whitespace is not a digit
    [InlineData("00000000000007G3")] // not hex
    [InlineData("0x000000000007D3")] // prefix
    [InlineData("8000000000000000")] // above long.MaxValue: never a real position
    public void TryDecode_RefusesAnythingButSixteenHexDigits(string? value)
    {
        CaseTrackerFeedCursor.TryDecode(value, out _).ShouldBeFalse();
    }

    [Fact]
    public void Encode_RefusesANegativePosition()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CaseTrackerFeedCursor.Encode(-1));
    }
}
