using CompanyHQ.Cli;

namespace UnitTests;

public sealed class CliArgumentParserTests
{
    [Fact]
    public void ParsesAllFlags()
    {
        var result = CliArgumentParser.Parse([
            "--c", "Acme", "--u", "deploy", "--sip", "10.0.0.2",
            "--p", "secret", "--conf", "keys", "--root", "server"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new CliArguments(
                CliCommand.CreateCompany, "Acme", null, "deploy", "10.0.0.2", "secret", "keys", "server"),
            result.Arguments);
    }

    [Fact]
    public void RejectsMissingCompany()
    {
        var result = CliArgumentParser.Parse(["--root", "server"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--c", result.Error);
    }

    [Fact]
    public void ParsesServiceVerbWithoutCompany()
    {
        var result = CliArgumentParser.Parse(["service", "Billing"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(CliCommand.ScaffoldService, result.Arguments!.Command);
        Assert.Equal("Billing", result.Arguments.ServiceName);
        // Left null on purpose: Program fills it from the working directory.
        Assert.Null(result.Arguments.CompanyName);
    }

    [Fact]
    public void ParsesServiceVerbWithExplicitCompanyAndRoot()
    {
        var result = CliArgumentParser.Parse(["service", "Billing", "--c", "Acme", "--root", "server"]);

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme", result.Arguments!.CompanyName);
        Assert.Equal("server", result.Arguments.ServerRoot);
    }

    [Fact]
    public void RejectsServiceVerbWithoutName()
    {
        var result = CliArgumentParser.Parse(["service", "--c", "Acme"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("ServiceName", result.Error);
    }

    [Fact]
    public void RejectsRemoteOptionsOnServiceVerb()
    {
        var result = CliArgumentParser.Parse(["service", "Billing", "--sip", "10.0.0.2", "--u", "deploy"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("remote", result.Error);
    }
}
