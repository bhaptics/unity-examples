using UnityEditor;
[InitializeOnLoad]
public static class CheckIfSimulatorEnabled
{
    private const string MENU_NAME = "WaveVR/Simulator/Enable Simulator";

    private static bool enabled_;
    /// Called on load thanks to the InitializeOnLoad attribute
    static CheckIfSimulatorEnabled()
    {
        CheckIfSimulatorEnabled.enabled_ = EditorPrefs.GetBool(CheckIfSimulatorEnabled.MENU_NAME, false);

        /// Delaying until first editor tick so that the menu
        /// will be populated before setting check state, and
        /// re-apply correct action
        EditorApplication.delayCall += () => {
            PerformAction(CheckIfSimulatorEnabled.enabled_);
        };
    }

    [MenuItem(CheckIfSimulatorEnabled.MENU_NAME)]
    private static void ToggleAction()
    {

        /// Toggling action
        PerformAction(!CheckIfSimulatorEnabled.enabled_);
    }

    public static void PerformAction(bool enabled)
    {

        /// Set checkmark on menu item
        Menu.SetChecked(CheckIfSimulatorEnabled.MENU_NAME, enabled);
        /// Saving editor state
        EditorPrefs.SetBool(CheckIfSimulatorEnabled.MENU_NAME, enabled);

        CheckIfSimulatorEnabled.enabled_ = enabled;
    }

    [MenuItem(CheckIfSimulatorEnabled.MENU_NAME, validate = true)]
    public static bool ValidateEnabled()
    {
        Menu.SetChecked(CheckIfSimulatorEnabled.MENU_NAME, enabled_);
        return true;
    }
}