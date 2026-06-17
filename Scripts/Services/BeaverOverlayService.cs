using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.CameraSystem;
using Timberborn.Characters;
using Timberborn.CoreUI;
using Timberborn.EntityNaming;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using TwitchBorn.Core;
using TwitchBorn.Registry;
using TwitchBorn.Settings;
using TwitchBorn.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwitchBorn.Services
{
    public class BeaverOverlayService : ILoadableSingleton, IUnloadableSingleton, IInputProcessor
    {
        private const int MaxBeaverNameLength = 32;
        private const int MaxViewerNameLength = 32;
        private const string OwnedByLocKey = "Eurymachus.TwitchBorn.Overlay.OwnedBy";
        private const string ShowNameplatesKey = "ShowTwitchBornNameplates";

        private const int OverlayPaddingLeft = 8;
        private const int OverlayPaddingRight = 8;
        private const int OverlayPaddingTop = 4;
        private const int OverlayPaddingBottom = 4;

        private const int OverlayFramePadding = 2;
        private const int NameplateBodyPaddingLeft = 11;
        private const int NameplateBodyPaddingRight = 11;
        private const int NameplateBodyPaddingTop = 3;
        private const int NameplateBodyPaddingBottom = 3;

        private const int OverlayBorderWidth = 2;

        private const string FrameBackgroundResourcePath = "UI/Images/Backgrounds/bg-4";
        private const string BodyBackgroundResourcePath = "UI/Images/Backgrounds/bg-3";
        private const string NameplateBackgroundResourcePath = "UI/Images/Backgrounds/bg-3";
        private const int FrameBackgroundSlice = 16;
        private const int BodyBackgroundSlice = 9;
        private const int NameplateBackgroundSlice = 9;
        private const float FrameBackgroundSliceScale = 0.5f;
        private const float BodyBackgroundSliceScale = 0.5f;
        private const float NameplateBackgroundSliceScale = 0.5f;

        private const float OverlayFallbackWidth = 130f;
        private const float OverlayFallbackHeight = 28f;
        private const float ExpansionSpeed = 8.5f;

        private const float OverlayMinMessageWidth = 96f;
        private const float OverlayMaxMessageWidth = 230f;
        private const float MessageMaxTextWidth = 208f;
        private const float MessageExpandedHeightPadding = 4f;
        private const float MessageMinimumExtraHeight = 2f;
        private const float OverlayWidthAnimationSpeed = 520f;
        private const float OverlayHeightAnimationSpeed = 220f;

        private static readonly Color OverlayFrameTint = new Color32(255, 255, 255, 255);
        private static readonly Color OverlayBodyTint = new Color32(255, 255, 255, 255);
        private static readonly Color NameplateBodyTint = new Color32(255, 255, 255, 255);
        private static readonly Color MessageTextColor = new Color32(232, 229, 214, 255);
        private static readonly Color NameTextColor = new Color32(235, 235, 235, 255);
        private static readonly Color NameplateFrameTint = new Color32(255, 255, 255, 255);
        private static readonly Color NameplateFrameHoverTint = new Color32(255, 243, 178, 255);
        private static readonly Color NameplateFrameSelectedTint = new Color32(160, 218, 255, 255);
        private static readonly Color NameplateLightBodyTint = new Color32(255, 244, 190, 255);

        private const float OverlayDistanceTieThreshold = 0.08f;
        private const float OverlayAnchorYUpSmoothingSpeed = 24f;
        private const float OverlayAnchorYDownSmoothingSpeed = 0.35f;
        private const float OverlayAnchorYTeleportThreshold = 6f;
        private const float OverlayAnchorYDownDeadZone = 0.85f;

        private readonly ILoc _loc;
        private readonly CameraService _cameraService;
        private readonly Underlay _underlay;
        private readonly OverlaySettingsOwner _settingsOwner;
        private readonly BeaverRegistry _beaverRegistry;
        private readonly EntitySelectionService _entitySelectionService;
        private readonly ITooltipRegistrar _tooltipRegistrar;

        private readonly InputSettings _inputSettings;
        private readonly InputService _inputService;

        private readonly List<BeaverOverlay> _overlays = new List<BeaverOverlay>();

        private VisualElement _root;
        private GameObject _updateDriverObject;
        private bool _forceNameplatesKeyHeld;

        public BeaverOverlayService(
            CameraService cameraService,
            Underlay underlay,
            OverlaySettingsOwner settingsOwner,
            BeaverRegistry beaverRegistry,
            EntitySelectionService entitySelectionService,
            ITooltipRegistrar tooltipRegistrar,
            InputSettings inputSettings,
            InputService inputService,
            ILoc loc)
        {
            _cameraService = cameraService;
            _underlay = underlay;
            _settingsOwner = settingsOwner;
            _beaverRegistry = beaverRegistry;
            _entitySelectionService = entitySelectionService;
            _tooltipRegistrar = tooltipRegistrar;
            _inputSettings = inputSettings;
            _inputService = inputService;
            _loc = loc;
        }
        public void Load()
        {
            _root = new VisualElement();
            _root.name = "ClaimedBeaverOverlayRoot";
            _root.pickingMode = PickingMode.Ignore;

            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;

            _underlay.Add(_root);

            _updateDriverObject = new GameObject("ClaimedBeaverOverlayUpdater");

            var updater = _updateDriverObject.AddComponent<BeaverOverlayUpdater>();
            updater.Initialize(this);
            _inputService.AddInputProcessor(this);

            TwitchBornLog.Info("Claimed beaver overlay service loaded.");
        }

        public bool ProcessInput()
        {
            _forceNameplatesKeyHeld = ReadForceNameplatesKeyHeld();
            return false;
        }

        public void Unload()
        {
            _inputService.RemoveInputProcessor(this);
            _forceNameplatesKeyHeld = false;

            if (_updateDriverObject != null)
            {
                UnityEngine.Object.Destroy(_updateDriverObject);
                _updateDriverObject = null;
            }

            if (_root != null)
            {
                _root.RemoveFromHierarchy();
                _root = null;
            }

            _overlays.Clear();

            TwitchBornLog.Info("Claimed beaver overlay service unloaded.");
        }

        private bool IsForceNameplatesKeyHeld()
        {
            return _forceNameplatesKeyHeld;
        }

        private bool ReadForceNameplatesKeyHeld()
        {
            try
            {
                return _inputService != null && _inputService.IsKeyHeld(ShowNameplatesKey);
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private bool ShouldShowMessage(BeaverOverlay overlay)
        {
            if (overlay == null || !overlay.HasActiveMessage)
            {
                return false;
            }

            return overlay.DistanceToCamera <= _settingsOwner.MessageVisibilityDistanceSetting.Value;
        }

        public void ShowMessage(
            Character character,
            string beaverName,
            string viewerName,
            string message)
        {
            if (character == null)
            {
                TwitchBornLog.Info("Cannot show overlay message because character was null.");
                return;
            }

            if (_root == null)
            {
                TwitchBornLog.Info("Cannot show overlay message because UI root was null.");
                return;
            }

            var safeBeaverName = GetOverlayDisplayName(
                character,
                beaverName,
                out var hasNameColor,
                out var nameColor);

            var safeViewerName = SanitizeText(viewerName, MaxViewerNameLength);
            var safeMessage = WrapText(
                SanitizeText(message, _settingsOwner.MaxMessageLengthSetting.Value),
                _settingsOwner.WrapLineLengthSetting.Value);

            var overlay = FindOverlay(character);

            if (overlay == null)
            {
                overlay = CreateOverlay(
                    character,
                    safeBeaverName,
                    safeViewerName,
                    hasNameColor,
                    nameColor);
                _overlays.Add(overlay);
            }

            overlay.SetDisplayNames(
                safeBeaverName,
                safeViewerName,
                hasNameColor,
                nameColor);

            CaptureCurrentOverlaySize(overlay);

            overlay.SetMessage(safeMessage);
            SetTargetSizeFromMessage(overlay, safeMessage);

            overlay.LastShownAt = Time.unscaledTime;
            overlay.ExpiresAt = Time.unscaledTime + _settingsOwner.MessageDurationSeconds.Value;

            ApplyOverlayStyle(overlay);
            EnforceMaxActiveMessages();

            TwitchBornLog.Info("Overlay message shown for " + safeBeaverName + " owned by " + safeViewerName + ": " + safeMessage);
        }

        public void UpdateBubbles()
        {
            if (_root == null || Camera.main == null)
            {
                return;
            }

            SyncClaimedBeaverOverlays();

            for (var i = _overlays.Count - 1; i >= 0; i--)
            {
                var overlay = _overlays[i];

                if (overlay == null || overlay.Character == null)
                {
                    RemoveOverlayAt(i);
                    continue;
                }

                ApplyOverlayStyle(overlay);
                UpdateOverlayExpansion(overlay);
                UpdateOverlayPositionAndVisibility(overlay);
            }

            SortOverlayDrawOrder();
        }

        private void SyncClaimedBeaverOverlays()
        {
            var targets = _beaverRegistry.GetClaimedBeaverOverlayTargets();

            foreach (var target in targets)
            {
                if (target == null || target.Character == null)
                {
                    continue;
                }

                var overlay = FindOverlay(target.Character);

                if (overlay == null)
                {
                    var newBeaverName = ParseOverlayDisplayName(
                        target.BeaverName,
                        target.BeaverName,
                        out var newHasNameColor,
                        out var newNameColor);

                    overlay = CreateOverlay(
                        target.Character,
                        newBeaverName,
                        SanitizeText(target.ViewerName, MaxViewerNameLength),
                        newHasNameColor,
                        newNameColor);

                    _overlays.Add(overlay);
                    continue;
                }

                var safeBeaverName = ParseOverlayDisplayName(
                    target.BeaverName,
                    target.BeaverName,
                    out var hasNameColor,
                    out var nameColor);

                var safeViewerName = SanitizeText(target.ViewerName, MaxViewerNameLength);

                overlay.SetDisplayNames(
                    safeBeaverName,
                    safeViewerName,
                    hasNameColor,
                    nameColor);
            }
        }

        private static Color WithAlpha(Color color, int alpha)
        {
            alpha = Mathf.Clamp(alpha, 0, 255);

            return new Color(
                color.r,
                color.g,
                color.b,
                alpha / 255f);
        }

        private static Color GetOverlayNameTextColor(BeaverOverlay overlay)
        {
            if (overlay != null && overlay.HasNameColor)
            {
                return overlay.NameColor;
            }

            return NameTextColor;
        }

        private static Color GetNameplateBodyTint(BeaverOverlay overlay)
        {
            if (overlay != null && overlay.HasNameColor && IsDarkColor(overlay.NameColor))
            {
                return NameplateLightBodyTint;
            }

            return NameplateBodyTint;
        }

        private static bool IsDarkColor(Color color)
        {
            var luminance = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            return luminance < 0.42f;
        }

        private void ApplyOverlayStyle(BeaverOverlay overlay)
        {
            if (overlay == null || overlay.Element == null)
            {
                return;
            }

            var shellAlpha = overlay.Expansion;
            var shellAlphaByte = Mathf.RoundToInt(255f * shellAlpha);
            var shellPadding = Mathf.RoundToInt(OverlayFramePadding * shellAlpha);
            var bodyPaddingLeft = Mathf.RoundToInt(OverlayPaddingLeft * shellAlpha);
            var bodyPaddingRight = Mathf.RoundToInt(OverlayPaddingRight * shellAlpha);
            var bodyPaddingTop = Mathf.RoundToInt(OverlayPaddingTop * shellAlpha);
            var bodyPaddingBottom = Mathf.RoundToInt(OverlayPaddingBottom * shellAlpha);

            overlay.Element.style.backgroundColor = Color.clear;
            overlay.Element.style.paddingLeft = 0;
            overlay.Element.style.paddingRight = 0;
            overlay.Element.style.paddingTop = 0;
            overlay.Element.style.paddingBottom = 0;

            if (overlay.OuterFrame != null)
            {
                overlay.OuterFrame.style.paddingLeft = shellPadding;
                overlay.OuterFrame.style.paddingRight = shellPadding;
                overlay.OuterFrame.style.paddingTop = shellPadding;
                overlay.OuterFrame.style.paddingBottom = shellPadding;
                overlay.OuterFrame.style.alignSelf = Align.Center;
                overlay.OuterFrame.style.width = overlay.Expansion > 0.01f
                    ? Length.Percent(100)
                    : StyleKeyword.Auto;

                SetNineSliceTint(overlay.OuterFrame, WithAlpha(OverlayFrameTint, shellAlphaByte));
            }

            if (overlay.OuterBody != null)
            {
                overlay.OuterBody.style.paddingLeft = bodyPaddingLeft;
                overlay.OuterBody.style.paddingRight = bodyPaddingRight;
                overlay.OuterBody.style.paddingTop = bodyPaddingTop;
                overlay.OuterBody.style.paddingBottom = bodyPaddingBottom;
                overlay.OuterBody.style.alignItems = Align.Center;
                overlay.OuterBody.style.width = overlay.Expansion > 0.01f
                    ? Length.Percent(100)
                    : StyleKeyword.Auto;

                SetNineSliceTint(overlay.OuterBody, WithAlpha(OverlayBodyTint, shellAlphaByte));
            }

            if (overlay.NameplateFrame != null)
            {
                overlay.NameplateFrame.style.paddingLeft = OverlayFramePadding;
                overlay.NameplateFrame.style.paddingRight = OverlayFramePadding;
                overlay.NameplateFrame.style.paddingTop = OverlayFramePadding;
                overlay.NameplateFrame.style.paddingBottom = OverlayFramePadding;
                overlay.NameplateFrame.style.alignSelf = Align.Center;

                var nameplateFrameTint = NameplateFrameTint;

                if (IsOverlayBeaverSelected(overlay))
                {
                    nameplateFrameTint = NameplateFrameSelectedTint;
                }

                if (overlay.IsHovered)
                {
                    nameplateFrameTint = NameplateFrameHoverTint;
                }

                SetNineSliceTint(overlay.NameplateFrame, nameplateFrameTint);
            }

            if (overlay.NameplateBody != null)
            {
                overlay.NameplateBody.style.paddingLeft = NameplateBodyPaddingLeft;
                overlay.NameplateBody.style.paddingRight = NameplateBodyPaddingRight;
                overlay.NameplateBody.style.paddingTop = NameplateBodyPaddingTop;
                overlay.NameplateBody.style.paddingBottom = NameplateBodyPaddingBottom;
                SetNineSliceTint(overlay.NameplateBody, GetNameplateBodyTint(overlay));
            }

            if (overlay.NameLabel != null)
            {
                overlay.NameLabel.style.color = GetOverlayNameTextColor(overlay);
                overlay.NameLabel.style.backgroundColor = Color.clear;
                overlay.NameLabel.style.unityTextOutlineWidth = 0f;
                if (_settingsOwner.NameplateShadowEnabled.Value)
                {
                    overlay.NameLabel.style.textShadow = new TextShadow
                    {
                        offset = new Vector2(1f, 1f),
                        blurRadius = 1f,
                        color = new Color32(0, 0, 0, 190)
                    };
                }
                overlay.NameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                overlay.NameLabel.style.whiteSpace = WhiteSpace.NoWrap;
                overlay.NameLabel.style.fontSize = _settingsOwner.OverlayFontSizeSetting.Value;
                overlay.NameLabel.style.paddingLeft = 0;
                overlay.NameLabel.style.paddingRight = 0;
                overlay.NameLabel.style.paddingTop = 0;
                overlay.NameLabel.style.paddingBottom = 0;
                overlay.NameLabel.style.borderTopWidth = 0;
                overlay.NameLabel.style.borderRightWidth = 0;
                overlay.NameLabel.style.borderBottomWidth = 0;
                overlay.NameLabel.style.borderLeftWidth = 0;
            }

            if (overlay.MessageContainer != null)
            {
                overlay.MessageContainer.style.overflow = Overflow.Hidden;
                overlay.MessageContainer.style.marginTop = Mathf.RoundToInt(8f * shellAlpha);
                overlay.MessageContainer.style.width = Length.Percent(100);
            }

            if (overlay.MessageLabel != null)
            {
                overlay.MessageLabel.style.color = MessageTextColor;
                overlay.MessageLabel.style.backgroundColor = Color.clear;
                overlay.MessageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                overlay.MessageLabel.style.marginTop = 0;
                overlay.MessageLabel.style.marginLeft = 0;
                overlay.MessageLabel.style.marginRight = 0;
                overlay.MessageLabel.style.marginBottom = 0;

                overlay.MessageLabel.style.paddingLeft = 0;
                overlay.MessageLabel.style.paddingRight = 0;
                overlay.MessageLabel.style.paddingTop = 0;
                overlay.MessageLabel.style.paddingBottom = 0;

                overlay.MessageLabel.style.whiteSpace = WhiteSpace.Normal;
                overlay.MessageLabel.style.fontSize = Mathf.Max(8, _settingsOwner.OverlayFontSizeSetting.Value - 1);
                overlay.MessageLabel.style.maxWidth = MessageMaxTextWidth;
            }

            ApplyMeasureStyle(overlay);
        }

        private static void SetNineSliceTint(VisualElement element, Color tint)
        {
            var nineSliceElement = element as TwitchBornNineSliceVisualElement;

            if (nineSliceElement == null)
            {
                return;
            }

            nineSliceElement.SetTint(tint);
        }

        private void ApplyMeasureStyle(BeaverOverlay overlay)
        {
            if (overlay == null)
            {
                return;
            }

            if (overlay.MeasureElement != null)
            {
                overlay.MeasureElement.style.paddingLeft = OverlayPaddingLeft;
                overlay.MeasureElement.style.paddingRight = OverlayPaddingRight;
                overlay.MeasureElement.style.paddingTop = OverlayPaddingTop;
                overlay.MeasureElement.style.paddingBottom = OverlayPaddingBottom;
                overlay.MeasureElement.style.borderTopWidth = OverlayBorderWidth;
                overlay.MeasureElement.style.borderRightWidth = OverlayBorderWidth;
                overlay.MeasureElement.style.borderBottomWidth = OverlayBorderWidth;
                overlay.MeasureElement.style.borderLeftWidth = OverlayBorderWidth;
            }

            if (overlay.MeasureNameLabel != null)
            {
                overlay.MeasureNameLabel.style.fontSize = _settingsOwner.OverlayFontSizeSetting.Value;
                overlay.MeasureNameLabel.style.whiteSpace = WhiteSpace.NoWrap;
                overlay.MeasureNameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            }

            if (overlay.MeasureMessageLabel != null)
            {
                overlay.MeasureMessageLabel.style.fontSize = Mathf.Max(8, _settingsOwner.OverlayFontSizeSetting.Value - 1);
                overlay.MeasureMessageLabel.style.whiteSpace = WhiteSpace.Normal;
                overlay.MeasureMessageLabel.style.unityTextAlign = TextAnchor.UpperLeft;
                overlay.MeasureMessageLabel.style.marginTop = 2;
                overlay.MeasureMessageLabel.style.marginLeft = 2;
                overlay.MeasureMessageLabel.style.marginRight = 2;
                overlay.MeasureMessageLabel.style.marginBottom = 0;
                overlay.MeasureMessageLabel.style.maxWidth = MessageMaxTextWidth;
            }
        }

        private BeaverOverlay CreateOverlay(
    Character character,
    string beaverName,
    string viewerName,
    bool hasNameColor,
    Color nameColor)
        {
            var element = new VisualElement();
            element.name = "BeaverOverlay";
            element.pickingMode = PickingMode.Ignore;
            element.style.position = Position.Absolute;
            element.style.display = DisplayStyle.Flex;
            element.style.opacity = 0f;
            element.style.flexDirection = FlexDirection.Column;
            element.style.alignItems = Align.Center;
            element.style.minWidth = 0;
            element.style.maxWidth = OverlayMaxMessageWidth;
            element.style.alignSelf = Align.FlexStart;
            element.style.backgroundColor = Color.clear;

            var outerFrame = new TwitchBornNineSliceVisualElement();
            outerFrame.name = "ClaimedBeaverOuterFrame";
            outerFrame.pickingMode = PickingMode.Ignore;
            outerFrame.SetBackground(
                FrameBackgroundResourcePath,
                FrameBackgroundSlice,
                FrameBackgroundSliceScale);
            outerFrame.style.flexDirection = FlexDirection.Column;
            outerFrame.style.alignSelf = Align.Center;
            outerFrame.style.alignItems = Align.Center;
            outerFrame.style.paddingLeft = 0;
            outerFrame.style.paddingRight = 0;
            outerFrame.style.paddingTop = 0;
            outerFrame.style.paddingBottom = 0;

            var outerBody = new TwitchBornNineSliceVisualElement();
            outerBody.name = "ClaimedBeaverOuterBody";
            outerBody.pickingMode = PickingMode.Ignore;
            outerBody.SetBackground(
                BodyBackgroundResourcePath,
                BodyBackgroundSlice,
                BodyBackgroundSliceScale);
            outerBody.style.flexDirection = FlexDirection.Column;
            outerBody.style.alignItems = Align.Center;
            outerBody.style.paddingLeft = 0;
            outerBody.style.paddingRight = 0;
            outerBody.style.paddingTop = 0;
            outerBody.style.paddingBottom = 0;

            var nameplateFrame = new TwitchBornNineSliceVisualElement();
            nameplateFrame.name = "ClaimedBeaverNameplateFrame";
            nameplateFrame.pickingMode = PickingMode.Position;
            nameplateFrame.SetBackground(
                FrameBackgroundResourcePath,
                FrameBackgroundSlice,
                FrameBackgroundSliceScale);
            nameplateFrame.style.flexDirection = FlexDirection.Column;
            nameplateFrame.style.alignSelf = Align.Center;
            nameplateFrame.style.paddingLeft = OverlayFramePadding;
            nameplateFrame.style.paddingRight = OverlayFramePadding;
            nameplateFrame.style.paddingTop = OverlayFramePadding;
            nameplateFrame.style.paddingBottom = OverlayFramePadding;

            var nameplateBody = new TwitchBornNineSliceVisualElement();
            nameplateBody.name = "ClaimedBeaverNameplateBody";
            nameplateBody.pickingMode = PickingMode.Ignore;
            nameplateBody.SetBackground(
                NameplateBackgroundResourcePath,
                NameplateBackgroundSlice,
                NameplateBackgroundSliceScale);
            nameplateBody.style.flexDirection = FlexDirection.Column;
            nameplateBody.style.paddingLeft = NameplateBodyPaddingLeft;
            nameplateBody.style.paddingRight = NameplateBodyPaddingRight;
            nameplateBody.style.paddingTop = NameplateBodyPaddingTop;
            nameplateBody.style.paddingBottom = NameplateBodyPaddingBottom;

            var nameLabel = new Label();
            nameLabel.name = "ClaimedBeaverNameplate";
            nameLabel.pickingMode = PickingMode.Ignore;
            nameLabel.text = beaverName;
            nameLabel.style.backgroundColor = Color.clear;
            nameLabel.style.paddingLeft = 0;
            nameLabel.style.paddingRight = 0;
            nameLabel.style.paddingTop = 0;
            nameLabel.style.paddingBottom = 0;

            var messageContainer = new VisualElement();
            messageContainer.name = "ClaimedBeaverMessageContainer";
            messageContainer.pickingMode = PickingMode.Ignore;
            messageContainer.style.overflow = Overflow.Hidden;
            messageContainer.style.height = 0;
            messageContainer.style.opacity = 0;
            messageContainer.style.marginTop = 0;
            messageContainer.style.flexDirection = FlexDirection.Column;
            messageContainer.style.width = Length.Percent(100);

            var messageLabel = new Label();
            messageLabel.name = "ClaimedBeaverMessage";
            messageLabel.pickingMode = PickingMode.Ignore;
            messageLabel.style.maxWidth = MessageMaxTextWidth;
            messageLabel.style.whiteSpace = WhiteSpace.Normal;
            messageLabel.style.backgroundColor = Color.clear;

            nameplateBody.Add(nameLabel);
            nameplateFrame.Add(nameplateBody);
            messageContainer.Add(messageLabel);
            outerBody.Add(nameplateFrame);
            outerBody.Add(messageContainer);
            outerFrame.Add(outerBody);
            element.Add(outerFrame);

            _root.Add(element);

            var measureElement = new VisualElement();
            measureElement.name = "ClaimedBeaverOverlayMeasure";
            measureElement.pickingMode = PickingMode.Ignore;
            measureElement.style.position = Position.Absolute;
            measureElement.style.left = -10000;
            measureElement.style.top = -10000;
            measureElement.style.flexDirection = FlexDirection.Column;
            measureElement.style.alignItems = Align.Center;
            measureElement.style.minWidth = 0;
            measureElement.style.maxWidth = OverlayMaxMessageWidth;
            measureElement.style.opacity = 0;

            var measureNameLabel = new Label();
            measureNameLabel.name = "ClaimedBeaverNameplateMeasure";
            measureNameLabel.pickingMode = PickingMode.Ignore;
            measureNameLabel.text = beaverName;
            measureNameLabel.style.paddingLeft = NameplateBodyPaddingLeft + OverlayFramePadding;
            measureNameLabel.style.paddingRight = NameplateBodyPaddingRight + OverlayFramePadding;
            measureNameLabel.style.paddingTop = NameplateBodyPaddingTop + OverlayFramePadding;
            measureNameLabel.style.paddingBottom = NameplateBodyPaddingBottom + OverlayFramePadding;

            var measureMessageContainer = new VisualElement();
            measureMessageContainer.name = "ClaimedBeaverMessageContainerMeasure";
            measureMessageContainer.pickingMode = PickingMode.Ignore;
            measureMessageContainer.style.marginTop = 3;
            measureMessageContainer.style.flexDirection = FlexDirection.Column;

            var measureMessageLabel = new Label();
            measureMessageLabel.name = "ClaimedBeaverMessageMeasure";
            measureMessageLabel.pickingMode = PickingMode.Ignore;
            measureMessageLabel.style.maxWidth = MessageMaxTextWidth;
            measureMessageLabel.style.whiteSpace = WhiteSpace.Normal;

            measureMessageContainer.Add(measureMessageLabel);
            measureElement.Add(measureNameLabel);
            measureElement.Add(measureMessageContainer);

            _root.Add(measureElement);

            var overlay = new BeaverOverlay(
                character,
                element,
                outerFrame,
                outerBody,
                nameplateFrame,
                nameplateBody,
                nameLabel,
                messageContainer,
                messageLabel,
                measureElement,
                measureNameLabel,
                measureMessageContainer,
                measureMessageLabel,
                beaverName,
                viewerName,
                hasNameColor,
                nameColor);

            RegisterOverlayInteraction(element, overlay);
            RegisterOverlayInteraction(outerFrame, overlay);
            RegisterOverlayInteraction(nameplateFrame, overlay);
            RegisterOverlayTooltip(element, overlay);
            RegisterOverlayTooltip(outerFrame, overlay);
            RegisterOverlayTooltip(nameplateFrame, overlay);

            ApplyOverlayStyle(overlay);
            ApplyMeasureStyle(overlay);
            return overlay;
        }

        private void RegisterOverlayInteraction(VisualElement target, BeaverOverlay overlay)
        {
            target.RegisterCallback<PointerEnterEvent>(_ =>
            {
                overlay.IsHovered = true;
            });

            target.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                overlay.IsHovered = false;
            });

            target.RegisterCallback<ClickEvent>(evt =>
            {
                SelectOverlayBeaver(overlay);
                evt.StopPropagation();
            });

            target.RegisterCallback<WheelEvent>(evt =>
            {
                ForwardOverlayScrollToCamera(evt);
            });
        }

        private void RegisterOverlayTooltip(VisualElement target, BeaverOverlay overlay)
        {
            _tooltipRegistrar.Register(target, () =>
            {
                if (overlay == null)
                {
                    return "";
                }

                return BuildOwnerTooltip(overlay.ViewerName);
            });
        }

        private void SelectOverlayBeaver(BeaverOverlay overlay)
        {
            if (overlay == null || overlay.Character == null)
            {
                return;
            }

            _entitySelectionService.Select(overlay.Character);
        }

        private void ForwardOverlayScrollToCamera(WheelEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            if (Mathf.Approximately(evt.delta.y, 0f))
            {
                return;
            }

            var direction = evt.delta.y > 0f ? -1f : 1f;

            if (_inputSettings.InvertZoom)
            {
                direction *= -1f;
            }

            _cameraService.ModifyZoomLevel(direction * _inputSettings.MouseWheelCameraZoomSpeed);

            evt.StopPropagation();
        }

        private bool IsOverlayBeaverSelected(BeaverOverlay overlay)
        {
            if (overlay == null || overlay.Character == null)
            {
                return false;
            }

            SelectableObject selectableObject = null;

            try
            {

                if (!overlay.Character.TryGetComponent(out selectableObject))
                {
                    return false;
                }
            }
            catch (NullReferenceException)
            {
                TwitchBornLog.Error("Null reference exception occurred while checking beaver selection.");
                return false;
            }

            return selectableObject != null && _entitySelectionService.IsSelected(selectableObject);
        }

        private static Vector3 StabilizeOverlayWorldPosition(
            BeaverOverlay overlay,
            Vector3 rawWorldPosition)
        {
            if (overlay == null)
            {
                return rawWorldPosition;
            }

            if (!overlay.HasSmoothedWorldY)
            {
                overlay.SetSmoothedWorldY(rawWorldPosition.y);
                return rawWorldPosition;
            }

            var yDelta = rawWorldPosition.y - overlay.SmoothedWorldY;
            var absoluteYDelta = Mathf.Abs(yDelta);

            if (absoluteYDelta > OverlayAnchorYTeleportThreshold)
            {
                overlay.SetSmoothedWorldY(rawWorldPosition.y);
                return rawWorldPosition;
            }

            var targetY = rawWorldPosition.y;

            if (yDelta < 0f && absoluteYDelta <= OverlayAnchorYDownDeadZone)
            {
                targetY = overlay.SmoothedWorldY;
            }

            var smoothingSpeed = targetY >= overlay.SmoothedWorldY
                ? OverlayAnchorYUpSmoothingSpeed
                : OverlayAnchorYDownSmoothingSpeed;

            var smoothing = 1f - Mathf.Exp(-smoothingSpeed * Time.unscaledDeltaTime);

            var smoothedY = Mathf.Lerp(
                overlay.SmoothedWorldY,
                targetY,
                smoothing);

            overlay.SetSmoothedWorldY(smoothedY);

            return new Vector3(
                rawWorldPosition.x,
                smoothedY,
                rawWorldPosition.z);
        }

        private void UpdateOverlayPositionAndVisibility(BeaverOverlay overlay)
        {
            if (_root == null || _root.panel == null)
            {
                return;
            }

            var followTarget = GetBubbleFollowTarget(overlay.Character);

            if (followTarget == null)
            {
                overlay.Hide();
                return;
            }

            var rawWorldPosition = followTarget.position + new Vector3(0f, _settingsOwner.OverlayHeightOffset.Value, 0f);
            var worldPosition = StabilizeOverlayWorldPosition(overlay, rawWorldPosition);

            if (!_cameraService.IsInFront(worldPosition))
            {
                overlay.Hide();
                return;
            }

            overlay.DistanceToCamera = Vector3.Distance(Camera.main.transform.position, worldPosition);

            var forceNameplateVisible = IsForceNameplatesKeyHeld();
            var messageVisible = ShouldShowMessage(overlay);
            var selectedVisible = IsOverlayBeaverSelected(overlay);
            var passiveNameplateVisible =
                _settingsOwner.AlwaysDisplayNameplates.Value
                && overlay.DistanceToCamera <= _settingsOwner.NameplateVisibilityDistanceSetting.Value;

            var nameplateVisible =
                forceNameplateVisible
                || selectedVisible
                || messageVisible
                || passiveNameplateVisible;

            var collapsingMessage = overlay.Expansion > 0.01f;

            if (!nameplateVisible && !messageVisible && !collapsingMessage)
            {
                overlay.Hide();
                return;
            }

            overlay.Show();

            var panelPosition = _cameraService.WorldSpaceToPanelSpace(_root, worldPosition);

            var width = overlay.Element.resolvedStyle.width;
            var height = overlay.Element.resolvedStyle.height;

            if (float.IsNaN(width) || width <= 0f)
            {
                width = OverlayFallbackWidth;
            }

            if (float.IsNaN(height) || height <= 0f)
            {
                height = OverlayFallbackHeight;
            }

            overlay.Element.style.left = panelPosition.x - width * 0.5f;
            overlay.Element.style.top = panelPosition.y - height;
        }

        private void UpdateOverlayExpansion(BeaverOverlay overlay)
        {
            if (overlay == null || overlay.Element == null || overlay.MessageContainer == null)
            {
                return;
            }

            UpdateMeasuredTargetSize(overlay);

            var shouldShowMessage = ShouldShowMessage(overlay);

            if (shouldShowMessage && overlay.TargetMessageHeight <= 0f)
            {
                overlay.NeedsSizeMeasurement = true;
            }

            var targetExpansion = shouldShowMessage ? 1f : 0f;
            var expansionStep = Time.unscaledDeltaTime * ExpansionSpeed;

            overlay.Expansion = Mathf.MoveTowards(overlay.Expansion, targetExpansion, expansionStep);

            var widthStep = Time.unscaledDeltaTime * OverlayWidthAnimationSpeed;
            var heightStep = Time.unscaledDeltaTime * OverlayHeightAnimationSpeed;

            overlay.AnimatedOverlayWidth = Mathf.MoveTowards(
                overlay.AnimatedOverlayWidth,
                overlay.TargetOverlayWidth,
                widthStep);

            overlay.AnimatedMessageHeight = Mathf.MoveTowards(
                overlay.AnimatedMessageHeight,
                overlay.TargetMessageHeight,
                heightStep);

            var isMessageMode = shouldShowMessage || overlay.Expansion > 0.01f;

            UpdateOverlayPickingMode(overlay, isMessageMode);

            if (isMessageMode)
            {
                overlay.Element.style.width = Mathf.Max(OverlayMinMessageWidth, overlay.AnimatedOverlayWidth);
                overlay.MessageContainer.style.height = overlay.AnimatedMessageHeight * overlay.Expansion;
                overlay.MessageContainer.style.opacity = overlay.Expansion;
                return;
            }

            overlay.Element.style.width = StyleKeyword.Auto;
            overlay.MessageContainer.style.height = 0;
            overlay.MessageContainer.style.opacity = 0;

            overlay.AnimatedOverlayWidth = 0f;
            overlay.TargetOverlayWidth = 0f;
            overlay.AnimatedMessageHeight = 0f;
            overlay.TargetMessageHeight = 0f;
            overlay.NeedsSizeMeasurement = false;
        }

        private static void UpdateOverlayPickingMode(BeaverOverlay overlay, bool isMessageMode)
        {
            if (overlay == null)
            {
                return;
            }

            if (overlay.Element != null)
            {
                overlay.Element.pickingMode = isMessageMode
                    ? PickingMode.Position
                    : PickingMode.Ignore;
            }

            if (overlay.OuterFrame != null)
            {
                overlay.OuterFrame.pickingMode = isMessageMode
                    ? PickingMode.Position
                    : PickingMode.Ignore;
            }

            if (overlay.OuterBody != null)
            {
                overlay.OuterBody.pickingMode = PickingMode.Ignore;
            }

            if (overlay.NameplateFrame != null)
            {
                overlay.NameplateFrame.pickingMode = isMessageMode
                    ? PickingMode.Ignore
                    : PickingMode.Position;
            }

            if (overlay.NameLabel != null)
            {
                overlay.NameLabel.pickingMode = PickingMode.Ignore;
            }
        }

        private static void CaptureCurrentOverlaySize(BeaverOverlay overlay)
        {
            if (overlay == null || overlay.Element == null)
            {
                return;
            }

            var currentWidth = overlay.Element.resolvedStyle.width;

            if (!float.IsNaN(currentWidth) && currentWidth > 0f)
            {
                overlay.AnimatedOverlayWidth = currentWidth;
                overlay.TargetOverlayWidth = currentWidth;
            }

            if (overlay.MessageContainer != null)
            {
                var currentMessageHeight = overlay.MessageContainer.resolvedStyle.height;

                if (!float.IsNaN(currentMessageHeight) && currentMessageHeight > 0f)
                {
                    overlay.AnimatedMessageHeight = currentMessageHeight;
                    overlay.TargetMessageHeight = currentMessageHeight;
                }
            }
        }

        private void UpdateMeasuredTargetSize(BeaverOverlay overlay)
        {
            if (overlay == null)
            {
                return;
            }

            if (!ShouldShowMessage(overlay))
            {
                overlay.TargetMessageHeight = 0f;
                return;
            }

            if (!overlay.NeedsSizeMeasurement && overlay.TargetMessageHeight > 0f && overlay.TargetOverlayWidth > 0f)
            {
                return;
            }

            var message = overlay.MessageLabel == null ? "" : overlay.MessageLabel.text;
            SetTargetSizeFromMessage(overlay, message);
        }

        private void SetTargetSizeFromMessage(BeaverOverlay overlay, string message)
        {
            if (overlay == null)
            {
                return;
            }

            var fontSize = Mathf.Max(8, _settingsOwner.OverlayFontSizeSetting.Value - 1);
            var lineCount = CountTextLines(message);
            var longestLineLength = GetLongestLineLength(message);

            var estimatedCharacterWidth = fontSize * 0.55f;
            var estimatedLineHeight = Mathf.Ceil(fontSize * 1.28f);

            var estimatedTextWidth = longestLineLength * estimatedCharacterWidth;
            var estimatedTextHeight = lineCount * estimatedLineHeight;

            var targetWidth = estimatedTextWidth
                            + OverlayPaddingLeft
                            + OverlayPaddingRight
                            + OverlayFramePadding * 2f
                            + 8f;

            var currentNameplateWidth = 0f;

            if (overlay.NameplateFrame != null)
            {
                currentNameplateWidth = overlay.NameplateFrame.resolvedStyle.width;

                if (float.IsNaN(currentNameplateWidth) || currentNameplateWidth <= 0f)
                {
                    currentNameplateWidth = overlay.NameplateFrame.contentRect.width;
                }
            }

            if (!float.IsNaN(currentNameplateWidth) && currentNameplateWidth > 0f)
            {
                targetWidth = Mathf.Max(targetWidth, currentNameplateWidth + OverlayPaddingLeft + OverlayPaddingRight);
            }

            var targetMessageHeight = estimatedTextHeight
                                    + MessageExpandedHeightPadding
                                    + MessageMinimumExtraHeight;

            overlay.TargetOverlayWidth = Mathf.Clamp(
                targetWidth,
                OverlayMinMessageWidth,
                OverlayMaxMessageWidth);

            overlay.TargetMessageHeight = Mathf.Max(
                estimatedLineHeight + MessageExpandedHeightPadding,
                targetMessageHeight);

            if (overlay.AnimatedOverlayWidth <= 0f)
            {
                overlay.AnimatedOverlayWidth = overlay.TargetOverlayWidth;
            }

            if (overlay.AnimatedMessageHeight <= 0f)
            {
                overlay.AnimatedMessageHeight = overlay.TargetMessageHeight;
            }

            overlay.NeedsSizeMeasurement = false;
        }

        private static int CountTextLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1;
            }

            var lineCount = 1;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lineCount++;
                }
            }

            return Mathf.Max(1, lineCount);
        }

        private static int GetLongestLineLength(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var longestLineLength = 0;
            var currentLineLength = 0;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    if (currentLineLength > longestLineLength)
                    {
                        longestLineLength = currentLineLength;
                    }

                    currentLineLength = 0;
                    continue;
                }

                currentLineLength++;
            }

            if (currentLineLength > longestLineLength)
            {
                longestLineLength = currentLineLength;
            }

            return longestLineLength;
        }

        private static float GetMeasuredOverlayWidth(BeaverOverlay overlay)
        {
            if (overlay == null || overlay.MeasureElement == null)
            {
                return 0f;
            }

            var width = overlay.MeasureElement.resolvedStyle.width;

            if (float.IsNaN(width) || width <= 0f)
            {
                width = overlay.MeasureElement.contentRect.width;
            }

            return width;
        }

        private void EnforceMaxActiveMessages()
        {
            var maxActiveMessages = _settingsOwner.MaxActiveOverlaysSetting.Value;

            if (maxActiveMessages <= 0)
            {
                return;
            }

            while (CountActiveMessages() > maxActiveMessages)
            {
                var oldestIndex = FindOldestActiveMessageIndex();

                if (oldestIndex < 0)
                {
                    return;
                }

                var overlay = _overlays[oldestIndex];

                if (overlay != null)
                {
                    overlay.ExpiresAt = 0f;
                }
            }
        }

        private int CountActiveMessages()
        {
            var count = 0;

            foreach (var overlay in _overlays)
            {
                if (overlay != null && overlay.HasActiveMessage)
                {
                    count++;
                }
            }

            return count;
        }

        private int FindOldestActiveMessageIndex()
        {
            var oldestIndex = -1;
            var oldestShownAt = float.MaxValue;

            for (var i = 0; i < _overlays.Count; i++)
            {
                var overlay = _overlays[i];

                if (overlay == null || !overlay.HasActiveMessage)
                {
                    continue;
                }

                if (overlay.LastShownAt < oldestShownAt)
                {
                    oldestShownAt = overlay.LastShownAt;
                    oldestIndex = i;
                }
            }

            return oldestIndex;
        }

        private BeaverOverlay FindOverlay(Character character)
        {
            foreach (var overlay in _overlays)
            {
                if (overlay.Character == character)
                {
                    return overlay;
                }
            }

            return null;
        }

        private void SortOverlayDrawOrder()
        {
            _overlays.Sort(CompareOverlayDrawOrder);

            foreach (var overlay in _overlays)
            {
                if (overlay != null && overlay.Element != null && overlay.Element.parent == _root)
                {
                    overlay.Element.BringToFront();
                }
            }
        }

        private static int CompareOverlayDrawOrder(BeaverOverlay a, BeaverOverlay b)
        {
            if (ReferenceEquals(a, b))
            {
                return 0;
            }

            if (a == null)
            {
                return -1;
            }

            if (b == null)
            {
                return 1;
            }

            var aHasMessage = a.HasActiveMessage;
            var bHasMessage = b.HasActiveMessage;

            if (aHasMessage != bHasMessage)
            {
                return aHasMessage ? 1 : -1;
            }

            if (aHasMessage)
            {
                var messageTimeComparison = a.LastShownAt.CompareTo(b.LastShownAt);

                if (messageTimeComparison != 0)
                {
                    return messageTimeComparison;
                }

                return CompareOverlayNames(a, b);
            }

            var distanceDelta = Mathf.Abs(a.DistanceToCamera - b.DistanceToCamera);

            if (distanceDelta > OverlayDistanceTieThreshold)
            {
                return b.DistanceToCamera.CompareTo(a.DistanceToCamera);
            }

            return CompareOverlayNames(a, b);
        }

        private static int CompareOverlayNames(BeaverOverlay a, BeaverOverlay b)
        {
            var nameComparison = string.Compare(
                a.BeaverName ?? "",
                b.BeaverName ?? "",
                StringComparison.OrdinalIgnoreCase);

            if (nameComparison != 0)
            {
                return nameComparison;
            }

            var aId = GetOverlayCharacterInstanceId(a);
            var bId = GetOverlayCharacterInstanceId(b);

            return aId.CompareTo(bId);
        }

        private static int GetOverlayCharacterInstanceId(BeaverOverlay overlay)
        {
            if (overlay == null || overlay.Character == null || overlay.Character.Transform == null)
            {
                return 0;
            }

            return overlay.Character.Transform.GetInstanceID();
        }

        private void RemoveOverlayAt(int index)
        {
            var overlay = _overlays[index];

            if (overlay != null)
            {
                overlay.RemoveFromParent();
            }

            _overlays.RemoveAt(index);
        }

        private static Transform GetBubbleFollowTarget(Character character)
        {
            if (character == null || character.Transform == null)
            {
                return null;
            }

            var root = character.Transform;
            var renderer = root.GetComponentInChildren<Renderer>();

            if (renderer != null)
            {
                return renderer.transform;
            }

            if (root.childCount > 0)
            {
                return root.GetChild(0);
            }

            return root;
        }

        private static string GetOverlayDisplayName(
            Character character,
            string fallbackName,
            out bool hasNameColor,
            out Color nameColor)
        {
            var entityName = GetCharacterEntityName(character);

            if (!string.IsNullOrEmpty(entityName))
            {
                return ParseOverlayDisplayName(
                    entityName,
                    fallbackName,
                    out hasNameColor,
                    out nameColor);
            }

            return ParseOverlayDisplayName(
                fallbackName,
                fallbackName,
                out hasNameColor,
                out nameColor);
        }

        private static string GetCharacterEntityName(Character character)
        {
            if (character == null)
            {
                return "";
            }

            NamedEntity namedEntity;

            if (character.TryGetComponent(out namedEntity) && !string.IsNullOrEmpty(namedEntity.EntityName))
            {
                return namedEntity.EntityName;
            }

            return "";
        }

        private static string ParseOverlayDisplayName(
            string sourceName,
            string fallbackName,
            out bool hasNameColor,
            out Color nameColor)
        {
            hasNameColor = false;
            nameColor = NameTextColor;

            var plainName = TwitchBornTextSanitizer.SanitizeDisplayName(
                sourceName,
                MaxBeaverNameLength,
                out var hexColor);

            if (string.IsNullOrEmpty(plainName))
            {
                plainName = SanitizeText(fallbackName, MaxBeaverNameLength);
            }

            if (!string.IsNullOrEmpty(hexColor) && ColorUtility.TryParseHtmlString(hexColor, out var parsedColor))
            {
                hasNameColor = true;
                nameColor = parsedColor;
            }

            return plainName;
        }

        private string BuildOwnerTooltip(string viewerName)
        {
            if (string.IsNullOrEmpty(viewerName))
            {
                return "";
            }

            return _loc.T(OwnedByLocKey, viewerName);
        }

        private static string SanitizeText(string value, int maxLength)
        {
            return TwitchBornTextSanitizer.SanitizePlainText(value, maxLength);
        }

        private static string WrapText(string text, int maxLineLength)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            maxLineLength = Mathf.Max(4, maxLineLength);

            var words = text.Split(' ');
            var builder = new StringBuilder();
            var currentLineLength = 0;

            foreach (var rawWord in words)
            {
                if (string.IsNullOrEmpty(rawWord))
                {
                    continue;
                }

                var word = rawWord;

                while (word.Length > maxLineLength)
                {
                    if (currentLineLength > 0)
                    {
                        builder.Append('\n');
                        currentLineLength = 0;
                    }

                    builder.Append(word.Substring(0, maxLineLength));
                    word = word.Substring(maxLineLength);

                    if (word.Length > 0)
                    {
                        builder.Append('\n');
                    }
                }

                if (string.IsNullOrEmpty(word))
                {
                    continue;
                }

                if (currentLineLength > 0 && currentLineLength + word.Length + 1 > maxLineLength)
                {
                    builder.Append('\n');
                    builder.Append(word);
                    currentLineLength = word.Length;
                    continue;
                }

                if (currentLineLength > 0)
                {
                    builder.Append(' ');
                    currentLineLength++;
                }

                builder.Append(word);
                currentLineLength += word.Length;
            }

            return builder.ToString();
        }

        private class BeaverOverlayUpdater : MonoBehaviour
        {
            private BeaverOverlayService _service;

            public void Initialize(BeaverOverlayService service)
            {
                _service = service;
            }

            private void Update()
            {
                if (_service != null)
                {
                    _service.UpdateBubbles();
                }
            }
        }
    }
}