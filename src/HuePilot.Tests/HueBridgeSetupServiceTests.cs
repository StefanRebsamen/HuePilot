using HuePilot.Infrastructure.Hue;

namespace HuePilot.Tests;

public sealed class HueBridgeSetupServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-an-ip")]
    public async Task PairAsync_IpAddressIsInvalid_ThrowsArgumentExceptionBeforeNetworkCall(string ipAddress)
    {
        var setup = new HueBridgeSetupService();

        await Assert.ThrowsAsync<ArgumentException>(() => setup.PairAsync(ipAddress));
    }
}
