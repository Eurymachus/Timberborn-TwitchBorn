using Timberborn.Characters;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwitchBorn.UI
{
    public class BeaverOverlay
    {
        public Character Character { get; private set; }
        public VisualElement Element { get; private set; }
        public VisualElement OuterFrame { get; private set; }
        public VisualElement OuterBody { get; private set; }
        public VisualElement NameplateFrame { get; private set; }
        public VisualElement NameplateBody { get; private set; }
        public Label NameLabel { get; private set; }
        public VisualElement MessageContainer { get; private set; }
        public Label MessageLabel { get; private set; }

        public VisualElement MeasureElement { get; private set; }
        public Label MeasureNameLabel { get; private set; }
        public VisualElement MeasureMessageContainer { get; private set; }
        public Label MeasureMessageLabel { get; private set; }

        public float ExpiresAt { get; set; }
        public float LastShownAt { get; set; }
        public float DistanceToCamera { get; set; }
        public float Expansion { get; set; }
        public bool HasSmoothedWorldY { get; private set; }
        public float SmoothedWorldY { get; private set; }

        public float AnimatedOverlayWidth { get; set; }
        public float TargetOverlayWidth { get; set; }
        public float AnimatedMessageHeight { get; set; }
        public float TargetMessageHeight { get; set; }
        public bool NeedsSizeMeasurement { get; set; }
        public bool IsHovered { get; set; }

        public string BeaverName { get; private set; }
        public string ViewerName { get; private set; }
        public bool HasNameColor { get; private set; }
        public Color32 NameColor { get; private set; }
        public bool HasNameShadowColor { get; private set; }
        public Color32 NameShadowColor { get; private set; }

        public bool HasActiveMessage
        {
            get
            {
                return Time.unscaledTime < ExpiresAt;
            }
        }

        public BeaverOverlay(
            Character character,
            VisualElement element,
            VisualElement outerFrame,
            VisualElement outerBody,
            VisualElement nameplateFrame,
            VisualElement nameplateBody,
            Label nameLabel,
            VisualElement messageContainer,
            Label messageLabel,
            VisualElement measureElement,
            Label measureNameLabel,
            VisualElement measureMessageContainer,
            Label measureMessageLabel,
            string beaverName,
            string viewerName,
            bool hasNameColor,
            Color32 nameColor,
            bool hasNameShadowColor,
            Color32 nameShadowColor)
        {
            Character = character;
            Element = element;
            OuterFrame = outerFrame;
            OuterBody = outerBody;
            NameplateFrame = nameplateFrame;
            NameplateBody = nameplateBody;
            NameLabel = nameLabel;
            MessageContainer = messageContainer;
            MessageLabel = messageLabel;
            MeasureElement = measureElement;
            MeasureNameLabel = measureNameLabel;
            MeasureMessageContainer = measureMessageContainer;
            MeasureMessageLabel = measureMessageLabel;
            BeaverName = beaverName ?? "";
            ViewerName = viewerName ?? "";
            HasNameColor = hasNameColor;
            NameColor = nameColor;
            HasNameShadowColor = hasNameShadowColor;
            NameShadowColor = nameShadowColor;

            ExpiresAt = 0f;
            LastShownAt = 0f;
            DistanceToCamera = 0f;
            Expansion = 0f;

            AnimatedOverlayWidth = 0f;
            TargetOverlayWidth = 0f;
            AnimatedMessageHeight = 0f;
            TargetMessageHeight = 0f;
            NeedsSizeMeasurement = false;
            IsHovered = false;
            HasSmoothedWorldY = false;
            SmoothedWorldY = 0f;
        }

        public void SetDisplayNames(
            string beaverName,
            string viewerName,
            bool hasNameColor,
            Color32 nameColor,
            bool hasNameShadowColor,
            Color32 nameShadowColor)
        {
            BeaverName = beaverName ?? "";
            ViewerName = viewerName ?? "";
            HasNameColor = hasNameColor;
            NameColor = nameColor;
            HasNameShadowColor = hasNameShadowColor;
            NameShadowColor = nameShadowColor;

            if (NameLabel != null)
            {
                NameLabel.text = BeaverName;
            }

            if (MeasureNameLabel != null)
            {
                MeasureNameLabel.text = BeaverName;
            }
        }

        public void SetSmoothedWorldY(float value)
        {
            SmoothedWorldY = value;
            HasSmoothedWorldY = true;
        }

        public void ResetSmoothedWorldY()
        {
            SmoothedWorldY = 0f;
            HasSmoothedWorldY = false;
        }

        public void SetMessage(string message)
        {
            if (MessageLabel != null)
            {
                MessageLabel.text = message ?? "";
            }

            if (MeasureMessageLabel != null)
            {
                MeasureMessageLabel.text = message ?? "";
            }

            NeedsSizeMeasurement = true;
        }

        public void Show()
        {
            if (Element == null)
            {
                return;
            }

            Element.style.display = DisplayStyle.Flex;
            Element.style.opacity = 1f;
        }

        public void Hide()
        {
            if (Element == null)
            {
                return;
            }

            Element.style.display = DisplayStyle.Flex;
            Element.style.opacity = 0f;
            Element.pickingMode = PickingMode.Ignore;

            if (OuterFrame != null)
            {
                OuterFrame.pickingMode = PickingMode.Ignore;
            }

            if (OuterBody != null)
            {
                OuterBody.pickingMode = PickingMode.Ignore;
            }

            if (NameplateFrame != null)
            {
                NameplateFrame.pickingMode = PickingMode.Ignore;
            }

            if (NameplateBody != null)
            {
                NameplateBody.pickingMode = PickingMode.Ignore;
            }

            if (NameLabel != null)
            {
                NameLabel.pickingMode = PickingMode.Ignore;
            }

            if (MessageContainer != null)
            {
                MessageContainer.pickingMode = PickingMode.Ignore;
            }

            if (MessageLabel != null)
            {
                MessageLabel.pickingMode = PickingMode.Ignore;
            }
        }

        public void RemoveFromParent()
        {
            if (Element != null && Element.parent != null)
            {
                Element.parent.Remove(Element);
            }

            if (MeasureElement != null && MeasureElement.parent != null)
            {
                MeasureElement.parent.Remove(MeasureElement);
            }
        }
    }
}