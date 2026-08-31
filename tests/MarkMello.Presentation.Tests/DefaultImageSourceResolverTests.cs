using MarkMello.Infrastructure.Images;

namespace MarkMello.Presentation.Tests;

public sealed class DefaultImageSourceResolverTests
{
    [Fact]
    public async Task TryOpenAsyncDecodesBase64DataImageUri()
    {
        var resolver = new DefaultImageSourceResolver();

        await using var stream = await resolver.TryOpenAsync(
            "data:image/png;base64,AQIDBA==",
            baseDirectory: null,
            CancellationToken.None);

        Assert.NotNull(stream);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        Assert.Equal([1, 2, 3, 4], copy.ToArray());
    }

    [Fact]
    public async Task TryOpenAsyncDecodesPercentEscapedBase64DataImageUri()
    {
        var resolver = new DefaultImageSourceResolver();

        await using var stream = await resolver.TryOpenAsync(
            "data:image/png;base64,%2Bw==",
            baseDirectory: null,
            CancellationToken.None);

        Assert.NotNull(stream);
        using var copy = new MemoryStream();
        await stream!.CopyToAsync(copy);
        Assert.Equal([251], copy.ToArray());
    }
}
