using UnityEngine;

namespace BrineTime
{
    /// <summary>Interactable counter: Market buys your whole cooler, Store opens the shop, Ferry opens travel.</summary>
    public class Stall : MonoBehaviour
    {
        public enum Kind { Market, Store, Ferry }

        public Kind Type;

        public string Prompt()
        {
            switch (Type)
            {
                case Kind.Market:
                    int n = GameState.Cooler.Count;
                    return n == 0 ? "Fish Market (cooler empty)" : $"Sell {n} catch{(n == 1 ? "" : "es")} for ${GameState.CoolerValue()}";
                case Kind.Ferry:
                    return "Take the ferry";
                default:
                    return "Browse the General Store";
            }
        }

        public void Interact(PlayerController player)
        {
            switch (Type)
            {
                case Kind.Market:
                    int count = GameState.Cooler.Count;
                    int total = GameState.SellAll();
                    if (total == 0)
                    {
                        Sfx.Play("deny");
                        GameState.Notify("\"Bring me something that swims, friend.\"", new Color(0.9f, 0.9f, 0.8f));
                    }
                    else
                    {
                        Sfx.Play("cash");
                        GameState.Notify($"Sold {count} catch{(count == 1 ? "" : "es")} for ${total}!", new Color(0.5f, 1f, 0.5f));
                        NetSession.SendEvent("sold", total, 0f, false, "");
                    }
                    break;
                case Kind.Ferry:
                    player.Hud.OpenPanel(Hud.Panel.Travel);
                    break;
                default:
                    player.Hud.OpenPanel(Hud.Panel.Store);
                    break;
            }
        }
    }
}
