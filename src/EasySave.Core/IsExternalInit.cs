// Polyfill pour init / record sur netstandard2.x (IsExternalInit est inclus dans .NET 5+)
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
