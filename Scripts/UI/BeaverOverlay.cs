using Timberborn.Characters;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwitchBorn.UI
{
    public class BeaverOverlay
    {
        public Character Character { get; private set; }
        public VisualElement Element { get; private set; }
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

        public float AnimatedOverlayWidth { get; set; }
        public float TargetOverlayWidth { get; set; }
        public float AnimatedMessageHeight { get; set; }
        public float TargetMessageHeight { get; set; }
        public bool NeedsSizeMeasurement { get; set; }
        public bool IsHovered { get; set; }

        public string BeaverName { get; private set; }
        public string ViewerName { get; private set; }

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
            Label nameLabel,
            VisualElement messageContainer,
            Label messageLabel,
            VisualElement measureElement,
            Label measureNameLabel,
            VisualElement measureMessageContainer,
            Label measureMessageLabel,
            string beaverName,
            string viewerName)
        {
            Character = character;
            Element = element;
            NameLabel = nameLabel;
            MessageContainer = messageContainer;
            MessageLabel = messageLabel;
            MeasureElement = measureElement;
            MeasureNameLabel = measureNameLabel;
            MeasureMessageContainer = measureMessageContainer;
            MeasureMessageLabel = measureMessageLabel;
            BeaverName = beaverName ?? "";
            ViewerName = viewerName ?? "";

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
        }

        public void SetDisplayNames(
            string beaverName,
            string viewerName)
        {
            BeaverName = beaverName ?? "";
            ViewerName = viewerName ?? "";

            if (NameLabel != null)
            {
                NameLabel.text = BeaverName;
            }

            if (MeasureNameLabel != null)
            {
                MeasureNameLabel.text = BeaverName;
            }
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
            if (Element != null)
            {
                Element.style.display = DisplayStyle.Flex;
            }
        }

        public void Hide()
        {
            if (Element != null)
            {
                Element.style.display = DisplayStyle.None;
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