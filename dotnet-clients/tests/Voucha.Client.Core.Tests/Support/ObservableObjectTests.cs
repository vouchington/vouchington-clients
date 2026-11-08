using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class ObservableObjectTests
{
  [Fact]
  public void NotifiesTheAssignedPropertyBeforeItsDependent()
  {
    var probe = new PropertyProbe();

    probe.Name = "ada";

    Assert.Equal("ada", probe.Name);
    Assert.Equal(["Name", "HasName"], probe.Changes);
  }

  [Fact]
  public void SkipsDependentNotificationWhenTheValueIsUnchanged()
  {
    var probe = new PropertyProbe { Name = "ada", Count = 2 };
    probe.Changes.Clear();

    probe.Name = "ada";
    probe.Count = 2;

    Assert.Empty(probe.Changes);
  }

  [Fact]
  public void NotifiesTheAssignedPropertyBeforeEachDependent()
  {
    var probe = new PropertyProbe();

    probe.Label = "ada";

    Assert.Equal(["Label", "HasLabel", "LabelLength"], probe.Changes);
  }

  [Fact]
  public void SkipsEveryDependentWhenTheValueIsUnchanged()
  {
    var probe = new PropertyProbe { Label = "ada" };
    probe.Changes.Clear();

    probe.Label = "ada";

    Assert.Empty(probe.Changes);
  }

  [Fact]
  public void NotifiesAgainWhenAValueTypeChanges()
  {
    var probe = new PropertyProbe();

    probe.Count = 1;
    probe.Count = 0;

    Assert.Equal(
        ["Count", "IsPositive", "Count", "IsPositive"],
        probe.Changes);
  }

  private sealed class PropertyProbe : ObservableObject
  {
    private string? name;
    private int count;
    public List<string?> Changes { get; } = [];

    public PropertyProbe() => PropertyChanged += (_, args) => Changes.Add(args.PropertyName);

    public string? Name
    {
      get => name;
      set => SetPropertyAndNotify(ref name, value, nameof(HasName));
    }

    public bool HasName => Name is not null;

    public int Count
    {
      get => count;
      set => SetPropertyAndNotify(ref count, value, nameof(IsPositive));
    }

    public bool IsPositive => Count > 0;

    private string? label;

    public string? Label
    {
      get => label;
      set => SetPropertyAndNotify(ref label, value, [nameof(HasLabel), nameof(LabelLength)]);
    }

    public bool HasLabel => Label is not null;

    public int LabelLength => Label?.Length ?? 0;
  }
}
