using UnityEngine;

public class SlimeController : MonoBehaviour
{
    private const float WalkSpeed = 7f;
    private const float AirPump = 13f;
    private const float GrappleReach = 18f;
    private const float CursorSnap = 1.7f;

    private GameBootstrap game;
    private Rigidbody2D body;
    private DistanceJoint2D tendrilJoint;
    private LineRenderer tendrilLine;
    private GrappleAnchor latchedAnchor;
    private Vector2 movementInput;
    private bool controllable;
    private SpriteRenderer slimeRenderer;
    private Vector2 previousFixedVelocity;
    private float controlGlitchTimer = 5f;
    private float latchedDistance;
    private bool controlsReversed;

    public void Configure(GameBootstrap owner, Rigidbody2D rigidbody)
    {
        game = owner;
        body = rigidbody;
        Transform bodyVisual = transform.Find("Slime body");
        slimeRenderer = bodyVisual != null ? bodyVisual.GetComponent<SpriteRenderer>() : null;

        tendrilJoint = gameObject.AddComponent<DistanceJoint2D>();
        tendrilJoint.autoConfigureConnectedAnchor = false;
        tendrilJoint.autoConfigureDistance = false;
        tendrilJoint.maxDistanceOnly = false;
        tendrilJoint.enableCollision = false;
        tendrilJoint.enabled = false;

        GameObject lineObject = new GameObject("Grappling tendril");
        lineObject.transform.SetParent(transform, false);
        tendrilLine = lineObject.AddComponent<LineRenderer>();
        tendrilLine.useWorldSpace = true;
        tendrilLine.positionCount = 2;
        tendrilLine.startWidth = 0.09f;
        tendrilLine.endWidth = 0.045f;
        tendrilLine.startColor = new Color(0.64f, 1f, 0.48f);
        tendrilLine.endColor = new Color(0.32f, 0.78f, 0.4f);
        tendrilLine.numCapVertices = 3;
        tendrilLine.sortingOrder = 20;
        tendrilLine.material = new Material(Shader.Find("Sprites/Default"));
        tendrilLine.enabled = false;
    }

    public void SetControllable(bool value)
    {
        controllable = value;
        if (!value)
        {
            movementInput = Vector2.zero;
            if (body != null)
                body.linearVelocity = Vector2.zero;
            Detach();
        }
    }

    public void SetPhysicsActive(bool value)
    {
        if (body != null)
            body.simulated = value;
    }

    private void Update()
    {
        if (!controllable || game == null || !game.IsPlaying)
        {
            movementInput = Vector2.zero;
            return;
        }

        movementInput = Vector2.zero;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) movementInput.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) movementInput.x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) movementInput.y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) movementInput.y += 1f;
        movementInput = Vector2.ClampMagnitude(movementInput, 1f);

        controlGlitchTimer -= Time.deltaTime;
        if (controlGlitchTimer <= 0f)
        {
            controlsReversed = !controlsReversed;
            controlGlitchTimer = controlsReversed ? 0.45f : Random.Range(4.5f, 7f);
        }
        if (controlsReversed)
            movementInput = -movementInput;

        if (Input.GetMouseButtonDown(0))
            TryLatchToAimedKnot();
        if (Input.GetMouseButtonUp(0) || (latchedAnchor != null && !Input.GetMouseButton(0)))
            Detach();

        if (latchedAnchor != null && tendrilLine != null)
        {
            tendrilLine.SetPosition(0, latchedAnchor.transform.position);
            tendrilLine.SetPosition(1, transform.position + Vector3.up * 0.2f);
        }

        if (slimeRenderer != null)
        {
            float squash = Mathf.Clamp01(body.linearVelocity.magnitude / WalkSpeed);
            float wobble = Mathf.Sin(Time.time * 9f) * 0.035f;
            slimeRenderer.transform.localScale = new Vector3(0.92f + squash * 0.08f + wobble, 0.82f - squash * 0.05f - wobble, 1f);
        }
    }

    private void FixedUpdate()
    {
        if (body == null || !controllable || game == null || !game.IsPlaying)
            return;

        previousFixedVelocity = body.linearVelocity;

        if (latchedAnchor != null)
        {
            tendrilJoint.distance = Mathf.Max(2.3f, latchedDistance + Mathf.Sin(Time.time * 15f) * 0.3f);
            if (movementInput.sqrMagnitude > 0.01f)
                body.AddForce(movementInput * AirPump, ForceMode2D.Force);
            return;
        }

        Vector2 targetVelocity = movementInput * WalkSpeed;
        body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, targetVelocity, 38f * Time.fixedDeltaTime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TurretEnemy turret = collision.collider != null ? collision.collider.GetComponent<TurretEnemy>() : null;
        if (turret == null && collision.gameObject != null)
            turret = collision.gameObject.GetComponent<TurretEnemy>();
        if (turret == null || game == null || !game.IsPlaying)
            return;

        bool rammedWhileSwinging = latchedAnchor != null && previousFixedVelocity.magnitude >= 3.8f;
        if (rammedWhileSwinging)
        {
            Vector2 awayFromTurret = body.position - (Vector2)turret.transform.position;
            if (awayFromTurret.sqrMagnitude < 0.01f)
                awayFromTurret = -previousFixedVelocity;
            Detach();
            turret.DestroyFromImpact();
            body.linearVelocity = awayFromTurret.normalized * 6.2f;
        }
        else
        {
            game.PlayerHit();
        }
    }

    private void TryLatchToAimedKnot()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;
        Vector3 mouseWorld3 = camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -camera.transform.position.z));
        Vector2 mouseWorld = mouseWorld3;
        GrappleAnchor[] anchors = FindObjectsOfType<GrappleAnchor>();
        GrappleAnchor best = null;
        float bestCursorDistance = CursorSnap;
        for (int i = 0; i < anchors.Length; i++)
        {
            if (anchors[i] == null)
                continue;
            float reach = Vector2.Distance(body.position, anchors[i].transform.position);
            if (reach > GrappleReach)
                continue;
            float cursorDistance = Vector2.Distance(mouseWorld, anchors[i].transform.position);
            if (cursorDistance < bestCursorDistance)
            {
                bestCursorDistance = cursorDistance;
                best = anchors[i];
            }
        }

        if (best == null)
            return;
        latchedAnchor = best;
        tendrilJoint.connectedBody = null;
        tendrilJoint.connectedAnchor = best.transform.position;
        latchedDistance = Mathf.Max(2.6f, Vector2.Distance(body.position, best.transform.position) * 0.96f);
        tendrilJoint.distance = latchedDistance;
        tendrilJoint.enabled = true;
        tendrilLine.enabled = true;
    }

    private void Detach()
    {
        latchedAnchor = null;
        if (tendrilJoint != null)
            tendrilJoint.enabled = false;
        if (tendrilLine != null)
            tendrilLine.enabled = false;
    }

    public void PrepareRespawn()
    {
        Detach();
        controllable = false;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }
    }

    public void Respawn(Vector2 position)
    {
        if (body != null)
        {
            body.simulated = true;
            body.position = position;
            body.linearVelocity = Vector2.zero;
        }
        transform.position = position;
        SetControllable(true);
    }
}
