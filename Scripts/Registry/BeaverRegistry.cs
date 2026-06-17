using System;
using System.Collections.Generic;
using Timberborn.Beavers;
using Timberborn.Characters;
using Timberborn.EntityNaming;
using Timberborn.EntitySystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using TwitchBorn.Core;
using TwitchBorn.Models;
using Timberborn.Bots;
using TwitchBorn.Settings;

namespace TwitchBorn.Registry
{
    public class BeaverRegistry : ILoadableSingleton, IPostLoadableSingleton, ISaveableSingleton
    {
        private enum BeaverNamePolicy
        {
            DoNotRename,
            EnforceAssignedName
        }

        private static readonly SingletonKey RegistryKey = new SingletonKey("TwitchBornViewerBeaverRegistry");

        private static readonly ListKey<string> ViewerSourcesKey = new ListKey<string>("ViewerSources");
        private static readonly ListKey<string> ViewerSourceUserIdsKey = new ListKey<string>("ViewerSourceUserIds");
        private static readonly ListKey<string> ViewerDisplayNamesKey = new ListKey<string>("ViewerDisplayNames");
        private static readonly ListKey<Guid> ViewerBeaverRecordIdsKey = new ListKey<Guid>("ViewerBeaverRecordIds");
        private static readonly ListKey<bool> ViewerHasLivingClaimsKey = new ListKey<bool>("ViewerHasLivingClaims");
        private static readonly ListKey<string> ViewerLastAssignedBeaverNamesKey = new ListKey<string>("ViewerLastAssignedBeaverNames");
        private static readonly ListKey<string> ViewerLastDeadBeaverNamesKey = new ListKey<string>("ViewerLastDeadBeaverNames");
        private static readonly ListKey<bool> ViewerQueuedAfterDeathKey = new ListKey<bool>("ViewerQueuedAfterDeath");
        private static readonly ListKey<string> ViewerFirstSeenAtUtcKey = new ListKey<string>("ViewerFirstSeenAtUtc");
        private static readonly ListKey<string> ViewerLastSeenAtUtcKey = new ListKey<string>("ViewerLastSeenAtUtc");
        private static readonly ListKey<int> ViewerChatMessageCountsKey = new ListKey<int>("ViewerChatMessageCounts");
        private static readonly ListKey<string> ViewerNameColorHexesKey = new ListKey<string>("ViewerNameColorHexes");
        private static readonly ListKey<string> ViewerNameShadowColorHexesKey = new ListKey<string>("ViewerNameShadowColorHexes");

        private static readonly ListKey<Guid> BeaverRecordIdsKey = new ListKey<Guid>("BeaverRecordIds");
        private static readonly ListKey<Guid> BeaverEntityIdsKey = new ListKey<Guid>("BeaverEntityIds");
        private static readonly ListKey<string> BeaverOriginalNamesKey = new ListKey<string>("BeaverOriginalNames");
        private static readonly ListKey<string> BeaverAssignedNamesKey = new ListKey<string>("BeaverAssignedNames");
        private static readonly ListKey<string> BeaverAssignedAtUtcKey = new ListKey<string>("BeaverAssignedAtUtc");
        private static readonly ListKey<string> BeaverLastSeenAtUtcKey = new ListKey<string>("BeaverLastSeenAtUtc");
        private static readonly ListKey<int> BeaverRenameCountsKey = new ListKey<int>("BeaverRenameCounts");
        private static readonly ListKey<int> BeaverRerollCountsKey = new ListKey<int>("BeaverRerollCounts");
        private static readonly ListKey<bool> BeaverIsActiveKey = new ListKey<bool>("BeaverIsActive");

        private static readonly ListKey<Guid> BeaverHistoryRecordIdsKey = new ListKey<Guid>("BeaverHistoryRecordIds");
        private static readonly ListKey<Guid> BeaverHistoryEntityIdsKey = new ListKey<Guid>("BeaverHistoryEntityIds");
        private static readonly ListKey<string> BeaverHistoryReasonsKey = new ListKey<string>("BeaverHistoryReasons");
        private static readonly ListKey<string> BeaverHistoryNamesKey = new ListKey<string>("BeaverHistoryNames");
        private static readonly ListKey<string> BeaverHistoryRecordedAtUtcKey = new ListKey<string>("BeaverHistoryRecordedAtUtc");
        private static readonly ListKey<int> BeaverHistoryAgesKey = new ListKey<int>("BeaverHistoryAges");

        private static readonly ListKey<string> PendingClaimSourcesKey = new ListKey<string>("PendingClaimSources");
        private static readonly ListKey<string> PendingClaimSourceUserIdsKey = new ListKey<string>("PendingClaimSourceUserIds");
        private static readonly ListKey<string> PendingClaimLoginNamesKey = new ListKey<string>("PendingClaimLoginNames");
        private static readonly ListKey<string> PendingClaimDisplayNamesKey = new ListKey<string>("PendingClaimDisplayNames");

        private readonly ISingletonLoader _singletonLoader;
        private readonly CharacterPopulation _characterPopulation;
        private readonly EventBus _eventBus;
        private readonly ClaimSettingsOwner _claimSettingsOwner;

        private readonly Dictionary<string, ViewerRecord> _viewersByKey = new Dictionary<string, ViewerRecord>();
        private readonly Dictionary<Guid, BeaverRecord> _beaversByRecordId = new Dictionary<Guid, BeaverRecord>();
        private readonly Dictionary<Guid, BeaverRecord> _beaversByEntityId = new Dictionary<Guid, BeaverRecord>();
        private readonly Dictionary<Guid, BeaverRecord> _beaversByHistoricalEntityId = new Dictionary<Guid, BeaverRecord>();
        private readonly List<ActiveBeaver> _activeBeaverCache = new List<ActiveBeaver>();
        private readonly List<ViewerIdentity> _pendingClaims = new List<ViewerIdentity>();
        private readonly HashSet<string> _pendingClaimKeys = new HashSet<string>();
        private readonly HashSet<Guid> _internalRenameEntityIds = new HashSet<Guid>();

        private bool _cacheDirty = true;

        public event Action<BeaverCommandResult> PendingClaimAssigned;
        public event Action<BeaverCommandResult> BeaverLifecycleNotification;

        public BeaverRegistry(
            ISingletonLoader singletonLoader,
            CharacterPopulation characterPopulation,
            EventBus eventBus,
            ClaimSettingsOwner claimSettingsOwner)
        {
            _singletonLoader = singletonLoader;
            _characterPopulation = characterPopulation;
            _eventBus = eventBus;
            _claimSettingsOwner = claimSettingsOwner;
        }

        public void Load()
        {
            _eventBus.Register(this);
            LoadRecords();

            TwitchBornLog.Info("Beaver registry loaded. Viewers: " + _viewersByKey.Count + ", Beavers: " + _beaversByRecordId.Count);
        }

        public void PostLoad()
        {
            MarkCacheDirty();

            TwitchBornLog.Info("Beaver registry post-load ready. Active beavers: " + GetActiveBeavers().Count);

            ProcessPendingClaims();
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            var viewerSources = new List<string>();
            var viewerSourceUserIds = new List<string>();
            var viewerDisplayNames = new List<string>();
            var viewerBeaverRecordIds = new List<Guid>();
            var viewerHasLivingClaims = new List<bool>();
            var viewerLastAssignedBeaverNames = new List<string>();
            var viewerLastDeadBeaverNames = new List<string>();
            var viewerQueuedAfterDeath = new List<bool>();
            var viewerFirstSeenAtUtc = new List<string>();
            var viewerLastSeenAtUtc = new List<string>();
            var viewerChatMessageCounts = new List<int>();
            var viewerNameColorHexes = new List<string>();
            var viewerNameShadowColorHexes = new List<string>();

            foreach (var viewerRecord in _viewersByKey.Values)
            {
                viewerSources.Add(viewerRecord.Source ?? "");
                viewerSourceUserIds.Add(viewerRecord.SourceUserId ?? "");
                viewerDisplayNames.Add(viewerRecord.DisplayName ?? "");
                viewerBeaverRecordIds.Add(viewerRecord.BeaverRecordId);
                viewerHasLivingClaims.Add(viewerRecord.HasLivingClaim);
                viewerLastAssignedBeaverNames.Add(viewerRecord.LastAssignedBeaverName ?? "");
                viewerLastDeadBeaverNames.Add(viewerRecord.LastDeadBeaverName ?? "");
                viewerQueuedAfterDeath.Add(viewerRecord.QueuedAfterDeath);
                viewerFirstSeenAtUtc.Add(viewerRecord.FirstSeenAtUtc ?? "");
                viewerLastSeenAtUtc.Add(viewerRecord.LastSeenAtUtc ?? "");
                viewerChatMessageCounts.Add(viewerRecord.ChatMessageCount);
                viewerNameColorHexes.Add(viewerRecord.NameColorHex ?? "");
                viewerNameShadowColorHexes.Add(viewerRecord.NameShadowColorHex ?? "");
            }

            var beaverRecordIds = new List<Guid>();
            var beaverEntityIds = new List<Guid>();
            var beaverOriginalNames = new List<string>();
            var beaverAssignedNames = new List<string>();
            var beaverAssignedAtUtc = new List<string>();
            var beaverLastSeenAtUtc = new List<string>();
            var beaverRenameCounts = new List<int>();
            var beaverRerollCounts = new List<int>();
            var beaverIsActive = new List<bool>();
            var beaverHistoryRecordIds = new List<Guid>();
            var beaverHistoryEntityIds = new List<Guid>();
            var beaverHistoryReasons = new List<string>();
            var beaverHistoryNames = new List<string>();
            var beaverHistoryRecordedAtUtc = new List<string>();
            var beaverHistoryAges = new List<int>();

            foreach (var beaverRecord in _beaversByRecordId.Values)
            {
                beaverRecordIds.Add(beaverRecord.BeaverRecordId);
                beaverEntityIds.Add(beaverRecord.EntityId);
                beaverOriginalNames.Add(beaverRecord.OriginalName ?? "");
                beaverAssignedNames.Add(beaverRecord.AssignedName ?? "");
                beaverAssignedAtUtc.Add(beaverRecord.AssignedAtUtc ?? "");
                beaverLastSeenAtUtc.Add(beaverRecord.LastSeenAtUtc ?? "");
                beaverRenameCounts.Add(beaverRecord.RenameCount);
                beaverRerollCounts.Add(beaverRecord.RerollCount);
                beaverIsActive.Add(beaverRecord.IsActive);

                if (beaverRecord.EntityHistory == null)
                {
                    continue;
                }

                foreach (var historyEntry in beaverRecord.EntityHistory)
                {
                    if (historyEntry == null || historyEntry.EntityId == Guid.Empty)
                    {
                        continue;
                    }

                    beaverHistoryRecordIds.Add(beaverRecord.BeaverRecordId);
                    beaverHistoryEntityIds.Add(historyEntry.EntityId);
                    beaverHistoryReasons.Add(historyEntry.Reason ?? "");
                    beaverHistoryNames.Add(historyEntry.NameAtTime ?? "");
                    beaverHistoryRecordedAtUtc.Add(historyEntry.RecordedAtUtc ?? "");
                    beaverHistoryAges.Add(historyEntry.AgeAtTime);
                }
            }

            var pendingClaimSources = new List<string>();
            var pendingClaimSourceUserIds = new List<string>();
            var pendingClaimLoginNames = new List<string>();
            var pendingClaimDisplayNames = new List<string>();

            foreach (var pendingClaim in _pendingClaims)
            {
                if (pendingClaim == null || !pendingClaim.IsValid)
                {
                    continue;
                }

                pendingClaimSources.Add(pendingClaim.Source ?? "");
                pendingClaimSourceUserIds.Add(pendingClaim.SourceUserId ?? "");
                pendingClaimLoginNames.Add(pendingClaim.LoginName ?? "");
                pendingClaimDisplayNames.Add(pendingClaim.DisplayName ?? "");
            }

            var saver = singletonSaver.GetSingleton(RegistryKey);

            saver.Set(ViewerSourcesKey, viewerSources);
            saver.Set(ViewerSourceUserIdsKey, viewerSourceUserIds);
            saver.Set(ViewerDisplayNamesKey, viewerDisplayNames);
            saver.Set(ViewerBeaverRecordIdsKey, viewerBeaverRecordIds);
            saver.Set(ViewerHasLivingClaimsKey, viewerHasLivingClaims);
            saver.Set(ViewerLastAssignedBeaverNamesKey, viewerLastAssignedBeaverNames);
            saver.Set(ViewerLastDeadBeaverNamesKey, viewerLastDeadBeaverNames);
            saver.Set(ViewerQueuedAfterDeathKey, viewerQueuedAfterDeath);
            saver.Set(ViewerFirstSeenAtUtcKey, viewerFirstSeenAtUtc);
            saver.Set(ViewerLastSeenAtUtcKey, viewerLastSeenAtUtc);
            saver.Set(ViewerChatMessageCountsKey, viewerChatMessageCounts);
            saver.Set(ViewerNameColorHexesKey, viewerNameColorHexes);
            saver.Set(ViewerNameShadowColorHexesKey, viewerNameShadowColorHexes);

            saver.Set(BeaverRecordIdsKey, beaverRecordIds);
            saver.Set(BeaverEntityIdsKey, beaverEntityIds);
            saver.Set(BeaverOriginalNamesKey, beaverOriginalNames);
            saver.Set(BeaverAssignedNamesKey, beaverAssignedNames);
            saver.Set(BeaverAssignedAtUtcKey, beaverAssignedAtUtc);
            saver.Set(BeaverLastSeenAtUtcKey, beaverLastSeenAtUtc);
            saver.Set(BeaverRenameCountsKey, beaverRenameCounts);
            saver.Set(BeaverRerollCountsKey, beaverRerollCounts);
            saver.Set(BeaverIsActiveKey, beaverIsActive);
            saver.Set(BeaverHistoryRecordIdsKey, beaverHistoryRecordIds);
            saver.Set(BeaverHistoryEntityIdsKey, beaverHistoryEntityIds);
            saver.Set(BeaverHistoryReasonsKey, beaverHistoryReasons);
            saver.Set(BeaverHistoryNamesKey, beaverHistoryNames);
            saver.Set(BeaverHistoryRecordedAtUtcKey, beaverHistoryRecordedAtUtc);
            saver.Set(BeaverHistoryAgesKey, beaverHistoryAges);

            saver.Set(PendingClaimSourcesKey, pendingClaimSources);
            saver.Set(PendingClaimSourceUserIdsKey, pendingClaimSourceUserIds);
            saver.Set(PendingClaimLoginNamesKey, pendingClaimLoginNames);
            saver.Set(PendingClaimDisplayNamesKey, pendingClaimDisplayNames);
        }

        private Character TryGetBeaver(ViewerIdentity viewer, BeaverNamePolicy policy)
        {
            if (viewer == null || !viewer.IsValid)
            {
                TwitchBornLog.Info("Cannot get beaver for invalid viewer.");
                return null;
            }

            var viewerRecord = TryGetViewerRecord(viewer);

            if (viewerRecord == null)
            {
                TwitchBornLog.Info("No viewer record for " + viewer.SafeDisplayName);
                return null;
            }

            var beaverRecord = TryGetBeaverRecord(viewerRecord.BeaverRecordId);

            if (beaverRecord == null)
            {
                TwitchBornLog.Info("No beaver record for " + viewer.SafeDisplayName);
                return null;
            }

            var character = FindActiveBeaverByEntityId(beaverRecord.EntityId);

            return ValidateBeaver(
                beaverRecord,
                character,
                policy);
        }

        public Character TryGetBeaver(ViewerIdentity viewer)
        {
            return TryGetBeaver(viewer, BeaverNamePolicy.EnforceAssignedName);
        }

        public Character TryGetBeaverWithoutRename(ViewerIdentity viewer)
        {
            return TryGetBeaver(viewer, BeaverNamePolicy.DoNotRename);
        }

        public Character ClaimBeaver(ViewerIdentity viewer)
        {
            if (viewer == null || !viewer.IsValid)
            {
                TwitchBornLog.Info("Cannot claim beaver for invalid viewer.");
                return null;
            }

            var viewerRecord = GetOrCreateViewerRecord(viewer);

            var existingBeaverRecord = TryGetBeaverRecord(viewerRecord.BeaverRecordId);

            if (existingBeaverRecord != null)
            {
                var existingCharacter = FindActiveBeaverByEntityId(existingBeaverRecord.EntityId);

                var validatedCharacter = ValidateBeaver(
                    existingBeaverRecord,
                    existingCharacter,
                    BeaverNamePolicy.EnforceAssignedName);

                if (validatedCharacter != null)
                {
                    viewerRecord.HasLivingClaim = true;
                    viewerRecord.LastDeadBeaverName = "";
                    viewerRecord.QueuedAfterDeath = false;
                    viewerRecord.LastAssignedBeaverName = SanitizeCleanBeaverName(existingBeaverRecord.AssignedName);
                    return validatedCharacter;
                }

                MarkBeaverRecordStale(existingBeaverRecord, "lost");
                viewerRecord.BeaverRecordId = Guid.Empty;
                viewerRecord.HasLivingClaim = false;

                TwitchBornLog.Info("Existing beaver record for " + viewer.SafeDisplayName + " is stale. Registering a new beaver.");
            }

            var registeredCharacter = RegisterBeaver(viewer, viewerRecord);

            if (registeredCharacter == null)
            {
                return null;
            }

            var registeredBeaverRecord = TryGetBeaverRecord(viewerRecord.BeaverRecordId);

            if (registeredBeaverRecord == null)
            {
                TwitchBornLog.Info("Registered beaver for " + viewer.SafeDisplayName + " but beaver record was missing.");
                viewerRecord.HasLivingClaim = true;
                viewerRecord.LastDeadBeaverName = "";
                viewerRecord.QueuedAfterDeath = false;
                return registeredCharacter;
            }

            viewerRecord.HasLivingClaim = true;
            viewerRecord.LastDeadBeaverName = "";
            viewerRecord.QueuedAfterDeath = false;
            viewerRecord.LastAssignedBeaverName = SanitizeCleanBeaverName(registeredBeaverRecord.AssignedName);

            return ValidateBeaver(
                registeredBeaverRecord,
                registeredCharacter,
                BeaverNamePolicy.EnforceAssignedName);
        }

        public Character RenameBeaver(
            ViewerIdentity viewer,
            string requestedName,
            out string safeName)
        {
            safeName = SanitizeCleanBeaverName(requestedName);

            if (viewer == null || !viewer.IsValid)
            {
                TwitchBornLog.Info("Cannot rename beaver for invalid viewer.");
                return null;
            }

            if (string.IsNullOrEmpty(safeName))
            {
                TwitchBornLog.Info("Cannot rename beaver for " + viewer.SafeDisplayName + " because the requested name was empty.");
                return null;
            }

            var viewerRecord = TryGetViewerRecord(viewer);

            if (viewerRecord == null)
            {
                TwitchBornLog.Info("Cannot rename beaver for " + viewer.SafeDisplayName + " because no viewer record exists.");
                return null;
            }

            var beaverRecord = TryGetBeaverRecord(viewerRecord.BeaverRecordId);

            if (beaverRecord == null)
            {
                TwitchBornLog.Info("Cannot rename beaver for " + viewer.SafeDisplayName + " because no beaver record exists.");
                return null;
            }

            var character = FindActiveBeaverByEntityId(beaverRecord.EntityId);

            var validatedCharacter = ValidateBeaver(
                beaverRecord,
                character,
                BeaverNamePolicy.DoNotRename);

            if (validatedCharacter == null)
            {
                return null;
            }

            var namedEntity = validatedCharacter.GetComponent<NamedEntity>();

            if (namedEntity == null)
            {
                return null;
            }

            beaverRecord.AssignedName = safeName;
            beaverRecord.RenameCount++;
            beaverRecord.LastSeenAtUtc = NowUtc();
            viewerRecord.LastAssignedBeaverName = safeName;
            viewerRecord.HasLivingClaim = true;
            viewerRecord.LastDeadBeaverName = "";
            viewerRecord.QueuedAfterDeath = false;

            SetEntityNameInternally(
                beaverRecord.EntityId,
                namedEntity,
                BuildStyledEntityName(beaverRecord, viewerRecord));

            MarkCacheDirty();

            TwitchBornLog.Info("Renamed claimed beaver for " + viewer.SafeDisplayName + " to " + safeName + ".");

            return validatedCharacter;
        }

        public bool IsPendingClaim(ViewerIdentity viewer)
        {
            if (viewer == null || !viewer.IsValid)
            {
                return false;
            }

            return _pendingClaimKeys.Contains(ViewerIdentity.CreateViewerKey(viewer));
        }

        public bool TryGetPreviousClaimDiedResult(
            ViewerIdentity viewer,
            out BeaverCommandResult result)
        {
            result = null;

            if (viewer == null || !viewer.IsValid)
            {
                return false;
            }

            var viewerRecord = TryGetViewerRecord(viewer);

            if (viewerRecord == null)
            {
                return false;
            }

            if (viewerRecord.HasLivingClaim)
            {
                return false;
            }

            if (string.IsNullOrEmpty(viewerRecord.LastDeadBeaverName))
            {
                return false;
            }

            result = new BeaverCommandResult(
                viewerRecord.QueuedAfterDeath
                    ? BeaverCommandResultType.AlreadyQueuedAfterDeath
                    : BeaverCommandResultType.PreviousClaimDied,
                viewer,
                null,
                viewerRecord.LastDeadBeaverName,
                "",
                viewerRecord.LastDeadBeaverName,
                !viewerRecord.QueuedAfterDeath,
                false,
                viewerRecord.QueuedAfterDeath);

            return true;
        }

        public bool TryEnqueuePendingClaim(ViewerIdentity viewer)
        {
            if (viewer == null || !viewer.IsValid)
            {
                TwitchBornLog.Info("Cannot queue invalid viewer claim.");
                return false;
            }

            var viewerKey = ViewerIdentity.CreateViewerKey(viewer);

            if (_pendingClaimKeys.Contains(viewerKey))
            {
                return false;
            }

            GetOrCreateViewerRecord(viewer);

            var pendingViewer = new ViewerIdentity(
                viewer.Source,
                viewer.SourceUserId,
                viewer.LoginName,
                viewer.SafeDisplayName);

            _pendingClaims.Add(pendingViewer);
            _pendingClaimKeys.Add(viewerKey);

            TwitchBornLog.Info("Queued pending beaver claim for " + viewer.SafeDisplayName + ".");

            return true;
        }

        public void ProcessPendingClaims()
        {
            if (_pendingClaims.Count == 0)
            {
                return;
            }

            TwitchBornLog.Info("Processing pending beaver claim queue. Pending: " + _pendingClaims.Count);

            var assignedResults = new List<BeaverCommandResult>();

            var index = 0;

            while (index < _pendingClaims.Count)
            {
                var viewer = _pendingClaims[index];

                if (viewer == null || !viewer.IsValid)
                {
                    RemovePendingClaimAt(index);
                    continue;
                }

                var existingBeaver = TryGetBeaver(viewer);

                if (existingBeaver != null)
                {
                    RemovePendingClaimAt(index);
                    continue;
                }

                var claimedBeaver = ClaimBeaver(viewer);

                if (claimedBeaver == null)
                {
                    index++;
                    continue;
                }

                RemovePendingClaimAt(index);

                var result = new BeaverCommandResult(
                    BeaverCommandResultType.AssignedFromQueue,
                    viewer,
                    claimedBeaver,
                    GetCharacterName(claimedBeaver),
                    "currently claimed");

                assignedResults.Add(result);
            }

            foreach (var assignedResult in assignedResults)
            {
                TwitchBornLog.Info(
                    "Assigned pending claim: " +
                    assignedResult.Viewer.SafeDisplayName +
                    " -> " +
                    assignedResult.BeaverName);

                PendingClaimAssigned?.Invoke(assignedResult);
            }
        }

        public void RecordChatMessage(ViewerIdentity viewer)
        {
            var viewerRecord = TryGetViewerRecord(viewer);

            if (viewerRecord == null)
            {
                return;
            }

            viewerRecord.DisplayName = viewer.SafeDisplayName;
            viewerRecord.LastSeenAtUtc = NowUtc();
            viewerRecord.ChatMessageCount++;
        }

        public bool TrySetViewerNameColour(
            ViewerIdentity viewer,
            string requestedColour,
            out string normalizedColour)
        {
            normalizedColour = "";

            if (viewer == null || !viewer.IsValid)
            {
                return false;
            }

            if (!TryNormalizeNameColorHex(requestedColour, out normalizedColour))
            {
                return false;
            }

            var viewerRecord = GetOrCreateViewerRecord(viewer);
            viewerRecord.NameColorHex = normalizedColour;
            viewerRecord.LastSeenAtUtc = NowUtc();

            EnforceViewerStyledName(viewerRecord);
            MarkCacheDirty();
            return true;
        }

        public void ClearViewerNameColour(ViewerIdentity viewer)
        {
            if (viewer == null || !viewer.IsValid)
            {
                return;
            }

            var viewerRecord = GetOrCreateViewerRecord(viewer);
            viewerRecord.NameColorHex = "";
            viewerRecord.LastSeenAtUtc = NowUtc();

            EnforceViewerStyledName(viewerRecord);
            MarkCacheDirty();
        }

        public bool TrySetViewerNameShadow(
            ViewerIdentity viewer,
            string requestedColour,
            out string normalizedColour)
        {
            normalizedColour = "";

            if (viewer == null || !viewer.IsValid)
            {
                return false;
            }

            if (!TryNormalizeShadowColorHex(requestedColour, out normalizedColour))
            {
                return false;
            }

            var viewerRecord = GetOrCreateViewerRecord(viewer);
            viewerRecord.NameShadowColorHex = normalizedColour;
            viewerRecord.LastSeenAtUtc = NowUtc();

            MarkCacheDirty();
            return true;
        }

        public void ClearViewerNameShadow(ViewerIdentity viewer)
        {
            if (viewer == null || !viewer.IsValid)
            {
                return;
            }

            var viewerRecord = GetOrCreateViewerRecord(viewer);
            viewerRecord.NameShadowColorHex = "";
            viewerRecord.LastSeenAtUtc = NowUtc();

            MarkCacheDirty();
        }

        public bool TryGetClaimedViewerName(
            Character character,
            out string viewerName)
        {
            viewerName = "";

            if (character == null)
            {
                return false;
            }

            var entityId = GetEntityId(character);

            if (entityId == Guid.Empty)
            {
                return false;
            }

            BeaverRecord beaverRecord;

            if (!_beaversByEntityId.TryGetValue(entityId, out beaverRecord))
            {
                return false;
            }

            if (beaverRecord == null || !beaverRecord.IsActive)
            {
                return false;
            }

            foreach (var viewerRecord in _viewersByKey.Values)
            {
                if (viewerRecord == null)
                {
                    continue;
                }

                if (viewerRecord.BeaverRecordId != beaverRecord.BeaverRecordId)
                {
                    continue;
                }

                viewerName = string.IsNullOrEmpty(viewerRecord.DisplayName)
                    ? beaverRecord.AssignedName
                    : viewerRecord.DisplayName;

                return !string.IsNullOrEmpty(viewerName);
            }

            return false;
        }

        public bool TryGetClaimedViewerNameByUniqueDisplayedName(
            string displayedName,
            out string viewerName)
        {
            viewerName = "";

            if (string.IsNullOrEmpty(displayedName))
            {
                return false;
            }

            var matches = 0;
            var matchedViewerName = "";

            foreach (var target in GetClaimedBeaverOverlayTargets())
            {
                if (target == null || target.Character == null)
                {
                    continue;
                }

                if (!string.Equals(target.BeaverName, displayedName, StringComparison.Ordinal))
                {
                    continue;
                }

                matches++;
                matchedViewerName = target.ViewerName;

                if (matches > 1)
                {
                    viewerName = "";
                    return false;
                }
            }

            if (matches != 1 || string.IsNullOrEmpty(matchedViewerName))
            {
                return false;
            }

            viewerName = matchedViewerName;
            return true;
        }

        public List<ClaimedBeaverOverlayTarget> GetClaimedBeaverOverlayTargets()
        {
            var targets = new List<ClaimedBeaverOverlayTarget>();
            var seenEntityIds = new HashSet<Guid>();

            foreach (var viewerRecord in _viewersByKey.Values)
            {
                if (viewerRecord == null || viewerRecord.BeaverRecordId == Guid.Empty)
                {
                    continue;
                }

                var beaverRecord = TryGetBeaverRecord(viewerRecord.BeaverRecordId);

                if (beaverRecord == null || !beaverRecord.IsActive || beaverRecord.EntityId == Guid.Empty)
                {
                    continue;
                }

                if (seenEntityIds.Contains(beaverRecord.EntityId))
                {
                    continue;
                }

                var character = FindActiveBeaverByEntityId(beaverRecord.EntityId);

                if (character == null || !character.Alive)
                {
                    continue;
                }

                var beaverName = beaverRecord.AssignedName;

                NamedEntity namedEntity;

                if (character.TryGetComponent(out namedEntity) && !string.IsNullOrEmpty(namedEntity.EntityName))
                {
                    beaverName = namedEntity.EntityName;
                }

                targets.Add(new ClaimedBeaverOverlayTarget(
                    character,
                    BuildStyledEntityName(beaverRecord, viewerRecord),
                    string.IsNullOrEmpty(viewerRecord.DisplayName) ? beaverRecord.AssignedName : viewerRecord.DisplayName,
                    viewerRecord.NameColorHex,
                    viewerRecord.NameShadowColorHex));

                seenEntityIds.Add(beaverRecord.EntityId);
            }

            return targets;
        }

        public void MarkCacheDirty()
        {
            _cacheDirty = true;
        }

        [OnEvent]
        public void OnCharacterCreated(CharacterCreatedEvent characterCreatedEvent)
        {
            if (characterCreatedEvent == null || characterCreatedEvent.Character == null)
            {
                return;
            }

            if (IsClaimableCharacter(characterCreatedEvent.Character))
            {
                MarkCacheDirty();
                TryRebindCreatedCharacter(characterCreatedEvent.Character);
                ProcessPendingClaims();
            }
        }

        [OnEvent]
        public void OnCharacterKilled(CharacterKilledEvent characterKilledEvent)
        {
            if (characterKilledEvent == null || characterKilledEvent.Character == null)
            {
                return;
            }

            if (!IsClaimableCharacter(characterKilledEvent.Character))
            {
                return;
            }

            MarkCacheDirty();

            var entityId = GetEntityId(characterKilledEvent.Character);

            if (entityId == Guid.Empty)
            {
                return;
            }

            BeaverRecord beaverRecord;

            if (_beaversByEntityId.TryGetValue(entityId, out beaverRecord))
            {
                HandleClaimedBeaverKilled(beaverRecord, characterKilledEvent.Character);
                return;
            }

            if (_beaversByHistoricalEntityId.TryGetValue(entityId, out beaverRecord))
            {
                AddBeaverHistoryEntry(beaverRecord, entityId, "previous_entity_removed", GetCharacterName(characterKilledEvent.Character), characterKilledEvent.Character.Age);
            }
        }

        [OnEvent]
        public void OnEntityNameChanged(EntityNameChangedEvent entityNameChangedEvent)
        {
            if (entityNameChangedEvent == null || entityNameChangedEvent.Entity == null)
            {
                return;
            }

            var entityId = entityNameChangedEvent.Entity.EntityId;
            var isInternalRename = _internalRenameEntityIds.Remove(entityId);

            BeaverRecord beaverRecord;

            if (!_beaversByEntityId.TryGetValue(entityId, out beaverRecord))
            {
                return;
            }

            if (beaverRecord == null || !beaverRecord.IsActive)
            {
                return;
            }

            var character = FindActiveBeaverByEntityId(entityId);

            if (character == null)
            {
                ValidateBeaver(
                    beaverRecord,
                    character,
                    BeaverNamePolicy.EnforceAssignedName);

                return;
            }

            var namedEntity = character.GetComponent<NamedEntity>();

            if (namedEntity == null)
            {
                ValidateBeaver(
                    beaverRecord,
                    character,
                    BeaverNamePolicy.EnforceAssignedName);

                return;
            }

            var expectedName = GetExpectedBeaverName(beaverRecord);
            var nameWasInvalid = !string.IsNullOrEmpty(expectedName) &&
                !string.Equals(namedEntity.EntityName, expectedName, StringComparison.Ordinal);

            ValidateBeaver(
                beaverRecord,
                character,
                BeaverNamePolicy.EnforceAssignedName);

            if (isInternalRename || !nameWasInvalid)
            {
                return;
            }

            _eventBus.Post(new ClaimedBeaverRenameRejectedEvent(
                expectedName,
                GetViewerNameForBeaverRecord(beaverRecord)));
        }

        private void HandleClaimedBeaverKilled(BeaverRecord beaverRecord, Character deadCharacter)
        {
            if (beaverRecord == null)
            {
                return;
            }

            var viewerRecord = GetViewerRecordForBeaverRecord(beaverRecord);
            var viewer = CreateViewerIdentity(viewerRecord);
            var deadBeaverName = GetLifecycleBeaverName(beaverRecord, deadCharacter);

            MarkBeaverRecordStale(beaverRecord, "entity_removed");

            if (viewerRecord != null)
            {
                viewerRecord.BeaverRecordId = Guid.Empty;
                viewerRecord.HasLivingClaim = false;
                viewerRecord.LastDeadBeaverName = deadBeaverName;
                viewerRecord.QueuedAfterDeath = false;

                if (_claimSettingsOwner.KeepAssignedNameAcrossClaims.Value)
                {
                    viewerRecord.LastAssignedBeaverName = deadBeaverName;
                }
            }

            if (viewer == null || !viewer.IsValid)
            {
                return;
            }

            if (_claimSettingsOwner.AutoClaimOnDeath.Value)
            {
                var replacement = ClaimBeaver(viewer);

                if (replacement != null)
                {
                    if (_claimSettingsOwner.NotifyViewerOnDeath.Value)
                    {
                        NotifyBeaverLifecycle(new BeaverCommandResult(
                            BeaverCommandResultType.AutoReclaimedAfterDeath,
                            viewer,
                            replacement,
                            GetCharacterName(replacement),
                            "",
                            deadBeaverName,
                            false,
                            true,
                            false));
                    }

                    return;
                }

                if (TryEnqueuePendingClaim(viewer))
                {
                    if (viewerRecord != null)
                    {
                        viewerRecord.QueuedAfterDeath = true;
                    }

                    if (_claimSettingsOwner.NotifyViewerOnDeath.Value)
                    {
                        NotifyBeaverLifecycle(new BeaverCommandResult(
                            BeaverCommandResultType.QueuedAfterDeath,
                            viewer,
                            null,
                            "",
                            "",
                            deadBeaverName,
                            false,
                            false,
                            true));
                    }

                    return;
                }
            }

            if (_claimSettingsOwner.NotifyViewerOnDeath.Value)
            {
                NotifyBeaverLifecycle(new BeaverCommandResult(
                    BeaverCommandResultType.Died,
                    viewer,
                    null,
                    deadBeaverName,
                    "",
                    deadBeaverName,
                    false,
                    false,
                    false));
            }
        }

        private void TryRebindCreatedCharacter(Character character)
        {
            if (character == null || !character.Alive)
            {
                return;
            }

            var newEntityId = GetEntityId(character);

            if (newEntityId == Guid.Empty)
            {
                return;
            }

            if (_beaversByEntityId.ContainsKey(newEntityId))
            {
                return;
            }

            var characterName = GetCharacterName(character);

            if (string.IsNullOrEmpty(characterName))
            {
                return;
            }

            var beaverRecord = FindRebindCandidateByAssignedName(characterName);

            if (beaverRecord == null)
            {
                return;
            }

            var oldEntityId = beaverRecord.EntityId;

            if (oldEntityId == newEntityId)
            {
                return;
            }

            RebindBeaverRecord(beaverRecord, character, newEntityId, "grow_up_rebind");
        }

        private BeaverRecord FindRebindCandidateByAssignedName(string characterName)
        {
            BeaverRecord candidate = null;

            foreach (var beaverRecord in _beaversByRecordId.Values)
            {
                if (beaverRecord == null || string.IsNullOrEmpty(beaverRecord.AssignedName))
                {
                    continue;
                }

                if (!string.Equals(beaverRecord.AssignedName, characterName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (candidate != null)
                {
                    return null;
                }

                candidate = beaverRecord;
            }

            if (candidate == null)
            {
                return null;
            }

            var currentCharacter = FindActiveBeaverByEntityId(candidate.EntityId);

            if (currentCharacter != null && currentCharacter.Alive && IsCharacterVisiblyActive(currentCharacter))
            {
                return null;
            }

            return candidate;
        }

        private void RebindBeaverRecord(
            BeaverRecord beaverRecord,
            Character character,
            Guid newEntityId,
            string reason)
        {
            if (beaverRecord == null || character == null || newEntityId == Guid.Empty)
            {
                return;
            }

            var oldEntityId = beaverRecord.EntityId;

            if (oldEntityId != Guid.Empty)
            {
                _beaversByEntityId.Remove(oldEntityId);
                _beaversByHistoricalEntityId[oldEntityId] = beaverRecord;
            }

            beaverRecord.EntityId = newEntityId;
            beaverRecord.IsActive = true;
            beaverRecord.LastSeenAtUtc = NowUtc();

            _beaversByEntityId[newEntityId] = beaverRecord;

            AddBeaverHistoryEntry(beaverRecord, newEntityId, reason, GetCharacterName(character), character.Age);

            var namedEntity = character.GetComponent<NamedEntity>();

            if (namedEntity != null)
            {
                EnforceAssignedName(beaverRecord, namedEntity);
            }

            MarkCacheDirty();

            TwitchBornLog.Info("Rebound claimed beaver " + GetBeaverRecordLabel(beaverRecord) + " from " + oldEntityId + " to " + newEntityId + " via " + reason + ".");

            var viewerRecord = GetViewerRecordForBeaverRecord(beaverRecord);

            if (viewerRecord != null)
            {
                viewerRecord.BeaverRecordId = beaverRecord.BeaverRecordId;
                viewerRecord.HasLivingClaim = true;
                viewerRecord.LastDeadBeaverName = "";
                viewerRecord.QueuedAfterDeath = false;
                viewerRecord.LastAssignedBeaverName = SanitizeCleanBeaverName(beaverRecord.AssignedName);
            }

            if (_claimSettingsOwner.NotifyViewerOnGrowUp.Value &&
                string.Equals(reason, "grow_up_rebind", StringComparison.Ordinal))
            {
                var viewer = CreateViewerIdentity(viewerRecord);

                NotifyBeaverLifecycle(new BeaverCommandResult(
                    BeaverCommandResultType.GrownUp,
                    viewer,
                    character,
                    GetLifecycleBeaverName(beaverRecord, character),
                    "",
                    "",
                    false,
                    false,
                    false));
            }
        }

        private void MarkBeaverRecordStale(BeaverRecord beaverRecord, string reason)
        {
            if (beaverRecord == null)
            {
                return;
            }

            if (beaverRecord.EntityId != Guid.Empty)
            {
                _beaversByEntityId.Remove(beaverRecord.EntityId);
                _beaversByHistoricalEntityId[beaverRecord.EntityId] = beaverRecord;
                AddBeaverHistoryEntry(beaverRecord, beaverRecord.EntityId, reason, beaverRecord.AssignedName, -1);
            }

            beaverRecord.IsActive = false;
            beaverRecord.LastSeenAtUtc = NowUtc();

            MarkCacheDirty();
        }

        private static bool IsCharacterVisiblyActive(Character character)
        {
            if (character == null)
            {
                return false;
            }

            return character.GameObject != null && character.GameObject.activeInHierarchy;
        }

        private string GetAssignedNameForClaim(ViewerIdentity viewer, ViewerRecord viewerRecord)
        {
            if (_claimSettingsOwner.KeepAssignedNameAcrossClaims.Value &&
                viewerRecord != null &&
                !string.IsNullOrEmpty(viewerRecord.LastAssignedBeaverName))
            {
                var previousAssignedName = SanitizeBeaverName(viewerRecord.LastAssignedBeaverName);

                if (!string.IsNullOrEmpty(previousAssignedName))
                {
                    return previousAssignedName;
                }
            }

            if (viewer == null)
            {
                return "";
            }

            return SanitizeBeaverName(viewer.SafeDisplayName);
        }

        private Character RegisterBeaver(ViewerIdentity viewer, ViewerRecord viewerRecord)
        {
            var assignedName = GetAssignedNameForClaim(viewer, viewerRecord);

            if (string.IsNullOrEmpty(assignedName))
            {
                TwitchBornLog.Info("Cannot register beaver for empty assigned name.");
                return null;
            }

            var preNamedCharacter = FindUnassignedActiveBeaverByName(assignedName);

            if (preNamedCharacter != null)
            {
                var entityId = GetEntityId(preNamedCharacter);
                var namedEntity = preNamedCharacter.GetComponent<NamedEntity>();

                var beaverRecord = CreateBeaverRecord(
                    entityId,
                    namedEntity.EntityName,
                    assignedName,
                    true);

                AddBeaverHistoryEntry(beaverRecord, entityId, "claimed", namedEntity.EntityName, preNamedCharacter.Age);
                AddBeaverRecord(beaverRecord);

                viewerRecord.BeaverRecordId = beaverRecord.BeaverRecordId;
                viewerRecord.HasLivingClaim = true;
                viewerRecord.LastAssignedBeaverName = assignedName;
                viewerRecord.LastDeadBeaverName = "";
                viewerRecord.QueuedAfterDeath = false;

                TwitchBornLog.Info("Adopted pre-named beaver: " + assignedName + " -> " + entityId);
                return preNamedCharacter;
            }

            var availableCharacter = FindYoungestUnassignedActiveBeaver();

            if (availableCharacter == null)
            {
                TwitchBornLog.Info("No available beaver to assign for " + assignedName);
                return null;
            }

            var availableNamedEntity = availableCharacter.GetComponent<NamedEntity>();
            var availableEntityId = GetEntityId(availableCharacter);
            var originalName = availableNamedEntity.EntityName;

            TwitchBornLog.Info("Assigning beaver " + originalName + " to " + assignedName);

            SetEntityNameInternally(availableEntityId, availableNamedEntity, assignedName);

            var newBeaverRecord = CreateBeaverRecord(
                availableEntityId,
                originalName,
                assignedName,
                true);

            AddBeaverHistoryEntry(newBeaverRecord, availableEntityId, "claimed", assignedName, availableCharacter.Age);
            AddBeaverRecord(newBeaverRecord);

            viewerRecord.BeaverRecordId = newBeaverRecord.BeaverRecordId;
            viewerRecord.HasLivingClaim = true;
            viewerRecord.LastAssignedBeaverName = assignedName;
            viewerRecord.LastDeadBeaverName = "";
            viewerRecord.QueuedAfterDeath = false;

            MarkCacheDirty();

            TwitchBornLog.Info("Assigned " + assignedName + " -> " + availableEntityId);
            return availableCharacter;
        }

        private Character ValidateBeaver(
            BeaverRecord beaverRecord,
            Character character,
            BeaverNamePolicy namePolicy)
        {
            if (beaverRecord == null)
            {
                return null;
            }

            if (character == null)
            {
                MarkBeaverRecordStale(beaverRecord, "lost");
                return null;
            }

            var beaverLabel = GetBeaverRecordLabel(beaverRecord);

            if (!character.Alive)
            {
                MarkBeaverRecordStale(beaverRecord, "entity_removed");
                TwitchBornLog.Info("Beaver record " + beaverLabel + " points to a dead beaver.");
                return null;
            }

            if (!IsClaimableCharacter(character))
            {
                beaverRecord.IsActive = false;
                TwitchBornLog.Info("Beaver record " + beaverLabel + " points to a non-claimable character.");
                return null;
            }

            var namedEntity = character.GetComponent<NamedEntity>();

            if (namedEntity == null)
            {
                beaverRecord.IsActive = false;
                TwitchBornLog.Info("Beaver record " + beaverLabel + " points to a beaver without NamedEntity.");
                return null;
            }

            if (namePolicy == BeaverNamePolicy.EnforceAssignedName)
            {
                EnforceAssignedName(beaverRecord, namedEntity);
            }

            beaverRecord.LastSeenAtUtc = NowUtc();
            beaverRecord.IsActive = true;

            TwitchBornLog.Info("Beaver match: " + beaverLabel + " -> " + beaverRecord.EntityId);
            return character;
        }

        private void EnforceAssignedName(
            BeaverRecord beaverRecord,
            NamedEntity namedEntity)
        {
            var expectedName = GetExpectedBeaverName(beaverRecord);

            if (string.IsNullOrEmpty(expectedName))
            {
                return;
            }

            if (string.Equals(namedEntity.EntityName, expectedName, StringComparison.Ordinal))
            {
                return;
            }

            TwitchBornLog.Info("Beaver name mismatch. Renaming " + namedEntity.EntityName + " to " + expectedName);

            SetEntityNameInternally(beaverRecord.EntityId, namedEntity, expectedName);

            beaverRecord.AssignedName = SanitizeCleanBeaverName(beaverRecord.AssignedName);
            beaverRecord.RenameCount++;

            MarkCacheDirty();
        }

        private void SetEntityNameInternally(
            Guid entityId,
            NamedEntity namedEntity,
            string name)
        {
            if (namedEntity == null)
            {
                return;
            }

            if (entityId != Guid.Empty)
            {
                _internalRenameEntityIds.Add(entityId);
            }

            namedEntity.SetEntityName(name);
        }

        private void EnforceViewerStyledName(ViewerRecord viewerRecord)
        {
            if (viewerRecord == null || viewerRecord.BeaverRecordId == Guid.Empty)
            {
                return;
            }

            var beaverRecord = TryGetBeaverRecord(viewerRecord.BeaverRecordId);

            if (beaverRecord == null || beaverRecord.EntityId == Guid.Empty)
            {
                return;
            }

            var character = FindActiveBeaverByEntityId(beaverRecord.EntityId);

            if (character == null)
            {
                return;
            }

            var namedEntity = character.GetComponent<NamedEntity>();

            if (namedEntity == null)
            {
                return;
            }

            EnforceAssignedName(beaverRecord, namedEntity);
        }

        private ViewerRecord GetViewerRecordForBeaverRecord(BeaverRecord beaverRecord)
        {
            if (beaverRecord == null)
            {
                return null;
            }

            foreach (var viewerRecord in _viewersByKey.Values)
            {
                if (viewerRecord == null)
                {
                    continue;
                }

                if (viewerRecord.BeaverRecordId == beaverRecord.BeaverRecordId)
                {
                    return viewerRecord;
                }
            }

            return null;
        }

        private static string BuildStyledEntityName(BeaverRecord beaverRecord, ViewerRecord viewerRecord)
        {
            if (beaverRecord == null)
            {
                return "";
            }

            var cleanName = SanitizeCleanBeaverName(beaverRecord.AssignedName);

            if (string.IsNullOrEmpty(cleanName))
            {
                return "";
            }

            var colourHex = viewerRecord == null ? "" : NormalizeNameColorHexOrEmpty(viewerRecord.NameColorHex);

            if (string.IsNullOrEmpty(colourHex))
            {
                return cleanName;
            }

            return "<#" + colourHex.Substring(1, 6) + ">" + cleanName;
        }

        private string GetViewerNameForBeaverRecord(BeaverRecord beaverRecord)
        {
            if (beaverRecord == null)
            {
                return "";
            }

            foreach (var viewerRecord in _viewersByKey.Values)
            {
                if (viewerRecord == null)
                {
                    continue;
                }

                if (viewerRecord.BeaverRecordId != beaverRecord.BeaverRecordId)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(viewerRecord.DisplayName))
                {
                    return viewerRecord.DisplayName;
                }

                return beaverRecord.AssignedName ?? "";
            }

            return beaverRecord.AssignedName ?? "";
        }

        private static string GetBeaverRecordLabel(BeaverRecord beaverRecord)
        {
            if (beaverRecord == null)
            {
                return "<null>";
            }

            if (!string.IsNullOrEmpty(beaverRecord.AssignedName))
            {
                return beaverRecord.AssignedName;
            }

            if (beaverRecord.BeaverRecordId != Guid.Empty)
            {
                return beaverRecord.BeaverRecordId.ToString();
            }

            if (beaverRecord.EntityId != Guid.Empty)
            {
                return beaverRecord.EntityId.ToString();
            }

            return "<unknown>";
        }

        private ViewerRecord TryGetViewerRecord(ViewerIdentity viewer)
        {
            if (viewer == null || !viewer.IsValid)
            {
                return null;
            }

            ViewerRecord viewerRecord;

            if (_viewersByKey.TryGetValue(ViewerIdentity.CreateViewerKey(viewer), out viewerRecord))
            {
                return viewerRecord;
            }

            return null;
        }

        private ViewerRecord GetOrCreateViewerRecord(ViewerIdentity viewer)
        {
            var viewerKey = ViewerIdentity.CreateViewerKey(viewer);

            ViewerRecord viewerRecord;

            if (_viewersByKey.TryGetValue(viewerKey, out viewerRecord))
            {
                viewerRecord.DisplayName = viewer.SafeDisplayName;
                viewerRecord.LastSeenAtUtc = NowUtc();
                return viewerRecord;
            }

            var now = NowUtc();

            viewerRecord = new ViewerRecord
            {
                Source = viewer.Source,
                SourceUserId = viewer.SourceUserId,
                DisplayName = viewer.SafeDisplayName,
                BeaverRecordId = Guid.Empty,
                HasLivingClaim = false,
                LastAssignedBeaverName = "",
                LastDeadBeaverName = "",
                QueuedAfterDeath = false,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                ChatMessageCount = 0,
                NameColorHex = "",
                NameShadowColorHex = ""
            };

            _viewersByKey[viewerKey] = viewerRecord;

            TwitchBornLog.Info("Created viewer record: " + viewer.SafeDisplayName + " / " + viewerKey);
            return viewerRecord;
        }

        private BeaverRecord TryGetBeaverRecord(Guid beaverRecordId)
        {
            if (beaverRecordId == Guid.Empty)
            {
                return null;
            }

            BeaverRecord beaverRecord;

            if (_beaversByRecordId.TryGetValue(beaverRecordId, out beaverRecord))
            {
                return beaverRecord;
            }

            return null;
        }

        private void AddBeaverRecord(BeaverRecord beaverRecord)
        {
            if (beaverRecord == null)
            {
                return;
            }

            if (beaverRecord.EntityHistory == null)
            {
                beaverRecord.EntityHistory = new List<BeaverEntityHistoryEntry>();
            }

            _beaversByRecordId[beaverRecord.BeaverRecordId] = beaverRecord;

            if (beaverRecord.EntityId != Guid.Empty && beaverRecord.IsActive)
            {
                _beaversByEntityId[beaverRecord.EntityId] = beaverRecord;
            }

            foreach (var historyEntry in beaverRecord.EntityHistory)
            {
                if (historyEntry == null || historyEntry.EntityId == Guid.Empty || historyEntry.EntityId == beaverRecord.EntityId)
                {
                    continue;
                }

                _beaversByHistoricalEntityId[historyEntry.EntityId] = beaverRecord;
            }
        }

        private void LoadRecords()
        {
            _viewersByKey.Clear();
            _beaversByRecordId.Clear();
            _beaversByEntityId.Clear();
            _beaversByHistoricalEntityId.Clear();
            _pendingClaims.Clear();
            _pendingClaimKeys.Clear();

            IObjectLoader loader;

            if (!_singletonLoader.TryGetSingleton(RegistryKey, out loader))
            {
                return;
            }

            LoadViewerRecords(loader);
            LoadBeaverRecords(loader);
            LoadBeaverHistoryRecords(loader);
            EnsureLoadedBeaverRecordsHaveHistory();
            RebuildBeaverEntityIndexes();
            LoadPendingClaims(loader);
        }

        private void LoadViewerRecords(IObjectLoader loader)
        {
            if (!loader.Has(ViewerSourcesKey))
            {
                return;
            }

            var sources = loader.Get(ViewerSourcesKey);
            var sourceUserIds = loader.Has(ViewerSourceUserIdsKey) ? loader.Get(ViewerSourceUserIdsKey) : new List<string>();
            var displayNames = loader.Has(ViewerDisplayNamesKey) ? loader.Get(ViewerDisplayNamesKey) : new List<string>();
            var beaverRecordIds = loader.Has(ViewerBeaverRecordIdsKey) ? loader.Get(ViewerBeaverRecordIdsKey) : new List<Guid>();
            var hasLivingClaims = loader.Has(ViewerHasLivingClaimsKey) ? loader.Get(ViewerHasLivingClaimsKey) : new List<bool>();
            var lastAssignedBeaverNames = loader.Has(ViewerLastAssignedBeaverNamesKey) ? loader.Get(ViewerLastAssignedBeaverNamesKey) : new List<string>();
            var lastDeadBeaverNames = loader.Has(ViewerLastDeadBeaverNamesKey) ? loader.Get(ViewerLastDeadBeaverNamesKey) : new List<string>();
            var queuedAfterDeath = loader.Has(ViewerQueuedAfterDeathKey) ? loader.Get(ViewerQueuedAfterDeathKey) : new List<bool>();
            var firstSeenAtUtc = loader.Has(ViewerFirstSeenAtUtcKey) ? loader.Get(ViewerFirstSeenAtUtcKey) : new List<string>();
            var lastSeenAtUtc = loader.Has(ViewerLastSeenAtUtcKey) ? loader.Get(ViewerLastSeenAtUtcKey) : new List<string>();
            var chatMessageCounts = loader.Has(ViewerChatMessageCountsKey) ? loader.Get(ViewerChatMessageCountsKey) : new List<int>();
            var nameColorHexes = loader.Has(ViewerNameColorHexesKey) ? loader.Get(ViewerNameColorHexesKey) : new List<string>();
            var nameShadowColorHexes = loader.Has(ViewerNameShadowColorHexesKey) ? loader.Get(ViewerNameShadowColorHexesKey) : new List<string>();

            for (var i = 0; i < sources.Count; i++)
            {
                var source = GetStringAt(sources, i);
                var sourceUserId = GetStringAt(sourceUserIds, i);

                if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(sourceUserId))
                {
                    continue;
                }

                var viewerRecord = new ViewerRecord
                {
                    Source = source,
                    SourceUserId = sourceUserId,
                    DisplayName = GetStringAt(displayNames, i),
                    BeaverRecordId = GetGuidAt(beaverRecordIds, i),
                    HasLivingClaim = loader.Has(ViewerHasLivingClaimsKey)
                        ? GetBoolAt(hasLivingClaims, i)
                        : GetGuidAt(beaverRecordIds, i) != Guid.Empty,
                    LastAssignedBeaverName = SanitizeCleanBeaverName(GetStringAt(lastAssignedBeaverNames, i)),
                    LastDeadBeaverName = SanitizeCleanBeaverName(GetStringAt(lastDeadBeaverNames, i)),
                    QueuedAfterDeath = GetBoolAt(queuedAfterDeath, i),
                    FirstSeenAtUtc = GetStringAt(firstSeenAtUtc, i),
                    LastSeenAtUtc = GetStringAt(lastSeenAtUtc, i),
                    ChatMessageCount = GetIntAt(chatMessageCounts, i),
                    NameColorHex = NormalizeNameColorHexOrEmpty(GetStringAt(nameColorHexes, i)),
                    NameShadowColorHex = NormalizeShadowColorHexOrEmpty(GetStringAt(nameShadowColorHexes, i))
                };

                var viewerKey = ViewerIdentity.CreateViewerKey(viewerRecord.Source, viewerRecord.SourceUserId);
                _viewersByKey[viewerKey] = viewerRecord;
            }
        }

        private void LoadBeaverRecords(IObjectLoader loader)
        {
            if (!loader.Has(BeaverRecordIdsKey))
            {
                return;
            }

            var beaverRecordIds = loader.Get(BeaverRecordIdsKey);
            var entityIds = loader.Has(BeaverEntityIdsKey) ? loader.Get(BeaverEntityIdsKey) : new List<Guid>();
            var originalNames = loader.Has(BeaverOriginalNamesKey) ? loader.Get(BeaverOriginalNamesKey) : new List<string>();
            var assignedNames = loader.Has(BeaverAssignedNamesKey) ? loader.Get(BeaverAssignedNamesKey) : new List<string>();
            var assignedAtUtc = loader.Has(BeaverAssignedAtUtcKey) ? loader.Get(BeaverAssignedAtUtcKey) : new List<string>();
            var lastSeenAtUtc = loader.Has(BeaverLastSeenAtUtcKey) ? loader.Get(BeaverLastSeenAtUtcKey) : new List<string>();
            var renameCounts = loader.Has(BeaverRenameCountsKey) ? loader.Get(BeaverRenameCountsKey) : new List<int>();
            var rerollCounts = loader.Has(BeaverRerollCountsKey) ? loader.Get(BeaverRerollCountsKey) : new List<int>();
            var isActive = loader.Has(BeaverIsActiveKey) ? loader.Get(BeaverIsActiveKey) : new List<bool>();

            for (var i = 0; i < beaverRecordIds.Count; i++)
            {
                var beaverRecordId = GetGuidAt(beaverRecordIds, i);

                if (beaverRecordId == Guid.Empty)
                {
                    continue;
                }

                var beaverRecord = new BeaverRecord
                {
                    BeaverRecordId = beaverRecordId,
                    EntityId = GetGuidAt(entityIds, i),
                    OriginalName = GetStringAt(originalNames, i),
                    AssignedName = GetStringAt(assignedNames, i),
                    AssignedAtUtc = GetStringAt(assignedAtUtc, i),
                    LastSeenAtUtc = GetStringAt(lastSeenAtUtc, i),
                    RenameCount = GetIntAt(renameCounts, i),
                    RerollCount = GetIntAt(rerollCounts, i),
                    IsActive = GetBoolAt(isActive, i)
                };

                AddBeaverRecord(beaverRecord);
            }
        }

        private void LoadBeaverHistoryRecords(IObjectLoader loader)
        {
            if (!loader.Has(BeaverHistoryRecordIdsKey))
            {
                return;
            }

            var beaverRecordIds = loader.Get(BeaverHistoryRecordIdsKey);
            var entityIds = loader.Has(BeaverHistoryEntityIdsKey) ? loader.Get(BeaverHistoryEntityIdsKey) : new List<Guid>();
            var reasons = loader.Has(BeaverHistoryReasonsKey) ? loader.Get(BeaverHistoryReasonsKey) : new List<string>();
            var names = loader.Has(BeaverHistoryNamesKey) ? loader.Get(BeaverHistoryNamesKey) : new List<string>();
            var recordedAtUtc = loader.Has(BeaverHistoryRecordedAtUtcKey) ? loader.Get(BeaverHistoryRecordedAtUtcKey) : new List<string>();
            var ages = loader.Has(BeaverHistoryAgesKey) ? loader.Get(BeaverHistoryAgesKey) : new List<int>();

            for (var i = 0; i < beaverRecordIds.Count; i++)
            {
                var beaverRecordId = GetGuidAt(beaverRecordIds, i);
                var entityId = GetGuidAt(entityIds, i);

                if (beaverRecordId == Guid.Empty || entityId == Guid.Empty)
                {
                    continue;
                }

                BeaverRecord beaverRecord;

                if (!_beaversByRecordId.TryGetValue(beaverRecordId, out beaverRecord) || beaverRecord == null)
                {
                    continue;
                }

                AddBeaverHistoryEntry(
                    beaverRecord,
                    entityId,
                    GetStringAt(reasons, i),
                    GetStringAt(names, i),
                    GetIntAt(ages, i),
                    GetStringAt(recordedAtUtc, i));
            }
        }

        private void EnsureLoadedBeaverRecordsHaveHistory()
        {
            foreach (var beaverRecord in _beaversByRecordId.Values)
            {
                if (beaverRecord == null || beaverRecord.EntityId == Guid.Empty)
                {
                    continue;
                }

                if (beaverRecord.EntityHistory != null && beaverRecord.EntityHistory.Count > 0)
                {
                    continue;
                }

                AddBeaverHistoryEntry(beaverRecord, beaverRecord.EntityId, "claimed", beaverRecord.AssignedName, -1);
            }
        }

        private void RebuildBeaverEntityIndexes()
        {
            _beaversByEntityId.Clear();
            _beaversByHistoricalEntityId.Clear();

            var beaverRecords = new List<BeaverRecord>(_beaversByRecordId.Values);

            foreach (var beaverRecord in beaverRecords)
            {
                AddBeaverRecord(beaverRecord);
            }
        }

        private void LoadPendingClaims(IObjectLoader loader)
        {
            if (!loader.Has(PendingClaimSourcesKey))
            {
                return;
            }

            var sources = loader.Get(PendingClaimSourcesKey);
            var sourceUserIds = loader.Has(PendingClaimSourceUserIdsKey) ? loader.Get(PendingClaimSourceUserIdsKey) : new List<string>();
            var loginNames = loader.Has(PendingClaimLoginNamesKey) ? loader.Get(PendingClaimLoginNamesKey) : new List<string>();
            var displayNames = loader.Has(PendingClaimDisplayNamesKey) ? loader.Get(PendingClaimDisplayNamesKey) : new List<string>();

            for (var i = 0; i < sources.Count; i++)
            {
                var source = GetStringAt(sources, i);
                var sourceUserId = GetStringAt(sourceUserIds, i);

                if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(sourceUserId))
                {
                    continue;
                }

                var viewer = new ViewerIdentity(
                    source,
                    sourceUserId,
                    GetStringAt(loginNames, i),
                    GetStringAt(displayNames, i));

                var viewerKey = ViewerIdentity.CreateViewerKey(viewer);

                if (_pendingClaimKeys.Contains(viewerKey))
                {
                    continue;
                }

                _pendingClaims.Add(viewer);
                _pendingClaimKeys.Add(viewerKey);
            }

            TwitchBornLog.Info("Loaded pending beaver claims: " + _pendingClaims.Count);
        }

        private void RemovePendingClaimAt(int index)
        {
            if (index < 0 || index >= _pendingClaims.Count)
            {
                return;
            }

            var viewer = _pendingClaims[index];

            if (viewer != null && viewer.IsValid)
            {
                _pendingClaimKeys.Remove(ViewerIdentity.CreateViewerKey(viewer));
            }

            _pendingClaims.RemoveAt(index);
        }

        private static ViewerIdentity CreateViewerIdentity(ViewerRecord viewerRecord)
        {
            if (viewerRecord == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(viewerRecord.Source) || string.IsNullOrEmpty(viewerRecord.SourceUserId))
            {
                return null;
            }

            return new ViewerIdentity(
                viewerRecord.Source,
                viewerRecord.SourceUserId,
                viewerRecord.DisplayName ?? "",
                viewerRecord.DisplayName ?? "");
        }

        private void NotifyBeaverLifecycle(BeaverCommandResult result)
        {
            if (result == null || result.Viewer == null || !result.Viewer.IsValid)
            {
                return;
            }

            BeaverLifecycleNotification?.Invoke(result);
        }

        private static string GetLifecycleBeaverName(BeaverRecord beaverRecord, Character character)
        {
            if (beaverRecord != null)
            {
                var assignedName = SanitizeCleanBeaverName(beaverRecord.AssignedName);

                if (!string.IsNullOrEmpty(assignedName))
                {
                    return assignedName;
                }
            }

            var characterName = SanitizeCleanBeaverName(GetCharacterName(character));

            if (!string.IsNullOrEmpty(characterName))
            {
                return characterName;
            }

            return "your beaver";
        }

        private static string GetCharacterName(Character character)
        {
            if (character == null)
            {
                return "";
            }

            var namedEntity = character.GetComponent<NamedEntity>();

            if (namedEntity == null || string.IsNullOrEmpty(namedEntity.EntityName))
            {
                return "Your beaver";
            }

            return namedEntity.EntityName;
        }

        private List<ActiveBeaver> GetActiveBeavers()
        {
            if (!_cacheDirty)
            {
                return _activeBeaverCache;
            }

            _activeBeaverCache.Clear();

            foreach (var character in _characterPopulation.Characters)
            {
                if (!character.Alive)
                {
                    continue;
                }

                if (!IsCharacterVisiblyActive(character))
                {
                    continue;
                }

                if (!IsClaimableCharacter(character))
                {
                    continue;
                }

                var namedEntity = character.GetComponent<NamedEntity>();
                var entityComponent = character.GetComponent<EntityComponent>();

                if (namedEntity == null || entityComponent == null)
                {
                    continue;
                }

                _activeBeaverCache.Add(new ActiveBeaver(character, namedEntity, entityComponent.EntityId, character.Age));
            }

            _cacheDirty = false;
            return _activeBeaverCache;
        }

        private Character FindActiveBeaverByEntityId(Guid entityId)
        {
            if (entityId == Guid.Empty)
            {
                return null;
            }

            foreach (var activeBeaver in GetActiveBeavers())
            {
                if (activeBeaver.EntityId == entityId)
                {
                    return activeBeaver.Character;
                }
            }

            return null;
        }

        private Character FindUnassignedActiveBeaverByName(string displayName)
        {
            foreach (var activeBeaver in GetActiveBeavers())
            {
                if (IsEntityAssigned(activeBeaver.EntityId))
                {
                    continue;
                }

                if (string.Equals(activeBeaver.NamedEntity.EntityName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return activeBeaver.Character;
                }
            }

            return null;
        }

        private Character FindYoungestUnassignedActiveBeaver()
        {
            ActiveBeaver youngest = null;

            foreach (var activeBeaver in GetActiveBeavers())
            {
                if (IsEntityAssigned(activeBeaver.EntityId))
                {
                    continue;
                }

                if (youngest == null || activeBeaver.Age < youngest.Age)
                {
                    youngest = activeBeaver;
                }
            }

            return youngest == null ? null : youngest.Character;
        }

        private bool IsEntityAssigned(Guid entityId)
        {
            BeaverRecord beaverRecord;

            if (!_beaversByEntityId.TryGetValue(entityId, out beaverRecord))
            {
                return false;
            }

            return beaverRecord.IsActive;
        }

        private bool IsClaimableCharacter(Character character)
        {
            if (character == null)
            {
                return false;
            }

            if (character.GetComponent<Beaver>() != null)
            {
                return true;
            }

            return _claimSettingsOwner.AllowBotClaims.Value
                && character.GetComponent<Bot>() != null;
        }

        private static Guid GetEntityId(Character character)
        {
            var entityComponent = character.GetComponent<EntityComponent>();

            if (entityComponent == null)
            {
                return Guid.Empty;
            }

            return entityComponent.EntityId;
        }

        private static BeaverRecord CreateBeaverRecord(
            Guid entityId,
            string originalName,
            string assignedName,
            bool isActive)
        {
            var now = NowUtc();

            return new BeaverRecord
            {
                BeaverRecordId = Guid.NewGuid(),
                EntityId = entityId,
                OriginalName = originalName,
                AssignedName = assignedName,
                AssignedAtUtc = now,
                LastSeenAtUtc = now,
                RenameCount = 0,
                RerollCount = 0,
                IsActive = isActive
            };
        }

        private static void AddBeaverHistoryEntry(
            BeaverRecord beaverRecord,
            Guid entityId,
            string reason,
            string nameAtTime,
            int ageAtTime,
            string recordedAtUtc = null)
        {
            if (beaverRecord == null || entityId == Guid.Empty)
            {
                return;
            }

            if (beaverRecord.EntityHistory == null)
            {
                beaverRecord.EntityHistory = new List<BeaverEntityHistoryEntry>();
            }

            foreach (var existingEntry in beaverRecord.EntityHistory)
            {
                if (existingEntry == null)
                {
                    continue;
                }

                if (existingEntry.EntityId == entityId && string.Equals(existingEntry.Reason, reason ?? "", StringComparison.Ordinal))
                {
                    return;
                }
            }

            beaverRecord.EntityHistory.Add(new BeaverEntityHistoryEntry
            {
                EntityId = entityId,
                Reason = reason ?? "",
                NameAtTime = nameAtTime ?? "",
                RecordedAtUtc = string.IsNullOrEmpty(recordedAtUtc) ? NowUtc() : recordedAtUtc,
                AgeAtTime = ageAtTime
            });
        }

        private static string SanitizeBeaverName(string value)
        {
            return TwitchBornTextSanitizer.SanitizeBeaverEntityName(value, 24);
        }

        private static string SanitizeCleanBeaverName(string value)
        {
            string ignoredHexColor;
            return TwitchBornTextSanitizer.SanitizeDisplayName(value, 24, out ignoredHexColor);
        }

        private static bool TryNormalizeNameColorHex(string value, out string normalizedColour)
        {
            return TryNormalizeHexColor(value, false, out normalizedColour);
        }

        private static bool TryNormalizeShadowColorHex(string value, out string normalizedColour)
        {
            return TryNormalizeHexColor(value, true, out normalizedColour);
        }

        private static string NormalizeNameColorHexOrEmpty(string value)
        {
            string normalizedColour;
            return TryNormalizeNameColorHex(value, out normalizedColour) ? normalizedColour : "";
        }

        private static string NormalizeShadowColorHexOrEmpty(string value)
        {
            string normalizedColour;
            return TryNormalizeShadowColorHex(value, out normalizedColour) ? normalizedColour : "";
        }

        private static bool TryNormalizeHexColor(
            string value,
            bool allowAlpha,
            out string normalizedColour)
        {
            normalizedColour = "";

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();

            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                return false;
            }

            var expectedLength = allowAlpha && trimmed.Length == 9 ? 9 : 7;

            if (trimmed.Length != expectedLength)
            {
                return false;
            }

            for (var i = 1; i < trimmed.Length; i++)
            {
                if (!IsHex(trimmed[i]))
                {
                    return false;
                }
            }

            normalizedColour = trimmed.ToUpperInvariant();
            return true;
        }

        private static bool IsHex(char value)
        {
            return (value >= '0' && value <= '9')
                || (value >= 'a' && value <= 'f')
                || (value >= 'A' && value <= 'F');
        }

        private static string NowUtc()
        {
            return "utc|" + DateTime.UtcNow.ToString("O");
        }

        private string GetExpectedBeaverName(BeaverRecord beaverRecord)
        {
            return BuildStyledEntityName(beaverRecord, GetViewerRecordForBeaverRecord(beaverRecord));
        }

        private static string GetStringAt(List<string> values, int index)
        {
            if (index >= 0 && index < values.Count)
            {
                return values[index];
            }

            return "";
        }

        private static int GetIntAt(List<int> values, int index)
        {
            if (index >= 0 && index < values.Count)
            {
                return values[index];
            }

            return 0;
        }

        private static bool GetBoolAt(List<bool> values, int index)
        {
            return index >= 0 && index < values.Count && values[index];
        }

        private static Guid GetGuidAt(List<Guid> values, int index)
        {
            if (index >= 0 && index < values.Count)
            {
                return values[index];
            }

            return Guid.Empty;
        }

        private class ActiveBeaver
        {
            public Character Character { get; private set; }
            public NamedEntity NamedEntity { get; private set; }
            public Guid EntityId { get; private set; }
            public int Age { get; private set; }

            public ActiveBeaver(Character character, NamedEntity namedEntity, Guid entityId, int age)
            {
                Character = character;
                NamedEntity = namedEntity;
                EntityId = entityId;
                Age = age;
            }
        }
    }
}