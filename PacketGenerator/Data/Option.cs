using CommandLine;

namespace PacketGenerator.Data;

public class Option
{
    [Option('p', "path", Required = true, HelpText = "Path of the packet definition file.")]
    public required string PacketDefinitionPath { get; init; }

    [Option('a', "assembly", Required = true, HelpText = "Path of the packet definition assembly.")]
    public required string PacketDefinitionAssemblyPath { get; init; }

    [Option('o', "output", Required = true, HelpText = "Output path of the generated files.")]
    public required string OutputPath { get; init; }

    [Option('t', "template", Required = true, HelpText = "Path of the template directory.")]
    public required string TemplatePath { get; init; }

    [Option("use-wide-string", Default = false, HelpText = "Use wide string.")]
    public required bool UseWideString { get; init; }
}