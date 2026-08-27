#pragma warning disable CA1031
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private IReadOnlyList<ProfileLink> profileLinks = [];
  private string profileLinkType = "url";
  private string profileLinkAddressText = string.Empty;
  private string profileLinkHandle = string.Empty;
  private string profileLinkName = string.Empty;
  private string profileLinkImageId = string.Empty;
  private string? editingProfileLinkId;

  public IReadOnlyList<ProfileLink> ProfileLinks
  {
    get => profileLinks;
    private set
    {
      if (SetProperty(ref profileLinks, value))
      {
        OnPropertyChanged(nameof(LocalizedProfileLinks));
      }
    }
  }

  public string ProfileLinkType
  {
    get => profileLinkType;
    set
    {
      if (SetProperty(ref profileLinkType, value ?? "url"))
      {
        OnPropertyChanged(nameof(SelectedProfileLinkTypeOption));
      }
    }
  }

  public string ProfileLinkAddressText
  {
    get => profileLinkAddressText;
    set => SetProperty(ref profileLinkAddressText, value ?? string.Empty);
  }

  public string ProfileLinkHandle
  {
    get => profileLinkHandle;
    set => SetProperty(ref profileLinkHandle, value ?? string.Empty);
  }

  public string ProfileLinkName
  {
    get => profileLinkName;
    set => SetProperty(ref profileLinkName, value ?? string.Empty);
  }

  public string ProfileLinkImageId
  {
    get => profileLinkImageId;
    set => SetProperty(ref profileLinkImageId, value ?? string.Empty);
  }

  public string? EditingProfileLinkId
  {
    get => editingProfileLinkId;
    private set => SetProperty(ref editingProfileLinkId, value);
  }

  public async Task SaveProfileLinkAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      if (EditingProfileLinkId is { Length: > 0 } id)
      {
        var response = await settingsService.UpdateProfileLinkAsync(id, BuildUpdateBody(), cancellationToken)
            .ConfigureAwait(true);
        ProfileLinks = ProfileLinks.Select(link => link.Id == id ? response.ProfileLink : link).ToArray();
        ResetProfileLinkDraft();
        return;
      }

      var created = await settingsService.CreateProfileLinkAsync(BuildCreateBody(), cancellationToken)
          .ConfigureAwait(true);
      ProfileLinks = [.. ProfileLinks, created.ProfileLink];
      ResetProfileLinkDraft();
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  public void EditProfileLink(ProfileLink link)
  {
    ArgumentNullException.ThrowIfNull(link);
    EditingProfileLinkId = link.Id;
    ProfileLinkType = link.LinkType;
    ProfileLinkAddressText = link.Url ?? string.Empty;
    ProfileLinkHandle = link.Handle ?? string.Empty;
    ProfileLinkName = link.Name ?? string.Empty;
    ProfileLinkImageId = link.ImageId ?? string.Empty;
  }

  public void ResetProfileLinkDraft()
  {
    EditingProfileLinkId = null;
    ProfileLinkType = "url";
    ProfileLinkAddressText = string.Empty;
    ProfileLinkHandle = string.Empty;
    ProfileLinkName = string.Empty;
    ProfileLinkImageId = string.Empty;
  }

  public async Task DeleteProfileLinkAsync(ProfileLink link, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(link);
    try
    {
      await settingsService.DeleteProfileLinkAsync(link.Id, cancellationToken).ConfigureAwait(true);
      ProfileLinks = ProfileLinks.Where(item => item.Id != link.Id).ToArray();
      if (EditingProfileLinkId == link.Id)
      {
        ResetProfileLinkDraft();
      }
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  public async Task MoveProfileLinkAsync(
      ProfileLink link,
      int offset,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(link);
    var links = ProfileLinks.ToList();
    var index = links.FindIndex(item => item.Id == link.Id);
    if (index < 0) return;

    var targetIndex = Math.Clamp(index + offset, 0, links.Count - 1);
    if (targetIndex == index) return;

    links.RemoveAt(index);
    links.Insert(targetIndex, link);

    try
    {
      var response = await settingsService.ReorderProfileLinksAsync(
          new ReorderProfileLinksBody(links.Select(item => item.Id).ToArray()),
          cancellationToken).ConfigureAwait(true);
      ProfileLinks = response.Results;
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
  }

  private CreateProfileLinkBody BuildCreateBody() =>
      new(ProfileLinkType, EmptyToNull(ProfileLinkAddressText), EmptyToNull(ProfileLinkHandle),
          EmptyToNull(ProfileLinkName), EmptyToNull(ProfileLinkImageId));

  private UpdateProfileLinkBody BuildUpdateBody() =>
      new(EmptyToNull(ProfileLinkAddressText), EmptyToNull(ProfileLinkHandle),
          EmptyToNull(ProfileLinkName), EmptyToNull(ProfileLinkImageId));

  private static string? EmptyToNull(string? value) =>
      string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
#pragma warning restore CA1031
