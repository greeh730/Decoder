using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        public float waitTimeAtWaypoint = 2.0f;
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
        var existingGuards = FindObjectsByType<GuardPawn>(FindObjectsInactive.Include);
        if (existingGuards != null)
        {
            foreach (var g in existingGuards)
            {
                if (g == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(g.gameObject);
                else Destroy(g.gameObject);
#else
                Destroy(g.gameObject);
#endif
            }
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

        // ---------------------------------------------------------
        // 1. CORRIDOR SEGMENTS (EXPANDED FACILITY NETWORK: ~114m Loop)
        // ---------------------------------------------------------

        // A. Main Central Hallway (Runs along Z from -22.0 to +28.0 at X = 13.5, Length: 50.0m)
        // Doorway into vestibule is centered at Z = -1.5m with width 2.4m
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(13.5f, 0.0f, 3.0f),
            size: new Vector3(3.0f, wallH, 50.0f),
            wallMat, floorMat, steelMat,
            openWestDoorAtZ: -1.5f);

        // B. Lab Entrance Vestibule (Connects MetalDoor3 at (9.67, 1.09, -0.52) to Main Hallway at X = 12.0)
        CreateVestibuleSection(floorsGroup, wallsGroup, lightsGroup, wallMat, floorMat, steelMat, wallH);

        // C. North Wing (Branches East from Main at Z = 26.0 to X = 24.5, Length: 11.0m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(19.0f, 0.0f, 26.0f),
            size: new Vector3(11.0f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // D. South Wing (Branches East from Main at Z = -20.0 to X = 24.5, Length: 11.0m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(19.0f, 0.0f, -20.0f),
            size: new Vector3(11.0f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // E. Mid North Cross (Research Branch at Z = 13.5 from X = 13.5 to X = 24.5)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(19.0f, 0.0f, 13.5f),
            size: new Vector3(11.0f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // F. Mid South Cross (Archive Branch at Z = -6.5 from X = 13.5 to X = 24.5)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(19.0f, 0.0f, -6.5f),
            size: new Vector3(11.0f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // G. East Perimeter Hallway (Connects North and South at X = 24.5 from Z = -21.0 to +27.0, Length: 48.0m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(24.5f, 0.0f, 3.0f),
            size: new Vector3(2.6f, wallH, 48.0f),
            wallMat, floorMat, steelMat);

        // H. West Back Maintenance Passage (Runs along Z outside comnata at X = 3.08 from Z = -2.0 to +9.0)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(3.08f, 0.0f, 3.5f),
            size: new Vector3(2.0f, wallH * 0.8f, 11.0f),
            wallMat, floorMat, steelMat);

        // ---------------------------------------------------------
        // 2. CHECKPOINTS & SECURITY PROPS
        // ---------------------------------------------------------
        CreateCheckpointGate(doorsGroup, new Vector3(13.5f, 0f, 15.0f), "СЕКТОР А", redMat, steelMat);
        CreateCheckpointGate(doorsGroup, new Vector3(13.5f, 0f, -7.0f), "СЕКТОР Б", redMat, steelMat);
        CreateCheckpointGate(doorsGroup, new Vector3(24.5f, 0f, 20.0f), "КПП СЕВЕР", redMat, steelMat);
        CreateCheckpointGate(doorsGroup, new Vector3(24.5f, 0f, -14.0f), "КПП ЮГ", redMat, steelMat);

        // ---------------------------------------------------------
        // 3. WAYPOINT ROUTES
        // ---------------------------------------------------------
        var wpRoot = new GameObject("PatrolWaypoints").transform;
        wpRoot.SetParent(root, false);

        routes.Clear();

        // ROUTE 1: Expanded Facility Perimeter Circuit (~114m Loop)
        // Guard walks past the lab vestibule briefly once every ~80 seconds!
        var r1 = new PatrolRoute
        {
            routeName = "Маршрут 1: Периметр безопасности и сектор ЭВМ",
            routeColor = new Color(1.0f, 0.85f, 0.2f),
            isLoop = true,
            waitTimeAtWaypoint = 1.8f
        };
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP0_DoorLab", new Vector3(13.5f, 0.03f, -1.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP1_SectorB", new Vector3(13.5f, 0.03f, -7.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP2_MainSouth", new Vector3(13.5f, 0.03f, -14.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP3_GeneratorSouth", new Vector3(13.5f, 0.03f, -20.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP4_SouthCross", new Vector3(19.0f, 0.03f, -20.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP5_EastSouthJunction", new Vector3(24.5f, 0.03f, -20.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP6_EastMidSouth", new Vector3(24.5f, 0.03f, -6.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP7_EastCenter", new Vector3(24.5f, 0.03f, 3.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP8_EastMidNorth", new Vector3(24.5f, 0.03f, 13.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP9_EastNorthJunction", new Vector3(24.5f, 0.03f, 26.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP10_NorthCross", new Vector3(19.0f, 0.03f, 26.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP11_MainNorth", new Vector3(13.5f, 0.03f, 26.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP12_SectorA", new Vector3(13.5f, 0.03f, 15.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP13_MainApproach", new Vector3(13.5f, 0.03f, 6.0f)));
        routes.Add(r1);

        // ROUTE 2: Archive & Technical Perimeter (Red)
        var r2 = new PatrolRoute
        {
            routeName = "Маршрут 2: Технический архив и КПП ЮГ",
            routeColor = new Color(0.95f, 0.25f, 0.25f),
            isLoop = true,
            waitTimeAtWaypoint = 2.5f
        };
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP0_EastGateNorth", new Vector3(24.5f, 0.03f, 13.5f)));
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP1_EastCenter", new Vector3(24.5f, 0.03f, 3.5f)));
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP2_EastMidSouth", new Vector3(24.5f, 0.03f, -6.5f)));
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP3_EastSouthGate", new Vector3(24.5f, 0.03f, -14.0f)));
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP4_SouthCrossArchive", new Vector3(19.0f, 0.03f, -6.5f)));
        routes.Add(r2);

        // ROUTE 3: Back Maintenance Service Crawlway (Cyan)
        var r3 = new PatrolRoute
        {
            routeName = "Маршрут 3: Служебный тоннель за серверами",
            routeColor = new Color(0.2f, 0.85f, 0.95f),
            isLoop = true,
            waitTimeAtWaypoint = 1.5f
        };
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP0_ServiceSouth", new Vector3(3.08f, 0.03f, -1.5f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP1_ServiceBehindRack", new Vector3(3.08f, 0.03f, 3.5f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP2_ServiceNorth", new Vector3(3.08f, 0.03f, 8.5f)));
        routes.Add(r3);
    }

    private void CreateVestibuleSection(Transform floors, Transform walls, Transform lights,
        Material wallMat, Material floorMat, Material steelMat, float wallH)
    {
        // 1. Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "VestibuleFloor";
        floor.transform.SetParent(floors, false);
        floor.transform.position = new Vector3(10.60f, -0.05f, -1.50f);
        floor.transform.localScale = new Vector3(3.40f, 0.10f, 2.50f);
        if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

        // 2. Ceiling
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "VestibuleCeiling";
        ceiling.transform.SetParent(walls, false);
        ceiling.transform.position = new Vector3(10.60f, wallH + 0.05f, -1.50f);
        ceiling.transform.localScale = new Vector3(3.40f, 0.10f, 2.50f);
        if (wallMat != null) ceiling.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // 3. South Wall
        var wallS = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallS.name = "VestibuleWall_South";
        wallS.transform.SetParent(walls, false);
        wallS.transform.position = new Vector3(10.60f, wallH * 0.5f, -2.75f);
        wallS.transform.localScale = new Vector3(3.40f, wallH, 0.20f);
        if (wallMat != null) wallS.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // 4. West Wall (from Z = -2.75 to Z = -0.52 at X = 8.90)
        var wallW = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallW.name = "VestibuleWall_West";
        wallW.transform.SetParent(walls, false);
        wallW.transform.position = new Vector3(8.90f, wallH * 0.5f, -1.635f);
        wallW.transform.localScale = new Vector3(0.20f, wallH, 2.27f);
        if (wallMat != null) wallW.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // 5. Lamp
        var lampFixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lampFixture.name = "VestibuleLamp";
        lampFixture.transform.SetParent(lights, false);
        lampFixture.transform.position = new Vector3(10.60f, wallH - 0.15f, -1.50f);
        lampFixture.transform.localScale = new Vector3(0.25f, 0.08f, 1.20f);
        if (steelMat != null) lampFixture.GetComponent<MeshRenderer>().sharedMaterial = steelMat;
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
                float doorOpeningW = 2.4f;
                float halfOpening = doorOpeningW * 0.5f;

                float seg1L = Mathf.Max(0.5f, (doorZ - halfOpening) - (center.z - halfL));
                float seg2L = Mathf.Max(0.5f, (center.z + halfL) - (doorZ + halfOpening));

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
                lintel.transform.localScale = new Vector3(0.2f, size.y * 0.3f, doorOpeningW);
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

        // Red Hazard Strip / Stanchion Barrier (Trigger so pathing isn't blocked)
        var barrier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrier.transform.SetParent(gateObj.transform, false);
        barrier.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        barrier.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        barrier.transform.localScale = new Vector3(0.06f, 1.0f, 0.06f);
        var col = barrier.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
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
/// Autonomous Soviet Security Guard Pawn.
/// Features:
/// 1. Long perimeter patrol loop keeping guard away 90% of the time.
/// 2. Early footstep warning audio (audible at 17.5m, while hearing range is 9.5m).
/// 3. Door breach sequence: walks to MetalDoor3, swings door open, enters room.
/// 4. Chases player inside room; upon touching player triggers Soviet Game Over!
/// </summary>
public class GuardPawn : MonoBehaviour
{
    public enum GuardState
    {
        Patrolling,
        InvestigatingSuspicious,
        BreachMovingToDoor,
        BreachOpeningDoor,
        BreachEnteringRoom,
        ChasingPlayer,
        GameOver
    }

    public static bool IsGameOver { get; private set; } = false;
    public static bool IsCheatPeaceMode { get; private set; } = false;

    public static void ToggleCheatPeaceMode()
    {
        IsCheatPeaceMode = !IsCheatPeaceMode;

        var guards = FindObjectsByType<GuardPawn>(FindObjectsInactive.Include);
        if (guards != null)
        {
            foreach (var g in guards)
            {
                if (g == null) continue;
                g.suspicion = 0f;
                if (IsCheatPeaceMode && (g.currentState == GuardState.BreachMovingToDoor || 
                                         g.currentState == GuardState.BreachOpeningDoor || 
                                         g.currentState == GuardState.BreachEnteringRoom || 
                                         g.currentState == GuardState.ChasingPlayer || 
                                         g.currentState == GuardState.InvestigatingSuspicious))
                {
                    g.currentState = GuardState.Patrolling;
                }
            }
        }

        if (IsCheatPeaceMode)
        {
            MainframeBootQuest.Instance?.ShowNotification("<color=#00FFFF>[ЧИТ-КОД АКТИВИРОВАН: GreeH]</color> Режим неприкосновенности! Охрана нейтрализована.");
        }
        else
        {
            MainframeBootQuest.Instance?.ShowNotification("<color=#FFCC00>[ЧИТ-КОД ДЕАКТИВИРОВАН: GreeH]</color> Патруль вернулся в штатный режим.");
        }
    }

    private GuardPatrolSystem.PatrolRoute route;
    private int currentWaypointIndex = 0;
    private float walkSpeed = 1.8f;
    private float turnSpeed = 180f;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    // State machine
    private GuardState currentState = GuardState.Patrolling;

    // Door and room breach points
    private readonly Vector3 vestibuleEntrancePos = new Vector3(12.2f, 0.0f, -1.50f);
    private readonly Vector3 outsideDoorPos = new Vector3(9.67f, 0.0f, -1.25f);
    private readonly Vector3 doorwayThresholdPos = new Vector3(9.67f, 0.0f, 0.30f);
    private readonly Vector3 roomSearchPos = new Vector3(8.50f, 0.0f, 1.80f);
    private readonly Vector3 labDoorCoord = new Vector3(9.67f, 0.0f, -0.52f);

    private Transform doorHingePivot;
    private float doorOpenProgress = 0f;
    private float suspicion = 0f;
    private float lastAlertNotifyTime = -10f;
    private float stepTimer = 0f;
    private bool isLeftStep = false;

    // Procedural Audio
    private AudioSource footstepAudioSource;
    private AudioSource sfxAudioSource;
    private AudioClip footstepLeftClip;
    private AudioClip footstepRightClip;
    private AudioClip doorCreakClip;
    private AudioClip alertStingClip;

    public float Suspicion => suspicion;
    public bool IsInvestigating => suspicion > 0.35f;
    public bool IsHighAlert => suspicion >= 0.85f;
    public GuardState CurrentState => currentState;

    private void Awake()
    {
        IsGameOver = false;
        SetupAudio();
        FindDoorHingePivot();
    }

    public void Initialize(GuardPatrolSystem.PatrolRoute assignedRoute, float speed, float turnSpd)
    {
        route = assignedRoute;
        walkSpeed = speed;
        turnSpeed = turnSpd;
        SetupAudio();
        FindDoorHingePivot();
        BuildGuardMesh();
    }

    private void FindDoorHingePivot()
    {
        if (doorHingePivot != null) return;

        var pivotObj = GameObject.Find("DoorHingePivot");
        if (pivotObj != null)
        {
            doorHingePivot = pivotObj.transform;
            return;
        }

        var door = GameObject.Find("MetalDoor3");
        if (door != null && door.transform.parent != null && door.transform.parent.name == "DoorHingePivot")
        {
            doorHingePivot = door.transform.parent;
        }
    }

    private void SetupAudio()
    {
        if (footstepAudioSource != null) return;

        // 1. Footstep 3D AudioSource (audible up to 17.5m, well before guard hearing range 9.5m)
        var footstepObj = new GameObject("GuardFootstepAudio");
        footstepObj.transform.SetParent(transform, false);
        footstepObj.transform.localPosition = Vector3.zero;

        footstepAudioSource = footstepObj.AddComponent<AudioSource>();
        footstepAudioSource.spatialBlend = 1.0f; // 3D sound
        footstepAudioSource.rolloffMode = AudioRolloffMode.Linear;
        footstepAudioSource.minDistance = 2.5f;
        footstepAudioSource.maxDistance = 17.5f;
        footstepAudioSource.volume = 0.95f;
        footstepAudioSource.playOnAwake = false;

        // 2. SFX AudioSource (creak, sting)
        var sfxObj = new GameObject("GuardSFXAudio");
        sfxObj.transform.SetParent(transform, false);
        sfxObj.transform.localPosition = Vector3.up * 1.5f;

        sfxAudioSource = sfxObj.AddComponent<AudioSource>();
        sfxAudioSource.spatialBlend = 0.5f;
        sfxAudioSource.rolloffMode = AudioRolloffMode.Linear;
        sfxAudioSource.minDistance = 3.0f;
        sfxAudioSource.maxDistance = 25.0f;
        sfxAudioSource.volume = 1.0f;
        sfxAudioSource.playOnAwake = false;

        // Generate procedural audio clips
        CreateFootstepClips();
        CreateDoorCreakClip();
        CreateAlertStingClip();
    }

    private void CreateFootstepClips()
    {
        int sampleRate = 44100;

        // Clip 1: Left boot heel click & concrete scrape
        int len1 = (int)(sampleRate * 0.19f);
        float[] d1 = new float[len1];
        System.Random r1 = new System.Random(1337);
        for (int i = 0; i < len1; i++)
        {
            float t = (float)i / sampleRate;
            float thud = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 32f);
            float slap = Mathf.Sin(2f * Mathf.PI * 170f * t) * Mathf.Exp(-t * 45f);
            float crunch = ((float)r1.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 38f);
            d1[i] = Mathf.Clamp(thud * 0.50f + slap * 0.35f + crunch * 0.25f, -1f, 1f);
        }
        footstepLeftClip = AudioClip.Create("GuardBoot_L", len1, 1, sampleRate, false);
        footstepLeftClip.SetData(d1, 0);

        // Clip 2: Right boot sole impact thud
        int len2 = (int)(sampleRate * 0.21f);
        float[] d2 = new float[len2];
        System.Random r2 = new System.Random(4242);
        for (int i = 0; i < len2; i++)
        {
            float t = (float)i / sampleRate;
            float thud = Mathf.Sin(2f * Mathf.PI * 72f * t) * Mathf.Exp(-t * 26f);
            float slap = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 40f);
            float crunch = ((float)r2.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 42f);
            d2[i] = Mathf.Clamp(thud * 0.58f + slap * 0.28f + crunch * 0.20f, -1f, 1f);
        }
        footstepRightClip = AudioClip.Create("GuardBoot_R", len2, 1, sampleRate, false);
        footstepRightClip.SetData(d2, 0);
    }

    private void CreateDoorCreakClip()
    {
        int sampleRate = 44100;
        int len = (int)(sampleRate * 1.10f);
        float[] d = new float[len];
        System.Random rnd = new System.Random(777);

        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sampleRate;
            // Swept metallic groan
            float freq = Mathf.Lerp(190f, 310f, Mathf.Sin(t * 8f) * 0.5f + 0.5f);
            float groan = Mathf.Sin(2f * Mathf.PI * freq * t);
            // Gritty distortion friction
            float rustle = ((float)rnd.NextDouble() * 2f - 1f) * 0.22f;
            float env = Mathf.Sin(Mathf.Clamp01(t / 1.10f) * Mathf.PI);
            d[i] = Mathf.Clamp((groan * 0.65f + rustle) * env, -1f, 1f);
        }
        doorCreakClip = AudioClip.Create("GuardDoorCreak", len, 1, sampleRate, false);
        doorCreakClip.SetData(d, 0);
    }

    private void CreateAlertStingClip()
    {
        int sampleRate = 44100;
        int len = (int)(sampleRate * 1.4f);
        float[] d = new float[len];

        for (int i = 0; i < len; i++)
        {
            float t = (float)i / sampleRate;
            // Dissonant brass chord (110Hz + 155Hz + 220Hz + 311Hz)
            float c1 = Mathf.Sin(2f * Mathf.PI * 110f * t);
            float c2 = Mathf.Sin(2f * Mathf.PI * 155.5f * t);
            float c3 = Mathf.Sin(2f * Mathf.PI * 220f * t);
            float c4 = Mathf.Sin(2f * Mathf.PI * 311f * t);
            float chord = (c1 + c2 * 0.8f + c3 * 0.6f + c4 * 0.5f) * 0.35f;
            float punch = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 12f) * 0.45f;
            float env = Mathf.Exp(-t * 2.2f);
            d[i] = Mathf.Clamp((chord + punch) * env, -1f, 1f);
        }
        alertStingClip = AudioClip.Create("GuardAlertSting", len, 1, sampleRate, false);
        alertStingClip.SetData(d, 0);
    }

    private void Update()
    {
        if (IsGameOver) return;

        if (IsCheatPeaceMode)
        {
            suspicion = 0f;
            if (currentState != GuardState.Patrolling)
            {
                currentState = GuardState.Patrolling;
            }
            PatrolWaypoints();
            return;
        }

        UpdateStealthDetection();

        if (currentState >= GuardState.BreachMovingToDoor)
        {
            UpdateBreach();
            return;
        }

        if (suspicion > 0.40f)
        {
            currentState = GuardState.InvestigatingSuspicious;
            InvestigateDoorSuspicious();
            return;
        }

        currentState = GuardState.Patrolling;
        PatrolWaypoints();
    }

    private void UpdateStealthDetection()
    {
        float distToDoor = Vector3.Distance(transform.position, labDoorCoord);

        // 1. Acoustic detection (Mainframe 50Hz hum / cooling fans)
        bool isNoiseActive = MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsNoiseActive;
        float noiseRad = MainframeBootQuest.Instance != null ? MainframeBootQuest.Instance.NoiseRadius : 9.5f;
        bool hearsNoise = isNoiseActive && distToDoor <= noiseRad;

        // 2. Running noise (Player sprinting inside lab room)
        bool playerRunning = PlayerController.Instance != null && PlayerController.Instance.IsRunning && distToDoor <= 11.5f;

        // 3. Optical detection (CRT screen green phosphor glow)
        bool isLightActive = MonitorTerminal.Instance != null && MonitorTerminal.Instance.IsLightEmitting;
        float lightDist = MonitorTerminal.Instance != null ? MonitorTerminal.Instance.LightEmissionDistance : 7.0f;
        bool inMainHallway = Mathf.Abs(transform.position.x - 13.5f) < 2.5f && transform.position.z >= -10f && transform.position.z <= 12f;
        bool seesLight = isLightActive && inMainHallway && distToDoor <= lightDist;

        // 4. Suspicion Rate
        if (hearsNoise && seesLight)
        {
            suspicion += 0.75f * Time.deltaTime;
        }
        else if (hearsNoise)
        {
            float prox = 1.0f - Mathf.Clamp01(distToDoor / noiseRad);
            suspicion += (0.42f + prox * 0.45f) * Time.deltaTime;
        }
        else if (playerRunning)
        {
            suspicion += 0.50f * Time.deltaTime;
        }
        else if (seesLight)
        {
            float brightness = MonitorTerminal.Instance != null ? MonitorTerminal.Instance.BrightnessLevel : 1.0f;
            suspicion += (brightness > 0.6f ? 0.30f : 0.15f) * Time.deltaTime;
        }
        else
        {
            // Silent & calm decay
            suspicion = Mathf.Max(0f, suspicion - 0.20f * Time.deltaTime);
        }

        suspicion = Mathf.Clamp01(suspicion);

        // High Alert Trigger: Breach door sequence
        if (suspicion >= 0.85f && currentState < GuardState.BreachMovingToDoor)
        {
            StartBreachSequence();
        }
    }

    private void StartBreachSequence()
    {
        currentState = GuardState.BreachMovingToDoor;
        FindDoorHingePivot();

        if (Time.time - lastAlertNotifyTime > 8.0f)
        {
            lastAlertNotifyTime = Time.time;
            MainframeBootQuest.Instance?.ShowNotification("<color=#FF3333>[⚠ ОХРАНА У ДВЕРИ!]</color> «Кто там шумит в машинном зале?! Откройте!»");
        }
    }

    private void UpdateBreach()
    {
        switch (currentState)
        {
            case GuardState.BreachMovingToDoor:
                // If in main hallway, first move to vestibule entrance
                if (transform.position.x > 12.0f)
                {
                    Vector3 toVestibule = vestibuleEntrancePos - transform.position;
                    toVestibule.y = 0f;
                    if (toVestibule.magnitude > 0.35f)
                    {
                        MoveTowards(vestibuleEntrancePos, walkSpeed * 1.15f);
                        return;
                    }
                }

                // Move directly to outside door position
                Vector3 toDoor = outsideDoorPos - transform.position;
                toDoor.y = 0f;
                if (toDoor.magnitude > 0.35f)
                {
                    MoveTowards(outsideDoorPos, walkSpeed * 1.15f);
                }
                else
                {
                    // Reached outside door! Begin opening!
                    currentState = GuardState.BreachOpeningDoor;
                    doorOpenProgress = 0f;

                    if (sfxAudioSource != null && doorCreakClip != null)
                    {
                        sfxAudioSource.PlayOneShot(doorCreakClip, 1.0f);
                    }

                    MainframeBootQuest.Instance?.ShowNotification("<color=#FF2222>[⚠ ДВЕРЬ ВЗЛОМАНА!]</color> «Служба безопасности! Всем оставаться на местах!»");
                }
                break;

            case GuardState.BreachOpeningDoor:
                // Face the door (pointing +Z into room)
                Quaternion faceDoor = Quaternion.LookRotation(Vector3.forward);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, faceDoor, turnSpeed * Time.deltaTime);

                // Swing door open
                doorOpenProgress += Time.deltaTime * 0.9f; // opens in ~1.1s
                float openAngle = Mathf.Lerp(0f, -90f, Mathf.Clamp01(doorOpenProgress));

                if (doorHingePivot != null)
                {
                    doorHingePivot.localRotation = Quaternion.Euler(0f, openAngle, 0f);
                }

                if (doorOpenProgress >= 1.0f)
                {
                    currentState = GuardState.BreachEnteringRoom;
                }
                break;

            case GuardState.BreachEnteringRoom:
                // Step across threshold into the room
                Vector3 toThresh = doorwayThresholdPos - transform.position;
                toThresh.y = 0f;
                if (toThresh.magnitude > 0.35f)
                {
                    MoveTowards(doorwayThresholdPos, walkSpeed * 1.1f);
                }
                else
                {
                    currentState = GuardState.ChasingPlayer;
                }
                break;

            case GuardState.ChasingPlayer:
                if (GameVictorySystem.IsGameWon)
                {
                    currentState = GuardState.Patrolling;
                    suspicion = 0f;
                    break;
                }
                var player = PlayerController.Instance != null ? PlayerController.Instance.transform : Camera.main?.transform;
                if (player != null)
                {
                    Vector3 pPos = player.position;
                    pPos.y = transform.position.y;
                    float distToPlayer = Vector3.Distance(transform.position, pPos);

                    if (distToPlayer <= 1.25f)
                    {
                        TriggerGameOver();
                    }
                    else
                    {
                        MoveTowards(pPos, walkSpeed * 1.25f);
                    }
                }
                break;
        }
    }

    private void InvestigateDoorSuspicious()
    {
        Vector3 toDoor = labDoorCoord - transform.position;
        toDoor.y = 0f;

        if (toDoor.sqrMagnitude > 0.01f)
        {
            Quaternion lookRot = Quaternion.LookRotation(toDoor.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRot, (turnSpeed * 0.7f) * Time.deltaTime);
        }

        transform.position += transform.forward * (walkSpeed * 0.40f) * Time.deltaTime;
        UpdateFootstepSounds(walkSpeed * 0.40f);
    }

    private void PatrolWaypoints()
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
        targetPos.y = transform.position.y;

        Vector3 toTarget = targetPos - transform.position;
        float dist = toTarget.magnitude;

        if (dist <= 0.35f)
        {
            isWaiting = true;
            waitTimer = route.waitTimeAtWaypoint;
            return;
        }

        MoveTowards(targetPos, walkSpeed);
    }

    private void MoveTowards(Vector3 targetPos, float speed)
    {
        targetPos.y = transform.position.y;
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            transform.position += transform.forward * (speed * Time.deltaTime);

            UpdateFootstepSounds(speed);
        }
    }

    private void UpdateFootstepSounds(float currentSpeed)
    {
        stepTimer += Time.deltaTime * (currentSpeed / walkSpeed);
        if (stepTimer >= 0.52f)
        {
            stepTimer = 0f;
            isLeftStep = !isLeftStep;
            var clip = isLeftStep ? footstepLeftClip : footstepRightClip;
            if (footstepAudioSource != null && clip != null)
            {
                footstepAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                footstepAudioSource.PlayOneShot(clip, 0.95f);
            }
        }
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

    public void TriggerGameOver()
    {
        if (IsCheatPeaceMode || IsGameOver || GameVictorySystem.IsGameWon) return;
        IsGameOver = true;
        currentState = GuardState.GameOver;

        if (sfxAudioSource != null && alertStingClip != null)
        {
            sfxAudioSource.PlayOneShot(alertStingClip, 1.0f);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void RestartGame()
    {
        IsGameOver = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnGUI()
    {
        // Active cheat mode indicator badge in top right corner
        if (IsCheatPeaceMode && !IsGameOver)
        {
            var badgeStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            badgeStyle.normal.textColor = new Color(0.2f, 1.0f, 0.85f);
            GUI.color = new Color(0.08f, 0.18f, 0.12f, 0.85f);
            GUI.Box(new Rect(Screen.width - 245f, 12f, 230f, 28f), "★ ЧИТ: GreeH [ОХРАНА OFF]", badgeStyle);
            GUI.color = Color.white;
        }

        if (!IsGameOver) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        float screenW = Screen.width;
        float screenH = Screen.height;

        // Dark red tinted vignette overlay
        GUI.color = new Color(0.06f, 0.01f, 0.01f, 0.93f);
        GUI.DrawTexture(new Rect(0, 0, screenW, screenH), Texture2D.whiteTexture);

        // Frame
        float boxW = Mathf.Min(680f, screenW - 40f);
        float boxH = 430f;
        float boxX = (screenW - boxW) * 0.5f;
        float boxY = (screenH - boxH) * 0.5f;

        // Border Box
        GUI.color = new Color(0.85f, 0.15f, 0.15f, 0.95f);
        GUI.Box(new Rect(boxX, boxY, boxW, boxH), "");

        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(boxX + 25f, boxY + 22f, boxW - 50f, boxH - 44f));

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenW * 0.022f, 20f, 30f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1.0f, 0.25f, 0.25f);

        var subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenW * 0.013f, 13f, 17f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = new Color(1.0f, 0.85f, 0.35f);

        var bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenW * 0.012f, 12f, 15f)),
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        bodyStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);

        GUILayout.Label("ЗАДЕРЖАН СЛУЖБОЙ БЕЗОПАСНОСТИ", titleStyle);
        GUILayout.Space(6f);
        GUILayout.Label("СЕКРЕТНЫЙ ОБЪЕКТ «ВЕКТОР-74» — НАРУШЕНИЕ РЕЖИМА", subStyle);
        GUILayout.Space(18f);

        GUILayout.Label(
            "Дежурный патруль вошёл в вычислительный зал через служебную дверь и задержал вас на месте преступления.\n\n" +
            "Несанкционированный шум аппаратуры ЭВМ и взлом шифра были зафиксированы комендатурой.\n" +
            "Вся аппаратура опечатана. Смена сорвана.",
            bodyStyle
        );

        GUILayout.FlexibleSpace();

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenW * 0.014f, 14f, 18f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        btnStyle.normal.textColor = Color.white;

        if (GUILayout.Button("[ R ]   ПЕРЕЗАПУСТИТЬ СМЕНУ", btnStyle, GUILayout.Height(46f)))
        {
            RestartGame();
        }

        GUILayout.Space(8f);
        GUILayout.EndArea();
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
