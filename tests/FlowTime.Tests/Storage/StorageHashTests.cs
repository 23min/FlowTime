using FlowTime.Contracts.Storage;

namespace FlowTime.Tests.Storage;

public sealed class StorageHashTests
{
    [Fact]
    public void ComputeSha256_ReturnsExpectedHash()
    {
        var hash = StorageHash.ComputeSha256("hello");

        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("xyz")]
    [InlineData("123")]
    public void IsValid_ReturnsFalse_ForInvalidHash(string value)
    {
        Assert.False(StorageHash.IsValid(value));
    }

    [Fact]
    public void IsValid_ReturnsTrue_ForLowercaseHex()
    {
        var hash = StorageHash.ComputeSha256("hello");

        Assert.True(StorageHash.IsValid(hash));
    }
}
