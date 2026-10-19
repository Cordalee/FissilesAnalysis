using UnityEditor;
using UnityEngine;

namespace Fissiles.EditorUI
{
    /// <summary>The Tools > Fissiles Analytics menu. Keep the paths in sync with the "menu:" links in FAReadme.asset.</summary>
    public static class FAMenu
    {
        const string Root = "Tools/Fissiles Analytics/";

        [MenuItem(Root + "配置窗口", false, 1)]
        static void Settings() => FASettingsWindow.Open();

        [MenuItem(Root + "打开数据看板 (开发项目)", false, 20)]
        static void DashboardDev() => FASettingsWindow.OpenDashboard(true);

        [MenuItem(Root + "打开数据看板 (正式项目)", false, 21)]
        static void DashboardRelease() => FASettingsWindow.OpenDashboard(false);

        [MenuItem(Root + "使用说明 (README)", false, 40)]
        static void Readme() => FAReadmeEditor.Select();

        [MenuItem(Root + "发送测试事件 (运行中)", false, 60)]
        static void TestEvent()
        {
            FA.Track("editor_test", "time", System.DateTime.Now.ToString("HH:mm:ss"));
            FA.Flush();
            Debug.Log("[FA] 已发送测试事件 editor_test，稍后在看板「实时」页查看。如果没有出现，先确认已经登录（SDK 在登录后才启动）。");
        }

        [MenuItem(Root + "发送测试事件 (运行中)", true)]
        static bool TestEventValid() => Application.isPlaying;
    }
}
