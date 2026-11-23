using UnityEngine;

public class TurretEnemy : MonoBehaviour
{
    private GameBootstrap game;
    private Transform barrelPivot;
    private Transform target;
    private float fireCooldown;
    private Vector3 homePosition;
    private bool destroyed;

    public void Configure(GameBootstrap owner, Transform pivot)
    {
        game = owner;
        barrelPivot = pivot;
        target = game.PlayerTransform;
        homePosition = transform.localPosition;
        fireCooldown = Random.Range(1f, 2f);
    }

    private void Update()
    {
        if (game == null || !game.IsPlaying)
            return;
        Vector3 bobbingPosition = homePosition;
        bobbingPosition.y += Mathf.Sin(Time.time * 8f + transform.position.x) * 0.075f;
        transform.localPosition = bobbingPosition;
        if (target == null)
            target = game.PlayerTransform;
        if (target == null)
            return;

        Vector2 direction = (Vector2)target.position - (Vector2)transform.position;
        if (direction.sqrMagnitude < 0.01f)
            return;
        direction.Normalize();
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        angle += Mathf.Sin(Time.time * 6f + transform.position.x) * 8f;
        barrelPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        fireCooldown -= Time.deltaTime;
        if (fireCooldown <= 0f)
        {
            Vector2 messyAim = (Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f)) * direction).normalized;
            game.FireTurretBolt((Vector2)barrelPivot.position + messyAim * 0.78f, messyAim);
            fireCooldown = Random.Range(1.1f, 3.1f);
        }
    }

    public void DestroyFromImpact()
    {
        if (destroyed)
            return;
        destroyed = true;
        game.TurretDestroyed();
        Destroy(gameObject);
    }
}
