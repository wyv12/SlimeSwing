using UnityEngine;

public class Projectile : MonoBehaviour
{
    private GameBootstrap game;
    private float lifetime = 4f;
    private bool spent;

    public void Configure(GameBootstrap owner)
    {
        game = owner;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ResolveContact(IsSlimeCollider(other));
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        bool hitSlime = IsSlimeCollider(collision.collider) || IsSlimeCollider(collision.otherCollider);
        if (!hitSlime && collision.gameObject != null)
            hitSlime = collision.gameObject.GetComponentInParent<SlimeController>() != null;
        ResolveContact(hitSlime);
    }

    private bool IsSlimeCollider(Collider2D other)
    {
        return other != null && other.GetComponentInParent<SlimeController>() != null;
    }

    private void ResolveContact(bool hitSlime)
    {
        if (spent)
            return;

        if (hitSlime && game != null)
            game.PlayerHit();

        spent = true;
        Destroy(gameObject);
    }
}
