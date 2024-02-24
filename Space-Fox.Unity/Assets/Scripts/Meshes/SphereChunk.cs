﻿using System;
using UnityEngine;
using Zenject;

namespace SpaceFox
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class SphereChunk : DisposableMonoBehaviour
    {
        private static readonly Vector3 Center = Vector3.zero;

        private static readonly Vector3[] CubeSideDirections = new[] {
                Vector3.left,
                Vector3.right,
                Vector3.down,
                Vector3.up,
                Vector3.back,
                Vector3.forward};

        [Inject] private readonly UpdateProxy UpdateProxy = default;
        [Inject] private readonly ObservableTransform.Factory ObservableTransformFactory = default;

        private readonly ObservableValue<float> Radius = new();

        private ObservableTransform Observer;
        private ObservableTransform Self;

        private bool IsDirty = false;

        [Range(0.1f, 100f)]
        [SerializeField] private float CurrentRadius = default;

        [SerializeField] private Transform TrackedTransform = default;

        protected override void AwakeBeforeDestroy()
        {
            //TODO Check rotation when calculation quadrant
            //TODO Generate whole sphere when far

            Observer = ObservableTransformFactory.Create(TrackedTransform, UpdateType.Update);
            Self = ObservableTransformFactory.Create(transform, UpdateType.Update);

            Observer.Position.Subscribe(SetDirty).While(this);
            Self.Position.Subscribe(SetDirty).While(this);
            Radius.Subscribe(SetDirty).While(this);

            UpdateProxy.LateUpdate.Subscribe(OnLateUpdate).While(this);
        }

        private void SetDirty()
            => IsDirty = true;

        private void OnLateUpdate()
        {
            Radius.Value = CurrentRadius;

            if (IsDirty)
            {
                IsDirty = false;
                RegenerageMesh();
            }
        }

        private void RegenerageMesh()
        {
            var sphere = GetMesh();

            var mesh = new Mesh();

            mesh.ApplyData(sphere);

            GetComponent<MeshFilter>().mesh = mesh;
        }

        private MeshPolygoned GetMesh()
        {
            var vectorToSurface = Observer.Position.Value - Self.Position.Value;
            var direction = CubeSideDirections.GetMax(d => Vector3.Dot(d, vectorToSurface));
            var rotation = Quaternion.FromToRotation(Vector3.right, direction);
            var localUp = rotation * Vector3.up;
            var localForward = rotation * Vector3.forward;

            var orthant = new Vector3[4]
            {
                direction + localUp + localForward,
                direction - localUp + localForward,
                direction - localUp - localForward,
                direction + localUp - localForward,
            };

            //Will try to find barycentric coordinates of vectorToSurface in this orthant
            var topNormal = Vector3.Cross(orthant[0], orthant[3]);
            var bottomNormal = Vector3.Cross(orthant[1], orthant[2]);
            var y = DecomppositeByPlanes(vectorToSurface, topNormal, bottomNormal);

            var forwardNormal = Vector3.Cross(orthant[0], orthant[1]);
            var backNormal = Vector3.Cross(orthant[2], orthant[3]);
            var x = DecomppositeByPlanes(vectorToSurface, backNormal, forwardNormal);

            for (var i = 0; i < orthant.Length; i++)
                orthant[i] = GetLocalVertexPosition(orthant[i]);

            var mesh = MeshPolygoned.GetPolygon(orthant);

            var distance = vectorToSurface.magnitude - Radius.Value;

            //for (var i = 0; i < RecursiveDepth.Value; i++)
            //    mesh.Subdivide(GetLocalVertexPosition);

            return mesh;
        }

        private float DecomppositeByPlanes(Vector3 vector, Vector3 normal0, Vector3 normal1)
        {
            //Slice is the plane, that is perpendicular to two basis planes
            var sliceNormal = Vector3.Cross(normal0, normal1).normalized;

            //Directions of the intersections of planes with slice
            var direction0 = Vector3.Cross(normal0, sliceNormal);
            var direction1 = Vector3.Cross(normal1, sliceNormal);
            var vectorProjectionToSlice = vector - sliceNormal * Vector3.Dot(sliceNormal, vector);
            var (a, b) = DecompositeByBasis(vectorProjectionToSlice, direction0, direction1);
            return a / (a + b);
        }

        private (float X, float Y) DecompositeByBasis(Vector3 vector, Vector3 base0, Vector3 base1)
        {
            //Using Gaussian elimination method
            var ab = Vector3.Dot(base0, base1);
            var a2 = Vector3.Dot(base0, base0);
            var b2 = Vector3.Dot(base1, base1);
            var va = Vector3.Dot(vector, base0);
            var vb = Vector3.Dot(vector, base1);
            var y = (vb - va * ab / a2) / (b2 - ab * ab / a2);
            var x = (va - y * ab) / a2;
            return (x, y);
        }

        private Vector3 GetLocalVertexPosition(Vector3 position)
            => Center + Radius.Value * (position - Center).normalized;
    }
}
