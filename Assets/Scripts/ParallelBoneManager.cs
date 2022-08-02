using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public class ParallelBoneManager : MonoBehaviour
{
    [SerializeField] int m_capacity = 64;
    [SerializeField] int m_updateRate = 60;
    public static ParallelBoneManager Instance => _instance;

    static ParallelBoneManager _instance;

    private static Queue<ParallelBone> _loadingQueue = new Queue<ParallelBone>();
    private static Queue<ParallelBone> _removeQueue = new Queue<ParallelBone>();

    private List<ParallelBone> _boneList = new List<ParallelBone>();

    private NativeList<ParallelBone.Particle> _particles;
    private NativeList<ParallelBone.ParticleTree> _particleTrees;
    private NativeList<ParallelBoneCollider> _colliders;
    private Stack<int> _collAppendPositions = new Stack<int>();

    private int _particleTreeCount = 0;
    private int _colliderGroupCount = 0;

    private TransformAccessArray _rootParentTransforms;
    private TransformAccessArray _particleTransforms;
    private TransformAccessArray _colliderTransforms;

    private JobHandle _lastJobHandle;
    public void Init()
    {
        _particles = new NativeList<ParallelBone.Particle>(Allocator.Persistent);
        _particleTrees = new NativeList<ParallelBone.ParticleTree>(Allocator.Persistent);
        _colliders = new NativeList<ParallelBoneCollider>(Allocator.Persistent);

        _rootParentTransforms = new TransformAccessArray(m_capacity, 64);
        _particleTransforms = new TransformAccessArray(m_capacity * ParallelBone.MAX_TRANSFORM_LIMIT, 64);
        _colliderTransforms = new TransformAccessArray(m_capacity * ParallelBone.MAX_COLLIDER_LIMIT, 64);
    }
    void UpdateQueue()
    {
        UpdateIncomeQueue();
        UpdateOutcomeQueue();
    }
    private void UpdateIncomeQueue()
    {
        while (_loadingQueue.Count > 0)
        {

            var target = _loadingQueue.Dequeue();
            if (!_boneList.Contains(target))
            {
                _boneList.Add(target);

                for (int i = 0; i < target.m_ParticleTrees.Count; i++)
                {
                    var pt = target.m_ParticleTrees[i];
                    pt.m_HeadIndex = _particleTreeCount * ParallelBone.MAX_TRANSFORM_LIMIT;

                    _rootParentTransforms.Add(target.m_RootParentTransforms[i]);

                    for (int j = 0; j < ParallelBone.MAX_TRANSFORM_LIMIT; j++)
                    {
                        _particleTransforms.Add(target._ParticleTransforms[i * ParallelBone.MAX_TRANSFORM_LIMIT + j]);
                    }

                    if (target.SetupParallelCollider())
                    {
                        if (_collAppendPositions.Count > 0)//Overwrite or Append at last?
                        {
                            int collIndex = _collAppendPositions.Pop();

                            for (int j = 0; j < ParallelBone.MAX_COLLIDER_LIMIT; j++)
                            {
                                _colliderTransforms[collIndex + j] = target._ColliderTransforms[j];
                                _colliders[collIndex + j] = target.m_ParallelColliders[j];
                            }
                            pt.m_EffectiveColliderIndex = collIndex;
                        }
                        else
                        {
                            for (int j = 0; j < ParallelBone.MAX_COLLIDER_LIMIT; j++)
                            {
                                _colliderTransforms.Add(target._ColliderTransforms[j]);
                                _colliders.Add(target.m_ParallelColliders[j]);
                            }
                            pt.m_EffectiveColliderIndex = _colliderGroupCount * ParallelBone.MAX_COLLIDER_LIMIT;
                            _colliderGroupCount++; //Append
                        }  
                    }
                    else
                    {
                        pt.m_EffectiveColliderIndex = -1;
                    }

                    target.m_ParticleTrees[i] = pt; //Apply

                    _particleTrees.Add(pt);
                    _particleTreeCount++;
                }
                _particles.AddRange(target.m_Particles);
            }
        }
    }

    private void UpdateOutcomeQueue()
    {
        while (_removeQueue.Count > 0)
        {
            var target = _removeQueue.Dequeue();
            int boneIndex = _boneList.IndexOf(target);
            if (boneIndex >= 0)
            {
                for (int i = 0; i < target.m_ParticleTrees.Count; i++)
                {
                    var pt = target.m_ParticleTrees[i];
                    int ptIndex = pt.m_HeadIndex / ParallelBone.MAX_TRANSFORM_LIMIT;
                    bool isEnd = ptIndex == _particleTrees.Length - 1;
                    if (isEnd)
                    {
                        _particleTrees.RemoveAt(ptIndex);
                        _rootParentTransforms.RemoveAtSwapBack(ptIndex);

                        for (int j = pt.m_HeadIndex + ParallelBone.MAX_TRANSFORM_LIMIT - 1; j >= pt.m_HeadIndex; j--)
                        {
                            _particles.RemoveAt(j);
                            _particleTransforms.RemoveAtSwapBack(j);
                        }

                    }
                    else
                    {
                        _particleTrees.RemoveAtSwapBack(ptIndex);
                        var newPt = _particleTrees[ptIndex];

                        for (int j = pt.m_HeadIndex + ParallelBone.MAX_TRANSFORM_LIMIT - 1; j >= pt.m_HeadIndex; j--)
                        {
                            _particles.RemoveAtSwapBack(j);
                            _particleTransforms.RemoveAtSwapBack(j);
                        }
                        newPt.m_HeadIndex = pt.m_HeadIndex;

                        _particleTrees[ptIndex] = newPt;

                    }

                    if (pt.m_EffectiveColliderIndex >= 0)
                    {
                        for (int j = pt.m_EffectiveColliderIndex + ParallelBone.MAX_COLLIDER_LIMIT - 1; j >= pt.m_EffectiveColliderIndex; j--)
                        {
                            var c = _colliders[j];
                            c.Validate = false;
                            _colliders[j] = c;
                        }
                        _collAppendPositions.Push(pt.m_EffectiveColliderIndex);//set overwrite position
                    }
                    _particleTreeCount--;
                    _boneList.RemoveAt(boneIndex);
                }
            }
        }
    }
    public static void Register(ParallelBone target)
    {
        _loadingQueue.Enqueue(target);
    }
    public static void UnRegister(ParallelBone target)
    {
        _removeQueue.Enqueue(target);
    }
    public void Dispose()
    {
        _lastJobHandle.Complete();

        _loadingQueue.Clear();
        _boneList.Clear();

        if (_particles.IsCreated) _particles.Dispose();
        if (_particleTrees.IsCreated) _particleTrees.Dispose();
        if (_colliders.IsCreated) _colliders.Dispose();
        if (_rootParentTransforms.isCreated) _rootParentTransforms.Dispose();
        if (_particleTransforms.isCreated) _particleTransforms.Dispose();
        if (_colliderTransforms.isCreated) _colliderTransforms.Dispose();

    }
    private void OnDestroy()
    {
        Dispose();
    }
    private void LateUpdate()
    {
        if (!_lastJobHandle.IsCompleted) return;

        _lastJobHandle.Complete();

        UpdateQueue();

        if (_particleTreeCount <= 0) return;

        var rootJob = new ApplyRootPositionJob
        {
            ParticleTrees = _particleTrees,
            Particles = _particles
        };
        var rootHandle = rootJob.Schedule(_rootParentTransforms);

        var prepareJob = new PrepareJob
        {
            ParticleTrees = _particleTrees,
            Particles = _particles,
            ParticleTreeCount = _particleTreeCount
        };
        var prepareHandle = prepareJob.Schedule(rootHandle);

        float dt = Time.deltaTime;
        var update1Job = new UpdateParticles1Job
        {
            ParticleTrees = _particleTrees,
            Particles = _particles,
            ParticleTreeCount = _particleTreeCount,
            TimeVar = dt * m_updateRate,
            LoopIndex = 0
        };
        var update1Handle = update1Job.Schedule(_particleTreeCount, 1, prepareHandle);

        var prepareCollJob = new PrepareCollidersJob
        {
            Colliders = _colliders
        };
        var prepareCollHandle = prepareCollJob.Schedule(_colliderTransforms, update1Handle);

        var update2Job = new UpdateParticle2Job
        {
            ParticleTrees = _particleTrees,
            Particles = _particles,
            ParticleTreeCount = _particleTreeCount,
            TimeVar = dt * m_updateRate,
            Colliders = _colliders,
            MovePlane = new Plane()
        };
        var update2Handle = update2Job.Schedule(_particleTreeCount, 1, prepareCollHandle);

        var transJob = new ApplyParticlesToTransformsJob
        {
            ParticleTrees = _particleTrees,
            Particles = _particles,
            ParticleTreeCount = _particleTreeCount
        };
        var transHandle = transJob.Schedule(_particleTreeCount, 1, update2Handle);

        var finalJob = new FinalJob
        {
            Particles = _particles
        };
        var finalHandle = finalJob.Schedule(_particleTransforms, transHandle);

        _lastJobHandle = finalHandle;

        JobHandle.ScheduleBatchedJobs();

    }
    private void Awake()
    {
        if (_instance)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        Init();
    }


    [BurstCompile]
    struct ApplyRootPositionJob : IJobParallelForTransform
    {
        public NativeArray<ParallelBone.ParticleTree> ParticleTrees;
        [ReadOnly]
        public NativeArray<ParallelBone.Particle> Particles;
        public void Execute(int index, TransformAccess transform)
        {
            var pt = ParticleTrees[index];
            pt.m_RootParentPosition = transform.position;
            pt.m_RootParentRotation = transform.rotation;

            var root = Particles[pt.m_HeadIndex];
            pt.m_BonePosition = root.m_Position;

            pt.m_ObjectMove = pt.m_BonePosition - pt.m_ObjectPrevPosition;
            pt.m_ObjectPrevPosition = pt.m_BonePosition;
            pt.m_RestGravity = math.mul(root.m_Rotation, pt.m_LocalGravity);

            ParticleTrees[index] = pt;
        }
    }
    [BurstCompile]
    struct PrepareCollidersJob : IJobParallelForTransform
    {
        public NativeArray<ParallelBoneCollider> Colliders;
        public void Execute(int index, TransformAccess transform)
        {
            var c = Colliders[index];
            if (c.Validate)
            {
                c.Prepare(transform.localToWorldMatrix);
                Colliders[index] = c;
            }
        }
    }
    [BurstCompile]
    struct PrepareJob : IJob
    {
        [ReadOnly]
        public NativeArray<ParallelBone.ParticleTree> ParticleTrees;
        public NativeArray<ParallelBone.Particle> Particles;
        public int ParticleTreeCount;
        public void Execute()
        {
            for (int i = 0; i < ParticleTreeCount; i++)
            {
                var pt = ParticleTrees[i];

                for (int j = pt.m_HeadIndex; j < pt.m_HeadIndex + pt.m_ParticleCount; j++)
                {
                    var p = Particles[j];

                    if (j > pt.m_HeadIndex)
                    {
                        var p0 = Particles[pt.m_HeadIndex + p.m_ParentIndex];

                        p.m_TransformPosition = p0.m_TransformPosition + math.mul(p0.m_Rotation, p.m_TransformLocalPosition * p.m_ParentScale);
                        p.m_Rotation = math.mul(p0.m_Rotation, p.m_LocalRotation);
                    }
                    else
                    {
                        p.m_TransformPosition = pt.m_RootParentPosition + math.mul(pt.m_RootParentRotation, p.m_TransformLocalPosition * p.m_ParentScale);
                        p.m_Rotation = math.mul(pt.m_RootParentRotation, p.m_LocalRotation);
                    }

                    Particles[j] = p;
                }
            }
        }
    }
    [BurstCompile]
    struct UpdateParticles1Job : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<ParallelBone.ParticleTree> ParticleTrees;
        [NativeDisableParallelForRestriction]
        public NativeArray<ParallelBone.Particle> Particles;
        public int ParticleTreeCount;
        public float TimeVar;
        public int LoopIndex;
        public void Execute(int index)
        {
            var pt = ParticleTrees[index];
            var force = pt.m_Gravity;
            var fdir = pt.m_GravityNormalized;
            float3 pf = fdir * math.max(math.dot(pt.m_RestGravity, fdir), 0);
            force -= pf;
            force = (force + pt.m_Force) * (pt.m_ObjectScale * TimeVar);

            var objectMove = LoopIndex == 0 ? pt.m_ObjectMove : float3.zero;

            for (int i = pt.m_HeadIndex; i < pt.m_HeadIndex + pt.m_ParticleCount; i++)
            {
                var p = Particles[i];
                if (p.m_ParentIndex >= 0)
                {
                    // verlet integration
                    var v = p.m_Position - p.m_PrevPosition;
                    var rmove = objectMove * p.m_Inert;
                    p.m_PrevPosition = p.m_Position + rmove;
                    float damping = p.m_Damping;
                    if (p.m_isCollide)
                    {
                        damping += p.m_Friction;
                        if (damping > 1)
                        {
                            damping = 1;
                        }
                        p.m_isCollide = false;
                    }
                    p.m_Position += v * (1 - damping) + force + rmove;
                }
                else
                {
                    p.m_PrevPosition = p.m_Position;
                    p.m_Position = p.m_TransformPosition;
                }
                Particles[i] = p;
            }
        }
    }
    [BurstCompile]
    struct UpdateParticle2Job : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<ParallelBone.ParticleTree> ParticleTrees;
        [NativeDisableParallelForRestriction]
        public NativeArray<ParallelBone.Particle> Particles;
        [ReadOnly]
        public NativeArray<ParallelBoneCollider> Colliders;
        public int ParticleTreeCount;
        public float TimeVar;

        public Plane MovePlane;
        public void Execute(int index)
        {
            var pt = ParticleTrees[index];

            for (int i = pt.m_HeadIndex + 1; i < pt.m_HeadIndex + pt.m_ParticleCount; ++i)
            {
                var p = Particles[i];
                var p0 = Particles[pt.m_HeadIndex + p.m_ParentIndex];

                float restLen = math.distance(p0.m_TransformPosition, p.m_TransformPosition);
                // keep shape
                float stiffness = Mathf.Lerp(1.0f, p.m_Stiffness, pt.m_Weight);
                if (stiffness > 0 || p.m_Elasticity > 0)
                {
                    float4x4 m0 = float4x4.TRS(p0.m_TransformPosition, p0.m_Rotation, p0.m_ParentScale);
                    m0.c3 = new float4(p.m_Position.xyz, 0);
                    float3 restPos = 
                        p.m_TransformHash != 0 
                        ? math.mul(m0, new float4(p.m_TransformLocalPosition.xyz, 1)).xyz
                        : math.mul(m0, new float4(p.m_EndOffset, 1)).xyz;
                    float3 d = restPos - p.m_Position;
                    p.m_Position += d * (p.m_Elasticity * TimeVar);
                    if (stiffness > 0)
                    {
                        d = restPos - p.m_Position;
                        float len = math.length(d);
                        float maxlen = restLen * (1 - stiffness) * 2;
                        if (len > maxlen)
                        {
                            p.m_Position += d * ((len - maxlen) / len);
                        }
                    }
                }

                // collide
                if (pt.m_EffectiveColliderIndex >= 0 && pt.m_EffectiveColliderCount > 0)
                {
                    float particleRadius = p.m_Radius * pt.m_ObjectScale;

                    for (int j = pt.m_EffectiveColliderIndex; j < pt.m_EffectiveColliderIndex + pt.m_EffectiveColliderCount; ++j)
                    {
                        var c = Colliders[j];
                        p.m_isCollide |= c.Collide(ref p.m_Position, particleRadius);
                    }
                }

                // freeze axis, project to plane 
                if (pt.m_FreezeAxis > 0)
                {
                    Matrix4x4 m0 = float4x4.TRS(p0.m_TransformPosition, p0.m_Rotation, p0.m_ParentScale);
                    var planeNormal = math.normalize(m0.GetColumn(pt.m_FreezeAxis - 1)).xyz;
                    MovePlane.SetNormalAndPosition(planeNormal, p0.m_Position);
                    float3 posMove = MovePlane.normal * MovePlane.GetDistanceToPoint(p.m_Position);
                    p.m_Position -= posMove;
                }

                // keep length
                float3 dd = p0.m_Position - p.m_Position;
                float leng = math.length(dd);
                if (leng > 0)
                {
                    p.m_Position += dd * ((leng - restLen) / leng);
                }

                Particles[i] = p;
            }
        }
    }
    [BurstCompile]
    struct ApplyParticlesToTransformsJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<ParallelBone.ParticleTree> ParticleTrees;
        [NativeDisableParallelForRestriction]
        public NativeArray<ParallelBone.Particle> Particles;
        public int ParticleTreeCount;
        public void Execute(int index)
        {
            var pt = ParticleTrees[index];

            for (int i = pt.m_HeadIndex + 1; i < pt.m_HeadIndex + pt.m_ParticleCount; i++)
            {
                var p = Particles[i];
                var p0 = Particles[pt.m_HeadIndex + p.m_ParentIndex];

                if (p0.m_ChildCount <= 1)		// do not modify bone orientation if has more then one child
                {
                    float3 localPos = p.m_TransformHash != 0 ? p.m_TransformLocalPosition : p.m_EndOffset;
                    float3 v0 = math.mul(float4x4.TRS(p0.m_TransformPosition, p0.m_Rotation, p0.m_ParentScale), new float4(localPos, 0)).xyz;
                    float3 v1 = p.m_Position - p0.m_Position;

                    quaternion rot = Quaternion.FromToRotation(v0, v1);
                    p0.m_Rotation = math.mul(rot, p0.m_Rotation);

                    Particles[pt.m_HeadIndex + p.m_ParentIndex] = p0;
                }
            }
        }
    }
    [BurstCompile]
    struct FinalJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<ParallelBone.Particle> Particles;
        public void Execute(int index, TransformAccess transform)
        {
            transform.position = Particles[index].m_Position;
            transform.rotation = Particles[index].m_Rotation;
        }
    }
}
