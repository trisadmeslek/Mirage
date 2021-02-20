using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;

/**
 * Docs used:
 * UXML: https://docs.unity3d.com/Manual/UIE-UXML.html
 * USS: https://docs.unity3d.com/Manual/UIE-USS-SupportedProperties.html
 * Unity Guide: https://learn.unity.com/tutorial/uielements-first-steps/?tab=overview#5cd19ef9edbc2a1156524842
 * External Guide: https://www.raywenderlich.com/6452218-uielements-tutorial-for-unity-getting-started#toc-anchor-010
 */

namespace Mirage
{
    //this script handles the functionality of the UI
    [InitializeOnLoad]
    public class WelcomeWindow : EditorWindow
    {
        private Button lastClickedTab;
        private StyleColor defaultButtonBackgroundColor;
        private StyleColor defaultButtonBorderColor;

        #region Setup

        #region Urls

        private const string WelcomePageUrl = "https://miragenet.github.io/Mirage/index.html";
        private const string QuickStartUrl = "https://miragenet.github.io/Mirage/Articles/Guides/CommunityGuides/MirageQuickStartGuide/index.html";
        private const string ChangelogUrl = "https://github.com/MirageNet/Mirage/blob/master/Assets/Mirage/CHANGELOG.md";
        private const string BestPracticesUrl = "https://miragenet.github.io/Mirage/Articles/Guides/BestPractices.html";
        private const string FaqUrl = "https://miragenet.github.io/Mirage/Articles/Guides/FAQ.html";
        private const string SponsorUrl = "";
        private const string DiscordInviteUrl = "https://discord.gg/DTBPBYvexy";

        //TODO: Update package names once they are renamed
        private readonly List<Module> Modules = new List<Module>()
        {
            new Module { displayName = "LAN Discovery", packageName = "com.mirrorng.discovery", gitUrl = "https://github.com/MirageNet/Discovery.git?path=/Assets/Discovery"},
            new Module { displayName = "Momentum", packageName = "com.mirrorng.momentum", gitUrl = "https://github.com/MirageNet/Momentum.git?path=/Assets/Momentum" },
            new Module { displayName = "Steam (Facepunch)", packageName = "com.miragenet.steamyface", gitUrl = "https://github.com/MirageNet/SteamyFaceNG.git?path=/Assets/Mirage/Runtime/Transport/SteamyFaceMirror" },
            new Module { displayName = "Steam (Steamworks.NET)", packageName = "com.miragenet.steamy", gitUrl = "https://github.com/MirageNet/FizzySteamyMirror.git?path=/Assets/Mirage/Runtime/Transport/FizzySteamyMirror" },
            new Module { displayName = "Websockets", packageName = "com.mirrorng.websocket", gitUrl = "https://github.com/MirageNet/WebsocketNG.git?path=/Assets/Mirror/Websocket" },
        };

        #endregion

        //request for the module install
        private static AddRequest installRequest;
        private static RemoveRequest uninstallRequest;
        private static ListRequest listRequest;

        private static WelcomeWindow currentWindow;

        //window size of the welcome screen
        private static Vector2 windowSize = new Vector2(500, 415);
        private static string screenToOpenKey = "MirageScreenToOpen";

        //editorprefs keys
        private static string firstTimeVersionKey = string.Empty;
        private const string firstTimeMirageKey = "MirageWelcome";

        private static string GetVersion()
        {
            return typeof(NetworkIdentity).Assembly.GetName().Version.ToString();
        }

        #region Handle visibility

        private static bool ShowChangeLog
        {
            get
            {
                if (!EditorPrefs.GetBool(firstTimeMirageKey, false) && !EditorPrefs.GetBool(firstTimeVersionKey, false) && firstTimeVersionKey != "MirageUnknown")
                {
                    return false;
                }
                else if (EditorPrefs.GetBool(firstTimeMirageKey, false) && !EditorPrefs.GetBool(firstTimeVersionKey, false) && firstTimeVersionKey != "MirageUnknown")
                {
                    return true;
                }

                return false;
            }
        }

        //constructor (called by InitializeOnLoad)
        static WelcomeWindow()
        {
            EditorApplication.update += ShowWindowOnFirstStart;
        }

        private static void ShowWindowOnFirstStart()
        {
            EditorApplication.update -= ShowWindowOnFirstStart;
            firstTimeVersionKey = GetVersion();

            if ((!EditorPrefs.GetBool(firstTimeMirageKey, false) || !EditorPrefs.GetBool(firstTimeVersionKey, false)) && firstTimeVersionKey != "MirageUnknown")
            {
                EditorPrefs.SetString(screenToOpenKey, ShowChangeLog ? "ChangeLog" : "Welcome");

                OpenWindow();
            }
        }

        //open the window (also openable through the path below)
        [MenuItem("Window/Mirage/Welcome")]
        public static void OpenWindow()
        {
            //create the window
            currentWindow = GetWindow<WelcomeWindow>("Mirage Welcome Page");
            //set the position and size
            currentWindow.position = new Rect(new Vector2(100, 100), windowSize);
            //set min and max sizes so we cant readjust window size
            currentWindow.maxSize = windowSize;
            currentWindow.minSize = windowSize;
        }

        #endregion

        #endregion

        #region Displaying UI

        //the code to handle display and button clicking
        private void OnEnable()
        {
            //Load the UI
            //Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;
            VisualTreeAsset uxml = Resources.Load<VisualTreeAsset>("WelcomeWindow");
            StyleSheet uss = Resources.Load<StyleSheet>("WelcomeWindow");

            uxml.CloneTree(root);
            root.styleSheets.Add(uss);

            //set the version text
            Label versionText = root.Q<Label>("VersionText");
            versionText.text = "v" + GetVersion();

            //set button default colors (used in page nav)
            IStyle sampleStyle = rootVisualElement.Q<Button>("WelcomeButton").style;
            defaultButtonBackgroundColor = sampleStyle.backgroundColor;
            defaultButtonBorderColor = sampleStyle.borderTopColor;

            #region Page buttons

            ConfigureTab("WelcomeButton", "Welcome", WelcomePageUrl);
            ConfigureTab("ChangeLogButton", "ChangeLog", ChangelogUrl);
            ConfigureTab("QuickStartButton", "QuickStart", QuickStartUrl);
            ConfigureTab("BestPracticesButton", "BestPractices", BestPracticesUrl);
            ConfigureTab("FaqButton", "Faq", FaqUrl);
            ConfigureTab("SponsorButton", "Sponsor", SponsorUrl);
            ConfigureTab("DiscordButton", "Discord", DiscordInviteUrl);
            ConfigureModulesTab();

            ShowTab(EditorPrefs.GetString(screenToOpenKey, "Welcome"));
            #endregion
        }

        private void OnDisable()
        {
            //now that we have seen the welcome window, 
            //set this this to true so we don't load the window every time we recompile (for the current version)
            EditorPrefs.SetBool(firstTimeVersionKey, true);
            EditorPrefs.SetBool(firstTimeMirageKey, true);
        }

        private void ConfigureTab(string tabButtonName, string tab, string url)
        {
            Button tabButton = rootVisualElement.Q<Button>(tabButtonName);
            tabButton.clicked += () => 
            {
                ToggleMenuButtonColor(tabButton, true);
                ToggleMenuButtonColor(lastClickedTab, false);
                ShowTab(tab);
                lastClickedTab = tabButton;
            };

            Button redirectButton = rootVisualElement.Q<VisualElement>(tab).Q<Button>("Redirect");
            redirectButton.clicked += () => Application.OpenURL(url);
        }

        private void ShowTab(string screen)
        {
            VisualElement rightColumn = rootVisualElement.Q<VisualElement>("RightColumnBox");

            foreach (VisualElement tab in rightColumn.Children())
            {
                if (tab.name == screen)
                {
                    if (tab.name == "ChangeLog" && ShowChangeLog)
                    {
                        tab.Q<Label>("Header").text = "Change Log (updated)";
                    }

                    tab.style.display = DisplayStyle.Flex;
                }
                else
                {
                    tab.style.display = DisplayStyle.None;
                }
            }
        }

        private void ToggleMenuButtonColor(Button button, bool toggle)
        {
            if(button == null) { return; }

            if(toggle)
            {
                button.style.backgroundColor = button.resolvedStyle.backgroundColor;
                button.style.borderBottomColor = button.style.borderTopColor = button.style.borderLeftColor = button.style.borderRightColor = button.resolvedStyle.borderBottomColor;
            }
            else
            {
                button.style.backgroundColor = defaultButtonBackgroundColor;
                button.style.borderBottomColor = button.style.borderTopColor = button.style.borderLeftColor = button.style.borderRightColor = defaultButtonBorderColor;
            }
        }

        #region Modules

        //configure the module tab when the tab button is pressed
        private void ConfigureModulesTab()
        {
            Button tabButton = rootVisualElement.Q<Button>("ModulesButton");
            tabButton.clicked += () => ShowTab("Modules");

            listRequest = UnityEditor.PackageManager.Client.List(true, false);
            EditorApplication.update += ListModuleProgress;
        }

        //install the module
        private void InstallModule(string moduleName)
        {
            installRequest = UnityEditor.PackageManager.Client.Add(Modules.Find((x) => x.displayName == moduleName).gitUrl);

            //subscribe to InstallModuleProgress
            EditorApplication.update += InstallModuleProgress;
        }

        //uninstall the module
        private void UninstallModule(string moduleName)
        {
            uninstallRequest = UnityEditor.PackageManager.Client.Remove(Modules.Find((x) => x.displayName == moduleName).packageName);

            //subscribe to UninstallModuleProgress
            EditorApplication.update += UninstallModuleProgress;
        }

        //keeps track of the module install progress
        private void InstallModuleProgress()
        {
            Label waitLabel = rootVisualElement.Q<Label>("PleaseWaitLabel");

            if(installRequest.IsCompleted)
            {
                if(installRequest.Status == StatusCode.Success)
                {
                    Debug.Log("Module install successful.");
                }
                else if(installRequest.Status == StatusCode.Failure)
                {
                    Debug.LogError("Module install was unsuccessful. \n Error Code: " + installRequest.Error.errorCode + "\n Error Message: " + installRequest.Error.message);
                }

                waitLabel.style.visibility = Visibility.Hidden;
                EditorApplication.update -= InstallModuleProgress;

                //refresh the module tab
                currentWindow.Close();
                EditorPrefs.SetString(screenToOpenKey, "Modules");
                OpenWindow();
            }
            else
            {
                waitLabel.style.visibility = Visibility.Visible;
            }
        }

        private void UninstallModuleProgress()
        {
            Label waitLabel = rootVisualElement.Q<Label>("PleaseWaitLabel");

            if (uninstallRequest.IsCompleted)
            {
                EditorApplication.update -= UninstallModuleProgress;

                if (uninstallRequest.Status == StatusCode.Success)
                {
                    Debug.Log("Module uninstall successful.");
                }
                else if (uninstallRequest.Status == StatusCode.Failure)
                {
                    Debug.LogError("Module uninstall was unsuccessful. \n Error Code: " + uninstallRequest.Error.errorCode + "\n Error Message: " + uninstallRequest.Error.message);
                }

                waitLabel.style.visibility = Visibility.Hidden;

                //refresh the module tab
                currentWindow.Close();
                EditorPrefs.SetString(screenToOpenKey, "Modules");
                OpenWindow();
            }
            else
            {
                waitLabel.style.visibility = Visibility.Visible;
            }
        }

        private void ListModuleProgress()
        {
            if (listRequest.IsCompleted)
            {
                EditorApplication.update -= ListModuleProgress;

                if (listRequest.Status == StatusCode.Success)
                {
                    List<string> installedPackages = new List<string>();

                    foreach (var package in listRequest.Result)
                    {
                        Module? module = Modules.Find((x) => x.packageName == package.name);
                        if (module != null)
                        {
                            installedPackages.Add(module.Value.displayName);
                        }
                    }

                    ConfigureInstallButtons(installedPackages);
                }
                else if (listRequest.Status == StatusCode.Failure)
                {
                    Debug.LogError("There was an issue finding packages. \n Error Code: " + listRequest.Error.errorCode + "\n Error Message: " + listRequest.Error.message);
                }
            }
        }

        private void ConfigureInstallButtons(List<string> installedPackages)
        {
            foreach (VisualElement module in rootVisualElement.Q<VisualElement>("ModulesScrollView").Children())
            {
                Button installButton = module.Q<Button>("InstallButton");
                string moduleName = module.Q<Label>("Name").text;
                bool foundInInstalledPackages = installedPackages.Contains(moduleName);

                installButton.text = !foundInInstalledPackages ? "Install" : "Uninstall";
                if (!foundInInstalledPackages)
                {
                    installButton.clicked += () => { InstallModule(moduleName); };
                }
                else
                {
                    installButton.clicked += () => { UninstallModule(moduleName); };
                }
            }
        }

        #endregion

        #endregion
    }

    //module data type
    public struct Module
    {
        public string displayName;
        public string packageName;
        public string gitUrl;
    }
}
