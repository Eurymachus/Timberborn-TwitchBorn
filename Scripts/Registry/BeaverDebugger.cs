using System;
using Timberborn.Beavers;
using Timberborn.Characters;
using Timberborn.EntityNaming;
using Timberborn.SingletonSystem;
using UnityEngine;
using TwitchBorn.Core;

namespace TwitchBorn.Registry
{
    public class BeaverDebugger : ILoadableSingleton, IPostLoadableSingleton
    {
        private readonly CharacterPopulation _characterPopulation;

        private const string TestName = "Eurymachus";

        public BeaverDebugger(CharacterPopulation characterPopulation)
        {
            _characterPopulation = characterPopulation;
        }

        public void Load()
        {
            TwitchBornLog.Info("[TwitchBorn] Beaver debugger loaded.");
        }

        public void PostLoad()
        {
            TwitchBornLog.Info("[TwitchBorn] Beaver rename test starting.");

            try
            {
                foreach (var character in _characterPopulation.Characters)
                {
                    var beaver = character.GetComponent<Beaver>();

                    if (beaver == null)
                    {
                        continue;
                    }

                    var namedEntity = character.GetComponent<NamedEntity>();

                    if (namedEntity == null)
                    {
                        TwitchBornLog.Info("[TwitchBorn] Beaver found but NamedEntity component was null.");
                        continue;
                    }

                    var oldName = namedEntity.EntityName;

                    TwitchBornLog.Info("[TwitchBorn] Renaming first beaver from " + oldName + " to " + TestName);

                    namedEntity.SetEntityName(TestName);

                    TwitchBornLog.Info("[TwitchBorn] Rename call completed. Current name is " + namedEntity.EntityName);

                    return;
                }

                TwitchBornLog.Info("[TwitchBorn] No beaver found to rename.");
            }
            catch (Exception exception)
            {
                TwitchBornLog.Error("[TwitchBorn] Beaver rename test failed: " + exception);
            }
        }
    }
}