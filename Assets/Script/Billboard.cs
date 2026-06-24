using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        Vector3 dir = cam.transform.position - transform.position;
        dir.y = 0; // Y回転だけにする

        transform.rotation = Quaternion.LookRotation(dir);
    }
}