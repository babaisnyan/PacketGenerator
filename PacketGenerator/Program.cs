using System.Reflection;
using System.Text;
using CommandLine;
using Humanizer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PacketGenerator.Data;
using Scriban;

namespace PacketGenerator
{
    internal class Program
    {
        private static readonly Dictionary<string, string> CppTypeNames = new()
        {
            {"sbyte", "int8_t"},
            {"byte", "uint8_t"},
            {"short", "int16_t"},
            {"ushort", "uint16_t"},
            {"int", "int32_t"},
            {"uint", "uint32_t"},
            {"long", "int64_t"},
            {"ulong", "uint64_t"},
            {"float", "float"},
            {"double", "double"},
            {"bool", "bool"},
            {"List", "std::vector"},
            {"LinkedList", "std::list"},
            {"Tuple", "std::tuple"},
            {"Dictionary", "std::unordered_map"},
            {"SortedDictionary", "std::map"},
            {"FixedSizeString", "FixedSizeString"}
        };

        private static readonly Dictionary<string, string> CppIncludes = new()
        {
            {"std::vector", "<vector>"},
            {"std::list", "<list>"},
            {"std::tuple", "<tuple>"},
            {"std::unordered_map", "<unordered_map>"},
            {"std::map", "<map>"},
            {"std::string", "<string>"},
            {"std::wstring", "<string>"}
        };

        private static readonly Dictionary<int, string> CppSizeTypes = new()
        {
            {1, "uint8_t"},
            {2, "uint16_t"},
            {4, "uint32_t"},
            {8, "uint64_t"}
        };

        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Option>(args)
                  .WithParsed(Compile)
                  .WithNotParsed(HandleParseError);
        }

        private static void Compile(Option option)
        {
            var path = Path.GetFullPath(option.PacketDefinitionPath);
            var binaryPath = Path.GetFullPath(option.PacketDefinitionAssemblyPath);

            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Directory not found: {path}");
                return;
            }

            if (!Directory.Exists(binaryPath))
            {
                Console.WriteLine($"Directory not found: {binaryPath}");
                return;
            }

            CppTypeNames.Add("string", option.UseWideString ? "std::wstring" : "std::string");

            var sourceFiles = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
            var assemblies = Directory.GetFiles(binaryPath, "*.dll", SearchOption.TopDirectoryOnly);

            var parseOption = new CSharpParseOptions(LanguageVersion.Latest);
            var syntaxTrees = sourceFiles.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOption));
            var references = assemblies.Select(assembly => MetadataReference.CreateFromFile(assembly));
            var compilation = CSharpCompilation.Create("Packets")
                                               .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                                               .AddReferences(references)
                                               .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                                               .AddSyntaxTrees(syntaxTrees);
            var packetInfos = new List<PacketInfo>();
            var messageInfos = new List<MessageInfo>();
            var includes = new HashSet<string>();

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var collectionSymbol = semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.ICollection`1");
                var listSymbol = semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.IList`1");
                var dictionarySymbol = semanticModel.Compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2");
                var types = syntaxTree.GetRoot().DescendantNodes()
                                      .Where(x => x.Kind() == SyntaxKind.ClassDeclaration)
                                      .Cast<ClassDeclarationSyntax>()
                                      .Where(x => x.AttributeLists.Any(y => y.Attributes.Any(z => z.Name.ToString() == "SerializableMessage")))
                                      .ToList();

                if (collectionSymbol == null || listSymbol == null || dictionarySymbol == null)
                {
                    Console.WriteLine("Collection, List, or Dictionary type not found.");
                    return;
                }

                foreach (var syntax in types)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(syntax);

                    if (symbol == null)
                    {
                        continue;
                    }

                    var baseProperties = new List<PropertyDeclarationSyntax>();

                    if (symbol.BaseType.Name != "Object")
                    {
                        var hasAttribute = symbol.BaseType.GetAttributes().Any(x => x.AttributeClass.ToDisplayString().Contains("SerializableMessage"));

                        if (hasAttribute)
                        {
                            baseProperties = symbol.BaseType.GetMembers()
                                                   .SelectMany(x => x.DeclaringSyntaxReferences)
                                                   .SelectMany(x => x.SyntaxTree.GetRoot().DescendantNodes())
                                                   .Where(x => x.Kind() == SyntaxKind.PropertyDeclaration)
                                                   .Distinct()
                                                   .Cast<PropertyDeclarationSyntax>().ToList();
                            semanticModel = compilation.GetSemanticModel(symbol.BaseType.DeclaringSyntaxReferences.First().SyntaxTree);
                        }
                    }

                    var isPacket = syntax.AttributeLists
                                         .SelectMany(x => x.Attributes)
                                         .FirstOrDefault(x => x.Name.ToString() == "SerializableMessage")?.ArgumentList != null;

                    if (isPacket)
                    {
                        var packetInfo = new PacketInfo
                        {
                            IsFromClient = symbol.Name.StartsWith("Cs"),
                            Prefix = symbol.Name[..2],
                            Name = symbol.Name[2..],
                            Type = syntax.AttributeLists.SelectMany(x => x.Attributes)
                                         .Where(x => x.Name.ToString() == "SerializableMessage")
                                         .Select(x => x.ArgumentList?.Arguments.FirstOrDefault()?.Expression)
                                         .OfType<LiteralExpressionSyntax>()
                                         .Select(x => int.Parse(x.Token.ValueText))
                                         .First(),
                            Namespace = symbol.ContainingNamespace.ToDisplayString(Utils.NamespaceDisplayFormat),
                            Modifier = syntax.Modifiers.ToString(),
                            Members = syntax.DescendantNodes()
                                            .Where(x => x.Kind() == SyntaxKind.PropertyDeclaration)
                                            .Cast<PropertyDeclarationSyntax>()
                                            .Concat(baseProperties)
                                            .Select(x =>
                                            {
                                                var member = new PacketMember
                                                {
                                                    Name = x.Identifier.Text,
                                                    CppName = x.Identifier.Text.Underscore(),
                                                    TypeName = x.Type.ToString(),
                                                };
                                                var type = x.Type;

                                                if (x.Type is NullableTypeSyntax nullable)
                                                {
                                                    type = nullable.ElementType;
                                                    member.IsNullable = true;
                                                }

                                                if (type is GenericNameSyntax genericType)
                                                {
                                                    if (semanticModel.GetSymbolInfo(genericType).Symbol is INamedTypeSymbol memberTypeSymbol)
                                                    {
                                                        member.TypeName = genericType.ToString();
                                                        member.GenericTypeNames = GetGenericTypeNames(memberTypeSymbol);
                                                        member.IsCollection = IsCollectionType(memberTypeSymbol, collectionSymbol);
                                                        member.GenericTypeName = GetGenericTypeName(memberTypeSymbol, listSymbol, dictionarySymbol);
                                                        member.TypeId = (uint) member.TypeName.GetHashCode();
                                                        member.CppTypeName = GetCppTypeName(member.GenericTypeName, member.GenericTypeNames);
                                                    }

                                                    if (member.IsCollection)
                                                    {
                                                        var attribute = x.AttributeLists.SelectMany(y => y.Attributes)
                                                                         .FirstOrDefault(y => y.Name.ToString() == "SerializableCollection");

                                                        if (attribute != null)
                                                        {
                                                            var literalSyntax = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression as LiteralExpressionSyntax;
                                                            var size = (int) literalSyntax?.Token.Value!;
                                                            member.CollectionSizeType = CppSizeTypes[size];
                                                        }
                                                    }
                                                }

                                                if (member is {IsCollection: false, GenericTypeNames.Length: <= 1})
                                                {
                                                    member.CppTypeName = CppTypeNames.ContainsKey(type.ToString()) ? CppTypeNames[type.ToString()] : type.ToString();
                                                    member.TypeId = semanticModel.GetTypeInfo(type).Type.GetSymbolTypeCode();
                                                    member.IsFixedString = type.ToString() == "FixedSizeString";
                                                }

                                                if (member.IsNullable)
                                                {
                                                    includes.Add("<optional>");
                                                }

                                                includes.UnionWith(GetIncludes(member.CppTypeName));

                                                return member;
                                            })
                                            .ToList()
                        };

                        packetInfos.Add(packetInfo);
                    }
                    else
                    {
                        if (symbol.Name == "FixedSizeString") continue;

                        var messageInfo = new MessageInfo
                        {
                            Name = symbol.Name,
                            Namespace = symbol.ContainingNamespace.ToDisplayString(Utils.NamespaceDisplayFormat),
                            Modifier = syntax.Modifiers.ToString(),
                            Members = syntax.DescendantNodes()
                                            .Where(x => x.Kind() == SyntaxKind.PropertyDeclaration)
                                            .Cast<PropertyDeclarationSyntax>()
                                            .Concat(baseProperties)
                                            .Select(x =>
                                            {
                                                var member = new PacketMember
                                                {
                                                    Name = x.Identifier.Text,
                                                    CppName = x.Identifier.Text.Underscore(),
                                                    TypeName = x.Type.ToString()
                                                };
                                                var type = x.Type;

                                                if (x.Type is NullableTypeSyntax nullable)
                                                {
                                                    type = nullable.ElementType;
                                                    member.IsNullable = true;
                                                }

                                                if (type is GenericNameSyntax genericType)
                                                {
                                                    if (semanticModel.GetSymbolInfo(genericType).Symbol is INamedTypeSymbol memberTypeSymbol)
                                                    {
                                                        member.TypeName = genericType.ToString();
                                                        member.GenericTypeNames = GetGenericTypeNames(memberTypeSymbol);
                                                        member.IsCollection = IsCollectionType(memberTypeSymbol, collectionSymbol);
                                                        member.GenericTypeName = GetGenericTypeName(memberTypeSymbol, listSymbol, dictionarySymbol);
                                                        member.TypeId = (uint) member.TypeName.GetHashCode();
                                                        member.CppTypeName = GetCppTypeName(member.GenericTypeName, member.GenericTypeNames);
                                                    }

                                                    if (member.IsCollection)
                                                    {
                                                        var attribute = x.AttributeLists.SelectMany(y => y.Attributes)
                                                                         .FirstOrDefault(y => y.Name.ToString() == "SerializableCollection");

                                                        if (attribute != null)
                                                        {
                                                            var literalSyntax = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression as LiteralExpressionSyntax;
                                                            var size = (int) literalSyntax?.Token.Value!;
                                                            member.CollectionSizeType = CppSizeTypes[size];
                                                        }
                                                    }
                                                }

                                                if (member is {IsCollection: false, GenericTypeNames.Length: <= 1})
                                                {
                                                    member.CppTypeName = CppTypeNames.ContainsKey(type.ToString()) ? CppTypeNames[type.ToString()] : type.ToString();
                                                    member.TypeId = semanticModel.GetTypeInfo(type).Type.GetSymbolTypeCode();
                                                    member.IsFixedString = type.ToString() == "FixedSizeString";
                                                }

                                                if (member.IsNullable)
                                                {
                                                    includes.Add("<optional>");
                                                }

                                                includes.UnionWith(GetIncludes(member.CppTypeName));

                                                return member;
                                            })
                                            .ToList()
                        };

                        messageInfos.Add(messageInfo);
                    }
                }
            }

            var packetTemplateText = File.ReadAllText(Path.Combine(option.TemplatePath, "PacketTemplate.scriban"));
            var packetHandlerTemplateText = File.ReadAllText(Path.Combine(option.TemplatePath, "PacketHandlerTemplate.scriban"));
            var packetTemplate = Template.Parse(packetTemplateText);
            var packetHandlerTemplate = Template.Parse(packetHandlerTemplateText);
            var packetResult = packetTemplate.Render(new {UseWide = option.UseWideString, PacketInfoList = packetInfos.OrderBy(x => x.Type), MessageInfoList = messageInfos.OrderBy(x => x.Name), Includes = includes.Order()});
            var packetHandlerResult = packetHandlerTemplate.Render(new {PacketInfoList = packetInfos.OrderBy(x => x.Type), Includes = includes.Order()});

            if (packetResult != null && packetHandlerResult != null)
            {
                var outputPath = Path.GetFullPath(option.OutputPath);
                var packetPath = Path.Combine(outputPath, "packets.h");
                var packetHandlerPath = Path.Combine(outputPath, "packet_handler.h");

                File.WriteAllText(packetPath, packetResult, Encoding.UTF8);
                File.WriteAllText(packetHandlerPath, packetHandlerResult, Encoding.UTF8);
            }
        }

        private static string[] GetGenericTypeNames(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.TypeArguments.Select(t =>
            {
                if (t is INamedTypeSymbol {IsGenericType: true} namedType)
                {
                    return GetCppTypeName(GetGenericTypeName(namedType, null, null), GetGenericTypeNames(namedType));
                }

                return t.ToDisplayString().Contains('.') ? t.Name : t.ToDisplayString();
            }).ToArray();
        }

        private static bool IsCollectionType(INamedTypeSymbol typeSymbol, INamedTypeSymbol collectionSymbol)
        {
            return typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, collectionSymbol));
        }

        private static string GetGenericTypeName(INamedTypeSymbol typeSymbol, INamedTypeSymbol listSymbol, INamedTypeSymbol dictionarySymbol)
        {
            if (typeSymbol.AllInterfaces.Any(i =>　SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, listSymbol)))
            {
                return "List";
            }

            if (typeSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, dictionarySymbol)))
            {
                return "Dictionary";
            }

            return typeSymbol.Name;
        }

        private static string GetCppTypeName(string genericTypeName, string[] genericTypeNames)
        {
            if (!CppTypeNames.TryGetValue(genericTypeName, out var cppTypeName))
            {
                return genericTypeName;
            }

            if (genericTypeNames.Length == 0)
            {
                return cppTypeName;
            }

            return $"{cppTypeName}<{string.Join(", ", genericTypeNames.Select(t => CppTypeNames.GetValueOrDefault(t, t)))}>";
        }

        private static IEnumerable<string> GetIncludes(string typename)
        {
            return CppIncludes.Select(x => typename.Contains(x.Key) ? x.Value : string.Empty)
                              .Where(x => !string.IsNullOrEmpty(x));
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Console.WriteLine(error);
            }
        }
    }
}