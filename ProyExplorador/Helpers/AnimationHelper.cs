using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ProyExplorador.Helpers
{
    /// <summary>
    /// Animaciones reutilizables para transiciones de vistas y microinteracciones.
    /// Buenas practicas:
    ///   - Animar Opacity y Transform (GPU) NO Width/Height/Margin (CPU layout)
    ///   - Easing Cubic/Sine EaseOut para fluidez premium
    ///   - Instancias de EasingFunction static para reducir allocations
    /// </summary>
    public static class AnimationHelper
    {
        public static readonly Duration DurationFast   = new(TimeSpan.FromMilliseconds(160));
        public static readonly Duration DurationNormal = new(TimeSpan.FromMilliseconds(260));
        public static readonly Duration DurationSlow   = new(TimeSpan.FromMilliseconds(420));

        private static readonly CubicEase EaseOut  = new() { EasingMode = EasingMode.EaseOut };
        private static readonly CubicEase EaseIn   = new() { EasingMode = EasingMode.EaseIn };
        private static readonly SineEase  SineInOut = new() { EasingMode = EasingMode.EaseInOut };

        public static void FadeSlideIn(UIElement element, double fromY = 20)
        {
            element.Opacity = 0;
            var fade      = new DoubleAnimation(0, 1, DurationNormal) { EasingFunction = EaseOut };
            var translate = new DoubleAnimation(fromY, 0, DurationNormal) { EasingFunction = EaseOut };

            if (element.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform();
                element.RenderTransform = tt;
            }

            element.BeginAnimation(UIElement.OpacityProperty, fade);
            tt.BeginAnimation(TranslateTransform.YProperty, translate);
        }

        public static void FadeSlideOut(UIElement element, Action? completed = null)
        {
            var fade = new DoubleAnimation(element.Opacity, 0, DurationFast) { EasingFunction = EaseIn };
            if (completed is not null)
                fade.Completed += (_, _) => completed();
            element.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        public static void PulseScale(FrameworkElement element)
        {
            if (element.RenderTransform is not ScaleTransform st)
            {
                st = new ScaleTransform(1, 1);
                element.RenderTransform       = st;
                element.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            var anim = new DoubleAnimation(1, 0.93,
                new Duration(TimeSpan.FromMilliseconds(70)))
            {
                AutoReverse    = true,
                EasingFunction = EaseOut
            };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, anim.Clone());
        }

        public static void StartSkeletonShimmer(UIElement element)
        {
            var anim = new DoubleAnimation(0.2, 0.6,
                new Duration(TimeSpan.FromMilliseconds(800)))
            {
                AutoReverse    = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = SineInOut
            };
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        public static void StopSkeletonShimmer(UIElement element)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
        }

        public static void AnimateShadow(DropShadowEffect effect,
            double toOpacity, double toBlurRadius, double toDepth)
        {
            effect.BeginAnimation(DropShadowEffect.OpacityProperty,
                new DoubleAnimation(toOpacity, DurationFast));
            effect.BeginAnimation(DropShadowEffect.BlurRadiusProperty,
                new DoubleAnimation(toBlurRadius, DurationFast));
            effect.BeginAnimation(DropShadowEffect.ShadowDepthProperty,
                new DoubleAnimation(toDepth, DurationFast));
        }

        public static void SlideIn(UIElement element, double fromX = -30)
        {
            if (element.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform();
                element.RenderTransform = tt;
            }
            element.Opacity = 0;
            tt.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(fromX, 0, DurationNormal) { EasingFunction = EaseOut });
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, DurationNormal) { EasingFunction = EaseOut });
        }

        public static void FadeIn(UIElement element)
        {
            element.Opacity = 0;
            element.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, DurationNormal) { EasingFunction = EaseOut });
        }
    }
}
