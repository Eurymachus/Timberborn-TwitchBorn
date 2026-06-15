using Timberborn.SingletonSystem;
using UnityEngine;
using TwitchBorn.Core;

namespace TwitchBorn.Core
{
    public class TwitchBornInitializer : ILoadableSingleton, IPostLoadableSingleton
    {
        public void Load()
        {
            TwitchBornLog.Info("[TwitchBorn] Loaded");
        }

        public void PostLoad()
        {
            TwitchBornLog.Info("[TwitchBorn] Ready.");
        }
    }
}