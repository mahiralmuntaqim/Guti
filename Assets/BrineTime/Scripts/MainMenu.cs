using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrineTime
{
    /// <summary>Title screen: choose a name, then play solo, host a co-op session, or join a friend by IP.</summary>
    public class MainMenu : MonoBehaviour
    {
        public event Action SoloPressed;
        public event Action HostPressed;
        public event Action<string> JoinPressed;

        InputField _name, _ip;
        Text _status;
        Button[] _busyButtons;

        public static MainMenu Create()
        {
            var canvas = Ui.MakeCanvas("Main Menu", 50);
            var m = canvas.gameObject.AddComponent<MainMenu>();
            m.Build(canvas.transform);
            return m;
        }

        void Build(Transform root)
        {
            var bg = Ui.Panel(root, new Color(0.02f, 0.08f, 0.14f, 0.55f));
            Ui.Place(bg.rectTransform, new Vector4(0, 0, 1, 1), Vector2.zero, Vector2.zero);
            var panel = Ui.Panel(root, new Color(0.06f, 0.1f, 0.14f, 0.95f));
            Ui.Place(panel.rectTransform, new Vector4(0.5f, 0.5f, 0.5f, 0.5f), new Vector2(-300, -310), new Vector2(300, 310));
            Ui.VList(panel.rectTransform, 10, 30);

            var title = Ui.Label(panel.transform, "BRINE TIME", 56, new Color(1f, 0.92f, 0.5f), TextAnchor.MiddleCenter);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 66;
            var sub = Ui.Label(panel.transform, "cast  ·  fight  ·  sell  ·  sail", 18, new Color(0.7f, 0.85f, 0.95f), TextAnchor.MiddleCenter);
            sub.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;

            Caption(panel.transform, "Your name");
            _name = Ui.Input(panel.transform, "Angler");
            _name.characterLimit = 16;
            var solo = Ui.Button(panel.transform, "Play Solo", () => SoloPressed?.Invoke(), 22, 48);
            var host = Ui.Button(panel.transform, $"Host Co-op  (port {NetSession.Port})", () => HostPressed?.Invoke(), 22, 48);

            Caption(panel.transform, "Friend's IP address");
            var row = Ui.Rect(panel.transform, "JoinRow");
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            _ip = Ui.Input(row, "e.g. 192.168.1.20");
            _ip.GetComponent<LayoutElement>().flexibleWidth = 1;
            _ip.onSubmit.AddListener(_ => JoinPressed?.Invoke(_ip.text.Trim()));
            var join = Ui.Button(row, "   Join   ", () => JoinPressed?.Invoke(_ip.text.Trim()), 20);
            join.GetComponent<LayoutElement>().preferredWidth = 110;

            _status = Ui.Label(panel.transform, "", 16, new Color(1f, 0.8f, 0.5f));
            _status.gameObject.AddComponent<LayoutElement>().minHeight = 46;
            Ui.Button(panel.transform, "Quit", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }, 20);

            _busyButtons = new[] { solo, host, join };
            _name.text = PlayerPrefs.GetString("player_name", "");
            _ip.text = PlayerPrefs.GetString("last_ip", "");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Caption(Transform parent, string text)
        {
            var l = Ui.Label(parent, text, 16, new Color(0.8f, 0.85f, 0.9f));
            l.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
        }

        public string PlayerName => string.IsNullOrWhiteSpace(_name.text) ? "Angler" : _name.text.Trim();

        public void Remember()
        {
            PlayerPrefs.SetString("player_name", PlayerName);
            PlayerPrefs.SetString("last_ip", _ip.text.Trim());
            PlayerPrefs.Save();
        }

        public void SetStatus(string text) => _status.text = text;

        public void SetBusy(bool busy)
        {
            foreach (var b in _busyButtons) b.interactable = !busy;
        }
    }
}
