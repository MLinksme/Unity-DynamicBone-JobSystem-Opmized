using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ParallelBoneCopyer
{
    [MenuItem("DynamicBone/ConvertToParallelBone")]
    static void ConvertToParallelBone()
    {
        var relateObject = Selection.activeGameObject;

        var dynamicBone = relateObject.GetComponent<DynamicBone>();

        if (!dynamicBone)
        {
            Debug.Log("No DynamicBone Find");
            return;
        }

        var parallelBone = relateObject.AddComponent<ParallelBone>();

        parallelBone.m_Root = dynamicBone.m_Root;
        parallelBone.m_UpdateRate = dynamicBone.m_UpdateRate;
        parallelBone.m_Damping = dynamicBone.m_Damping;
        parallelBone.m_DampingDistrib = dynamicBone.m_DampingDistrib;
        parallelBone.m_Elasticity = dynamicBone.m_Elasticity;
        parallelBone.m_ElasticityDistrib = dynamicBone.m_ElasticityDistrib;
        parallelBone.m_Stiffness = dynamicBone.m_Stiffness;
        parallelBone.m_StiffnessDistrib = dynamicBone.m_StiffnessDistrib;
        parallelBone.m_Inert = dynamicBone.m_Inert;
        parallelBone.m_InertDistrib = dynamicBone.m_InertDistrib;
        parallelBone.m_Friction = dynamicBone.m_Friction;
        parallelBone.m_FrictionDistrib = dynamicBone.m_FrictionDistrib;
        parallelBone.m_Radius = dynamicBone.m_Radius;
        parallelBone.m_RadiusDistrib = dynamicBone.m_RadiusDistrib;
        parallelBone.m_EndLength = dynamicBone.m_EndLength;
        parallelBone.m_EndOffset = dynamicBone.m_EndOffset;
        parallelBone.m_Gravity = dynamicBone.m_Gravity;
        parallelBone.m_Force = dynamicBone.m_Force;
        parallelBone.m_BlendWeight = dynamicBone.m_BlendWeight;
        parallelBone.m_Colliders = dynamicBone.m_Colliders;
        parallelBone.m_Exclusions = dynamicBone.m_Exclusions;
        parallelBone.m_FreezeAxis = (ParallelBone.FreezeAxis)dynamicBone.m_FreezeAxis;
        parallelBone.m_DistantDisable = dynamicBone.m_DistantDisable;
        parallelBone.m_ReferenceObject = dynamicBone.m_ReferenceObject;
        parallelBone.m_DistanceToObject = dynamicBone.m_DistanceToObject;

        dynamicBone.enabled = false;
    }

    [MenuItem("DynamicBone/SearchDynamicBone")]
    static void SearchDynamicBone()
    {
        var relateObject = Selection.activeGameObject;
        SearchDynamicBone(relateObject.transform);
    }
    static void SearchDynamicBone(Transform t)
    {
        if (t.GetComponent<DynamicBone>())
        {
            Debug.Log(t.name);
        }

        for (int i = 0; i < t.childCount; i++)
        {
            SearchDynamicBone(t.GetChild(i));
        }
    }
}
