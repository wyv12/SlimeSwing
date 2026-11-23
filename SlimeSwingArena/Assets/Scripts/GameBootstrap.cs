using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    private enum GameState { Playing, Transitioning, Paused, GameOver, Victory }
    private const float LevelTransitionDuration = 2f;

    private struct WallSpec
    {
        public Vector2 center;
        public Vector2 size;

        public WallSpec(float x, float y, float width, float height)
        {
            center = new Vector2(x, y);
            size = new Vector2(width, height);
        }
    }

    private sealed class LevelSpec
    {
        public string title;
        public Vector2 size;
        public WallSpec[] walls;
        public Vector2[] anchors;
        public Vector2[] turrets;

        public LevelSpec(string levelTitle, WallSpec[] cover, Vector2[] grapplePoints, Vector2[] sentries)
        {
            title = levelTitle;
            size = new Vector2(24f, 14f);
            walls = cover;
            anchors = grapplePoints;
            turrets = sentries;
        }
    }

    private static GameBootstrap instance;
    private GameState state = GameState.Playing;
    private Camera gameCamera;
    private Transform levelRoot;
    private SlimeController player;
    private LevelSpec currentLevel;
    private int levelIndex;
    private int lives = 3;
    private int score;
    private Vector2 spawnPosition = new Vector2(-10f, -5f);
    private float respawnTimer;
    private int turretsRemaining;
    private float transitionTimer;
    private int completedLevel;
    private bool transitionLoadedNext;
    private bool transitioningToVictory;

    public bool IsPlaying { get { return state == GameState.Playing; } }
    public Transform PlayerTransform { get { return player != null ? player.transform : null; } }
    public int CurrentLevel { get { return levelIndex + 1; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGameExists()
    {
        if (FindObjectOfType<GameBootstrap>() == null)
            new GameObject("Slime Swing Runtime").AddComponent<GameBootstrap>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        Time.timeScale = 1f;
        Application.targetFrameRate = 60;
        Physics2D.gravity = Vector2.zero;
        CreateCamera();
        LoadLevel(0);
        SpawnPlayer();
        FitCameraToArena();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (state == GameState.Playing)
                SetState(GameState.Paused);
            else if (state == GameState.Paused)
                SetState(GameState.Playing);
        }

        if (state == GameState.Playing)
        {
            if (respawnTimer > 0f)
            {
                respawnTimer -= Time.deltaTime;
                if (respawnTimer <= 0f && player != null)
                    player.Respawn(spawnPosition);
            }

        }
        else if (state == GameState.Transitioning)
        {
            AdvanceLevelTransition();
        }

        FitCameraToArena();
    }

    private void AdvanceLevelTransition()
    {
        transitionTimer -= Time.unscaledDeltaTime;

        // Swap the world while the screen is fully dark.
        if (!transitionLoadedNext && transitionTimer <= 1f)
        {
            transitionLoadedNext = true;
            if (!transitioningToVictory)
            {
                if (player != null)
                    Destroy(player.gameObject);
                DestroyProjectiles();
                LoadLevel(levelIndex + 1);
                SpawnPlayer();
                FitCameraToArena();
            }
        }

        if (transitionTimer <= 0f)
        {
            transitionTimer = 0f;
            SetState(transitioningToVictory ? GameState.Victory : GameState.Playing);
        }
    }

    private void CreateCamera()
    {
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            gameCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }
        gameCamera.orthographic = true;
        gameCamera.orthographicSize = 8f;
        gameCamera.clearFlags = CameraClearFlags.SolidColor;
        gameCamera.backgroundColor = new Color(0.035f, 0.065f, 0.085f);
        gameCamera.transform.position = new Vector3(0f, 0f, -10f);
    }

    private void FitCameraToArena()
    {
        if (gameCamera == null || currentLevel == null)
            return;

        float halfHeight = currentLevel.size.y * 0.5f + 0.9f;
        float halfWidth = currentLevel.size.x * 0.5f + 0.9f;
        gameCamera.orthographicSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.5f, gameCamera.aspect));
        gameCamera.transform.position = new Vector3(0f, 0f, -10f);
    }

    private LevelSpec MakeLevel(int index)
    {
        if (index == 0)
            return new LevelSpec("THE GOO YARD",
                new[] { new WallSpec(-4f, 0f, 0.8f, 4.8f), new WallSpec(2.4f, 2.2f, 4f, 0.7f), new WallSpec(4f, -2.7f, 0.7f, 3.2f), new WallSpec(-0.4f, -3.4f, 3.6f, 0.7f) },
                new[] { new Vector2(-8f, -4.5f), new Vector2(-7f, 3.5f), new Vector2(0f, 0f), new Vector2(7.5f, 4.5f), new Vector2(7f, -4.5f) },
                new[] { new Vector2(-7.5f, 4.5f), new Vector2(4f, 4.4f), new Vector2(7.5f, -3.5f), new Vector2(0f, -5.3f) });
        if (index == 1)
            return new LevelSpec("SPLIT SHIFT",
                new[] { new WallSpec(-4.5f, -3f, 0.8f, 5f), new WallSpec(-1f, 1.7f, 4.8f, 0.7f), new WallSpec(4.5f, 3.2f, 0.8f, 4.2f), new WallSpec(2f, -2f, 4.4f, 0.7f), new WallSpec(-0.2f, -0.2f, 1.5f, 1.5f) },
                new[] { new Vector2(-8f, 4.5f), new Vector2(-6.5f, -4.5f), new Vector2(0f, 5f), new Vector2(0f, -5f), new Vector2(8f, 4.5f), new Vector2(8f, -4.5f) },
                new[] { new Vector2(-8f, 4.5f), new Vector2(-1f, 4.7f), new Vector2(7.8f, 4.7f), new Vector2(-1f, -5f), new Vector2(8f, -4.8f) });
        if (index == 2)
            return new LevelSpec("TANGLE TOWER",
                new[] { new WallSpec(-6f, 2f, 5f, 0.7f), new WallSpec(-3f, -2f, 0.7f, 5f), new WallSpec(0.7f, 2.2f, 0.7f, 4f), new WallSpec(4.5f, 0f, 5f, 0.7f), new WallSpec(7f, -3.6f, 0.7f, 3.2f), new WallSpec(1f, -4f, 4f, 0.7f) },
                new[] { new Vector2(-8f, 0f), new Vector2(-6f, 5f), new Vector2(-1.2f, 4f), new Vector2(3f, 5f), new Vector2(8f, 1f), new Vector2(4f, -5f), new Vector2(-3f, -5f) },
                new[] { new Vector2(-8f, 4.5f), new Vector2(-1.5f, 5f), new Vector2(7.8f, 4.5f), new Vector2(-8f, -1f), new Vector2(1.5f, -5f), new Vector2(8f, -4.5f) });
        if (index == 3)
            return new LevelSpec("SENTRY SKYWAY",
                new[] { new WallSpec(-6f, 0f, 0.7f, 6f), new WallSpec(-2.5f, 3f, 5f, 0.7f), new WallSpec(1.5f, -2.2f, 0.7f, 5.2f), new WallSpec(5f, 1.5f, 5.2f, 0.7f), new WallSpec(6.5f, -3.7f, 0.7f, 2.8f), new WallSpec(-1.8f, -4.5f, 4f, 0.7f) },
                new[] { new Vector2(-8f, 5f), new Vector2(-4f, -4.8f), new Vector2(0f, 5f), new Vector2(3f, -5f), new Vector2(8f, 4.5f), new Vector2(8f, -4.8f), new Vector2(-8f, 0f) },
                new[] { new Vector2(-8f, 4.8f), new Vector2(-2f, 5f), new Vector2(5f, 4.8f), new Vector2(8f, 0f), new Vector2(5f, -5f), new Vector2(-2f, -5f), new Vector2(-8f, -0.5f) });

        return new LevelSpec("THE LAST LATCH",
            new[] { new WallSpec(-7f, 0f, 0.7f, 7f), new WallSpec(-4f, 3.5f, 4.6f, 0.7f), new WallSpec(-3f, -3f, 0.7f, 5f), new WallSpec(0f, 0f, 4f, 0.7f), new WallSpec(3f, 3.5f, 0.7f, 5f), new WallSpec(6f, 0.8f, 4.5f, 0.7f), new WallSpec(6f, -3.8f, 0.7f, 2.8f), new WallSpec(0f, -5f, 4.8f, 0.7f) },
            new[] { new Vector2(-9f, 0f), new Vector2(-7f, 5f), new Vector2(-4f, -5f), new Vector2(0f, 5f), new Vector2(4f, -4.8f), new Vector2(8f, 4.7f), new Vector2(8f, -5f), new Vector2(0f, -1f) },
            new[] { new Vector2(-9f, 5f), new Vector2(-3f, 5f), new Vector2(3f, 5f), new Vector2(9f, 5f), new Vector2(-9f, 1.5f), new Vector2(-3f, -5f), new Vector2(3f, -5f), new Vector2(9f, -5f) });
    }

    private void LoadLevel(int index)
    {
        if (levelRoot != null)
        {
            levelRoot.gameObject.SetActive(false);
            Destroy(levelRoot.gameObject);
        }
        levelIndex = index;
        currentLevel = MakeLevel(index);
        levelRoot = new GameObject("Level " + (index + 1) + " - " + currentLevel.title).transform;
        spawnPosition = new Vector2(-10f, -5f);
        turretsRemaining = currentLevel.turrets.Length;
        CreateLevelArt();
    }

    private void CreateLevelArt()
    {
        GameArt.RectObject("Arena floor", levelRoot, Vector2.zero, currentLevel.size, new Color(0.12f, 0.2f, 0.21f), -100);
        for (int row = 0; row < 7; row++)
        {
            for (int column = 0; column < 12; column++)
            {
                if ((row + column) % 2 == 0)
                    GameArt.RectObject("Worn floor tile", levelRoot,
                        new Vector2(-11f + column * 2f, -6f + row * 2f), new Vector2(1.96f, 1.96f),
                        new Color(0.14f, 0.22f, 0.22f), -99);
            }
        }

        CreateWall(new Vector2(0f, 6.75f), new Vector2(24f, 0.5f));
        CreateWall(new Vector2(0f, -6.75f), new Vector2(24f, 0.5f));
        CreateWall(new Vector2(-11.75f, 0f), new Vector2(0.5f, 14f));
        CreateWall(new Vector2(11.75f, 0f), new Vector2(0.5f, 14f));
        for (int i = 0; i < currentLevel.walls.Length; i++)
            CreateWall(currentLevel.walls[i].center, currentLevel.walls[i].size);

        for (int i = 0; i < currentLevel.anchors.Length; i++)
            CreateAnchor(currentLevel.anchors[i]);
        for (int i = 0; i < currentLevel.turrets.Length; i++)
            CreateTurret(currentLevel.turrets[i]);
    }

    private void CreateWall(Vector2 center, Vector2 size)
    {
        GameArt.RectObject("Wall shadow", levelRoot, center + new Vector2(0.12f, -0.12f), size, new Color(0.045f, 0.085f, 0.09f), 1);
        GameObject wall = GameArt.RectObject("Arena cover", levelRoot, center, size, new Color(0.28f, 0.36f, 0.34f), 2);
        wall.AddComponent<BoxCollider2D>();
        if (size.x >= size.y)
            GameArt.RectObject("Wall scrape", levelRoot, center + new Vector2(0f, size.y * 0.18f), new Vector2(size.x * 0.58f, 0.045f), new Color(0.12f, 0.18f, 0.18f), 3);
        else
            GameArt.RectObject("Wall scrape", levelRoot, center + new Vector2(-size.x * 0.15f, 0f), new Vector2(0.045f, size.y * 0.52f), new Color(0.12f, 0.18f, 0.18f), 3);
    }

    private void CreateAnchor(Vector2 position)
    {
        GameObject anchor = new GameObject("Grapple knot");
        anchor.transform.SetParent(levelRoot, false);
        anchor.transform.localPosition = position;
        GameArt.CircleObject("Outer knot", anchor.transform, Vector2.zero, new Vector2(0.9f, 0.9f), new Color(0.1f, 0.23f, 0.2f), 15);
        GameArt.CircleObject("Glow ring", anchor.transform, Vector2.zero, new Vector2(0.65f, 0.65f), new Color(0.58f, 1f, 0.52f), 16);
        GameArt.CircleObject("Knot center", anchor.transform, Vector2.zero, new Vector2(0.25f, 0.25f), new Color(0.08f, 0.18f, 0.17f), 17);
        anchor.AddComponent<GrappleAnchor>();
    }

    private void CreateTurret(Vector2 location)
    {
        GameObject turret = new GameObject("Sentry turret");
        turret.transform.SetParent(levelRoot, false);
        turret.transform.localPosition = location;
        CircleCollider2D collider = turret.AddComponent<CircleCollider2D>();
        collider.radius = 0.46f;
        collider.isTrigger = false;
        GameArt.CircleObject("Sentry shadow", turret.transform, new Vector2(0.08f, -0.11f), new Vector2(1.02f, 1.02f), new Color(0.03f, 0.07f, 0.07f, 0.7f), 3);
        GameArt.CircleObject("Sentry base", turret.transform, Vector2.zero, new Vector2(0.9f, 0.9f), new Color(0.24f, 0.3f, 0.31f), 4);
        GameArt.CircleObject("Sentry eye", turret.transform, Vector2.zero, new Vector2(0.33f, 0.33f), new Color(1f, 0.38f, 0.23f), 5);
        GameArt.RectObject("Sentry paint chip", turret.transform, new Vector2(-0.27f, -0.08f), new Vector2(0.19f, 0.07f), new Color(0.1f, 0.15f, 0.16f), 6).transform.rotation = Quaternion.Euler(0f, 0f, -24f);
        GameObject barrel = new GameObject("Sentry barrel pivot");
        barrel.transform.SetParent(turret.transform, false);
        GameArt.RectObject("Sentry barrel", barrel.transform, new Vector2(0.39f, 0f), new Vector2(0.72f, 0.17f), new Color(0.7f, 0.74f, 0.66f), 6);
        turret.AddComponent<TurretEnemy>().Configure(this, barrel.transform);
    }

    private void SpawnPlayer()
    {
        GameObject slime = new GameObject("Slime");
        slime.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, 0f);
        Rigidbody2D body = slime.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearDamping = 0.7f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        CircleCollider2D collider = slime.AddComponent<CircleCollider2D>();
        collider.radius = 0.43f;
        PhysicsMaterial2D grip = new PhysicsMaterial2D("Slime grip");
        grip.friction = 0.35f;
        grip.bounciness = 0f;
        collider.sharedMaterial = grip;

        GameArt.CircleObject("Slime shadow", slime.transform, new Vector2(0.05f, -0.14f), new Vector2(0.95f, 0.44f), new Color(0.015f, 0.035f, 0.04f, 0.55f), 4);
        Color slimeGreen = new Color(0.4f, 0.9f, 0.43f);
        GameArt.CircleObject("Lumpy left edge", slime.transform, new Vector2(-0.34f, -0.17f), new Vector2(0.48f, 0.42f), slimeGreen, 5);
        GameArt.CircleObject("Lumpy right edge", slime.transform, new Vector2(0.32f, -0.2f), new Vector2(0.5f, 0.38f), new Color(0.36f, 0.84f, 0.4f), 5);
        GameArt.CircleObject("Uneven crown", slime.transform, new Vector2(0.16f, 0.31f), new Vector2(0.44f, 0.34f), new Color(0.45f, 0.94f, 0.46f), 5);
        GameArt.CircleObject("Slime body", slime.transform, Vector2.zero, new Vector2(0.92f, 0.82f), slimeGreen, 6);
        GameArt.CircleObject("Slime shine", slime.transform, new Vector2(-0.16f, 0.17f), new Vector2(0.24f, 0.13f), new Color(0.82f, 1f, 0.67f), 7);
        GameArt.CircleObject("Left eye", slime.transform, new Vector2(-0.15f, 0.02f), new Vector2(0.13f, 0.17f), new Color(0.97f, 1f, 0.9f), 8);
        GameArt.CircleObject("Right eye", slime.transform, new Vector2(0.15f, 0.02f), new Vector2(0.13f, 0.17f), new Color(0.97f, 1f, 0.9f), 8);
        GameArt.CircleObject("Left pupil", slime.transform, new Vector2(-0.12f, 0f), new Vector2(0.055f, 0.08f), new Color(0.05f, 0.15f, 0.12f), 9);
        GameArt.CircleObject("Right pupil", slime.transform, new Vector2(0.18f, 0f), new Vector2(0.055f, 0.08f), new Color(0.05f, 0.15f, 0.12f), 9);

        player = slime.AddComponent<SlimeController>();
        player.Configure(this, body);
        player.SetPhysicsActive(state != GameState.Transitioning);
        player.SetControllable(state == GameState.Playing);
    }

    private void StartGame()
    {
        Time.timeScale = 1f;
        lives = 3;
        score = 0;
        respawnTimer = 0f;
        transitionTimer = 0f;
        transitionLoadedNext = false;
        transitioningToVictory = false;
        if (player != null)
            Destroy(player.gameObject);
        DestroyProjectiles();
        state = GameState.Playing;
        LoadLevel(0);
        SpawnPlayer();
        FitCameraToArena();
    }

    private void DestroyProjectiles()
    {
        Projectile[] shots = FindObjectsOfType<Projectile>();
        for (int i = 0; i < shots.Length; i++)
            Destroy(shots[i].gameObject);
    }

    public void FireTurretBolt(Vector2 origin, Vector2 direction)
    {
        if (!IsPlaying)
            return;
        direction.Normalize();
        GameObject bolt = new GameObject("Sentry bolt");
        bolt.transform.position = origin;
        GameArt.CircleObject("Orange bolt", bolt.transform, Vector2.zero, new Vector2(0.2f, 0.2f), new Color(1f, 0.53f, 0.25f), 12);
        CircleCollider2D collider = bolt.AddComponent<CircleCollider2D>();
        collider.isTrigger = false;
        collider.radius = 0.11f;
        Rigidbody2D body = bolt.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        Projectile projectile = bolt.AddComponent<Projectile>();
        projectile.Configure(this);
        body.linearVelocity = direction * 6.3f;
    }

    public void PlayerHit()
    {
        if (!IsPlaying || respawnTimer > 0f || player == null)
            return;
        lives--;
        if (lives <= 0)
        {
            SetState(GameState.GameOver);
            player.SetPhysicsActive(false);
            return;
        }
        player.PrepareRespawn();
        respawnTimer = 0.65f;
    }

    public void TurretDestroyed()
    {
        if (!IsPlaying)
            return;
        score += 150;
        turretsRemaining = Mathf.Max(0, turretsRemaining - 1);
        if (turretsRemaining == 0)
            CompleteLevel();
    }

    private void CompleteLevel()
    {
        if (!IsPlaying)
            return;
        score += 500;
        completedLevel = levelIndex + 1;
        transitioningToVictory = levelIndex >= 4;
        transitionLoadedNext = false;
        transitionTimer = LevelTransitionDuration;
        if (!transitioningToVictory)
            lives = Mathf.Min(3, lives + 1);
        SetState(GameState.Transitioning);
    }

    private void SetState(GameState next)
    {
        state = next;
        Time.timeScale = next == GameState.Paused || next == GameState.GameOver || next == GameState.Victory ? 0f : 1f;
        if (player != null)
        {
            if (next == GameState.Transitioning)
                player.SetPhysicsActive(false);
            else if (next == GameState.Playing)
                player.SetPhysicsActive(true);

            if (next != GameState.Paused)
                player.SetControllable(next == GameState.Playing);
        }
    }

    private void OnGUI()
    {
        float scale = Mathf.Clamp(Screen.width / 1500f, 0.72f, 1.15f);
        if (state == GameState.Playing)
            DrawHud(scale);
        else if (state == GameState.Transitioning)
            DrawLevelTransition();
        else if (state == GameState.Paused)
            DrawPause(scale);
        else if (state == GameState.GameOver)
            DrawEnd(scale, false);
        else if (state == GameState.Victory)
            DrawEnd(scale, true);
    }

    private GUIStyle TextStyle(float size, Color color, TextAnchor anchor, FontStyle fontStyle)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = Mathf.RoundToInt(size);
        style.fontStyle = fontStyle;
        style.alignment = anchor;
        style.normal.textColor = color;
        return style;
    }

    private void DrawBackdrop(float alpha)
    {
        GUI.color = new Color(0.012f, 0.025f, 0.035f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawPanel(Rect rect)
    {
        GUI.color = new Color(0.025f, 0.07f, 0.08f, 0.97f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(0.35f, 0.8f, 0.58f, 0.95f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private GUIStyle MakeButton(float scale)
    {
        GUIStyle button = new GUIStyle(GUI.skin.button);
        button.fontSize = Mathf.RoundToInt(19f * scale);
        button.fontStyle = FontStyle.Bold;
        button.normal.textColor = new Color(0.04f, 0.13f, 0.1f);
        return button;
    }

    private void DrawLevelTransition()
    {
        float progress = 1f - transitionTimer / LevelTransitionDuration;
        float fadeIn = Mathf.Clamp01(progress / 0.24f);
        float fadeOut = Mathf.Clamp01((1f - progress) / 0.28f);
        float fade = Mathf.Min(fadeIn, fadeOut);

        GUI.color = new Color(0.008f, 0.02f, 0.024f, 0.97f * fade);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        string heading;
        string subheading;
        if (transitioningToVictory)
        {
            heading = "ALL FIVE LEVELS CLEARED";
            subheading = "THE GOO KEEPS ON ROLLING";
        }
        else if (transitionLoadedNext)
        {
            heading = "LEVEL " + CurrentLevel;
            subheading = currentLevel.title;
        }
        else
        {
            heading = "LEVEL " + completedLevel + " CLEARED";
            subheading = "DROPPING INTO THE NEXT ARENA...";
        }

        float scale = Mathf.Clamp(Screen.width / 1500f, 0.72f, 1.15f);
        GUI.color = new Color(0.74f, 1f, 0.58f, fade);
        GUI.Label(new Rect(0f, Screen.height * 0.43f, Screen.width, 62f * scale), heading,
            TextStyle(34f * scale, new Color(0.74f, 1f, 0.58f), TextAnchor.MiddleCenter, FontStyle.Bold));
        GUI.color = new Color(0.76f, 0.9f, 0.85f, fade);
        GUI.Label(new Rect(0f, Screen.height * 0.51f, Screen.width, 34f * scale), subheading,
            TextStyle(16f * scale, new Color(0.76f, 0.9f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Normal));
        GUI.color = Color.white;
    }

    private void DrawHud(float scale)
    {
        Rect panel = new Rect(18f, 18f, 315f * scale, 119f * scale);
        GUI.color = new Color(0.02f, 0.055f, 0.065f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(panel.x + 14f, panel.y + 5f, panel.width - 28f, 25f * scale), "LEVEL " + CurrentLevel + " / 5   " + currentLevel.title, TextStyle(15f * scale, new Color(0.75f, 1f, 0.58f), TextAnchor.MiddleLeft, FontStyle.Bold));
        GUI.Label(new Rect(panel.x + 14f, panel.y + 32f * scale, panel.width - 28f, 22f * scale), "TURRETS LEFT  " + turretsRemaining, TextStyle(14f * scale, new Color(1f, 0.68f, 0.39f), TextAnchor.MiddleLeft, FontStyle.Bold));
        GUI.Label(new Rect(panel.x + 14f, panel.y + 57f * scale, panel.width - 28f, 22f * scale), "GLOOPS  " + lives + "       SCORE  " + score.ToString("0000"), TextStyle(14f * scale, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold));
        GUI.Label(new Rect(panel.x + 14f, panel.y + 86f * scale, panel.width - 28f, 20f * scale), "WASD / ARROWS MOVE  •  MOUSE AIM  •  LMB GRAPPLE", TextStyle(9f * scale, new Color(0.69f, 0.83f, 0.78f), TextAnchor.MiddleLeft, FontStyle.Normal));
        GUI.Label(new Rect(Screen.width - 310f * scale, 20f, 290f * scale, 25f * scale), "DESTROY EVERY TURRET", TextStyle(12f * scale, new Color(0.77f, 0.93f, 0.85f), TextAnchor.MiddleRight, FontStyle.Bold));
        GUI.Label(new Rect(Screen.width - 350f * scale, 43f, 330f * scale, 22f * scale), "BUILD SPEED, THEN GRAPPLE-RAM", TextStyle(11f * scale, new Color(0.69f, 0.83f, 0.78f), TextAnchor.MiddleRight, FontStyle.Normal));
    }

    private void DrawPause(float scale)
    {
        DrawBackdrop(0.76f);
        float width = 420f * scale;
        float height = 315f * scale;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        DrawPanel(panel);
        GUI.Label(new Rect(panel.x + 20f, panel.y + 30f * scale, panel.width - 40f, 60f * scale), "PAUSED", TextStyle(42f * scale, new Color(0.74f, 1f, 0.58f), TextAnchor.MiddleCenter, FontStyle.Bold));
        GUI.Label(new Rect(panel.x + 20f, panel.y + 94f * scale, panel.width - 40f, 26f * scale), "The tendril is safely tucked away.", TextStyle(16f * scale, new Color(0.76f, 0.9f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Normal));
        GUIStyle button = MakeButton(scale);
        GUI.backgroundColor = new Color(0.69f, 1f, 0.49f);
        if (GUI.Button(new Rect(panel.x + 88f * scale, panel.y + 143f * scale, panel.width - 176f * scale, 47f * scale), "RESUME", button))
            SetState(GameState.Playing);
        GUI.backgroundColor = new Color(0.48f, 0.66f, 0.59f);
        if (GUI.Button(new Rect(panel.x + 88f * scale, panel.y + 204f * scale, panel.width - 176f * scale, 42f * scale), "RESTART RUN", button))
            StartGame();
        GUI.backgroundColor = Color.white;
    }

    private void DrawEnd(float scale, bool won)
    {
        DrawBackdrop(0.8f);
        float width = 510f * scale;
        float height = 350f * scale;
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        DrawPanel(panel);
        string heading = won ? "FIVE LEVELS CLEARED!" : "SPLAT!";
        Color tint = won ? new Color(0.74f, 1f, 0.58f) : new Color(1f, 0.59f, 0.39f);
        GUI.Label(new Rect(panel.x + 20f, panel.y + 38f * scale, panel.width - 40f, 62f * scale), heading, TextStyle(34f * scale, tint, TextAnchor.MiddleCenter, FontStyle.Bold));
        GUI.Label(new Rect(panel.x + 25f, panel.y + 112f * scale, panel.width - 50f, 35f * scale), "FINAL SCORE  " + score.ToString("0000"), TextStyle(20f * scale, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold));
        GUI.Label(new Rect(panel.x + 25f, panel.y + 154f * scale, panel.width - 50f, 30f * scale), won ? "That was one impressive bit of slime." : "The sentries got you this round.", TextStyle(15f * scale, new Color(0.76f, 0.9f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Normal));
        GUIStyle button = MakeButton(scale);
        GUI.backgroundColor = new Color(0.69f, 1f, 0.49f);
        if (GUI.Button(new Rect(panel.x + 100f * scale, panel.y + 222f * scale, panel.width - 200f * scale, 48f * scale), "PLAY AGAIN", button))
            StartGame();
        GUI.backgroundColor = Color.white;
    }
}
