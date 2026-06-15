using System.Collections.Generic;
using System.Text;
using Timberborn.CameraSystem;
using Timberborn.Characters;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.UIElements;
using TwitchBorn.Core;
using TwitchBorn.Registry;
using TwitchBorn.Settings;
using TwitchBorn.UI;
using Timberborn.SelectionSystem;
using Timberborn.InputSystem;
using Timberborn.Localization;
using Timberborn.TooltipSystem;

namespace TwitchBorn.Services
{
    public class BeaverOverlayService : ILoadableSingleton, IUnloadableSingleton
    {
        private const int MaxBeaverNameLength = 32;
        private const int MaxViewerNameLength = 32;
        private const string OwnedByLocKey = "Eurymachus.TwitchBorn.Overlay.OwnedBy";

        private const int OverlayPaddingLeft = 8;
        private const int OverlayPaddingRight = 8;
        private const int OverlayPaddingTop = 4;
        private const int OverlayPaddingBottom = 4;

        private const int OverlayBorderRadius = 7;
        private const int OverlayBorderWidth = 2;

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

        private static readonly Color OverlayBackgroundColor = new Color32(20, 39, 35, 235);
        private static readonly Color NameplateBackgroundColor = new Color32(31, 63, 55, 245);
        private static readonly Color MessageTextColor = new Color32(224, 224, 224, 255);
        private static readonly Color NameTextColor = new Color32(235, 235, 235, 255);
        private static readonly Color OverlayBorderColor = new Color32(178, 156, 108, 255);
        private static readonly Color NameplateBorderColor = new Color32(116, 147, 128, 180);
        private static readonly Color NameplateBorderHoverColor = new Color32(226, 210, 145, 240);
        private static readonly Color NameplateBorderSelectedColor = new Color32(118, 196, 255, 230);

        private readonly ILoc _loc;
        private readonly CameraService _cameraService;
        private readonly Underlay _underlay;
        private readonly OverlaySettingsOwner _settingsOwner;
        private readonly BeaverRegistry _beaverRegistry;
        private readonly EntitySelectionService _entitySelectionService;
        private readonly ITooltipRegistrar _tooltipRegistrar;

        private readonly InputSettings _inputSettings;

        private readonly List<BeaverOverlay> _overlays = new List<BeaverOverlay>();

        private VisualElement _root;
        private GameObject _updateDriverObject;

        public BeaverOverlayService(
            CameraService cameraService,
            Underlay underlay,
            OverlaySettingsOwner settingsOwner,
            BeaverRegistry beaverRegistry,
            EntitySelectionService entitySelectionService,
            ITooltipRegistrar tooltipRegistrar,
            InputSettings inputSettings,
            ILoc loc)
        {
            _cameraService = cameraService;
            _underlay = underlay;
            _settingsOwner = settingsOwner;
            _beaverRegistry = beaverRegistry;
            _entitySelectionService = entitySelectionService;
            _tooltipRegistrar = tooltipRegistrar;
            _inputSettings = inputSettings;
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

            TwitchBornLog.Info("[TwitchBorn] claimed beaver overlay service loaded.");
        }

        public void Unload()
        {
            if (_updateDriverObject != null)
            {
                Object.Destroy(_updateDriverObject);
                _updateDriverObject = null;
            }

            if (_root != null)
            {
                _root.RemoveFromHierarchy();
                _root = null;
            }

            _overlays.Clear();

            TwitchBornLog.Info("[TwitchBorn] claimed beaver overlay service unloaded.");
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
                TwitchBornLog.Info("[TwitchBorn] Cannot show overlay message because character was null.");
                return;
            }

            if (_root == null)
            {
                TwitchBornLog.Info("[TwitchBorn] Cannot show overlay message because UI root was null.");
                return;
            }

            var safeBeaverName = SanitizeText(beaverName, MaxBeaverNameLength);
            var safeViewerName = SanitizeText(viewerName, MaxViewerNameLength);
            var safeMessage = WrapText(
                SanitizeText(message, _settingsOwner.MaxMessageLengthSetting.Value),
                _settingsOwner.WrapLineLengthSetting.Value);

            var overlay = FindOverlay(character);

            if (overlay == null)
            {
                overlay = CreateOverlay(character, safeBeaverName, safeViewerName);
                _overlays.Add(overlay);
            }

            overlay.SetDisplayNames(
                safeBeaverName,
                safeViewerName);

            CaptureCurrentOverlaySize(overlay);

            overlay.SetMessage(safeMessage);
            SetTargetSizeFromMessage(overlay, safeMessage);

            overlay.LastShownAt = Time.unscaledTime;
            overlay.ExpiresAt = Time.unscaledTime + _settingsOwner.MessageDurationSeconds.Value;

            ApplyOverlayStyle(overlay);
            EnforceMaxActiveMessages();

            TwitchBornLog.Info("[TwitchBorn] Overlay message shown for " + safeBeaverName + " owned by " + safeViewerName + ": " + safeMessage);
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
                    overlay = CreateOverlay(
                        target.Character,
                        SanitizeText(target.BeaverName, MaxBeaverNameLength),
                        SanitizeText(target.ViewerName, MaxViewerNameLength));

                    _overlays.Add(overlay);
                    continue;
                }

                var safeBeaverName = SanitizeText(target.BeaverName, MaxBeaverNameLength);
                var safeViewerName = SanitizeText(target.ViewerName, MaxViewerNameLength);

                overlay.SetDisplayNames(
                    safeBeaverName,
                    safeViewerName);
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

        private void ApplyOverlayStyle(BeaverOverlay overlay)
        {
            if (overlay == null || overlay.Element == null)
            {
                return;
            }

            var shellAlpha = overlay.Expansion;

            overlay.Element.style.backgroundColor = WithAlpha(OverlayBackgroundColor, Mathf.RoundToInt(235f * shellAlpha));

            overlay.Element.style.paddingLeft = Mathf.RoundToInt(OverlayPaddingLeft * shellAlpha);
            overlay.Element.style.paddingRight = Mathf.RoundToInt(OverlayPaddingRight * shellAlpha);
            overlay.Element.style.paddingTop = Mathf.RoundToInt(OverlayPaddingTop * shellAlpha);
            overlay.Element.style.paddingBottom = Mathf.RoundToInt(OverlayPaddingBottom * shellAlpha);

            overlay.Element.style.borderTopLeftRadius = OverlayBorderRadius;
            overlay.Element.style.borderTopRightRadius = OverlayBorderRadius;
            overlay.Element.style.borderBottomLeftRadius = OverlayBorderRadius;
            overlay.Element.style.borderBottomRightRadius = OverlayBorderRadius;

            overlay.Element.style.borderTopWidth = Mathf.RoundToInt(OverlayBorderWidth * shellAlpha);
            overlay.Element.style.borderRightWidth = Mathf.RoundToInt(OverlayBorderWidth * shellAlpha);
            overlay.Element.style.borderBottomWidth = Mathf.RoundToInt(OverlayBorderWidth * shellAlpha);
            overlay.Element.style.borderLeftWidth = Mathf.RoundToInt(OverlayBorderWidth * shellAlpha);

            overlay.Element.style.borderTopColor = WithAlpha(OverlayBorderColor, Mathf.RoundToInt(255f * shellAlpha));
            overlay.Element.style.borderRightColor = WithAlpha(OverlayBorderColor, Mathf.RoundToInt(255f * shellAlpha));
            overlay.Element.style.borderBottomColor = WithAlpha(OverlayBorderColor, Mathf.RoundToInt(255f * shellAlpha));
            overlay.Element.style.borderLeftColor = WithAlpha(OverlayBorderColor, Mathf.RoundToInt(255f * shellAlpha));

            if (overlay.NameLabel != null)
            {
                overlay.NameLabel.style.color = NameTextColor;
                overlay.NameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                overlay.NameLabel.style.whiteSpace = WhiteSpace.NoWrap;
                overlay.NameLabel.style.fontSize = _settingsOwner.OverlayFontSizeSetting.Value;

                var nameplateBorderColor = NameplateBorderColor;

                if (IsOverlayBeaverSelected(overlay))
                {
                    nameplateBorderColor = NameplateBorderSelectedColor;
                }

                if (overlay.IsHovered)
                {
                    nameplateBorderColor = NameplateBorderHoverColor;
                }

                overlay.NameLabel.style.borderTopColor = nameplateBorderColor;
                overlay.NameLabel.style.borderRightColor = nameplateBorderColor;
                overlay.NameLabel.style.borderBottomColor = nameplateBorderColor;
                overlay.NameLabel.style.borderLeftColor = nameplateBorderColor;
            }

            if (overlay.MessageContainer != null)
            {
                overlay.MessageContainer.style.overflow = Overflow.Hidden;
            }

            if (overlay.MessageLabel != null)
            {
                overlay.MessageLabel.style.color = MessageTextColor;
                overlay.MessageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                overlay.MessageLabel.style.marginTop = 2;
                overlay.MessageLabel.style.marginLeft = 4;
                overlay.MessageLabel.style.marginRight = 4;
                overlay.MessageLabel.style.marginBottom = 2;

                overlay.MessageLabel.style.whiteSpace = WhiteSpace.Normal;
                overlay.MessageLabel.style.fontSize = Mathf.Max(8, _settingsOwner.OverlayFontSizeSetting.Value - 1);
                overlay.MessageLabel.style.maxWidth = MessageMaxTextWidth;
            }

            ApplyMeasureStyle(overlay);
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
            string viewerName)
        {
            var element = new VisualElement();
            element.name = "BeaverOverlay";
            element.pickingMode = PickingMode.Ignore;
            element.style.position = Position.Absolute;
            element.style.display = DisplayStyle.None;
            element.style.flexDirection = FlexDirection.Column;
            element.style.alignItems = Align.Center;
            element.style.minWidth = 0;
            element.style.maxWidth = OverlayMaxMessageWidth;
            element.style.alignSelf = Align.FlexStart;

            var nameLabel = new Label();
            nameLabel.name = "ClaimedBeaverNameplate";
            nameLabel.pickingMode = PickingMode.Position;
            nameLabel.text = beaverName;
            nameLabel.style.backgroundColor = NameplateBackgroundColor;
            nameLabel.style.paddingLeft = 10;
            nameLabel.style.paddingRight = 10;
            nameLabel.style.paddingTop = 3;
            nameLabel.style.paddingBottom = 3;
            nameLabel.style.borderTopLeftRadius = 5;
            nameLabel.style.borderTopRightRadius = 5;
            nameLabel.style.borderBottomLeftRadius = 5;
            nameLabel.style.borderBottomRightRadius = 5;
            nameLabel.style.borderTopWidth = 1;
            nameLabel.style.borderRightWidth = 1;
            nameLabel.style.borderBottomWidth = 1;
            nameLabel.style.borderLeftWidth = 1;
            nameLabel.style.borderTopColor = NameplateBorderColor;
            nameLabel.style.borderRightColor = NameplateBorderColor;
            nameLabel.style.borderBottomColor = NameplateBorderColor;
            nameLabel.style.borderLeftColor = NameplateBorderColor;

            var messageContainer = new VisualElement();
            messageContainer.name = "ClaimedBeaverMessageContainer";
            messageContainer.pickingMode = PickingMode.Ignore;
            messageContainer.style.overflow = Overflow.Hidden;
            messageContainer.style.height = 0;
            messageContainer.style.opacity = 0;
            messageContainer.style.marginTop = 3;
            messageContainer.style.flexDirection = FlexDirection.Column;

            var messageLabel = new Label();
            messageLabel.name = "ClaimedBeaverMessage";
            messageLabel.pickingMode = PickingMode.Ignore;
            messageLabel.style.maxWidth = MessageMaxTextWidth;
            messageLabel.style.whiteSpace = WhiteSpace.Normal;

            messageContainer.Add(messageLabel);
            element.Add(nameLabel);
            element.Add(messageContainer);

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
            measureNameLabel.style.paddingLeft = 10;
            measureNameLabel.style.paddingRight = 10;
            measureNameLabel.style.paddingTop = 3;
            measureNameLabel.style.paddingBottom = 3;

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
                nameLabel,
                messageContainer,
                messageLabel,
                measureElement,
                measureNameLabel,
                measureMessageContainer,
                measureMessageLabel,
                beaverName,
                viewerName);

                RegisterOverlayInteraction(element, overlay);
                RegisterOverlayInteraction(nameLabel, overlay);
                RegisterOverlayTooltip(element, overlay);
                RegisterOverlayTooltip(nameLabel, overlay);

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

            SelectableObject selectableObject;

            if (!overlay.Character.TryGetComponent(out selectableObject))
            {
                return false;
            }

            return _entitySelectionService.IsSelected(selectableObject);
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

            var worldPosition = followTarget.position + new Vector3(0f, _settingsOwner.OverlayHeightOffset.Value, 0f);

            if (!_cameraService.IsInFront(worldPosition))
            {
                overlay.Hide();
                return;
            }

            overlay.DistanceToCamera = Vector3.Distance(Camera.main.transform.position, worldPosition);

            var nameplateVisible = overlay.DistanceToCamera <= _settingsOwner.NameplateVisibilityDistanceSetting.Value;
            var messageVisible = ShouldShowMessage(overlay);
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

            if (overlay.NameLabel != null)
            {
                overlay.NameLabel.pickingMode = isMessageMode
                    ? PickingMode.Ignore
                    : PickingMode.Position;
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
                            + OverlayBorderWidth * 2f
                            + 8f;

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
            _overlays.Sort((a, b) => b.DistanceToCamera.CompareTo(a.DistanceToCamera));

            foreach (var overlay in _overlays)
            {
                if (overlay != null && overlay.Element != null && overlay.Element.parent == _root)
                {
                    overlay.Element.BringToFront();
                }
            }
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
            if (value == null)
            {
                return "";
            }

            var sanitized = value.Trim();

            sanitized = sanitized.Replace("\r", "");
            sanitized = sanitized.Replace("\n", " ");
            sanitized = sanitized.Replace("\t", " ");

            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized.Substring(0, maxLength);
            }

            return sanitized;
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