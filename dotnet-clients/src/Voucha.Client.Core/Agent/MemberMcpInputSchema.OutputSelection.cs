using System.Text.Json;

namespace Voucha.Client.Core.Agent;

internal static partial class MemberMcpInputSchema
{
  private static bool CheckSelectedRootOutput(JsonElement value, JsonElement root, int index, ref int remainingWork)
  {
    if (!root.TryGetProperty("anyOf", out var branches) || branches.ValueKind != JsonValueKind.Array ||
        index < 0 || index >= branches.GetArrayLength()) throw new InvalidDataException("Invalid root output selection.");
    if (!Check(value, root, root, 0, ref remainingWork, output: true, skipRootAnyOf: true)) return false;
    var anyAccepted = false;
    var selectedAccepted = false;
    var branchIndex = 0;
    foreach (var branch in branches.EnumerateArray())
    {
      var accepted = Check(value, branch, root, 1, ref remainingWork, output: true);
      anyAccepted |= accepted;
      if (branchIndex++ == index) selectedAccepted = accepted;
    }
    return anyAccepted && selectedAccepted;
  }
}
