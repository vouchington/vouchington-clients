namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private int avatarMutationGeneration;

  private bool IsCurrentAvatarMutation(int generation, CancellationToken cancellationToken) =>
      Volatile.Read(ref avatarMutationGeneration) == generation && !cancellationToken.IsCancellationRequested;
}
