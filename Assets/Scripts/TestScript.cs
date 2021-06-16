using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    public DrawProceduralTargetDB _TargetDB;
    public Mesh[] _TargetMeshes;
    public float _SpawnVolume = 5f;

    private void OnGUI()
    {
        if (GUI.Button(new Rect(10f, 10f, 100f, 50f), "Spawn"))
        {
            if (_TargetMeshes == null || _TargetMeshes.Length <= 0)
            {
                Debug.LogWarning("[TestScript] no TargetMeshes");
                return;
            }

            var position = new Vector3(Random.Range(-_SpawnVolume, _SpawnVolume), Random.Range(-_SpawnVolume, _SpawnVolume), Random.Range(-_SpawnVolume, _SpawnVolume));
            var rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));

            _TargetDB.AddInput(new DrawProceduralTargetDB.InputData
            {
                _Mesh = _TargetMeshes[Random.Range(0, _TargetMeshes.Length)],
                _LocalToWorldMatrix = Matrix4x4.TRS(position, rotation, Vector3.one),
            }); 
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * _SpawnVolume);
    }
}
