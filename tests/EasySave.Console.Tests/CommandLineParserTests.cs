using System.Collections.Generic;
using EasySave.Console.Cli;
using Xunit;

namespace EasySave.Console.Tests {

public sealed class CommandLineParserTests
{
    // --- ShouldRunTui tests ---

    [Fact]
    public void ShouldRunTui_ReturnsTrue_WhenArgsAreNull()
    {
        bool result = CommandLineParser.ShouldRunTui(null);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRunTui_ReturnsTrue_WhenNoArgs()
    {
        string[] args = { };

        bool result = CommandLineParser.ShouldRunTui(args);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRunTui_ReturnsTrue_WhenFirstArgIsTui_CaseInsensitive()
    {
        string[] argsLower = { "--tui" };
        string[] argsUpper = { "--TUI" };
        string[] argsMixed = { "--TuI" };

        Assert.True(CommandLineParser.ShouldRunTui(argsLower));
        Assert.True(CommandLineParser.ShouldRunTui(argsUpper));
        Assert.True(CommandLineParser.ShouldRunTui(argsMixed));
    }

    [Fact]
    public void ShouldRunTui_ReturnsFalse_WhenFirstArgIsNotTui()
    {
        string[] args = { "1-3" };

        bool result = CommandLineParser.ShouldRunTui(args);

        Assert.False(result);
    }

    // --- Parse tests ---

    [Fact]
    public void Parse_ReturnsEmptyList_WhenNoArgs()
    {
        string[] args = { };

        IReadOnlyList<int> result = CommandLineParser.Parse(args);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_ReturnsEmptyList_WhenArgsContainOnlyWhitespace()
    {
        string[] args = { "   " };

        IReadOnlyList<int> result = CommandLineParser.Parse(args);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_ParsesCommaSeparatedList()
    {
        string[] args = { "1,3,5" };

        IReadOnlyList<int> result = CommandLineParser.Parse(args);

        Assert.Equal(new List<int> { 1, 3, 5 }, result);
    }

    [Fact]
    public void Parse_ParsesSemicolonSeparatedList()
    {
        string[] args = { "1;3;5" };

        IReadOnlyList<int> result = CommandLineParser.Parse(args);

        Assert.Equal(new List<int> { 1, 3, 5 }, result);
    }

    [Fact]
    public void Parse_IgnoresOutOfRangeAndDuplicates_InList()
    {
        string[] args = { "0,1,1,6,3" };

        IReadOnlyList<int> result = CommandLineParser.Parse(args);

        Assert.Equal(new List<int> { 1, 6, 3 }, result);
    }

    [Fact]
    public void Parse_ParsesRangeExpression_InAscendingOrder()
    {
        string[] args = { "1-3" };

        IReadOnlyList<int> result = CommandLineParser.Parse(args);

        Assert.Equal(new List<int> { 1, 2, 3 }, result);
    }

    [Fact]
    public void Parse_ParsesRangeExpression_InDescendingOrder()
    {
        string[] args = { "3-1" };

        IReadOnlyList<int> result = CommandLineParser.Parse(args);

        Assert.Equal(new List<int> { 1, 2, 3 }, result);
    }

    [Fact]
    public void Parse_ClampsRange_ToAllowedJobIds()
    {
        string[] args = { "0-10" };

        IReadOnlyList<int> result = CommandLineParser.Parse(args);

        Assert.Equal(new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, result);
    }

    [Fact]
    public void Parse_ReturnsEmptyList_WhenRangeIsInvalid()
    {
        string[] args = { "1-abc" };

        IReadOnlyList<int> result = CommandLineParser.Parse(args);

        Assert.Empty(result);
    }
}
}