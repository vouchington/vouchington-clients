namespace Voucha.Client.Core.SpendingCategories;

public sealed partial class SpendingCategoriesViewModel
{
  private const int CompleteMismatchedTraversalsBeforeUpsertRetirement = 2;
  private void RestoreDeleted(SpendingCategoryRow row, int rank, bool hadLocal, PendingLocalUpsert? previousLocal)
  {
    pendingDeletions.Remove(row.Id);
    if (hadLocal && previousLocal is not null) localUpserts[row.Id] = previousLocal;
    var restored = values.Where(value => value.Id != row.Id).ToList();
    restored.Insert(Math.Min(rank, restored.Count), row.Value);
    Replace(restored);
  }

  private int BeginRead()
  {
    var readId = unchecked(++nextReadId);
    inFlightReadIds.Add(readId);
    foreach (var deletion in pendingDeletions.Values)
      if (deletion.MutationPending) deletion.ReadsRequiringOverlay.Add(readId);
    return readId;
  }
  private void Reconcile(
      Dictionary<string, SpendingCategory> display, Dictionary<string, SpendingCategory> response,
      int readId, int generation, bool replacesList, bool reachesEnd)
  {
    foreach (var (id, local) in localUpserts.ToArray())
    {
      var canObserve = !local.ReadsRequiringOverlay.Contains(readId);
      if (canObserve && replacesList) local.BeginConfirmation(generation);
      if (canObserve && response.TryGetValue(id, out var value) && value == local.Value)
      {
        localUpserts.Remove(id);
        continue;
      }
      if (canObserve && local.ConfirmationGeneration == generation && response.TryGetValue(id, out var candidate))
        local.ConfirmationCandidate = candidate;
      if (canObserve && local.ConfirmationGeneration == generation && reachesEnd && local.CompleteMismatchedTraversal() >= CompleteMismatchedTraversalsBeforeUpsertRetirement)
      {
        localUpserts.Remove(id);
        if (local.ConfirmationCandidate is { } observed) display[id] = observed;
        else display.Remove(id);
        continue;
      }
      display[id] = local.Value;
    }
    foreach (var (id, deletion) in pendingDeletions.ToArray())
    {
      if (!deletion.MutationPending && !deletion.ReadsRequiringOverlay.Contains(readId))
      {
        if (replacesList) deletion.BeginConfirmation(generation);
        if (deletion.ConfirmationGeneration == generation)
        {
          deletion.ConfirmationSawValue |= response.ContainsKey(id);
          if (reachesEnd && !deletion.ConfirmationSawValue)
          {
            pendingDeletions.Remove(id);
            continue;
          }
        }
      }
      display.Remove(id);
    }
  }
  private void CompleteRead(int readId)
  {
    inFlightReadIds.Remove(readId);
    foreach (var local in localUpserts.Values) local.ReadsRequiringOverlay.Remove(readId);
    foreach (var deletion in pendingDeletions.Values) deletion.ReadsRequiringOverlay.Remove(readId);
  }
  private void TrackLocalUpsert(SpendingCategory value) => localUpserts[value.Id] = new(value, [.. inFlightReadIds]);
  private void CompleteLocalDeletion(string id)
  {
    if (pendingDeletions.TryGetValue(id, out var deletion)) deletion.MutationPending = false;
  }

  private sealed record PendingLocalUpsert(SpendingCategory Value, HashSet<int> ReadsRequiringOverlay)
  {
    public int? ConfirmationGeneration { get; private set; }
    public SpendingCategory? ConfirmationCandidate { get; set; }
    private int completedMismatchedTraversals;
    public void BeginConfirmation(int generation) { ConfirmationGeneration = generation; ConfirmationCandidate = null; }
    public int CompleteMismatchedTraversal() => ++completedMismatchedTraversals;
  }
  private sealed record PendingLocalDeletion(HashSet<int> ReadsRequiringOverlay)
  {
    public bool MutationPending { get; set; } = true;
    public int? ConfirmationGeneration { get; private set; }
    public bool ConfirmationSawValue { get; set; }
    public void BeginConfirmation(int generation) { ConfirmationGeneration = generation; ConfirmationSawValue = false; }
  }
}
