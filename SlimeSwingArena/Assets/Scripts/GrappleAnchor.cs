using UnityEngine;

public class GrappleAnchor : MonoBehaviour
{
    private Transform ring;

    private void Awake()
    {
        if (transform.childCount > 1)
            ring = transform.GetChild(1);
    }

    private void Update()
    {
        if (ring == null)
            return;
        float pulse = 1f + Mathf.Sin(Time.time * 3.2f + transform.position.x) * 0.07f;
        ring.localScale = new Vector3(0.65f * pulse, 0.65f * pulse, 1f);
    }
}
