using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public struct ParallelBoneCollider
{
    public enum Direction
    {
        X, Y, Z
    }

    public Direction m_Direction;
    public float3 m_Center;
    public enum Bound
    {
        Outside,
        Inside
    }
    public Bound m_Bound;

    public float m_Radius;
    public float m_Height;
    public float m_Radius2;

    public bool Validate;
    public float Scale;

    // prepare data
    float m_ScaledRadius;
    float m_ScaledRadius2;
    float3 m_C0;
    float3 m_C1;
    float m_C01Distance;
    int m_CollideType;

    public bool Collide(ref float3 particlePosition, float particleRadius)
    {
        switch (m_CollideType)
        {
            case 0:
                return OutsideSphere(ref particlePosition, particleRadius, m_C0, m_ScaledRadius);
            case 1:
                return InsideSphere(ref particlePosition, particleRadius, m_C0, m_ScaledRadius);
            case 2:
                return OutsideCapsule(ref particlePosition, particleRadius, m_C0, m_C1, m_ScaledRadius, m_C01Distance);
            case 3:
                return InsideCapsule(ref particlePosition, particleRadius, m_C0, m_C1, m_ScaledRadius, m_C01Distance);
            case 4:
                return OutsideCapsule2(ref particlePosition, particleRadius, m_C0, m_C1, m_ScaledRadius, m_ScaledRadius2, m_C01Distance);
            case 5:
                return InsideCapsule2(ref particlePosition, particleRadius, m_C0, m_C1, m_ScaledRadius, m_ScaledRadius2, m_C01Distance);
        }
        return false;
    }

    public void Prepare(Matrix4x4 local2World)
    {
        float halfHeight = m_Height * 0.5f;

        if (m_Radius2 <= 0 || Mathf.Abs(m_Radius - m_Radius2) < 0.01f)
        {
            m_ScaledRadius = m_Radius * Scale;

            float h = halfHeight - m_Radius;
            if (h <= 0)
            {
                m_C0 = local2World.MultiplyPoint3x4(m_Center);

                if (m_Bound == Bound.Outside)
                {
                    m_CollideType = 0;
                }
                else
                {
                    m_CollideType = 1;
                }
            }
            else
            {
                Vector3 c0 = m_Center;
                Vector3 c1 = m_Center;

                switch (m_Direction)
                {
                    case Direction.X:
                        c0.x += h;
                        c1.x -= h;
                        break;
                    case Direction.Y:
                        c0.y += h;
                        c1.y -= h;
                        break;
                    case Direction.Z:
                        c0.z += h;
                        c1.z -= h;
                        break;
                }

                m_C0 = local2World.MultiplyPoint3x4(c0);
                m_C1 = local2World.MultiplyPoint3x4(c1);
                m_C01Distance = math.distance(m_C1, m_C0);

                if (m_Bound == Bound.Outside)
                {
                    m_CollideType = 2;
                }
                else
                {
                    m_CollideType = 3;
                }
            }
        }
        else
        {
            float r = Mathf.Max(m_Radius, m_Radius2);
            if (halfHeight - r <= 0)
            {
                m_ScaledRadius = r * Scale;
                m_C0 = local2World.MultiplyPoint3x4(m_Center);

                if (m_Bound == Bound.Outside)
                {
                    m_CollideType = 0;
                }
                else
                {
                    m_CollideType = 1;
                }
            }
            else
            {
                m_ScaledRadius = m_Radius * Scale;
                m_ScaledRadius2 = m_Radius2 * Scale;

                float h0 = halfHeight - m_Radius;
                float h1 = halfHeight - m_Radius2;
                Vector3 c0 = m_Center;
                Vector3 c1 = m_Center;

                switch (m_Direction)
                {
                    case Direction.X:
                        c0.x += h0;
                        c1.x -= h1;
                        break;
                    case Direction.Y:
                        c0.y += h0;
                        c1.y -= h1;
                        break;
                    case Direction.Z:
                        c0.z += h0;
                        c1.z -= h1;
                        break;
                }

                m_C0 = local2World.MultiplyPoint3x4(c0);
                m_C1 = local2World.MultiplyPoint3x4(c1);
                m_C01Distance = math.distance(m_C1, m_C0);

                if (m_Bound == Bound.Outside)
                {
                    m_CollideType = 4;
                }
                else
                {
                    m_CollideType = 5;
                }
            }
        }
    }

    static bool OutsideSphere(ref float3 particlePosition, float particleRadius, float3 sphereCenter, float sphereRadius)
    {
        float r = sphereRadius + particleRadius;
        float r2 = r * r;
        float3 d = particlePosition - sphereCenter;
        float dlen2 = math.lengthsq(d);

        // if is inside sphere, project onto sphere surface
        if (dlen2 > 0 && dlen2 < r2)
        {
            float dlen = math.sqrt(dlen2);
            particlePosition = sphereCenter + d * (r / dlen);
            return true;
        }
        return false;
    }
    static bool InsideSphere(ref float3 particlePosition, float particleRadius, float3 sphereCenter, float sphereRadius)
    {
        float r = sphereRadius - particleRadius;
        float r2 = r * r;
        float3 d = particlePosition - sphereCenter;
        float dlen2 = math.lengthsq(d);

        // if is outside sphere, project onto sphere surface
        if (dlen2 > r2)
        {
            float dlen = math.sqrt(dlen2);
            particlePosition = sphereCenter + d * (r / dlen);
            return true;
        }
        return false;
    }
    static bool OutsideCapsule(ref float3 particlePosition, float particleRadius, float3 capsuleP0, float3 capsuleP1, float capsuleRadius, float dirlen)
    {
        float r = capsuleRadius + particleRadius;
        float r2 = r * r;
        float3 dir = capsuleP1 - capsuleP0;
        float3 d = particlePosition - capsuleP0;
        float t = math.dot(d, dir);

        if (t <= 0)
        {
            // check sphere1
            float dlen2 = math.lengthsq(d);
            if (dlen2 > 0 && dlen2 < r2)
            {
                float dlen = math.sqrt(dlen2);
                particlePosition = capsuleP0 + d * (r / dlen);
                return true;
            }
        }
        else
        {
            float dirlen2 = dirlen * dirlen;
            if (t >= dirlen2)
            {
                // check sphere2
                d = particlePosition - capsuleP1;
                float dlen2 = math.lengthsq(d);
                if (dlen2 > 0 && dlen2 < r2)
                {
                    float dlen = math.sqrt(dlen2);
                    particlePosition = capsuleP1 + d * (r / dlen);
                    return true;
                }
            }
            else
            {
                // check cylinder
                float3 q = d - dir * (t / dirlen2);
                float qlen2 = math.lengthsq(q);
                if (qlen2 > 0 && qlen2 < r2)
                {
                    float qlen = math.sqrt(qlen2);
                    particlePosition += q * ((r - qlen) / qlen);
                    return true;
                }
            }
        }
        return false;
    }
    static bool InsideCapsule(ref float3 particlePosition, float particleRadius, float3 capsuleP0, float3 capsuleP1, float capsuleRadius, float dirlen)
    {
        float r = capsuleRadius - particleRadius;
        float r2 = r * r;
        float3 dir = capsuleP1 - capsuleP0;
        float3 d = particlePosition - capsuleP0;
        float t = Vector3.Dot(d, dir);

        if (t <= 0)
        {
            // check sphere1
            float dlen2 = math.lengthsq(d);
            if (dlen2 > r2)
            {
                float dlen = math.sqrt(dlen2);
                particlePosition = capsuleP0 + d * (r / dlen);
                return true;
            }
        }
        else
        {
            float dirlen2 = dirlen * dirlen;
            if (t >= dirlen2)
            {
                // check sphere2
                d = particlePosition - capsuleP1;
                float dlen2 = math.lengthsq(d);
                if (dlen2 > r2)
                {
                    float dlen = Mathf.Sqrt(dlen2);
                    particlePosition = capsuleP1 + d * (r / dlen);
                    return true;
                }
            }
            else
            {
                // check cylinder
                float3 q = d - dir * (t / dirlen2);
                float qlen2 = math.lengthsq(q);
                if (qlen2 > r2)
                {
                    float qlen = math.sqrt(qlen2);
                    particlePosition += q * ((r - qlen) / qlen);
                    return true;
                }
            }
        }
        return false;
    }
    static bool OutsideCapsule2(ref float3 particlePosition, float particleRadius, float3 capsuleP0, float3 capsuleP1, float capsuleRadius0, float capsuleRadius1, float dirlen)
    {
        float3 dir = capsuleP1 - capsuleP0;
        float3 d = particlePosition - capsuleP0;
        float t = math.dot(d, dir);

        if (t <= 0)
        {
            // check sphere1
            float r = capsuleRadius0 + particleRadius;
            float r2 = r * r;
            float dlen2 = math.lengthsq(d);
            if (dlen2 > 0 && dlen2 < r2)
            {
                float dlen = math.sqrt(dlen2);
                particlePosition = capsuleP0 + d * (r / dlen);
                return true;
            }
        }
        else
        {
            float dirlen2 = dirlen * dirlen;
            if (t >= dirlen2)
            {
                // check sphere2
                float r = capsuleRadius1 + particleRadius;
                float r2 = r * r;
                d = particlePosition - capsuleP1;
                float dlen2 = math.lengthsq(d);
                if (dlen2 > 0 && dlen2 < r2)
                {
                    float dlen = math.sqrt(dlen2);
                    particlePosition = capsuleP1 + d * (r / dlen);
                    return true;
                }
            }
            else
            {
                // check cylinder
                float3 q = d - dir * (t / dirlen2);
                float qlen2 = math.lengthsq(q);

                float klen = math.dot(d, dir / dirlen);
                float r = Mathf.Lerp(capsuleRadius0, capsuleRadius1, klen / dirlen) + particleRadius;
                float r2 = r * r;

                if (qlen2 > 0 && qlen2 < r2)
                {
                    float qlen = math.sqrt(qlen2);
                    particlePosition += q * ((r - qlen) / qlen);
                    return true;
                }
            }
        }
        return false;
    }
    static bool InsideCapsule2(ref float3 particlePosition, float particleRadius, float3 capsuleP0, float3 capsuleP1, float capsuleRadius0, float capsuleRadius1, float dirlen)
    {
        float3 dir = capsuleP1 - capsuleP0;
        float3 d = particlePosition - capsuleP0;
        float t = math.dot(d, dir);

        if (t <= 0)
        {
            // check sphere1
            float r = capsuleRadius0 - particleRadius;
            float r2 = r * r;
            float dlen2 = math.lengthsq(d);
            if (dlen2 > r2)
            {
                float dlen = math.sqrt(dlen2);
                particlePosition = capsuleP0 + d * (r / dlen);
                return true;
            }
        }
        else
        {
            float dirlen2 = dirlen * dirlen;
            if (t >= dirlen2)
            {
                // check sphere2
                float r = capsuleRadius1 - particleRadius;
                float r2 = r * r;
                d = particlePosition - capsuleP1;
                float dlen2 = math.lengthsq(d);
                if (dlen2 > r2)
                {
                    float dlen = math.sqrt(dlen2);
                    particlePosition = capsuleP1 + d * (r / dlen);
                    return true;
                }
            }
            else
            {
                // check cylinder
                float3 q = d - dir * (t / dirlen2);
                float qlen2 = math.lengthsq(q);

                float klen = math.dot(d, dir / dirlen);
                float r = Mathf.Lerp(capsuleRadius0, capsuleRadius1, klen / dirlen) - particleRadius;
                float r2 = r * r;

                if (qlen2 > r2)
                {
                    float qlen = math.sqrt(qlen2);
                    particlePosition += q * ((r - qlen) / qlen);
                    return true;
                }
            }
        }
        return false;
    }
}
