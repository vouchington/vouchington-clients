using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.PaymentCards;

public sealed partial class PaymentCardsViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Mutation failures preserve native drafts and rows.")]
  public async Task CreateAsync(PaymentCardTopic topic, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(topic);
    if (IsMutating) return;
    IsMutating = true; BeginMutationErrorScope();
    try
    {
      var created = await service.CreateAsync(topic.Id, cancellationToken).ConfigureAwait(true);
      locallyCreatedCardIds.Add(created.Id);
      ReplaceCards([.. cards, created]); TopicResults = [];
      CompleteMutationErrorScope();
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    { SetFailure(ex, UiMessageKey.NativeDotnetPaymentCardsOperationFailed); }
    finally { IsMutating = false; }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Mutation failures preserve native drafts and rows.")]
  public async Task SaveAsync(CancellationToken cancellationToken = default)
  {
    if (Draft is not { } current || IsMutating) return;
    BeginMutationErrorScope();
    if (!TryBuildUpdate(current, out var body)) return;
    if (body is null) { Draft = null; CompleteMutationErrorScope(); return; }
    IsMutating = true;
    try
    {
      var updated = await service.UpdateAsync(current.Original.Id, body, cancellationToken).ConfigureAwait(true);
      ReplaceCards(cards.Select(card => card.Id == updated.Id ? updated : card)); Draft = null;
      CompleteMutationErrorScope();
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    { SetFailure(ex, UiMessageKey.NativeDotnetPaymentCardsOperationFailed); }
    finally { IsMutating = false; }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Delete failures preserve native rows.")]
  public async Task DeleteAsync(PaymentCardRow row, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(row);
    if (IsMutating) return;
    IsMutating = true; BeginMutationErrorScope();
    try
    {
      await service.DeleteAsync(row.Id, cancellationToken).ConfigureAwait(true);
      deletedCardIds.Add(row.Id);
      locallyCreatedCardIds.Remove(row.Id);
      ReplaceCards(cards.Where(card => card.Id != row.Id).Select(card => card.WithoutDeletedParent(row.Id)));
      if (Draft?.Original.Id == row.Id) Draft = null;
      else if (Draft?.AuthorizedUserOfId == row.Id) Draft.AuthorizedUserOfId = null;
      CompleteMutationErrorScope();
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    { SetFailure(ex, UiMessageKey.NativeDotnetPaymentCardsOperationFailed); }
    finally { IsMutating = false; }
  }

  private bool TryBuildUpdate(PaymentCardDraft value, out UpdatePaymentCardBody? body)
  {
    var original = value.Original;
    if (!value.TryParseCreditLimit(out var limit))
    {
      SetError(UiText.Localized(UiMessageKey.NativeDotnetPaymentCardsCreditLimitValidation));
      body = null;
      return false;
    }
    var note = value.Note.Length == 0 ? null : value.Note;
    var parentId = value.IsAuthorizedUser ? value.AuthorizedUserOfId : null;
    body = new UpdatePaymentCardBody(
        DateChange(original.OpenedOn, value.OpenedOn),
        DateChange(original.ClosedOn, value.ClosedOn),
        DateChange(original.ReceivedSignUpBonusOn, value.ReceivedSignUpBonusOn),
        MoneyChange(original.CreditLimit, limit),
        original.IsAuthorizedUser == value.IsAuthorizedUser ? null : value.IsAuthorizedUser,
        StringChange(original.AuthorizedUserOfId, parentId),
        StringChange(original.Note, note));
    if (body.OpenedOn is null && body.ClosedOn is null && body.ReceivedSignUpBonusOn is null &&
        body.CreditLimit is null && body.IsAuthorizedUser is null && body.AuthorizedUserOfId is null && body.Note is null)
      body = null;
    return true;
  }

  private static JsonNullableDate? DateChange(DateOnly? oldValue, DateOnly? newValue) => oldValue == newValue ? null : newValue is { } date ? JsonNullableDate.FromDate(date) : JsonNullableDate.Null;
  private static JsonNullableMoney? MoneyChange(Money? oldValue, Money? newValue) => oldValue == newValue ? null : newValue is { } amount ? JsonNullableMoney.FromMoney(amount) : JsonNullableMoney.Null;
  private static JsonNullableString? StringChange(string? oldValue, string? newValue) => oldValue == newValue ? null : newValue is null ? JsonNullableString.Null : JsonNullableString.FromString(newValue);
}
