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
            var result = LangHelper.GetString("BackupCancel");

            Assert.NotNull(result);
            Assert.Contains("Cancel the backup.", result); 
        }

        [Fact]
        public void GetString_NonExistingKey_ReturnsNull()
        {
            var result = LangHelper.GetString("NonExistentKey");

            Assert.Null(result);
        }

        [Fact]
        public void GetString_NullKey_ReturnsNull()
        {
            Assert.Null(LangHelper.GetString(null));
        }
    }
}