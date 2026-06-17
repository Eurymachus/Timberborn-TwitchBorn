using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwitchBorn.UI
{
    public class TwitchBornNineSliceVisualElement : VisualElement
    {
        private readonly List<NineSliceQuad> _quads = new List<NineSliceQuad>();

        private Sprite _sprite;
        private string _resourcePath;
        private int _sliceTop;
        private int _sliceRight;
        private int _sliceBottom;
        private int _sliceLeft;
        private float _sliceScale;
        private Color _tint;

        public TwitchBornNineSliceVisualElement()
        {
            _sliceScale = 1f;
            _tint = Color.white;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetBackground(
            string resourcePath,
            int slice,
            float sliceScale)
        {
            SetBackground(
                resourcePath,
                slice,
                slice,
                slice,
                slice,
                sliceScale);
        }

        public void SetBackground(
            string resourcePath,
            int sliceTop,
            int sliceRight,
            int sliceBottom,
            int sliceLeft,
            float sliceScale)
        {
            _resourcePath = resourcePath ?? "";
            _sliceTop = Mathf.Max(0, sliceTop);
            _sliceRight = Mathf.Max(0, sliceRight);
            _sliceBottom = Mathf.Max(0, sliceBottom);
            _sliceLeft = Mathf.Max(0, sliceLeft);
            _sliceScale = Mathf.Max(0.01f, sliceScale);
            _sprite = string.IsNullOrEmpty(_resourcePath)
                ? null
                : Resources.Load<Sprite>(_resourcePath);

            MarkDirtyRepaint();
        }

        public void SetTint(Color tint)
        {
            _tint = tint;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            if (_sprite == null || _sprite.texture == null)
            {
                return;
            }

            var width = resolvedStyle.width;
            var height = resolvedStyle.height;

            if (float.IsNaN(width) || float.IsNaN(height) || width <= 0.01f || height <= 0.01f)
            {
                return;
            }

            _quads.Clear();
            BuildQuads(width, height, _quads);

            if (_quads.Count == 0)
            {
                return;
            }

            var mesh = context.Allocate(
                _quads.Count * 4,
                _quads.Count * 6,
                _sprite.texture);

            for (var i = 0; i < _quads.Count; i++)
            {
                WriteQuad(mesh, _quads[i], i * 4);
            }
        }

        private void BuildQuads(float width, float height, List<NineSliceQuad> quads)
        {
            var texture = _sprite.texture;
            var textureWidth = texture.width;
            var textureHeight = texture.height;

            var top = _sliceTop * _sliceScale;
            var right = _sliceRight * _sliceScale;
            var bottom = _sliceBottom * _sliceScale;
            var left = _sliceLeft * _sliceScale;

            var horizontalScale = Mathf.Min(width / Mathf.Max(0.01f, left + right), 1f);
            var verticalScale = Mathf.Min(height / Mathf.Max(0.01f, top + bottom), 1f);

            var renderedTop = top * verticalScale;
            var renderedRight = right * horizontalScale;
            var renderedBottom = bottom * verticalScale;
            var renderedLeft = left * horizontalScale;

            var uvTop = (float)_sliceTop / textureHeight * verticalScale;
            var uvRight = (float)_sliceRight / textureWidth * horizontalScale;
            var uvBottom = (float)_sliceBottom / textureHeight * verticalScale;
            var uvLeft = (float)_sliceLeft / textureWidth * horizontalScale;

            var canTileHorizontally = horizontalScale >= 1f;
            var canTileVertically = verticalScale >= 1f;

            var centerTextureWidth = Mathf.Max(1, textureWidth - _sliceLeft - _sliceRight);
            var centerTextureHeight = Mathf.Max(1, textureHeight - _sliceTop - _sliceBottom);
            var centerWidth = Mathf.Max(0f, width - renderedLeft - renderedRight);
            var centerHeight = Mathf.Max(0f, height - renderedTop - renderedBottom);

            var horizontalTileCount = canTileHorizontally
                ? Math.Max(Mathf.RoundToInt(centerWidth / (_sliceScale * centerTextureWidth)), 1)
                : 0;
            var verticalTileCount = canTileVertically
                ? Math.Max(Mathf.RoundToInt(centerHeight / (_sliceScale * centerTextureHeight)), 1)
                : 0;

            var horizontalTileWidth = horizontalTileCount > 0
                ? centerWidth / horizontalTileCount
                : 0f;
            var verticalTileHeight = verticalTileCount > 0
                ? centerHeight / verticalTileCount
                : 0f;

            AddQuad(quads, 0f, 0f, renderedLeft, renderedTop, 0f, 1f, uvLeft, 1f - uvTop);
            AddQuad(quads, width - renderedRight, 0f, width, renderedTop, 1f - uvRight, 1f, 1f, 1f - uvTop);
            AddQuad(quads, 0f, height - renderedBottom, renderedLeft, height, 0f, uvBottom, uvLeft, 0f);
            AddQuad(quads, width - renderedRight, height - renderedBottom, width, height, 1f - uvRight, uvBottom, 1f, 0f);

            for (var x = 0; x < horizontalTileCount; x++)
            {
                var x0 = renderedLeft + x * horizontalTileWidth;
                var x1 = renderedLeft + (x + 1) * horizontalTileWidth;
                AddQuad(quads, x0, 0f, x1, renderedTop, uvLeft, 1f, 1f - uvRight, 1f - uvTop);
                AddQuad(quads, x0, height - renderedBottom, x1, height, uvLeft, uvBottom, 1f - uvRight, 0f);
            }

            for (var y = 0; y < verticalTileCount; y++)
            {
                var y0 = renderedTop + y * verticalTileHeight;
                var y1 = renderedTop + (y + 1) * verticalTileHeight;
                AddQuad(quads, 0f, y0, renderedLeft, y1, 0f, 1f - uvTop, uvLeft, uvBottom);
                AddQuad(quads, width - renderedRight, y0, width, y1, 1f - uvRight, 1f - uvTop, 1f, uvBottom);
            }

            for (var y = 0; y < verticalTileCount; y++)
            {
                var y0 = renderedTop + y * verticalTileHeight;
                var y1 = renderedTop + (y + 1) * verticalTileHeight;

                for (var x = 0; x < horizontalTileCount; x++)
                {
                    var x0 = renderedLeft + x * horizontalTileWidth;
                    var x1 = renderedLeft + (x + 1) * horizontalTileWidth;
                    AddQuad(quads, x0, y0, x1, y1, uvLeft, 1f - uvTop, 1f - uvRight, uvBottom);
                }
            }
        }

        private static void AddQuad(
            List<NineSliceQuad> quads,
            float x0,
            float y0,
            float x1,
            float y1,
            float u0,
            float v0,
            float u1,
            float v1)
        {
            if (x1 - x0 <= 0.001f || y1 - y0 <= 0.001f)
            {
                return;
            }

            quads.Add(new NineSliceQuad(x0, y0, x1, y1, u0, v0, u1, v1));
        }

        private void WriteQuad(MeshWriteData mesh, NineSliceQuad quad, int vertexOffset)
        {
            mesh.SetNextVertex(CreateVertex(quad.X0, quad.Y0, quad.U0, quad.V0));
            mesh.SetNextVertex(CreateVertex(quad.X1, quad.Y0, quad.U1, quad.V0));
            mesh.SetNextVertex(CreateVertex(quad.X1, quad.Y1, quad.U1, quad.V1));
            mesh.SetNextVertex(CreateVertex(quad.X0, quad.Y1, quad.U0, quad.V1));

            mesh.SetNextIndex((ushort)vertexOffset);
            mesh.SetNextIndex((ushort)(vertexOffset + 1));
            mesh.SetNextIndex((ushort)(vertexOffset + 2));
            mesh.SetNextIndex((ushort)(vertexOffset + 2));
            mesh.SetNextIndex((ushort)(vertexOffset + 3));
            mesh.SetNextIndex((ushort)vertexOffset);
        }

        private Vertex CreateVertex(float x, float y, float u, float v)
        {
            return new Vertex
            {
                position = new Vector3(x, y, Vertex.nearZ),
                uv = new Vector2(u, v),
                tint = _tint
            };
        }

        private struct NineSliceQuad
        {
            public readonly float X0;
            public readonly float Y0;
            public readonly float X1;
            public readonly float Y1;
            public readonly float U0;
            public readonly float V0;
            public readonly float U1;
            public readonly float V1;

            public NineSliceQuad(
                float x0,
                float y0,
                float x1,
                float y1,
                float u0,
                float v0,
                float u1,
                float v1)
            {
                X0 = x0;
                Y0 = y0;
                X1 = x1;
                Y1 = y1;
                U0 = u0;
                V0 = v0;
                U1 = u1;
                V1 = v1;
            }
        }
    }
}