namespace Voucha.Client.Core.Agent;

public sealed partial class MemberMcpTokenManager
{
  /// <summary>Serializes old refresh writes before a newly redeemed credential generation.</summary>
  private async Task<T> SerializeMutationAsync<T>(Func<Task<T>> action)
  {
    Task previous;
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    lock (gate)
    {
      previous = mutationTail;
      mutationTail = completion.Task;
    }
    try
    {
      await previous.ConfigureAwait(false);
      return await action().ConfigureAwait(false);
    }
    finally { completion.TrySetResult(); }
  }
}
