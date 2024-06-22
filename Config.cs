namespace CameraFlow;
using RedLoader;
using RedLoader.Preferences;

public static class Config
{
    public static ConfigCategory Category { get; private set; }
    public static KeybindConfigEntry MenuKey { get; private set; }
    public static KeybindConfigEntry StartCamKey { get; private set; }
    public static KeybindConfigEntry PosKey { get; private set; }
    public static KeybindConfigEntry DrawKey { get; private set; }
    public static ConfigEntry<int> Speed { get; private set; }
    public static ConfigEntry<int> Delay { get; private set; }
    public static ConfigEntry<float> Opacity { get; private set; }
    public static ConfigEntry<bool> Godmode { get; private set; }
    public static ConfigEntry<int> Resolution { get; private set; }
    public static ConfigEntry<bool> ForceUi { get; private set; }
    public static ConfigEntry<bool> DebugLog { get; private set; }


    public static void Init()
    {
        Category = ConfigSystem.CreateFileCategory("Base Configuration", "Base Configuration", "CameraFlow.cfg");
        Speed = Category.CreateEntry(
            "speed",
            20,
            "Speed - Default 20",
            "How fast to move the camera");
        Speed.SetRange(1, 500); // Set the range in in-game seconds

        MenuKey = Category.CreateKeybindEntry(
               "menu_key", // Set identifier
               EInputKey.numpad5, // Set default input key
               "Open/Close the Camera Flow Menu", // //Set name displayed in mod menu settings
               "Open/Close the Camera Flow Menu"); //Set description shown on hovering mouse over displayed name

        StartCamKey = Category.CreateKeybindEntry(
               "startcam_key", // Set identifier
               EInputKey.numpad8, // Set default input key
               "Starts/Stops the camera", // //Set name displayed in mod menu settings
               "Starts/Stops the camera when the key is pressed"); //Set description shown on hovering mouse over displayed name

        PosKey = Category.CreateKeybindEntry(
               "pos_key", // Set identifier
               EInputKey.numpad6, // Set default input key
               "Place new Point", // //Set name displayed in mod menu settings
               "Place new Point at your current position and rotation"); //Set description shown on hovering mouse over displayed name
        DrawKey = Category.CreateKeybindEntry(
               "draw_key", // Set identifier
               EInputKey.numpad4, // Set default input key
               "Toggle the preview", // //Set name displayed in mod menu settings
               "Toggles the Path and Point preview"); //Set description shown on hovering mouse over displayed name

        Delay = Category.CreateEntry(
            "delay",
            0,
            "Delay - Default 0",
            "How long to wait before starting the camera movement. In frames.");

        Opacity = Category.CreateEntry(
            "opacity",
            0.8f,
            "Opacity",
            "The opacity of the camera flow menu.");
        Opacity.SetRange(0.1f, 1f);

        Godmode = Category.CreateEntry(
            "godmode",
            false,
            "Keep GodMode enabled",
            "Will not turn GodMode off when finishing the camera movement.");

        Category = ConfigSystem.CreateFileCategory("Debug Values", "Debug Values", "CameraFlow.cfg");

        Resolution = Category.CreateEntry(
        "resolution",
        250,
        "Calculation Resolution - Default 100",
        "How many points will be calculated per in game unit. Changing this should not be necessary!");
        Resolution.SetRange(1, 1000);

        ForceUi = Category.CreateEntry(
            "force_ui",
            false,
            "Force Enable UI",
            "Forces UI to be enabled on startup, necessary when the game is closed while the camera is moving");

        DebugLog = Category.CreateEntry(
            "debug_log",
            false,
            "Enable Debug Logging",
            "When enabled it will log detailed movement information. Only enable when necessary!");
    }

    // Same as the callback in "CreateSettings". Called when the settings ui is closed.
    public static void OnSettingsUiClosed() =>
        // Update the speed variable
        CameraFlow.CalculatePath();
}
