namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private string? bioHtml;

  public string? BioHtml
  {
    get => bioHtml;
    private set => SetProperty(ref bioHtml, value);
  }
}
