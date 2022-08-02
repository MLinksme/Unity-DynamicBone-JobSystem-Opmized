using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;

public class ParallelBone : MonoBehaviour
{
    public const int MAX_TRANSFORM_LIMIT = 12;
    public const int MAX_COLLIDER_LIMIT = 10;

    #region
#if UNITY_5_3_OR_NEWER
    [Tooltip("The roots of the transform hierarchy to apply physics.")]
#endif
    public Transform m_Root = null;
    public List<Transform> m_Roots = null;

    public bool m_LayoutParticle = false;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Internal physics simulation rate.")]
#endif
    public float m_UpdateRate = 60.0f;

    public enum UpdateMode
    {
        Normal,
        AnimatePhysics,
        UnscaledTime,
        Default
    }
    public UpdateMode m_UpdateMode = UpdateMode.Default;

#if UNITY_5_3_OR_NEWER
    [Tooltip("How much the bones slowed down.")]
#endif
    [Range(0, 1)]
    public float m_Damping = 0.1f;
    public AnimationCurve m_DampingDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("How much the force applied to return each bone to original orientation.")]
#endif
    [Range(0, 1)]
    public float m_Elasticity = 0.1f;
    public AnimationCurve m_ElasticityDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("How much bone's original orientation are preserved.")]
#endif
    [Range(0, 1)]
    public float m_Stiffness = 0.1f;
    public AnimationCurve m_StiffnessDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("How much character's position change is ignored in physics simulation.")]
#endif
    [Range(0, 1)]
    public float m_Inert = 0;
    public AnimationCurve m_InertDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("How much the bones slowed down when collide.")]
#endif
    public float m_Friction = 0;
    public AnimationCurve m_FrictionDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Each bone can be a sphere to collide with colliders. Radius describe sphere's size.")]
#endif
    public float m_Radius = 0;
    public AnimationCurve m_RadiusDistrib = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("If End Length is not zero, an extra bone is generated at the end of transform hierarchy.")]
#endif
    public float m_EndLength = 0;

#if UNITY_5_3_OR_NEWER
    [Tooltip("If End Offset is not zero, an extra bone is generated at the end of transform hierarchy.")]
#endif
    public Vector3 m_EndOffset = Vector3.zero;

#if UNITY_5_3_OR_NEWER
    [Tooltip("The force apply to bones. Partial force apply to character's initial pose is cancelled out.")]
#endif
    public Vector3 m_Gravity = Vector3.zero;

#if UNITY_5_3_OR_NEWER
    [Tooltip("The force apply to bones.")]
#endif
    public Vector3 m_Force = Vector3.zero;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Control how physics blends with existing animation.")]
#endif
    [Range(0, 1)]
    public float m_BlendWeight = 1.0f;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Collider objects interact with the bones.")]
#endif
    public List<DynamicBoneColliderBase> m_Colliders = null;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Bones exclude from physics simulation.")]
#endif
    public List<Transform> m_Exclusions = null;

    public enum FreezeAxis
    {
        None, X, Y, Z
    }
#if UNITY_5_3_OR_NEWER
    [Tooltip("Constrain bones to move on specified plane.")]
#endif	
    public FreezeAxis m_FreezeAxis = FreezeAxis.None;

#if UNITY_5_3_OR_NEWER
    [Tooltip("Disable physics simulation automatically if character is far from camera or player.")]
#endif
    public bool m_DistantDisable = false;
    public Transform m_ReferenceObject = null;
    public float m_DistanceToObject = 20;

    [HideInInspector]
    public bool m_Multithread = true;

    float m_Time = 0;
    float m_Weight = 1.0f;
    bool m_DistantDisabled = false;
    int m_PreUpdateCount = 0;
    #endregion

    public struct Particle
    {
        #region TransformProperty
        public float3 m_Position;

        public quaternion m_Rotation;
        public quaternion m_LocalRotation;

        public float3 m_ParentScale;

        public int m_TransformHash;
        #endregion
        public int m_Index;
        public int m_ParentIndex;
        public int m_ChildCount;
        public float m_Damping;
        public float m_Elasticity;
        public float m_Stiffness;
        public float m_Inert;
        public float m_Friction;
        public float m_Radius;
        public float m_BoneLength;
        public bool m_isCollide;

        public float3 m_PrevPosition;
        public float3 m_EndOffset;
        public float3 m_InitLocalPosition;
        public quaternion m_InitLocalRotation;

        // prepare data
        public float3 m_TransformPosition;
        public float3 m_TransformLocalPosition;
        public Matrix4x4 m_TransformLocalToWorldMatrix;
    }
    public struct ParticleTree
    {
        public int m_HeadIndex;
        #region Root
        public float3 m_RootParentPosition;
        public quaternion m_RootParentRotation;
        public int m_RootTransformHash;
        #endregion
        public float3 m_LocalGravity;
        public float m_BoneTotalLength;
        public int m_ParticleCount;
        public int m_ParticleOffset;
        // prepare data
        public float3 m_RestGravity;

        public float3 m_BonePosition;
        public float3 m_ObjectMove;
        public float3 m_ObjectPrevPosition;
        public float m_ObjectScale;

        public float m_Weight;
        public float3 m_Force;
        public float3 m_Gravity;
        public float3 m_GravityNormalized;
        public int m_FreezeAxis;

        public int m_EffectiveColliderIndex;
        public int m_EffectiveColliderCount;
    }

    public List<ParticleTree> m_ParticleTrees = new List<ParticleTree>();
    [HideInInspector]
    public List<Transform> m_RootTransforms = new List<Transform>();
    [HideInInspector]
    public List<Transform> m_RootParentTransforms = new List<Transform>();
    public NativeArray<Particle> m_Particles;
    public NativeArray<ParallelBoneCollider> m_ParallelColliders;
    [HideInInspector]
    public Transform[] _ParticleTransforms, _ColliderTransforms;

    public void SetupParticles()
    {
        ClearData();

        if (m_Root)
        {
            AppendParticleTree(m_Root);
        }

        if (m_Roots != null)
        {
            for (int i = 0; i < m_Roots.Count; ++i)
            {
                Transform root = m_Roots[i];
                if (root == null)
                    continue;

                if (m_ParticleTrees.Exists(x => x.m_RootTransformHash == root.GetHashCode()))
                    continue;

                AppendParticleTree(root);
            }
        }

        m_Particles = new NativeArray<Particle>(m_ParticleTrees.Count * MAX_TRANSFORM_LIMIT, Allocator.Persistent);
        _ParticleTransforms = new Transform[m_ParticleTrees.Count * MAX_TRANSFORM_LIMIT];

        for (int i = 0; i < m_ParticleTrees.Count; ++i)
        {
            ParticleTree pt = m_ParticleTrees[i];
            AppendParticles(ref pt, m_RootTransforms[i], -1, 0);
            m_ParticleTrees[i] = pt;
        }

        UpdateParameters();

        for (int i = 0; i < m_ParticleTrees.Count; ++i)
        {
            ParticleTree pt = m_ParticleTrees[i];
            for (int j = pt.m_HeadIndex; j < pt.m_HeadIndex + pt.m_ParticleCount; j++)
            {
                _ParticleTransforms[j].parent = null;
                _ParticleTransforms[j].hideFlags = HideFlags.HideInHierarchy;
            }
        }
    }
    void AppendParticleTree(Transform root)
    {
        if (root == null)
            return;

        var pt = new ParticleTree();
        pt.m_HeadIndex = m_ParticleTrees.Count * MAX_TRANSFORM_LIMIT;

        var rootParentTransform = root.parent;

        pt.m_RootParentPosition = rootParentTransform.position;
        pt.m_RootParentRotation = rootParentTransform.rotation;
        pt.m_RootTransformHash = root.GetHashCode();
        pt.m_ObjectScale = Mathf.Abs(root.lossyScale.x);
        pt.m_Force = m_Force;
        pt.m_Gravity = m_Gravity;
        pt.m_GravityNormalized = m_Gravity.normalized;
        pt.m_Weight = m_Weight;
        pt.m_LocalGravity = root.worldToLocalMatrix.MultiplyVector(m_Gravity).normalized * m_Gravity.magnitude;
        pt.m_FreezeAxis = (int)m_FreezeAxis;

        if (m_Colliders != null)
        {
            pt.m_EffectiveColliderCount = m_Colliders.Count;
        }

        m_ParticleTrees.Add(pt);
        m_RootTransforms.Add(root);
        m_RootParentTransforms.Add(rootParentTransform);
    }
    void AppendParticles(ref ParticleTree pt, Transform b, int parentIndex, float boneLength)
    {
        var p = new Particle();
        p.m_Index = pt.m_ParticleCount;
        p.m_ParentIndex = parentIndex;

        _ParticleTransforms[pt.m_HeadIndex + p.m_Index] = b;

        if (b != null)
        {
            p.m_TransformHash = b.GetHashCode();
            p.m_InitLocalPosition = p.m_TransformLocalPosition = b.localPosition;
            p.m_InitLocalRotation = p.m_LocalRotation = b.localRotation;

            p.m_Position = p.m_PrevPosition = b.position;
            p.m_Rotation = b.rotation;

            p.m_ParentScale = b.parent.lossyScale;
        }
        else 	// end bone
        {
            Transform pb = _ParticleTransforms[pt.m_HeadIndex + parentIndex];
            if (m_EndLength > 0)
            {
                Transform ppb = pb.parent;
                if (ppb != null)
                {
                    p.m_EndOffset = pb.InverseTransformPoint((pb.position * 2 - ppb.position)) * m_EndLength;
                }
                else
                {
                    p.m_EndOffset = new Vector3(m_EndLength, 0, 0);
                }
            }
            else
            {
                p.m_EndOffset = pb.InverseTransformPoint(transform.TransformDirection(m_EndOffset) + pb.position);
            }
            p.m_Position = p.m_PrevPosition = pb.TransformPoint(p.m_EndOffset);
            p.m_InitLocalPosition = Vector3.zero;
            p.m_InitLocalRotation = Quaternion.identity;
        }

        if (parentIndex >= 0)
        {
            boneLength += math.distance(_ParticleTransforms[pt.m_HeadIndex + parentIndex].position, p.m_Position);

            p.m_BoneLength = boneLength;
            pt.m_BoneTotalLength = Mathf.Max(pt.m_BoneTotalLength, boneLength);

            var particleOfParent = m_Particles[pt.m_HeadIndex + parentIndex];
            ++particleOfParent.m_ChildCount;
            m_Particles[pt.m_HeadIndex + parentIndex] = particleOfParent;
        }

        int index = pt.m_ParticleCount;
        m_Particles[pt.m_HeadIndex + p.m_Index] = p;
        pt.m_ParticleCount = pt.m_ParticleCount + 1;

        if (b != null)
        {
            for (int i = 0; i < b.childCount; ++i)
            {
                Transform child = b.GetChild(i);
                bool exclude = false;
                if (m_Exclusions != null)
                {
                    exclude = m_Exclusions.Contains(child);
                }
                if (!exclude)
                {
                    AppendParticles(ref pt, child, index, boneLength);
                }
                else if (m_EndLength > 0 || m_EndOffset != Vector3.zero)
                {
                    AppendParticles(ref pt, null, index, boneLength);
                }
            }

            if (b.childCount == 0 && (m_EndLength > 0 || m_EndOffset != Vector3.zero))
            {
                AppendParticles(ref pt, null, index, boneLength);
            }
        }
    }
    public void ClearData()
    {
        if (m_Particles.IsCreated)
        {
            m_Particles.Dispose();
        }
        if (m_ParallelColliders.IsCreated)
        {
            m_ParallelColliders.Dispose();
        }
        m_ParticleTrees.Clear();
        m_RootTransforms.Clear();
        m_RootParentTransforms.Clear();
        _ParticleTransforms = null;
        _ColliderTransforms = null;
    }
    void UpdateParameters(ParticleTree pt)
    {

        for (int i = pt.m_HeadIndex; i < pt.m_ParticleCount; ++i)
        {
            Particle p = m_Particles[i];
            p.m_Damping = m_Damping;
            p.m_Elasticity = m_Elasticity;
            p.m_Stiffness = m_Stiffness;
            p.m_Inert = m_Inert;
            p.m_Friction = m_Friction;
            p.m_Radius = m_Radius;

            if (pt.m_BoneTotalLength > 0)
            {
                float a = p.m_BoneLength / pt.m_BoneTotalLength;
                if (m_DampingDistrib != null && m_DampingDistrib.keys.Length > 0)
                    p.m_Damping *= m_DampingDistrib.Evaluate(a);
                if (m_ElasticityDistrib != null && m_ElasticityDistrib.keys.Length > 0)
                    p.m_Elasticity *= m_ElasticityDistrib.Evaluate(a);
                if (m_StiffnessDistrib != null && m_StiffnessDistrib.keys.Length > 0)
                    p.m_Stiffness *= m_StiffnessDistrib.Evaluate(a);
                if (m_InertDistrib != null && m_InertDistrib.keys.Length > 0)
                    p.m_Inert *= m_InertDistrib.Evaluate(a);
                if (m_FrictionDistrib != null && m_FrictionDistrib.keys.Length > 0)
                    p.m_Friction *= m_FrictionDistrib.Evaluate(a);
                if (m_RadiusDistrib != null && m_RadiusDistrib.keys.Length > 0)
                    p.m_Radius *= m_RadiusDistrib.Evaluate(a);
            }

            p.m_Damping = Mathf.Clamp01(p.m_Damping);
            p.m_Elasticity = Mathf.Clamp01(p.m_Elasticity);
            p.m_Stiffness = Mathf.Clamp01(p.m_Stiffness);
            p.m_Inert = Mathf.Clamp01(p.m_Inert);
            p.m_Friction = Mathf.Clamp01(p.m_Friction);
            p.m_Radius = Mathf.Max(p.m_Radius, 0);

            m_Particles[i] = p;
        }
    }
    public void UpdateParameters()
    {
        SetWeight(m_BlendWeight);

        for (int i = 0; i < m_ParticleTrees.Count; ++i)
        {
            UpdateParameters(m_ParticleTrees[i]);
        }
    }

    public bool SetupParallelCollider()
    {
        if (m_Colliders != null && m_Colliders.Count > 0)
        {
            m_ParallelColliders = new NativeArray<ParallelBoneCollider>(MAX_COLLIDER_LIMIT, Allocator.Persistent);
            _ColliderTransforms = new Transform[MAX_COLLIDER_LIMIT];
            for (int i = 0; i < m_Colliders.Count; i++)
            {
                _ColliderTransforms[i] = m_Colliders[i].transform;

                var c = new ParallelBoneCollider();
                var component = m_Colliders[i];
                c.m_Direction = (ParallelBoneCollider.Direction)component.m_Direction;
                c.m_Center = component.m_Center;
                c.m_Bound = (ParallelBoneCollider.Bound)component.m_Bound;

                if (component is DynamicBoneCollider dc)
                {
                    c.m_Radius = dc.m_Radius;
                    c.m_Height = dc.m_Height;
                    c.m_Radius2 = dc.m_Radius2;
                }

                c.Scale = Mathf.Abs(component.transform.lossyScale.x);
                c.Validate = true;

                m_ParallelColliders[i] = c;
            }
            return true;
        }
        return false;
    }
    public void SetWeight(float w)
    {

    }

    private void Awake()
    {
        SetupParticles();
    }
    private void OnDestroy()
    {
        ClearData();
    }
    private void OnEnable()
    {
        ParallelBoneManager.Register(this);
    }
    private void OnDisable()
    {
        ParallelBoneManager.UnRegister(this);
    }
}
