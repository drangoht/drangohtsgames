using DrangohtGames.Web.Games;

namespace DrangohtGames.Tests.Builders;

/// <summary>
/// Construit un <see cref="Game"/> valide par défaut ; chaque test ne déclare que ce qui
/// compte pour lui.
/// </summary>
internal sealed class GameBuilder
{
    private string _slug = "un-jeu";
    private string _title = "Un jeu";
    private int _id = 1;
    private LocalizedText _tagline = LocalizedText.Empty;
    private DateTimeOffset? _publishedAt = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private Price _price = Price.FromCents(0, "USD");
    private GamePlatforms _platforms = GamePlatforms.Windows;
    private IReadOnlyList<string> _tags = [];
    private string? _engine;
    private Uri? _coverUrl;
    private IReadOnlyList<Screenshot> _screenshots = [];
    private bool _isSelfHosted;
    private bool _isFeatured;

    public GameBuilder WithSlug(string slug)
    {
        _slug = slug;
        return this;
    }

    public GameBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public GameBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public GameBuilder WithTagline(LocalizedText tagline)
    {
        _tagline = tagline;
        return this;
    }

    public GameBuilder WithPublishedAt(DateTimeOffset? publishedAt)
    {
        _publishedAt = publishedAt;
        return this;
    }

    public GameBuilder WithPrice(Price price)
    {
        _price = price;
        return this;
    }

    public GameBuilder WithPlatforms(GamePlatforms platforms)
    {
        _platforms = platforms;
        return this;
    }

    public GameBuilder WithTags(params string[] tags)
    {
        _tags = tags;
        return this;
    }

    public GameBuilder WithEngine(string? engine)
    {
        _engine = engine;
        return this;
    }

    public GameBuilder WithCoverUrl(string coverUrl)
    {
        _coverUrl = new Uri(coverUrl);
        return this;
    }

    public GameBuilder WithScreenshots(params string[] urls)
    {
        _screenshots = [.. urls.Select(url => new Screenshot(new Uri(url), LocalizedText.Empty))];
        return this;
    }

    public GameBuilder WithSelfHosted()
    {
        _isSelfHosted = true;
        return this;
    }

    public GameBuilder WithFeatured()
    {
        _isFeatured = true;
        return this;
    }

    public Game Build() => new()
    {
        Id = _id,
        Slug = GameSlug.FromItchUrl(new Uri($"https://drangoht.itch.io/{_slug}"), _id),
        Title = _title,
        ItchUrl = new Uri($"https://drangoht.itch.io/{_slug}"),
        Price = _price,
        Tagline = _tagline,
        PublishedAt = _publishedAt,
        Platforms = _platforms,
        Tags = _tags,
        Engine = _engine,
        CoverUrl = _coverUrl,
        Screenshots = _screenshots,
        IsSelfHosted = _isSelfHosted,
        IsFeatured = _isFeatured,
    };
}
