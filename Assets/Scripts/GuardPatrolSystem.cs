using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guard Patrol & Facility Corridor System for DECODER.
/// Manages patrol waypoints, routes, guard AI pathing, and procedural corridor structures
/// outside the main laboratory room.
/// </summary>
public class GuardPatrolSystem : MonoBehaviour
{
    [System.Serializable]
    public class PatrolRoute
    {
        public string routeName = "Маршрут 1: Главный коридор";
        public Color routeColor = Color.yellow;
        public List<Transform> waypoints = new List<Transform>();
        public bool isLoop = true;
        [Tooltip("Seconds guard pauses to look around at each waypoint")]
        public float waitTimeAtWaypoint = 2.5f;
    }

    [Header("Patrol Routes")]
    [SerializeField] private List<PatrolRoute> routes = new List<PatrolRoute>();

    [Header("Guard Settings")]
    [SerializeField] private bool spawnGuardsOnStart = true;
    [SerializeField] private float guardWalkSpeed = 1.8f;
    [SerializeField] private float guardTurnSpeed = 180f;

    [Header("Debug Visualization")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float waypointRadius = 0.35f;

    public List<PatrolRoute> Routes => routes;

    private readonly List<GuardPawn> activeGuards = new List<GuardPawn>();

    private void Start()
    {
        if (spawnGuardsOnStart)
        {
            SpawnAllGuards();
        }
    }

    /// <summary>
    /// Spawns a stylized guard pawn for each configured patrol route.
    /// </summary>
    public void SpawnAllGuards()
    {
        // Clean existing
        foreach (var g in activeGuards)
        {
            if (g != null) Destroy(g.gameObject);
        }
        activeGuards.Clear();

        for (int i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            if (route.waypoints == null || route.waypoints.Count == 0) continue;

            var firstWp = route.waypoints[0];
            var guardObj = new GameObject($"Guard_{i + 1}_{route.routeName}");
            guardObj.transform.position = firstWp.position;
            guardObj.transform.rotation = firstWp.rotation;

            var guard = guardObj.AddComponent<GuardPawn>();
            guard.Initialize(route, guardWalkSpeed, guardTurnSpeed);
            activeGuards.Add(guard);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || routes == null) return;

        for (int r = 0; r < routes.Count; r++)
        {
            var route = routes[r];
            if (route.waypoints == null || route.waypoints.Count == 0) continue;

            Gizmos.color = route.routeColor;

            for (int i = 0; i < route.waypoints.Count; i++)
            {
                var wp = route.waypoints[i];
                if (wp == null) continue;

                // Waypoint sphere
                Gizmos.DrawWireSphere(wp.position + Vector3.up * 0.1f, waypointRadius);

                // Direction / connecting line
                Transform next = null;
                if (i < route.waypoints.Count - 1)
                {
                    next = route.waypoints[i + 1];
                }
                else if (route.isLoop && route.waypoints.Count > 1)
                {
                    next = route.waypoints[0];
                }

                if (next != null)
                {
                    Vector3 start = wp.position + Vector3.up * 0.1f;
                    Vector3 end = next.position + Vector3.up * 0.1f;
                    Gizmos.DrawLine(start, end);

                    // Direction arrow head
                    Vector3 dir = (end - start).normalized;
                    Vector3 mid = (start + end) * 0.5f;
                    Vector3 right = Vector3.Cross(Vector3.up, dir).normalized * 0.25f;
                    Gizmos.DrawLine(mid, mid - dir * 0.4f + right);
                    Gizmos.DrawLine(mid, mid - dir * 0.4f - right);
                }
            }
        }
    }

    // =========================================================================
    // PROCEDURAL CORRIDOR & WAYPOINT GENERATOR (Editor / Runtime Builder)
    // =========================================================================
    [ContextMenu("Rebuild Corridors And Routes")]
    public void RebuildFacilityNetwork()
    {
        // 1. Root container
        Transform root = transform.Find("FacilityCorridors");
        if (root != null)
        {
            DestroyImmediate(root.gameObject);
        }

        var rootObj = new GameObject("FacilityCorridors");
        rootObj.transform.SetParent(transform, false);
        root = rootObj.transform;

        // Fetch materials
        var wallMat = Resources.Load<Material>("Mat_SovietGreenWalls");
        if (wallMat == null) wallMat = FindMatByName("Mat_SovietGreenWalls");
        var floorMat = FindMatByName("Mat_VintageDeskWood");
        var steelMat = FindMatByName("Mat_DarkCharcoalSteel");
        var redMat = FindMatByName("Mat_SovietAccentRed");
        var doorMat = FindMatByName("Mat_DoorWood");

        // Groups
        var floorsGroup = new GameObject("Floors").transform;
        floorsGroup.SetParent(root, false);

        var wallsGroup = new GameObject("Walls").transform;
        wallsGroup.SetParent(root, false);

        var doorsGroup = new GameObject("Doors_And_Checkpoints").transform;
        doorsGroup.SetParent(root, false);

        var lightsGroup = new GameObject("CeilingLamps").transform;
        lightsGroup.SetParent(root, false);

        float wallH = 3.4f;
        float wallThick = 0.25f;

        // ---------------------------------------------------------
        // 1. CORRIDOR SEGMENTS
        // ---------------------------------------------------------
        // A. Main Central Hallway (Runs along Z from -8 to +15 at X = 13.5)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(13.5f, 0.0f, 3.5f),
            size: new Vector3(3.0f, wallH, 23.0f),
            wallMat, floorMat, steelMat,
            openWestDoorAtZ: 2.43f); // connects to our lab door

        // B. North Cross Wing (Branches East from Main at Z = 13.5 to X = 25.0)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(19.5f, 0.0f, 13.5f),
            size: new Vector3(10.0f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // C. South Cross Wing (Branches East from Main at Z = -6.5 to X = 25.0)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(19.5f, 0.0f, -6.5f),
            size: new Vector3(10.0f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // D. East Perimeter Hallway (Connects North and South at X = 24.5 from Z = -7.5 to 14.5)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(24.5f, 0.0f, 3.5f),
            size: new Vector3(2.6f, wallH, 22.0f),
            wallMat, floorMat, steelMat);

        // E. West Back Maintenance Passage (Runs along Z = -1 to +9 at X = 4.0)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(4.0f, 0.0f, 3.5f),
            size: new Vector3(2.0f, wallH * 0.8f, 11.0f),
            wallMat, floorMat, steelMat);

        // ---------------------------------------------------------
        // 2. CHECKPOINTS & SECURITY PROPS
        // ---------------------------------------------------------
        CreateCheckpointGate(doorsGroup, new Vector3(24.5f, 0f, 7.5f), "КПП СЕВЕР", redMat, steelMat);
        CreateCheckpointGate(doorsGroup, new Vector3(24.5f, 0f, -0.5f), "КПП ЮГ", redMat, steelMat);
        CreateCheckpointGate(doorsGroup, new Vector3(13.5f, 0f, 9.5f), "СЕКТОР А", redMat, steelMat);
        CreateCheckpointGate(doorsGroup, new Vector3(13.5f, 0f, -2.5f), "СЕКТОР Б", redMat, steelMat);

        // ---------------------------------------------------------
        // 3. WAYPOINT ROUTES
        // ---------------------------------------------------------
        var wpRoot = new GameObject("PatrolWaypoints").transform;
        wpRoot.SetParent(root, false);

        routes.Clear();

        // ROUTE 1: Main Corridor & Lab Perimeter Loop (Yellow)
        var r1 = new PatrolRoute
        {
            routeName = "Маршрут 1: Коридор лабораторий и дверь ЭВМ",
            routeColor = new Color(1.0f, 0.85f, 0.2f),
            isLoop = true,
            waitTimeAtWaypoint = 2.0f
        };
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP0_DoorLab", new Vector3(13.5f, 0.03f, 2.43f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP1_MainNorth", new Vector3(13.5f, 0.03f, 7.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP2_NorthCorner", new Vector3(13.5f, 0.03f, 13.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP3_NorthBranch", new Vector3(18.5f, 0.03f, 13.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP4_NorthJunction", new Vector3(13.5f, 0.03f, 13.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP5_MainMid", new Vector3(13.5f, 0.03f, -1.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP6_MainSouth", new Vector3(13.5f, 0.03f, -6.5f)));
        routes.Add(r1);

        // ROUTE 2: Perimeter Security & Checkpoints (Red)
        var r2 = new PatrolRoute
        {
            routeName = "Маршрут 2: Периметр безопасности и КПП",
            routeColor = new Color(0.95f, 0.25f, 0.25f),
            isLoop = true,
            waitTimeAtWaypoint = 3.0f
        };
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP0_EastGateNorth", new Vector3(24.5f, 0.03f, 13.5f)));
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP1_EastMidGate", new Vector3(24.5f, 0.03f, 7.5f)));
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP2_EastCenter", new Vector3(24.5f, 0.03f, 3.5f)));
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP3_EastMidSouth", new Vector3(24.5f, 0.03f, -0.5f)));
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP4_EastGateSouth", new Vector3(24.5f, 0.03f, -6.5f)));
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP5_SouthCorridor", new Vector3(19.0f, 0.03f, -6.5f)));
        routes.Add(r2);

        // ROUTE 3: Back Maintenance Service Crawlway (Cyan)
        var r3 = new PatrolRoute
        {
            routeName = "Маршрут 3: Служебный тоннель за серверами",
            routeColor = new Color(0.2f, 0.85f, 0.95f),
            isLoop = true,
            waitTimeAtWaypoint = 1.5f
        };
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP0_ServiceSouth", new Vector3(4.0f, 0.03f, -1.5f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP1_ServiceBehindRack", new Vector3(4.0f, 0.03f, 3.5f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP2_ServiceNorth", new Vector3(4.0f, 0.03f, 8.5f)));
        routes.Add(r3);
    }

    private void CreateHallwaySection(Transform floors, Transform walls, Transform lights,
        Vector3 center, Vector3 size, Material wallMat, Material floorMat, Material steelMat,
        float openWestDoorAtZ = -999f)
    {
        // 1. Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = $"Floor_{center.x}_{center.z}";
        floor.transform.SetParent(floors, false);
        floor.transform.position = new Vector3(center.x, -0.05f, center.z);
        floor.transform.localScale = new Vector3(size.x, 0.1f, size.z);
        if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

        // 2. Ceiling
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = $"Ceiling_{center.x}_{center.z}";
        ceiling.transform.SetParent(walls, false);
        ceiling.transform.position = new Vector3(center.x, size.y + 0.05f, center.z);
        ceiling.transform.localScale = new Vector3(size.x, 0.1f, size.z);
        if (wallMat != null) ceiling.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // 3. Walls (Left & Right or North & South)
        float halfW = size.x * 0.5f;
        float halfL = size.z * 0.5f;

        if (size.z >= size.x) // Running along Z
        {
            // East Wall
            var wallE = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallE.name = "Wall_East";
            wallE.transform.SetParent(walls, false);
            wallE.transform.position = new Vector3(center.x + halfW, size.y * 0.5f, center.z);
            wallE.transform.localScale = new Vector3(0.2f, size.y, size.z);
            if (wallMat != null) wallE.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // West Wall (with optional doorway opening)
            if (openWestDoorAtZ > -900f)
            {
                float doorZ = openWestDoorAtZ;
                float seg1L = Mathf.Max(0.5f, doorZ - (center.z - halfL) - 0.7f);
                float seg2L = Mathf.Max(0.5f, (center.z + halfL) - doorZ - 0.7f);

                var wallW1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallW1.name = "Wall_West_South";
                wallW1.transform.SetParent(walls, false);
                wallW1.transform.position = new Vector3(center.x - halfW, size.y * 0.5f, (center.z - halfL) + seg1L * 0.5f);
                wallW1.transform.localScale = new Vector3(0.2f, size.y, seg1L);
                if (wallMat != null) wallW1.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

                var wallW2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallW2.name = "Wall_West_North";
                wallW2.transform.SetParent(walls, false);
                wallW2.transform.position = new Vector3(center.x - halfW, size.y * 0.5f, (center.z + halfL) - seg2L * 0.5f);
                wallW2.transform.localScale = new Vector3(0.2f, size.y, seg2L);
                if (wallMat != null) wallW2.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

                // Lintel over door
                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = "Wall_West_Lintel";
                lintel.transform.SetParent(walls, false);
                lintel.transform.position = new Vector3(center.x - halfW, size.y * 0.85f, doorZ);
                lintel.transform.localScale = new Vector3(0.2f, size.y * 0.3f, 1.4f);
                if (wallMat != null) lintel.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            }
            else
            {
                var wallW = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wallW.name = "Wall_West";
                wallW.transform.SetParent(walls, false);
                wallW.transform.position = new Vector3(center.x - halfW, size.y * 0.5f, center.z);
                wallW.transform.localScale = new Vector3(0.2f, size.y, size.z);
                if (wallMat != null) wallW.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            }
        }
        else // Running along X
        {
            // North Wall
            var wallN = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallN.name = "Wall_North";
            wallN.transform.SetParent(walls, false);
            wallN.transform.position = new Vector3(center.x, size.y * 0.5f, center.z + halfL);
            wallN.transform.localScale = new Vector3(size.x, size.y, 0.2f);
            if (wallMat != null) wallN.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // South Wall
            var wallS = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallS.name = "Wall_South";
            wallS.transform.SetParent(walls, false);
            wallS.transform.position = new Vector3(center.x, size.y * 0.5f, center.z - halfL);
            wallS.transform.localScale = new Vector3(size.x, size.y, 0.2f);
            if (wallMat != null) wallS.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        }

        // 4. Industrial Fluorescent Lamps along center
        float spacing = 5.5f;
        int count = Mathf.Max(1, Mathf.FloorToInt((size.x > size.z ? size.x : size.z) / spacing));
        for (int i = 0; i <= count; i++)
        {
            float t = (count == 0) ? 0.5f : (float)i / count;
            Vector3 lampPos;
            if (size.z >= size.x)
                lampPos = new Vector3(center.x, size.y - 0.15f, (center.z - halfL + 1.5f) + t * (size.z - 3f));
            else
                lampPos = new Vector3((center.x - halfW + 1.5f) + t * (size.x - 3f), size.y - 0.15f, center.z);

            var lampFixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lampFixture.name = $"LampFixture_{i}";
            lampFixture.transform.SetParent(lights, false);
            lampFixture.transform.position = lampPos;
            lampFixture.transform.localScale = new Vector3(0.25f, 0.08f, 1.2f);
            if (steelMat != null) lampFixture.GetComponent<MeshRenderer>().sharedMaterial = steelMat;
        }
    }

    private void CreateCheckpointGate(Transform parent, Vector3 pos, string label, Material redMat, Material steelMat)
    {
        var gateObj = new GameObject($"Checkpoint_{label}");
        gateObj.transform.SetParent(parent, false);
        gateObj.transform.position = pos;

        // Arch Frame Posts
        var postL = GameObject.CreatePrimitive(PrimitiveType.Cube);
        postL.transform.SetParent(gateObj.transform, false);
        postL.transform.localPosition = new Vector3(-1.1f, 1.3f, 0f);
        postL.transform.localScale = new Vector3(0.18f, 2.6f, 0.18f);
        if (steelMat != null) postL.GetComponent<MeshRenderer>().sharedMaterial = steelMat;

        var postR = GameObject.CreatePrimitive(PrimitiveType.Cube);
        postR.transform.SetParent(gateObj.transform, false);
        postR.transform.localPosition = new Vector3(1.1f, 1.3f, 0f);
        postR.transform.localScale = new Vector3(0.18f, 2.6f, 0.18f);
        if (steelMat != null) postR.GetComponent<MeshRenderer>().sharedMaterial = steelMat;

        // Top Header Beam with Warning Red
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.transform.SetParent(gateObj.transform, false);
        beam.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        beam.transform.localScale = new Vector3(2.4f, 0.28f, 0.22f);
        if (redMat != null) beam.GetComponent<MeshRenderer>().sharedMaterial = redMat;

        // Red Hazard Strip / Stanchion Barrier
        var barrier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrier.transform.SetParent(gateObj.transform, false);
        barrier.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        barrier.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        barrier.transform.localScale = new Vector3(0.06f, 1.0f, 0.06f);
        if (redMat != null) barrier.GetComponent<MeshRenderer>().sharedMaterial = redMat;
    }

    private Transform CreateWaypoint(Transform parent, string name, Vector3 pos)
    {
        var wpObj = new GameObject(name);
        wpObj.transform.SetParent(parent, false);
        wpObj.transform.position = pos;
        return wpObj.transform;
    }

    private Material FindMatByName(string name)
    {
#if UNITY_EDITOR
        var guids = UnityEditor.AssetDatabase.FindAssets($"{name} t:Material");
        if (guids != null && guids.Length > 0)
        {
            var p = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p);
        }
#endif
        return null;
    }
}

/// <summary>
/// Autonomous patrol pawn representing a stylized Soviet security guard.
/// Patrolling along waypoints with smooth turns, pauses, and a visual stylized model.
/// </summary>
public class GuardPawn : MonoBehaviour
{
    private GuardPatrolSystem.PatrolRoute route;
    private int currentWaypointIndex = 0;
    private float walkSpeed = 1.8f;
    private float turnSpeed = 180f;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    public void Initialize(GuardPatrolSystem.PatrolRoute assignedRoute, float speed, float turnSpd)
    {
        route = assignedRoute;
        walkSpeed = speed;
        turnSpeed = turnSpd;
        BuildGuardMesh();
    }

    private void Update()
    {
        if (route == null || route.waypoints == null || route.waypoints.Count == 0) return;

        var targetWp = route.waypoints[currentWaypointIndex];
        if (targetWp == null) return;

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                AdvanceToNextWaypoint();
            }
            return;
        }

        Vector3 targetPos = targetWp.position;
        targetPos.y = transform.position.y; // Keep on plane

        Vector3 toTarget = targetPos - transform.position;
        float dist = toTarget.magnitude;

        if (dist <= 0.35f)
        {
            isWaiting = true;
            waitTimer = route.waitTimeAtWaypoint;
            return;
        }

        // Smooth rotate toward waypoint
        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

        // Move forward
        transform.position += transform.forward * walkSpeed * Time.deltaTime;
    }

    private void AdvanceToNextWaypoint()
    {
        if (currentWaypointIndex < route.waypoints.Count - 1)
        {
            currentWaypointIndex++;
        }
        else if (route.isLoop)
        {
            currentWaypointIndex = 0;
        }
    }

    private void BuildGuardMesh()
    {
        var toonShader = Shader.Find("Custom/SovietToonLit");
        if (toonShader == null) toonShader = Shader.Find("Universal Render Pipeline/Lit");

        var coatMat = new Material(toonShader);
        coatMat.SetColor("_BaseColor", new Color(0.18f, 0.24f, 0.20f)); // Soviet security olive-grey overcoat

        var headMat = new Material(toonShader);
        headMat.SetColor("_BaseColor", new Color(0.85f, 0.72f, 0.60f)); // Flesh tone

        var capMat = new Material(toonShader);
        capMat.SetColor("_BaseColor", new Color(0.15f, 0.18f, 0.22f)); // Peaked cap dark navy

        var redMat = new Material(toonShader);
        redMat.SetColor("_BaseColor", new Color(0.82f, 0.12f, 0.12f)); // Red star / armband

        // Body Coat
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Coat";
        DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        body.transform.localScale = new Vector3(0.55f, 0.85f, 0.45f);
        body.GetComponent<MeshRenderer>().sharedMaterial = coatMat;

        // Head
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        DestroyImmediate(head.GetComponent<Collider>());
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
        head.transform.localScale = new Vector3(0.32f, 0.35f, 0.32f);
        head.GetComponent<MeshRenderer>().sharedMaterial = headMat;

        // Officer Peaked Cap
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = "PeakedCap";
        DestroyImmediate(cap.GetComponent<Collider>());
        cap.transform.SetParent(transform, false);
        cap.transform.localPosition = new Vector3(0f, 1.90f, 0.02f);
        cap.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
        cap.transform.localScale = new Vector3(0.38f, 0.08f, 0.42f);
        cap.GetComponent<MeshRenderer>().sharedMaterial = capMat;

        // Red Cockade / Star on Cap
        var star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        star.name = "CapStar";
        DestroyImmediate(star.GetComponent<Collider>());
        star.transform.SetParent(transform, false);
        star.transform.localPosition = new Vector3(0f, 1.92f, 0.22f);
        star.transform.localScale = new Vector3(0.06f, 0.06f, 0.04f);
        star.GetComponent<MeshRenderer>().sharedMaterial = redMat;

        // Flashlight beam cone (Semi-transparent yellow cone)
        var flash = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        flash.name = "Flashlight";
        DestroyImmediate(flash.GetComponent<Collider>());
        flash.transform.SetParent(transform, false);
        flash.transform.localPosition = new Vector3(0.24f, 1.05f, 0.25f);
        flash.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        flash.transform.localScale = new Vector3(0.06f, 0.16f, 0.06f);
        if (capMat != null) flash.GetComponent<MeshRenderer>().sharedMaterial = capMat;
    }
}
