using Echo.Core.Rules;
using Echo.Game.Persistence;
using UnityEngine;

namespace Echo.Game.Cosmetics
{
    /// <summary>The power-ups a player holds (a user revision): up to PowerUpRules.MaxHeld of each, kept in the save.</summary>
    public static class PowerUpInventory
    {
        public static int Count(PowerUpsSaveData held, PowerUp p) => p switch
        {
            PowerUp.Shield => held.Shield,
            PowerUp.Phase => held.Phase,
            PowerUp.Lantern => held.Lantern,
            _ => held.ExtraLife,
        };

        public static bool CanAdd(PowerUpsSaveData held, PowerUp p) => Count(held, p) < PowerUpRules.MaxHeld;

        public static bool Add(PowerUpsSaveData held, PowerUp p)
        {
            if (!CanAdd(held, p)) return false;
            Set(held, p, Count(held, p) + 1);
            return true;
        }

        public static bool Take(PowerUpsSaveData held, PowerUp p)
        {
            if (Count(held, p) <= 0) return false;
            Set(held, p, Count(held, p) - 1);
            return true;
        }

        static void Set(PowerUpsSaveData held, PowerUp p, int value)
        {
            value = Mathf.Clamp(value, 0, PowerUpRules.MaxHeld);
            switch (p)
            {
                case PowerUp.Shield: held.Shield = value; break;
                case PowerUp.Phase: held.Phase = value; break;
                case PowerUp.Lantern: held.Lantern = value; break;
                default: held.ExtraLife = value; break;
            }
        }
    }
}
