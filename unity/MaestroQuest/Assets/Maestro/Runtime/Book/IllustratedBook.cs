// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Collections.Generic;
using Maestro.Quest.Art;
using UnityEngine;

namespace Maestro.Quest.Book
{
    /// <summary>Opaque physical paper and covers; browser UVs share one document.</summary>
    public sealed class IllustratedBook : MonoBehaviour
    {
        public const float PageWidth = .30f;
        public const float PageHeight = .43f;
        public const float Gutter = .009f;
        readonly List<Object> owned = new();
        readonly List<Material> pageMaterials = new();
        Transform ribbon;
        Transform turnLeaf;
        Coroutine turn;
        public bool ReducedMotion { get; set; }
        public bool Turning => turn != null;
        public Transform BookmarkRibbon => ribbon;

        public void Build()
        {
            if (pageMaterials.Count > 0) return;
            var cover = Own(IllustratedMaterials.Create(IllustratedMaterials.Cover));
            var stack = Own(IllustratedMaterials.Create(IllustratedMaterials.PageBlock));
            var ink = Own(IllustratedMaterials.Create(IllustratedMaterials.Ink, 0));
            foreach (var side in new[] { PageSide.Left, PageSide.Right })
            {
                float sign = side == PageSide.Left ? -1 : 1;
                Box(side + " cover", new Vector3(sign * (.5f * PageWidth + Gutter), 0, .024f), new Vector3(PageWidth + .025f, PageHeight + .027f, .009f), cover);
                Box(side + " page stack", new Vector3(sign * (.5f * PageWidth + Gutter), 0, .013f), new Vector3(PageWidth + .003f, PageHeight + .006f, .018f), stack);
                var material = Own(IllustratedMaterials.Create(Color.white, 0));
                pageMaterials.Add(material);
                var page = new GameObject(side + " page", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(BookPageTarget));
                page.transform.SetParent(transform, false);
                var mesh = Own(CreatePageMesh(side));
                page.GetComponent<MeshFilter>().sharedMesh = mesh;
                page.GetComponent<MeshRenderer>().sharedMaterial = material;
                page.GetComponent<MeshCollider>().sharedMesh = mesh;
                page.GetComponent<BookPageTarget>().Side = side;
                // Fine parallel pencil marks make stacked leaves read at a distance.
                for (int i = 0; i < 4; i++)
                    Box("Leaf edge", new Vector3(sign * (.5f * PageWidth + Gutter), -.5f * PageHeight - .002f, .005f + i * .004f), new Vector3(PageWidth, .00045f, .00045f), ink);
            }
            Box("Sewn spine", new Vector3(0, 0, .018f), new Vector3(.023f, PageHeight + .024f, .035f), cover);
            // Tucked between leaves; only the tail projects outside the content surface.
            ribbon = Box("Bookmark ribbon", new Vector3(-.105f, -.035f, .006f), new Vector3(.018f, PageHeight + .11f, .0008f), Own(IllustratedMaterials.Create(IllustratedMaterials.Ribbon))).transform;
            ribbon.gameObject.SetActive(false);
            turnLeaf = new GameObject("Turning leaf").transform;
            turnLeaf.SetParent(transform, false);
            var leaf = Box("Paper", new Vector3(PageWidth * .5f, 0, -.004f), new Vector3(PageWidth, PageHeight, .0007f), Own(IllustratedMaterials.Create(IllustratedMaterials.Paper)));
            leaf.transform.SetParent(turnLeaf, false);
            turnLeaf.gameObject.SetActive(false);
            BuildPencilDetails();
        }

        void BuildPencilDetails()
        {
            var paths = new List<Vector3[]>();
            foreach (float sign in new[] { -1f, 1f })
            {
                float outer = sign * (PageWidth + Gutter + .0125f);
                float inner = sign * (Gutter - .0125f);
                float top = (PageHeight + .027f) * .5f;
                // Closed physical contours around both faces of each cover.
                foreach (float depth in new[] { .0195f, .0285f })
                    paths.Add(new[] { new Vector3(inner,-top,depth), new Vector3(outer,-top,depth), new Vector3(outer,top,depth), new Vector3(inner,top,depth), new Vector3(inner,-top,depth) });
                foreach (float x in new[] { inner, outer }) foreach (float y in new[] { -top, top })
                    paths.Add(new[] { new Vector3(x,y,.0195f), new Vector3(x,y,.0285f) });
                // Layered paper edges and sparse hatching stay outside the page image.
                float paperOuter = sign * (PageWidth + Gutter + .0015f);
                for (int leaf = 0; leaf < 5; leaf++)
                {
                    float depth = .005f + .0034f * leaf;
                    float y = PageHeight * .5f + .0031f;
                    paths.Add(new[] { new Vector3(sign * Gutter,-y,depth), new Vector3(paperOuter,-y,depth+.0002f), new Vector3(paperOuter,y,depth), new Vector3(sign * Gutter,y,depth) });
                }
                for (int mark = 0; mark < 6; mark++)
                {
                    float y = -.17f + mark * .06f;
                    paths.Add(new[] { new Vector3(outer + sign*.0002f,y,.021f), new Vector3(outer + sign*.0002f,y+.01f,.027f) });
                }
            }
            // Stitches are visible along the exposed back of the binding.
            for (int stitch = 0; stitch < 7; stitch++)
            {
                float y = -.18f + stitch * .06f;
                paths.Add(new[] { new Vector3(-.009f,y,.036f), new Vector3(0,y+.003f,.0362f), new Vector3(.009f,y,.036f) });
            }
            var drawing = new GameObject("Cover contours, paper edges and stitches", typeof(PencilMarks));
            drawing.transform.SetParent(transform, false);
            drawing.GetComponent<PencilMarks>().SetPaths(paths, .00048f);
        }

        public void SetSurface(Texture texture)
        {
            foreach (var material in pageMaterials) material.mainTexture = texture;
        }

        public void SetBookmark(bool visible, PageSide side)
        {
            if (!ribbon) return;
            ribbon.gameObject.SetActive(visible);
            var position = ribbon.localPosition;
            position.x = (side == PageSide.Left ? -1 : 1) * .105f;
            ribbon.localPosition = position;
        }

        public void AnimateTurn(int direction)
        {
            if (ReducedMotion || !isActiveAndEnabled || !turnLeaf) return;
            if (turn != null) StopCoroutine(turn);
            turn = StartCoroutine(Turn(direction < 0 ? -1 : 1));
        }

        IEnumerator Turn(int direction)
        {
            turnLeaf.gameObject.SetActive(true);
            const float duration = .42f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                turnLeaf.localRotation = Quaternion.Euler(0, direction > 0 ? 180 * progress : 180 * (1 - progress), 0);
                yield return null;
            }
            turnLeaf.gameObject.SetActive(false);
            turn = null;
        }

        static Mesh CreatePageMesh(PageSide side)
        {
            const int segments = 24;
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[segments * 6];
            float offset = side == PageSide.Left ? -PageWidth - Gutter : Gutter;
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float spineDistance = side == PageSide.Left ? 1 - u : u;
                float depth = .004f * Mathf.Exp(-spineDistance * 12) - .003f * Mathf.Sin(spineDistance * Mathf.PI);
                vertices[i * 2] = new Vector3(offset + u * PageWidth, -.5f * PageHeight, depth);
                vertices[i * 2 + 1] = new Vector3(offset + u * PageWidth, .5f * PageHeight, depth);
                float textureU = (side == PageSide.Left ? 0 : .5f) + u * .5f;
                uvs[i * 2] = new Vector2(textureU, 0);
                uvs[i * 2 + 1] = new Vector2(textureU, 1);
                if (i == segments) continue;
                int v = i * 2, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = side + " curved book page", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        GameObject Box(string label, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = label;
            item.transform.SetParent(transform, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            ArtResources.Release(item.GetComponent<Collider>());
            return item;
        }

        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        void OnDisable()
        {
            if (turn != null) StopCoroutine(turn);
            turn = null;
            if (turnLeaf) turnLeaf.gameObject.SetActive(false);
        }
        void OnDestroy() { foreach (var item in owned) ArtResources.Release(item); }
    }
}
