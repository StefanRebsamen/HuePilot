using HuePilot.Application;

namespace HuePilot.Tests;

public sealed class AiApproachTests
{
    [Theory]
    [InlineData(AiApproach.Structured, "Structured output")]
    [InlineData(AiApproach.Tools, "Tool calling")]
    public void GetDisplayName_ReturnsTheApproachShownInChatTechnicalDetails(AiApproach approach, string expected)
    {
        Assert.Equal(expected, approach.GetDisplayName());
    }

    [Fact]
    public void FormatTechnicalDetails_IncludesOnlyTheApproachWhenDiagnosticsAreDisabled()
    {
        var details = AiTechnicalDetails.Format(AiApproach.Tools, "sensitive diagnostic output", includeDiagnostics: false);

        Assert.Equal("AI approach: Tool calling", details);
    }

    [Fact]
    public void FormatTechnicalDetails_AppendsDiagnosticsWhenTheyAreEnabled()
    {
        var details = AiTechnicalDetails.Format(AiApproach.Structured, "model response", includeDiagnostics: true);

        Assert.Equal($"AI approach: Structured output{Environment.NewLine}{Environment.NewLine}model response", details);
    }
}
