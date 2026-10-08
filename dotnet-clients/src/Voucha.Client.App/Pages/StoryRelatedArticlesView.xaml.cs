using Voucha.Client.Core.NewsFeeds;

namespace Voucha.Client.App.Pages;

public partial class StoryRelatedArticlesView : ContentView
{
  public static readonly BindableProperty PrimaryItemProperty = BindableProperty.Create(
      nameof(PrimaryItem), typeof(NewsFeedItem), typeof(StoryRelatedArticlesView), null);
  public static readonly BindableProperty CanVoteProperty = BindableProperty.Create(nameof(CanVote), typeof(bool), typeof(StoryRelatedArticlesView), false);
  public static readonly BindableProperty CanClearVoteProperty = BindableProperty.Create(nameof(CanClearVote), typeof(bool), typeof(StoryRelatedArticlesView), false);
  public static readonly BindableProperty CanRelateProperty = BindableProperty.Create(nameof(CanRelate), typeof(bool), typeof(StoryRelatedArticlesView), false);

  public StoryRelatedArticlesView() => InitializeComponent();
  public NewsFeedItem? PrimaryItem { get => (NewsFeedItem?)GetValue(PrimaryItemProperty); set => SetValue(PrimaryItemProperty, value); }
  public bool CanVote { get => (bool)GetValue(CanVoteProperty); set => SetValue(CanVoteProperty, value); }
  public bool CanClearVote { get => (bool)GetValue(CanClearVoteProperty); set => SetValue(CanClearVoteProperty, value); }
  public bool CanRelate { get => (bool)GetValue(CanRelateProperty); set => SetValue(CanRelateProperty, value); }
  public event EventHandler? LoadMoreRequested;
  private void OnToggleClicked(object? sender, EventArgs e)
  {
    if (PrimaryItem?.StoryArticles is { } group) group.IsExpanded = !group.IsExpanded;
  }
  private void OnLoadMoreClicked(object? sender, EventArgs e) => LoadMoreRequested?.Invoke(sender, e);
  public event EventHandler? ChooseVoteRequested;
  private void OnChooseVoteClicked(object? sender, EventArgs e) => ChooseVoteRequested?.Invoke(sender, e);
  public event EventHandler? ClearVoteRequested;
  private void OnClearVoteClicked(object? sender, EventArgs e) => ClearVoteRequested?.Invoke(sender, e);
  public event EventHandler? FollowerSendRequested;
  private void OnFollowerSendClicked(object? sender, EventArgs e) => FollowerSendRequested?.Invoke(sender, e);
  public event EventHandler? PlayRequested;
  private void OnPlayClicked(object? sender, EventArgs e) => PlayRequested?.Invoke(sender, e);
  public event EventHandler? EmbedPlayRequested;
  private void OnEmbedPlayClicked(object? sender, EventArgs e) => EmbedPlayRequested?.Invoke(sender, e);
  public event EventHandler? OpenRequested;
  private void OnOpenClicked(object? sender, EventArgs e) => OpenRequested?.Invoke(sender, e);
  public event EventHandler? EmbedOpenRequested;
  private void OnEmbedOpenClicked(object? sender, EventArgs e) => EmbedOpenRequested?.Invoke(sender, e);
  public event EventHandler? ToggleSourceRequested;
  private void OnToggleSourceClicked(object? sender, EventArgs e) => ToggleSourceRequested?.Invoke(sender, e);
  public event EventHandler? ToggleSourceMuteRequested;
  private void OnToggleSourceMuteClicked(object? sender, EventArgs e) => ToggleSourceMuteRequested?.Invoke(sender, e);
  public event EventHandler? ToggleTopicRequested;
  private void OnToggleTopicClicked(object? sender, EventArgs e) => ToggleTopicRequested?.Invoke(sender, e);
  public event EventHandler? ToggleTopicMuteRequested;
  private void OnToggleTopicMuteClicked(object? sender, EventArgs e) => ToggleTopicMuteRequested?.Invoke(sender, e);
  public event EventHandler? ToggleReadRequested;
  private void OnToggleReadClicked(object? sender, EventArgs e) => ToggleReadRequested?.Invoke(sender, e);
  public event EventHandler? SaveRequested;
  private void OnSaveClicked(object? sender, EventArgs e) => SaveRequested?.Invoke(sender, e);
  public event EventHandler? HideRequested;
  private void OnHideClicked(object? sender, EventArgs e) => HideRequested?.Invoke(sender, e);
  public event EventHandler? StartStoryDiscussionRequested;
  private void OnStartStoryDiscussionClicked(object? sender, EventArgs e) => StartStoryDiscussionRequested?.Invoke(sender, e);
  public event EventHandler? OpenStoryDiscussionRequested;
  private void OnOpenStoryDiscussionClicked(object? sender, EventArgs e) => OpenStoryDiscussionRequested?.Invoke(sender, e);
}
