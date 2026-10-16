using System.Collections;
using System.IO;
using UnityEngine;

namespace BrineTime
{
    /// <summary>
    /// Dev-only: a built player started with -gpdemo=DIR (plus -autoplay) fishes on its own and saves
    /// screenshots of each fishing beat (aim ring, nibble, bite, fight/lunge telegraph, catch) into DIR,
    /// logging "[gpdemo]" lines. Lets the gameplay feel be checked without a human at the mouse.
    /// Does nothing unless the argument is present.
    /// </summary>
    public class GameplayDemo : MonoBehaviour
    {
        string _dir;
        int _shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a.StartsWith("-gpdemo="))
                {
                    var go = new GameObject("GameplayDemo");
                    DontDestroyOnLoad(go);
                    go.AddComponent<GameplayDemo>()._dir = a.Substring("-gpdemo=".Length).Trim('"');
                }
        }

        void Shot(string name)
        {
            Directory.CreateDirectory(_dir);
            string path = Path.Combine(_dir, $"{_shot++:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[gpdemo] shot " + path);
        }

        static IEnumerator Wait(float s) { for (float t = 0; t < s; t += Time.deltaTime) yield return null; }

        IEnumerator Start()
        {
            PlayerController p = null;
            while (p == null || !p.isActiveAndEnabled) { p = FindFirstObjectByType<PlayerController>(); yield return null; }
            yield return Wait(3f);
            GameState.Persist = false;  // never touch the real save
            GameState.Reset();          // fresh start: bread, Driftwood Rod
            p.Automation = true;
            p.Turn(0, 8f - p.Pitch);
            var f = p.Fishing;
            for (int round = 0; round < 4; round++)
            {
                f.AutoReel = 1;
                yield return Wait(0.55f);
                Shot($"r{round}_aim_charge{f.Charge:F2}");
                yield return Wait(0.35f);
                Shot($"r{round}_aim_charge{f.Charge:F2}_onwater{f.AimOnWater}");
                f.AutoReel = 0;
                float waited = 0;
                bool nibbleShot = false;
                while (f.State != Fishing.FishState.Bite && waited < 20f)
                {
                    if (f.Nibbling && !nibbleShot) { nibbleShot = true; Shot($"r{round}_nibble"); Debug.Log("[gpdemo] nibble"); }
                    waited += Time.deltaTime;
                    yield return null;
                }
                if (f.State != Fishing.FishState.Bite) { f.ResetLine(); continue; }
                yield return Wait(0.12f);
                Shot($"r{round}_bite_window{f.BiteTimeLeft:F2}");
                yield return Wait(0.1f);
                f.AutoReel = 1;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                bool hold = true, warnShot = false, tensionShot = false;
                float t = 0;
                while (f.State == Fishing.FishState.Fight)
                {
                    if (hold && (f.Tension > 0.75f || f.LungeWarning > 0.3f)) hold = false;
                    else if (!hold && f.Tension < 0.3f && f.LungeWarning == 0f && !f.Lunging) hold = true;
                    f.AutoReel = hold ? 1 : 0;
                    if (!warnShot && f.LungeWarning > 0.5f) { warnShot = true; Shot($"r{round}_lunge_warning_{f.HookedStyle}"); }
                    if (!tensionShot && f.Tension > 0.7f) { tensionShot = true; Shot($"r{round}_tension{f.Tension:F2}"); }
                    t += Time.deltaTime;
                    yield return null;
                }
                f.AutoReel = 0;
                Debug.Log($"[gpdemo] round {round}: fight {t:F1}s, cooler {GameState.Cooler.Count}");
                yield return Wait(0.3f);
                Shot($"r{round}_after");
                yield return Wait(2.5f);
            }
            Debug.Log("[gpdemo] done");
            yield return Wait(1f);
            Application.Quit(0);
        }
    }
}
