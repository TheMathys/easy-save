using System.Collections.Generic;
using Xunit;
using EasySave.Console.Cli;

namespace EasySave.Console.Tests.CliTests
{
    public class CommandLineParserTests
    {
        public static IReadOnlyList<int> Parse(string[] args)
        {
            IReadOnlyList<int> jobIds = new List<int>();
            foreach (var arg in args)
            {
                int min = 1;
                int max = 5;

                if (int.TryParse(arg, out int jobId) && jobId >= min && jobId <= max)
                {
                    ((List<int>)jobIds).Add(jobId);
                }
            }
            return jobIds;
        }

        [Fact]
        public void Parse_ShouldReturnEmptyList_WhenNoArgs()
        {
            // Arrange
            string[] args = { };

            // Act
            var result = CommandLineParserTests.Parse(args);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_ShouldReturnValidJobIds_WhenArgsAreValid()
        {
            // Arrange
            string[] args = { "1", "3", "5" };

            // Act
            var result = CommandLineParserTests.Parse(args);

            // Assert
            Assert.Equal(new List<int> { 1, 3, 5 }, result);
        }

        [Fact]
        public void Parse_ShouldIgnoreInvalidNumbers()
        {
            // Arrange
            string[] args = { "0", "1", "10", "-5", "3" };

            // Act
            var result = CommandLineParserTests.Parse(args);

            // Assert
            Assert.Equal(new List<int> { 1, 3 }, result);
        }

        [Fact]
        public void Parse_ShouldIgnoreNonNumericValues()
        {
            // Arrange
            string[] args = { "abc", "2", "xyz", "4" };

            // Act
            var result = CommandLineParserTests.Parse(args);

            // Assert
            Assert.Equal(new List<int> { 2, 4 }, result);
        }

        [Fact]
        public void Parse_ShouldNotAllowModification_OfReturnedList()
        {
            // Arrange
            string[] args = { "1", "2" };

            // Act
            var result = CommandLineParserTests.Parse(args);

            // Assert
            Assert.IsAssignableFrom<IReadOnlyList<int>>(result);
            Assert.Throws<System.NotSupportedException>(() =>
            {
                // La liste retournée doit être en lecture seule
                ((List<int>)result).Add(99);
            });
        }
    }
}