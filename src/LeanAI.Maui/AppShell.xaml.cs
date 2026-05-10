namespace LeanAI.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        SetValue(Shell.TabBarForegroundColorProperty, Color.FromArgb("#D28B5C")); // Copper — active tab
        SetValue(Shell.TabBarUnselectedColorProperty, Color.FromArgb("#9A9EAB")); // Nickel — inactive tabs
    }
}
