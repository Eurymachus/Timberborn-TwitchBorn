using Timberborn.BehaviorSystem;
using Timberborn.Characters;
using Timberborn.EntityNaming;
using Timberborn.EntitySystem;
using Timberborn.Localization;
using Timberborn.SleepSystem;
using Timberborn.WorkSystem;

namespace TwitchBorn.Services
{
    public class BeaverStatusTextProvider
    {
        private readonly ILoc _loc;

        public BeaverStatusTextProvider(ILoc loc)
        {
            _loc = loc;
        }

        public string GetBeaverName(Character beaver)
        {
            if (beaver == null)
            {
                return "your beaver";
            }

            NamedEntity namedEntity;

            if (beaver.TryGetComponent(out namedEntity) && !string.IsNullOrEmpty(namedEntity.EntityName))
            {
                return namedEntity.EntityName;
            }

            return "your beaver";
        }

        public string GetBeaverStatus(Character beaver)
        {
            if (beaver == null)
            {
                return "currently unknown";
            }

            BehaviorManager behaviorManager;

            if (beaver.TryGetComponent(out behaviorManager) &&
                behaviorManager.IsRunningBehavior<SleepNeedBehavior>())
            {
                return "sleeping";
            }

            Worker worker;

            if (!beaver.TryGetComponent(out worker))
            {
                return "currently active";
            }

            if (!worker.Employed)
            {
                return "unemployed";
            }

            var workplaceName = GetWorkplaceDisplayName(worker.Workplace);

            if (worker.JobRunning)
            {
                return "working as " + AddIndefiniteArticle(workplaceName);
            }

            WorkerWorkingHours workerWorkingHours;

            if (beaver.TryGetComponent(out workerWorkingHours) && !workerWorkingHours.AreWorkingHours)
            {
                return "on a break";
            }

            return "assigned to " + AddIndefiniteArticle(workplaceName);
        }

        private string GetWorkplaceDisplayName(Workplace workplace)
        {
            if (workplace == null)
            {
                return "workplace";
            }

            LabeledEntitySpec labeledEntitySpec;

            if (workplace.TryGetComponent(out labeledEntitySpec) &&
                !string.IsNullOrEmpty(labeledEntitySpec.DisplayNameLocKey))
            {
                var localized = _loc.T(labeledEntitySpec.DisplayNameLocKey);

                if (!string.IsNullOrEmpty(localized) && localized != labeledEntitySpec.DisplayNameLocKey)
                {
                    return localized;
                }
            }

            if (!string.IsNullOrEmpty(workplace.Name))
            {
                return SplitCamelCase(workplace.Name);
            }

            return "workplace";
        }

        private static string AddIndefiniteArticle(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "a workplace";
            }

            var lower = text.Trim().ToLowerInvariant();

            if (lower.StartsWith("a ") || lower.StartsWith("an "))
            {
                return text;
            }

            var first = lower[0];

            if (first == 'a' || first == 'e' || first == 'i' || first == 'o' || first == 'u')
            {
                return "an " + text;
            }

            return "a " + text;
        }

        private static string SplitCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            var result = "";

            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];

                if (i > 0 && char.IsUpper(character) && !char.IsWhiteSpace(value[i - 1]))
                {
                    result += " ";
                }

                result += character;
            }

            return result;
        }
    }
}