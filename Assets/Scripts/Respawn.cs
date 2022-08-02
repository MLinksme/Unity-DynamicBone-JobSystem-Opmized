/**
 * @file            
 * @author          
 * @copyright       
 * @created         2020-02-13 14:46:20
 * @updated         2020-02-13 14:46:20
 *
 * @brief           
 */
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class Respawn : MonoBehaviour
{
    [MenuItem("SpawnParallelPlayer/Start")]

    static void Spawn()
    {
        var target = GameObject.FindObjectOfType<Respawn>();
        target.CreateTarget();
    }


    public int Count;
    public int Cur;
    public float IntervalDis;
    public GameObject Target;

    // Start is called before the first frame update
    void Start()
    {
        float3 dual = Vector3.zero;
    }

    void CreateTarget()
    {
        int perLineCount = (int)Mathf.Sqrt(Count);

        for(int i = 0; i < Count; i++)
        {
            Cur++;

            float curX = i % perLineCount;
            float curZ = i / perLineCount;

            GameObject go = GameObject.Instantiate(Target);
            go.transform.parent = this.transform;
            go.transform.localPosition = new Vector3(curX * IntervalDis, 0, curZ * IntervalDis);
            go.SetActive(true);
        }
    }
}
