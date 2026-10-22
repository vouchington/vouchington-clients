using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;

namespace Voucha.Client.WindowsLanguageModelSurface.Tests;

internal static class WindowsLanguageModelSurfaceCatalog
{
  private static readonly string[] ForbiddenFragments =
  [
    "GenerateStructured",
    "MaxOutput",
    "OutputLimit",
    "ResponseLength",
    "Structured",
    "Token",
  ];

  internal static WindowsLanguageModelSurfaceReport Read(string assetsPath)
  {
    using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
    var root = document.RootElement;
    var folders = root.GetProperty("packageFolders").EnumerateObject().Select(folder => folder.Name).ToArray();
    var libraries = root.GetProperty("libraries");
    var sdk = OneLibrary(libraries, "Microsoft.WindowsAppSDK/");
    var ai = OneLibrary(libraries, "Microsoft.WindowsAppSDK.AI/");
    var nuspec = FindFile(folders, sdk.Path, sdk.Files.Single(file => file.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)));
    var metadata = ai.Files
        .Where(file => file.StartsWith("metadata/", StringComparison.Ordinal) && file.EndsWith(".winmd", StringComparison.OrdinalIgnoreCase))
        .Select(file => FindFile(folders, ai.Path, file))
        .ToArray();
    var textWinMd = metadata.Single(path => path.EndsWith("Microsoft.Windows.AI.Text.winmd", StringComparison.OrdinalIgnoreCase));
    var safetyWinMd = metadata.Single(path => path.EndsWith("Microsoft.Windows.AI.ContentSafety.winmd", StringComparison.OrdinalIgnoreCase));
    var forbidden = metadata.SelectMany(ForbiddenMembers).Distinct(StringComparer.Ordinal).ToArray();
    Array.Sort(forbidden, StringComparer.Ordinal);
    return new(
        sdk.Version,
        ai.Version,
        AiDependencyVersion(nuspec),
        Signatures(textWinMd, "Microsoft.Windows.AI.Text.LanguageModel"),
        Properties(textWinMd, "Microsoft.Windows.AI.Text.LanguageModelOptions"),
        Fields(textWinMd, "Microsoft.Windows.AI.Text.LanguageModelResponseStatus"),
        Properties(safetyWinMd, "Microsoft.Windows.AI.ContentSafety.ContentFilterOptions"),
        ParameterLists(textWinMd, "Microsoft.Windows.AI.Text.LanguageModel", "GetUsablePromptLength"),
        ParameterLists(textWinMd, "Microsoft.Windows.AI.Text.LanguageModel", "CreateContext"),
        ParameterLists(textWinMd, "Microsoft.Windows.AI.Text.TextSummarizer", "IsPromptLargerThanContext"),
        forbidden);
  }

  private static string AiDependencyVersion(string nuspecPath)
  {
    var matches = XDocument.Load(nuspecPath).Descendants()
        .Where(element => element.Name.LocalName == "dependency" && element.Attribute("id")?.Value == "Microsoft.WindowsAppSDK.AI")
        .ToArray();
    if (matches.Length != 1) throw new InvalidOperationException($"Expected one AI dependency in {nuspecPath}.");
    return matches[0].Attribute("version")?.Value ?? throw new InvalidOperationException($"Missing AI dependency version in {nuspecPath}.");
  }

  private static Library OneLibrary(JsonElement libraries, string prefix)
  {
    Library? found = null;
    foreach (var library in libraries.EnumerateObject())
    {
      if (!library.Name.StartsWith(prefix, StringComparison.Ordinal)) continue;
      if (found is not null) throw new InvalidOperationException($"Resolved more than one {prefix} package.");
      var slash = library.Name.IndexOf('/', StringComparison.Ordinal);
      var files = library.Value.GetProperty("files").EnumerateArray().Select(file => file.GetString() ?? "").ToArray();
      found = new(library.Name[(slash + 1)..], library.Value.GetProperty("path").GetString() ?? "", files);
    }
    return found ?? throw new InvalidOperationException($"Restored assets did not contain {prefix}.");
  }

  private static string FindFile(IReadOnlyList<string> folders, string packagePath, string relativeFile)
  {
    foreach (var folder in folders)
    {
      var path = Path.Combine(folder, packagePath, relativeFile);
      if (File.Exists(path)) return path;
    }
    throw new FileNotFoundException($"Missing restored package file {relativeFile}.");
  }

  private static string[] Signatures(string winmdPath, string fullName) =>
      Read(winmdPath, (reader, type) => type.GetMethods()
          .Select(reader.GetMethodDefinition)
          .Select(method => (Name: reader.GetString(method.Name), Signature: method.DecodeSignature(SignatureProvider.Instance, 0)))
          .Where(method => method.Name.Length > 0 && method.Name[0] is not ('.' or '<') && !method.Name.StartsWith("get_", StringComparison.Ordinal) && !method.Name.StartsWith("put_", StringComparison.Ordinal))
          .Select(method => $"{method.Name}({string.Join(", ", method.Signature.ParameterTypes)}) -> {method.Signature.ReturnType}")
          .ToArray(), fullName);

  private static string[] Properties(string winmdPath, string fullName) =>
      Read(winmdPath, (reader, type) => type.GetProperties()
          .Select(reader.GetPropertyDefinition)
          .Select(property => $"{reader.GetString(property.Name)} -> {property.DecodeSignature(SignatureProvider.Instance, 0).ReturnType}")
          .ToArray(), fullName);

  private static string[] Fields(string winmdPath, string fullName) =>
      Read(winmdPath, (reader, type) => type.GetFields()
          .Select(reader.GetFieldDefinition)
          .Select(field => reader.GetString(field.Name))
          .Where(name => name != "value__")
          .ToArray(), fullName);

  private static string[] ParameterLists(string winmdPath, string fullName, string methodName) =>
      Read(winmdPath, (reader, type) => type.GetMethods()
          .Select(reader.GetMethodDefinition)
          .Where(method => reader.GetString(method.Name) == methodName)
          .Select(method => "(" + string.Join(", ", method.GetParameters().Select(reader.GetParameter).Where(parameter => parameter.SequenceNumber > 0).OrderBy(parameter => parameter.SequenceNumber).Select(parameter => reader.GetString(parameter.Name))) + ")")
          .ToArray(), fullName);

  private static string[] ForbiddenMembers(string winmdPath)
  {
    using var stream = File.OpenRead(winmdPath);
    using var pe = new PEReader(stream);
    var reader = pe.GetMetadataReader();
    var found = new List<string>();
    foreach (var handle in reader.TypeDefinitions)
    {
      var type = reader.GetTypeDefinition(handle);
      var typeName = reader.GetString(type.Name);
      Collect(found, typeName, typeName);
      foreach (var methodHandle in type.GetMethods())
      {
        var method = reader.GetMethodDefinition(methodHandle);
        var methodName = reader.GetString(method.Name);
        Collect(found, typeName, methodName);
        foreach (var parameter in method.GetParameters().Select(reader.GetParameter).Where(parameter => parameter.SequenceNumber > 0))
          Collect(found, typeName + "." + methodName, reader.GetString(parameter.Name));
      }
      foreach (var property in type.GetProperties().Select(reader.GetPropertyDefinition))
        Collect(found, typeName, reader.GetString(property.Name));
      foreach (var field in type.GetFields().Select(reader.GetFieldDefinition))
        Collect(found, typeName, reader.GetString(field.Name));
    }
    return [.. found];
  }

  private static void Collect(List<string> found, string owner, string name)
  {
    if (ForbiddenFragments.Any(fragment => name.Contains(fragment, StringComparison.Ordinal)))
      found.Add(owner + "." + name);
  }

  private static string[] Read(string winmdPath, Func<MetadataReader, TypeDefinition, string[]> select, string fullName)
  {
    using var stream = File.OpenRead(winmdPath);
    using var pe = new PEReader(stream);
    var reader = pe.GetMetadataReader();
    TypeDefinition? found = null;
    foreach (var handle in reader.TypeDefinitions)
    {
      var type = reader.GetTypeDefinition(handle);
      if (reader.GetString(type.Namespace) + "." + reader.GetString(type.Name) != fullName) continue;
      if (found is not null) throw new InvalidOperationException($"Duplicate WinMD type {fullName}.");
      found = type;
    }
    if (found is null) throw new InvalidOperationException($"WinMD {winmdPath} has no {fullName}.");
    var values = select(reader, found.Value);
    Array.Sort(values, StringComparer.Ordinal);
    return values;
  }

  private readonly record struct Library(string Version, string Path, string[] Files);

  private sealed class SignatureProvider : ISignatureTypeProvider<string, object>
  {
    internal static readonly SignatureProvider Instance = new();

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
        Simple(reader.GetString(reader.GetTypeDefinition(handle).Name));

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
        Simple(reader.GetString(reader.GetTypeReference(handle).Name));

    public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', Math.Max(0, shape.Rank - 1)) + "]";

    public string GetByReferenceType(string elementType) => elementType + "&";

    public string GetPointerType(string elementType) => elementType + "*";

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
        genericType + "<" + string.Join(", ", typeArguments) + ">";

    public string GetGenericMethodParameter(object genericContext, int index) => "!!" + index;

    public string GetGenericTypeParameter(object genericContext, int index) => "!" + index;

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetPinnedType(string elementType) => elementType;

    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";

    private static string Simple(string name)
    {
      var tick = name.IndexOf('`', StringComparison.Ordinal);
      return tick < 0 ? name : name[..tick];
    }
  }
}

internal sealed record WindowsLanguageModelSurfaceReport(
    string WindowsAppSdkVersion,
    string AiVersion,
    string AiDependencyVersion,
    string[] LanguageModelSignatures,
    string[] OptionProperties,
    string[] ResponseStatuses,
    string[] ContentFilterProperties,
    string[] UsablePromptLengthParameters,
    string[] CreateContextParameters,
    string[] SummarizerCutoffParameters,
    string[] ForbiddenMembers)
{
  internal string ToJson() => JsonSerializer.Serialize(this);
}
