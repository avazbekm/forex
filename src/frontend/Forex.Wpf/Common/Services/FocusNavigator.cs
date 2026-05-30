namespace Forex.Wpf.Common.Services;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

/// <summary>
/// Universal focus navigation service for WPF applications.
/// Manages keyboard-based navigation between UI elements with automatic visibility checking.
/// Zero configuration required for standard WPF controls!
/// </summary>
public static class FocusNavigator
{
    #region Configuration (Optional - for advanced scenarios)

    /// <summary>
    /// Optional: Custom focusability checker. Only set if you need special logic beyond defaults.
    /// </summary>
    public static Func<UIElement, bool>? CustomFocusabilityChecker { get; set; }

    /// <summary>
    /// Optional: Custom focus handler. Only set if you need special focus behavior beyond defaults.
    /// </summary>
    public static Action<UIElement>? CustomFocusHandler { get; set; }

    #endregion

    #region Private Classes

    private class FocusContext
    {
        public List<UIElement> FocusOrder { get; set; } = [];
        public Dictionary<UIElement, KeyEventHandler> KeyDownHandlers { get; set; } = [];
        public Dictionary<Button, RoutedEventHandler> ClickHandlers { get; set; } = [];
        public FrameworkElement? RootView { get; set; }
    }

    #endregion

    #region Private Fields

    private static readonly Dictionary<FrameworkElement, FocusContext> ViewContexts = [];
    private static readonly Lock _lock = new();

    #endregion

    #region Public Methods

    public static void RegisterElements(List<UIElement> focusOrder)
    {
        if (focusOrder is null || focusOrder.Count == 0)
            return;

        var rootView = FindRootView(focusOrder[0]);
        if (rootView is null)
        {
            if (focusOrder[0] is FrameworkElement fe && !fe.IsLoaded)
            {
                void delayedRegistration(object? s, RoutedEventArgs e)
                {
                    fe.Loaded -= delayedRegistration;
                    RegisterElements(focusOrder);
                }
                fe.Loaded += delayedRegistration;
            }
            return;
        }

        lock (_lock)
        {
            if (ViewContexts.TryGetValue(rootView, out var oldContext))
            {
                CleanupContext(oldContext);
            }

            var context = new FocusContext
            {
                FocusOrder = [.. focusOrder],
                RootView = rootView
            };

            ViewContexts[rootView] = context;

            SetupViewLifecycle(rootView);
            RegisterKeyHandlers(context);
            FocusElement(focusOrder[0]);
        }
    }

    public static void SetFocusRedirect(Button triggerElement, UIElement returnElement)
    {
        if (triggerElement is null || returnElement is null)
            return;

        var rootView = FindRootView(triggerElement);
        if (rootView is null)
        {
            if (!triggerElement.IsLoaded)
            {
                void delayedRegistration(object? s, RoutedEventArgs e)
                {
                    triggerElement.Loaded -= delayedRegistration;
                    SetFocusRedirect(triggerElement, returnElement);
                }
                triggerElement.Loaded += delayedRegistration;
            }
            return;
        }

        lock (_lock)
        {
            if (!ViewContexts.TryGetValue(rootView, out var context))
            {
                context = new FocusContext { RootView = rootView };
                ViewContexts[rootView] = context;
                SetupViewLifecycle(rootView);
            }

            if (context.ClickHandlers.TryGetValue(triggerElement, out var oldHandler))
            {
                triggerElement.Click -= oldHandler;
            }

            void handler(object sender, RoutedEventArgs e)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    new Action(() => FocusElement(returnElement)),
                    DispatcherPriority.Input);
            }

            triggerElement.Click += handler;
            context.ClickHandlers[triggerElement] = handler;
        }
    }

    public static void UnregisterView(FrameworkElement view)
    {
        if (view is null)
            return;

        lock (_lock)
        {
            if (ViewContexts.TryGetValue(view, out var context))
            {
                CleanupContext(context);
                ViewContexts.Remove(view);
            }
        }
    }

    #endregion

    #region Private Helper Methods

    private static FrameworkElement? FindRootView(DependencyObject element)
    {
        var view = FindVisualParent<UserControl>(element) as FrameworkElement
                   ?? FindVisualParent<Page>(element) as FrameworkElement
                   ?? FindVisualParent<Window>(element);

        if (view is null)
        {
            DependencyObject current = element;
            while (current is not null)
            {
                if (current is UserControl or Page or Window)
                {
                    view = current as FrameworkElement;
                    break;
                }
                current = LogicalTreeHelper.GetParent(current);
            }
        }

        return view;
    }

    private static T? FindVisualParent<T>(DependencyObject element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T parent)
                return parent;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private static void SetupViewLifecycle(FrameworkElement view)
    {
        void unloadedHandler(object sender, RoutedEventArgs e)
        {
            view.Unloaded -= unloadedHandler;
            UnregisterView(view);
        }

        view.Unloaded += unloadedHandler;

        if (view is Page page)
        {
            void navigationHandler(object sender, System.Windows.Navigation.NavigationEventArgs e)
            {
                if (e.Content != page)
                {
                    UnregisterView(page);
                }
            }

            void loadedHandler(object sender, RoutedEventArgs e)
            {
                page.Loaded -= loadedHandler;
                if (page.NavigationService is not null)
                {
                    page.NavigationService.Navigated -= navigationHandler;
                    page.NavigationService.Navigated += navigationHandler;
                }
            }

            page.Loaded += loadedHandler;

            void cleanupNavigationHandler(object sender, RoutedEventArgs e)
            {
                page.Unloaded -= cleanupNavigationHandler;
                if (page.NavigationService is not null)
                {
                    page.NavigationService.Navigated -= navigationHandler;
                }
            }

            page.Unloaded += cleanupNavigationHandler;
        }
    }

    private static void RegisterKeyHandlers(FocusContext context)
    {
        foreach (var element in context.FocusOrder)
        {
            void handler(object s, KeyEventArgs e)
            {
                HandleKeyDown(element, e, context);
            }

            element.PreviewKeyDown += handler;
            context.KeyDownHandlers[element] = handler;
        }
    }

    private static void HandleKeyDown(UIElement element, KeyEventArgs e, FocusContext context)
    {
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        int currentIdx = context.FocusOrder.IndexOf(element);

        if (currentIdx == -1)
            return;

        UIElement? nextElement = null;

        if (e.Key == Key.Enter || e.Key == Key.Tab)
        {
            if (element is Button btn && e.Key == Key.Enter && !shift)
            {
                btn.Command?.Execute(btn.CommandParameter);
                btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                e.Handled = true;
                return;
            }

            nextElement = shift
                ? GetNextFocusableElement(context.FocusOrder, currentIdx, isForward: false)
                : GetNextFocusableElement(context.FocusOrder, currentIdx, isForward: true);
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            // Check if element is a UserControl containing a ComboBox (like FloatingImageComboBox)
            ComboBox? innerCombo = element switch
            {
                ComboBox cb => cb,
                UserControl uc => FindVisualChild<ComboBox>(uc),
                _ => null
            };

            if (innerCombo != null && innerCombo.IsDropDownOpen && e.Key is Key.Down or Key.Up)
            {
                // Let ComboBox handle its own navigation when dropdown is open
                return;
            }

            nextElement = element switch
            {
                TextBox tb => HandleTextBoxNavigation(e, tb, currentIdx, shift, context.FocusOrder),
                ComboBox cb => HandleComboBoxNavigation(e, currentIdx, shift, context.FocusOrder, cb),
                UserControl uc when innerCombo != null => HandleComboBoxNavigation(e, currentIdx, shift, context.FocusOrder, innerCombo),
                _ => HandleGeneralNavigation(e, currentIdx, shift, context.FocusOrder)
            };
        }

        if (nextElement is not null)
        {
            FocusElement(nextElement);
            e.Handled = true;
        }
    }

    private static UIElement? HandleTextBoxNavigation(KeyEventArgs e, TextBox tb, int currentIdx, bool shift, List<UIElement> focusOrder)
    {
        return e.Key switch
        {
            Key.Right when shift => GetNextFocusableElement(focusOrder, currentIdx, false),
            Key.Right when tb.SelectionLength == tb.Text.Length || tb.CaretIndex == tb.Text.Length
                => GetNextFocusableElement(focusOrder, currentIdx, true),

            Key.Left when shift => GetNextFocusableElement(focusOrder, currentIdx, true),
            Key.Left when tb.SelectionLength == tb.Text.Length || tb.CaretIndex == 0
                => GetNextFocusableElement(focusOrder, currentIdx, false),

            Key.Down => GetNextFocusableElement(focusOrder, currentIdx, !shift),
            Key.Up => GetNextFocusableElement(focusOrder, currentIdx, shift),

            _ => null
        };
    }

    private static UIElement? HandleComboBoxNavigation(KeyEventArgs e, int currentIdx, bool shift, List<UIElement> focusOrder, ComboBox comboBox)
    {
        return e.Key switch
        {
            Key.Down or Key.Up when comboBox.IsDropDownOpen => null,
            Key.Right => GetNextFocusableElement(focusOrder, currentIdx, !shift),
            Key.Left => GetNextFocusableElement(focusOrder, currentIdx, shift),
            _ => null
        };
    }

    private static UIElement? HandleGeneralNavigation(KeyEventArgs e, int currentIdx, bool shift, List<UIElement> focusOrder)
    {
        return e.Key switch
        {
            Key.Down or Key.Right => GetNextFocusableElement(focusOrder, currentIdx, !shift),
            Key.Up or Key.Left => GetNextFocusableElement(focusOrder, currentIdx, shift),
            _ => null
        };
    }

    private static UIElement? GetNextFocusableElement(List<UIElement> focusOrder, int currentIdx, bool isForward)
    {
        int count = focusOrder.Count;
        int step = isForward ? 1 : -1;

        for (int i = 1; i <= count; i++)
        {
            int nextIdx = (currentIdx + i * step + count) % count;

            if (IsElementFocusable(focusOrder[nextIdx]) && nextIdx != currentIdx)
            {
                return focusOrder[nextIdx];
            }
        }

        return null;
    }

    private static bool IsElementFocusable(UIElement element)
    {
        if (element.Visibility != Visibility.Visible || !element.IsEnabled)
            return false;

        // Check parent visibility in both Visual and Logical trees
        if (!IsParentChainVisible(element))
            return false;

        // Check custom focusability if provided
        if (CustomFocusabilityChecker != null)
        {
            return CustomFocusabilityChecker(element);
        }

        // Default focusability check - works for all standard WPF controls and UserControls
        return element switch
        {
            TextBox tb => tb.IsTabStop,
            ComboBox => true,
            Button btn => btn.IsTabStop,
            UserControl => true, // UserControls are focusable by default
            _ => element.Focusable
        };
    }

    private static bool IsParentChainVisible(UIElement element)
    {
        // Check Visual tree
        DependencyObject parent = VisualTreeHelper.GetParent(element);
        while (parent != null)
        {
            if (parent is UIElement parentElement)
            {
                if (parentElement.Visibility != Visibility.Visible || !parentElement.IsEnabled)
                    return false;
            }
            parent = VisualTreeHelper.GetParent(parent);
        }

        // Check Logical tree (important for UserControls)
        DependencyObject logicalParent = LogicalTreeHelper.GetParent(element);
        while (logicalParent != null)
        {
            if (logicalParent is UIElement logicalElement)
            {
                if (logicalElement.Visibility != Visibility.Visible || !logicalElement.IsEnabled)
                    return false;
            }
            logicalParent = LogicalTreeHelper.GetParent(logicalParent);
        }

        return true;
    }

    public static void FocusElement(UIElement element)
    {
        if (element == null) return;

        element.Dispatcher.BeginInvoke(new Action(() =>
        {
            element.Focus();

            // Try custom focus handler first
            if (CustomFocusHandler != null)
            {
                CustomFocusHandler(element);
                return;
            }

            // Auto-detect and handle common patterns
            switch (element)
            {
                case TextBox tb:
                    tb.SelectAll();
                    break;

                case ComboBox { IsEditable: true } cb:
                    if (cb.Template.FindName("PART_EditableTextBox", cb) is TextBox innerTextBox)
                    {
                        innerTextBox.Focus();
                        innerTextBox.SelectAll();
                    }
                    break;

                case UserControl uc:
                    // Auto-detect and focus first ComboBox or TextBox inside UserControl
                    TryFocusInnerControl(uc);
                    break;
            }
        }), DispatcherPriority.Input);
    }

    /// <summary>
    /// Automatically finds and focuses the first focusable control inside a UserControl
    /// </summary>
    private static void TryFocusInnerControl(UserControl userControl)
    {
        // Try to find ComboBox first (most common in custom controls)
        if (FindVisualChild<ComboBox>(userControl) is ComboBox combo)
        {
            combo.Focus();
            return;
        }

        // Then try TextBox
        if (FindVisualChild<TextBox>(userControl) is TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
            return;
        }

        // Finally, any focusable element
        if (FindFirstFocusableChild(userControl) is UIElement focusable)
        {
            focusable.Focus();
        }
    }

    /// <summary>
    /// Finds first focusable child recursively
    /// </summary>
    private static UIElement? FindFirstFocusableChild(DependencyObject parent)
    {
        if (parent == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is UIElement element && element.Focusable && element.IsEnabled && element.Visibility == Visibility.Visible)
                return element;

            var result = FindFirstFocusableChild(child);
            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>
    /// Helper: Finds first visual child of specific type
    /// </summary>
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }

        return null;
    }

    private static void CleanupContext(FocusContext context)
    {
        foreach (var kvp in context.KeyDownHandlers)
            kvp.Key.PreviewKeyDown -= kvp.Value;
        context.KeyDownHandlers.Clear();

        foreach (var kvp in context.ClickHandlers)
            kvp.Key.Click -= kvp.Value;
        context.ClickHandlers.Clear();

        context.FocusOrder.Clear();
    }

    #endregion
}