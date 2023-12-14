﻿using Avalonia.Input;

namespace Avalonia.Input;

public partial class XYFocus
{
    public static readonly AttachedProperty<InputElement> DownProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, InputElement, InputElement>("Down");

    public static void SetDown(InputElement obj, InputElement value) => obj.SetValue(DownProperty, value);
    public static InputElement GetDown(InputElement obj) => obj.GetValue(DownProperty);

    public static readonly AttachedProperty<InputElement> LeftProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, InputElement, InputElement>("Left");

    public static void SetLeft(InputElement obj, InputElement value) => obj.SetValue(LeftProperty, value);
    public static InputElement GetLeft(InputElement obj) => obj.GetValue(LeftProperty);

    public static readonly AttachedProperty<InputElement> RightProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, InputElement, InputElement>("Right");

    public static void SetRight(InputElement obj, InputElement value) =>
        obj.SetValue(RightProperty, value);

    public static InputElement GetRight(InputElement obj) => obj.GetValue(RightProperty);

    public static readonly AttachedProperty<InputElement> UpProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, InputElement, InputElement>("Up");

    public static void SetUp(InputElement obj, InputElement value) => obj.SetValue(UpProperty, value);
    public static InputElement GetUp(InputElement obj) => obj.GetValue(UpProperty);

    public static readonly AttachedProperty<XYFocusNavigationStrategy> DownNavigationStrategyProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, InputElement, XYFocusNavigationStrategy>(
            "DownNavigationStrategy", inherits: true);

    public static void SetDownNavigationStrategy(InputElement obj, XYFocusNavigationStrategy value) =>
        obj.SetValue(DownNavigationStrategyProperty, value);

    public static XYFocusNavigationStrategy GetDownNavigationStrategy(InputElement obj) =>
        obj.GetValue(DownNavigationStrategyProperty);

    public static readonly AttachedProperty<XYFocusNavigationStrategy> UpNavigationStrategyProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, InputElement, XYFocusNavigationStrategy>(
            "UpNavigationStrategy", inherits: true);

    public static void SetUpNavigationStrategy(InputElement obj, XYFocusNavigationStrategy value) =>
        obj.SetValue(UpNavigationStrategyProperty, value);

    public static XYFocusNavigationStrategy GetUpNavigationStrategy(InputElement obj) =>
        obj.GetValue(UpNavigationStrategyProperty);

    public static readonly AttachedProperty<XYFocusNavigationStrategy> LeftNavigationStrategyProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, InputElement, XYFocusNavigationStrategy>(
            "LeftNavigationStrategy", inherits: true);

    public static void SetLeftNavigationStrategy(InputElement obj, XYFocusNavigationStrategy value) =>
        obj.SetValue(LeftNavigationStrategyProperty, value);

    public static XYFocusNavigationStrategy GetLeftNavigationStrategy(InputElement obj) =>
        obj.GetValue(LeftNavigationStrategyProperty);


    public static readonly AttachedProperty<XYFocusNavigationStrategy> RightNavigationStrategyProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, InputElement, XYFocusNavigationStrategy>(
            "RightNavigationStrategy", inherits: true);

    public static void SetRightNavigationStrategy(InputElement obj, XYFocusNavigationStrategy value) =>
        obj.SetValue(RightNavigationStrategyProperty, value);

    public static XYFocusNavigationStrategy GetRightNavigationStrategy(InputElement obj) =>
        obj.GetValue(RightNavigationStrategyProperty);

    public static readonly AttachedProperty<bool> KeyboardNavigationEnabledProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, InputElement, bool>(
            "KeyboardNavigation", inherits: true);

    public static void SetKeyboardNavigationEnabled(InputElement obj, bool value) =>
        obj.SetValue(KeyboardNavigationEnabledProperty, value);

    public static bool GetKeyboardNavigationEnabled(InputElement obj) =>
        obj.GetValue(KeyboardNavigationEnabledProperty);

    public static readonly AttachedProperty<bool> IsFocusEngagementEnabledProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, InputElement, bool>("IsFocusEngagementEnabled");

    public static void SetIsFocusEngagementEnabled(InputElement obj, bool value) => obj.SetValue(IsFocusEngagementEnabledProperty, value);
    public static bool GetIsFocusEngagementEnabled(InputElement obj) => obj.GetValue(IsFocusEngagementEnabledProperty);

    public static readonly AttachedProperty<bool> IsFocusEngagedProperty =
        AvaloniaProperty.RegisterAttached<XYFocus, Visual, bool>("IsFocusEngaged", coerce: IsFocusEngagedCoerce);

    private static bool IsFocusEngagedCoerce(AvaloniaObject sender, bool value)
    {
        return value && sender is InputElement inputElement && GetIsFocusEngagementEnabled(inputElement);
    }

    public static void SetIsFocusEngaged(Visual obj, bool value) => obj.SetValue(IsFocusEngagedProperty, value);
    public static bool GetIsFocusEngaged(Visual obj) => obj.GetValue(IsFocusEngagedProperty);

    static XYFocus()
    {
        IsFocusEngagedProperty.Changed.AddClassHandler<Visual>((s, args) =>
        {
            // if ()
        });
    }
}