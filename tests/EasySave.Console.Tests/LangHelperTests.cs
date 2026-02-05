using System.Globalization;
using System.Reflection;
using System.Threading;
using EasySave.Console.Resources;
using Xunit;

namespace EasySave.Tests
{
    public class LangHelperTests
    {
        private CultureInfo _originalCulture;
        private CultureInfo _originalUICulture;

        public LangHelperTests()
        {
            _originalCulture = Thread.CurrentThread.CurrentCulture;
            _originalUICulture = Thread.CurrentThread.CurrentUICulture;
        }

        [Fact]
        public void GetString_ExistingKey_ReturnsCorrectString()
        {
            // Arrange : Assure-toi d'avoir Strings.resx avec une clé "Welcome"
            // Act
            var result = LangHelper.GetString("Welcome");

            // Assert : Adapte selon ta vraie valeur dans Strings.resx
            Assert.NotNull(result);
            Assert.Contains("Welcome", result); // Ou la vraie valeur
        }

        [Fact]
        public void GetString_NonExistingKey_ReturnsNull()
        {
            // Act
            var result = LangHelper.GetString("NonExistentKey");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetString_NullKey_ReturnsNull()
        {
            // Act & Assert
            Assert.Null(LangHelper.GetString(null));
        }
    }
}