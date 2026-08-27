using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.LandingPages;

public sealed partial class LandingPagesViewModel
{
  public async Task<bool> CreateAsync(
      string pageTitle,
      string? pageSubtitle,
      string? pageSlug,
      CancellationToken cancellationToken = default)
  {
    if (IsLoading) return false;
    BeginLoad();
    try
    {
      var response = await service
          .CreateAsync(new CreateLandingPageBody(pageTitle, NullableString(pageSubtitle), pageSlug), cancellationToken)
          .ConfigureAwait(true);
      ApplySelected(response.LandingPage);
      if (!string.IsNullOrWhiteSpace(initialSlug))
      {
        initialSlug = response.LandingPage.Slug;
      }
      await RefreshPagesAsync(cancellationToken).ConfigureAwait(true);
      CompleteLoad();
      return true;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (HandleMutationException(ex))
    {
      return false;
    }
  }

  public async Task<bool> SaveDetailsAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading || SelectedPage is null) return false;
    BeginLoad();
    try
    {
      var response = await service
          .UpdateAsync(SelectedPage.Id, new UpdateLandingPageBody(Title, NullableString(Subtitle), Slug), cancellationToken)
          .ConfigureAwait(true);
      var pagesResponse = await service.FetchPagesAsync(cancellationToken).ConfigureAwait(true);
      ApplySelectedMetadata(response.LandingPage);
      Pages = pagesResponse.Results.Select(LandingPageRow.FromPage).ToArray();
      if (!string.IsNullOrWhiteSpace(initialSlug))
      {
        initialSlug = response.LandingPage.Slug;
      }
      CompleteLoad();
      return true;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (HandleMutationException(ex))
    {
      return false;
    }
  }

  public async Task<bool> SetDefaultAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading || SelectedPage is null) return false;
    BeginLoad();
    try
    {
      var response = await service.SetDefaultAsync(SelectedPage.Id, cancellationToken).ConfigureAwait(true);
      ApplySelectedDefault(response.LandingPage);
      Pages = Pages.Select(page => page with { IsDefault = page.Id == response.LandingPage.Id }).ToArray();
      CompleteLoad();
      return true;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (HandleMutationException(ex))
    {
      return false;
    }
  }

  public async Task<bool> DeleteSelectedAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading || SelectedPage is null) return false;
    BeginLoad();
    try
    {
      var deletedId = SelectedPage.Id;
      await service.DeleteAsync(deletedId, cancellationToken).ConfigureAwait(true);
      ApplySelected(null);
      await RefreshPagesAsync(cancellationToken).ConfigureAwait(true);
      if (!string.IsNullOrWhiteSpace(initialSlug))
      {
        ApplySelected(null);
        CompleteLoad();
        return true;
      }

      var nextId = Pages.FirstOrDefault(page => page.Id != deletedId)?.Id;
      if (nextId is null)
      {
        ApplySelected(null);
        CompleteLoad();
        return true;
      }

      await LoadSelectedAsync(nextId, cancellationToken).ConfigureAwait(true);
      CompleteLoad();
      return true;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (HandleMutationException(ex))
    {
      return false;
    }
  }

  public async Task<bool> SaveContentAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading || SelectedPage is null) return false;
    BeginLoad();
    try
    {
      var body = new ReplaceLandingPageItemsBody(DraftItems.Select(item => item.ToInput()).ToArray());
      var response = await service.ReplaceItemsAsync(SelectedPage.Id, body, cancellationToken).ConfigureAwait(true);
      ApplySelectedContent(response.LandingPage);
      CompleteLoad();
      return true;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
      return false;
    }
    catch (Exception ex) when (HandleMutationException(ex))
    {
      return false;
    }
  }

  private bool HandleMutationException(Exception ex)
  {
    if (ex is not (VouchaApiException or HttpRequestException or InvalidOperationException))
    {
      return false;
    }

    ErrorMessage = ex.Message;
    State = LoadState.Error;
    return true;
  }

  private void ApplySelectedContent(LandingPage page)
  {
    DraftItems = page.Items ?? [];
    AcceptItemBaseline(page);
    var selected = page with { Title = Title, Subtitle = Subtitle, Slug = Slug, Items = DraftItems };
    SelectedPage = selected;
    Pages = Pages.Select(row => row.Id == page.Id
        ? LandingPageRow.FromPage(selected)
        : row).ToArray();
    ClearSelectedPageAnalytics();
  }

  private void ApplySelectedDefault(LandingPage page)
  {
    SelectedPage = page with { Title = Title, Subtitle = Subtitle, Slug = Slug, Items = DraftItems };
    NotifyBaselineState();
  }
}
