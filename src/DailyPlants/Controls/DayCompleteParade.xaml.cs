using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

#if WINDOWS
using Windows.UI.ViewManagement;
#endif

namespace DailyPlants.Controls;

/// <summary>
/// The once-a-day celebration shown when every serving for the day is complete:
/// a headline, a plate of squares landing one per serving, and a confetti drift.
/// Respects the OS "reduce motion" setting.
/// </summary>
public sealed partial class DayCompleteParade : UserControl
{
    private const int PlateColumns = 7;
    private const int SquareLandMs = 180;
    private const int SquareStaggerMs = 30;
    private const int CardIntroMs = 220;
    private const int ConfettiStaggerMs = 40;
    private const int ConfettiDriftMs = 480;

    private Storyboard? _cardStoryboard;
    private Storyboard? _plateStoryboard;
    private Storyboard? _confettiStoryboard;

    private Border[] ConfettiPieces { get; }
    private CompositeTransform[] ConfettiTransforms { get; }

    private readonly (double TranslateY, double Rotation)[] _confettiRestingPose;

    public DayCompleteParade()
    {
        InitializeComponent();

        ConfettiPieces = [Confetti0, Confetti1, Confetti2, Confetti3, Confetti4, Confetti5, Confetti6, Confetti7, Confetti8];
        ConfettiTransforms =
        [
            Confetti0Transform, Confetti1Transform, Confetti2Transform, Confetti3Transform, Confetti4Transform,
            Confetti5Transform, Confetti6Transform, Confetti7Transform, Confetti8Transform
        ];

        // AnimateConfetti reads each transform's current value as its From, so the resting
        // pose has to be exact on every replay. Snapshotting it here means Reset can restore
        // it directly rather than relying on Storyboard.Stop reverting the animated value.
        _confettiRestingPose = ConfettiTransforms
            .Select(t => (t.TranslateY, t.Rotation))
            .ToArray();
    }

    /// <summary>
    /// Shows the parade for a perfect day. <paramref name="totalServings"/> drives how many
    /// plate squares are built - it varies with how many checklists are enabled.
    /// </summary>
    public void Play(int totalServings, string headline, string subhead)
    {
        Reset();

        HeadlineText.Text = headline;
        SubheadText.Text = subhead;

        var animate = AreAnimationsEnabled();

        BuildPlate(totalServings, animate);

        RootGrid.Visibility = Visibility.Visible;
        AutomationProperties.SetName(RootGrid, $"{headline}. {subhead}");

        if (animate)
        {
            AnimateCardIn();
            AnimateConfetti();
        }
        else
        {
            CardBorder.Opacity = 1;
            CardTransform.ScaleX = 1;
            CardTransform.ScaleY = 1;
        }
    }

    /// <summary>Returns the control to its pristine, collapsed state so it is ready to play again.</summary>
    public void Reset()
    {
        _cardStoryboard?.Stop();
        _plateStoryboard?.Stop();
        _confettiStoryboard?.Stop();

        RootGrid.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(RootGrid, string.Empty);

        PlateRoot.Children.Clear();

        CardBorder.Opacity = 1;
        CardTransform.ScaleX = 1;
        CardTransform.ScaleY = 1;

        ConfettiHost.Visibility = Visibility.Collapsed;
        for (var i = 0; i < ConfettiPieces.Length; i++)
        {
            ConfettiPieces[i].Opacity = 0;
            ConfettiTransforms[i].TranslateY = _confettiRestingPose[i].TranslateY;
            ConfettiTransforms[i].Rotation = _confettiRestingPose[i].Rotation;
        }
    }

    private void BuildPlate(int totalServings, bool animate)
    {
        var squareStyle = (Style)Resources["ParadeSquareStyle"];
        var squares = new List<(Border Square, CompositeTransform Transform)>(totalServings);

        StackPanel? row = null;
        for (var i = 0; i < totalServings; i++)
        {
            if (i % PlateColumns == 0)
            {
                row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
                PlateRoot.Children.Add(row);
            }

            var transform = new CompositeTransform { TranslateY = animate ? 6 : 0 };
            var square = new Border { Style = squareStyle, Opacity = animate ? 0 : 1, RenderTransform = transform };

            row!.Children.Add(square);
            squares.Add((square, transform));
        }

        if (!animate || squares.Count == 0)
        {
            return;
        }

        var storyboard = new Storyboard();
        for (var i = 0; i < squares.Count; i++)
        {
            var (square, transform) = squares[i];
            var beginTime = TimeSpan.FromMilliseconds(i * SquareStaggerMs);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = beginTime,
                Duration = new Duration(TimeSpan.FromMilliseconds(SquareLandMs)),
                EasingFunction = ease
            };
            Storyboard.SetTarget(fade, square);
            Storyboard.SetTargetProperty(fade, "Opacity");
            storyboard.Children.Add(fade);

            var land = new DoubleAnimation
            {
                From = 6,
                To = 0,
                BeginTime = beginTime,
                Duration = new Duration(TimeSpan.FromMilliseconds(SquareLandMs)),
                EasingFunction = ease
            };
            Storyboard.SetTarget(land, transform);
            Storyboard.SetTargetProperty(land, "TranslateY");
            storyboard.Children.Add(land);
        }

        _plateStoryboard = storyboard;
        storyboard.Begin();
    }

    private void AnimateCardIn()
    {
        CardBorder.Opacity = 0;
        CardTransform.ScaleX = 0.94;
        CardTransform.ScaleY = 0.94;

        var storyboard = new Storyboard();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fade = new DoubleAnimation { From = 0, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(CardIntroMs)), EasingFunction = ease };
        Storyboard.SetTarget(fade, CardBorder);
        Storyboard.SetTargetProperty(fade, "Opacity");
        storyboard.Children.Add(fade);

        foreach (var property in new[] { "ScaleX", "ScaleY" })
        {
            var scale = new DoubleAnimation { From = 0.94, To = 1, Duration = new Duration(TimeSpan.FromMilliseconds(CardIntroMs)), EasingFunction = ease };
            Storyboard.SetTarget(scale, CardTransform);
            Storyboard.SetTargetProperty(scale, property);
            storyboard.Children.Add(scale);
        }

        _cardStoryboard = storyboard;
        storyboard.Begin();
    }

    private void AnimateConfetti()
    {
        ConfettiHost.Visibility = Visibility.Visible;

        var storyboard = new Storyboard();
        for (var i = 0; i < ConfettiPieces.Length; i++)
        {
            var piece = ConfettiPieces[i];
            var transform = ConfettiTransforms[i];
            var beginTime = TimeSpan.FromMilliseconds(i * ConfettiStaggerMs);
            var startY = transform.TranslateY;
            var startRotation = transform.Rotation;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = beginTime,
                Duration = new Duration(TimeSpan.FromMilliseconds(ConfettiDriftMs)),
                EasingFunction = ease
            };
            Storyboard.SetTarget(fade, piece);
            Storyboard.SetTargetProperty(fade, "Opacity");
            storyboard.Children.Add(fade);

            var drift = new DoubleAnimation
            {
                From = startY,
                To = 0,
                BeginTime = beginTime,
                Duration = new Duration(TimeSpan.FromMilliseconds(ConfettiDriftMs)),
                EasingFunction = ease
            };
            Storyboard.SetTarget(drift, transform);
            Storyboard.SetTargetProperty(drift, "TranslateY");
            storyboard.Children.Add(drift);

            var settle = new DoubleAnimation
            {
                From = startRotation * 1.6,
                To = startRotation,
                BeginTime = beginTime,
                Duration = new Duration(TimeSpan.FromMilliseconds(ConfettiDriftMs)),
                EasingFunction = ease
            };
            Storyboard.SetTarget(settle, transform);
            Storyboard.SetTargetProperty(settle, "Rotation");
            storyboard.Children.Add(settle);
        }

        _confettiStoryboard = storyboard;
        storyboard.Begin();
    }

    private static bool AreAnimationsEnabled()
    {
#if WINDOWS
        try
        {
            return new UISettings().AnimationsEnabled;
        }
        catch
        {
            return true;
        }
#else
        return true;
#endif
    }
}
