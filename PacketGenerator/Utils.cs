using Microsoft.CodeAnalysis;

namespace PacketGenerator;

public static class Utils
{
    public static readonly SymbolDisplayFormat NamespaceDisplayFormat = new(
        SymbolDisplayGlobalNamespaceStyle.Omitted,
        SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        SymbolDisplayGenericsOptions.IncludeTypeParameters,
        SymbolDisplayMemberOptions.IncludeParameters,
        SymbolDisplayDelegateStyle.NameAndSignature,
        SymbolDisplayExtensionMethodStyle.Default,
        SymbolDisplayParameterOptions.IncludeType,
        SymbolDisplayPropertyStyle.NameOnly,
        SymbolDisplayLocalOptions.None,
        SymbolDisplayKindOptions.None,
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes);


    public static uint GetSymbolTypeCode(this ITypeSymbol symbol)
    {
        var path = $"{symbol.ContainingNamespace.ToDisplayString(NamespaceDisplayFormat)}.{symbol.Name}";
        var hash = (uint) path.GetHashCode();

        return hash;
    }
}