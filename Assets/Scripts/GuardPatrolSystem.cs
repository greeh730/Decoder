﻿using System;
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

    public static void TriggerTimeExpiredAssault() => GuardPawn.TriggerTimeExpiredAssault();

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
        // 1. CORRIDOR SEGMENTS (EXPANDED WEST FACILITY & CENTRAL COMPLEX)
        // ---------------------------------------------------------

        // A. West Main Hallway (Runs along Z at X = -8.0 from Z = -22.0 to +28.0, Length: 50.0m, Width: 3.0m)
        // Spacious corridor 14+ meters away from the server wall.
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(-8.0f, 0.0f, 3.0f),
            size: new Vector3(3.0f, wallH, 50.0f),
            wallMat, floorMat, steelMat,
            eastOpeningsAtZ: new List<float> { 26.0f, 13.5f, -1.65f, -10.0f, -20.0f },
            closeStart: true, closeEnd: true);

        // B. West-to-Door Direct Approach Corridor (Runs along X at Z = -1.65 from X = -6.5 to X = 8.90, Length: 15.4m, Width: 2.2m)
        // Connects West Main Hallway directly to the Lab Vestibule and outside MetalDoor3!
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(1.20f, 0.0f, -1.65f),
            size: new Vector3(15.4f, wallH, 2.20f),
            wallMat, floorMat, steelMat);

        // C. Lab Entrance Vestibule (Spans X = 8.90 to 12.00 at Z = -1.65, Length: 3.10m, Width: 2.40m)
        // Connects West Approach Corridor on West, Central Hallway on East, and MetalDoor3 on North.
        CreateVestibuleSection(floorsGroup, wallsGroup, lightsGroup, wallMat, floorMat, steelMat, wallH);

        // D. Main Central Hallway (Runs along Z at X = 13.5 from Z = -22.0 to +28.0, Length: 50.0m, Width: 3.0m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(13.5f, 0.0f, 3.0f),
            size: new Vector3(3.0f, wallH, 50.0f),
            wallMat, floorMat, steelMat,
            westOpeningsAtZ: new List<float> { 26.0f, -1.65f, -20.0f },
            eastOpeningsAtZ: new List<float> { 26.0f, 13.5f, -6.5f, -20.0f },
            closeStart: true, closeEnd: true);

        // E. East Perimeter Hallway (Runs along Z at X = 24.5 from Z = -22.0 to +28.0, Length: 50.0m, Width: 2.6m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(24.5f, 0.0f, 3.0f),
            size: new Vector3(2.6f, wallH, 50.0f),
            wallMat, floorMat, steelMat,
            westOpeningsAtZ: new List<float> { 26.0f, 13.5f, -6.5f, -20.0f },
            closeStart: true, closeEnd: true);

        // F. North Concourse - West Segment (Runs along X at Z = 26.0 from X = -6.5 to X = 12.0, Length: 18.5m, Width: 2.8m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(2.75f, 0.0f, 26.0f),
            size: new Vector3(18.5f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // G. North Concourse - East Segment / North Wing (Runs along X at Z = 26.0 from X = 15.0 to X = 23.2, Length: 8.2m, Width: 2.8m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(19.1f, 0.0f, 26.0f),
            size: new Vector3(8.2f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // H. South Concourse - West Segment (Runs along X at Z = -20.0 from X = -6.5 to X = 12.0, Length: 18.5m, Width: 2.8m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(2.75f, 0.0f, -20.0f),
            size: new Vector3(18.5f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // I. South Concourse - East Segment / South Wing (Runs along X at Z = -20.0 from X = 15.0 to X = 23.2, Length: 8.2m, Width: 2.8m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(19.1f, 0.0f, -20.0f),
            size: new Vector3(8.2f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // J. East Mid-North Cross (Research Branch at Z = 13.5 from X = 15.0 to X = 23.2, Length: 8.2m, Width: 2.8m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(19.1f, 0.0f, 13.5f),
            size: new Vector3(8.2f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // K. East Mid-South Cross (Archive Branch at Z = -6.5 from X = 15.0 to X = 23.2, Length: 8.2m, Width: 2.8m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(19.1f, 0.0f, -6.5f),
            size: new Vector3(8.2f, wallH, 2.8f),
            wallMat, floorMat, steelMat);

        // L. West Technical Lab Bay (Branches East from West Main at Z = 13.5 from X = -6.5 to X = 0.5, Length: 7.0m, Width: 2.8m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(-3.0f, 0.0f, 13.5f),
            size: new Vector3(7.0f, wallH, 2.8f),
            wallMat, floorMat, steelMat,
            closeEnd: true);

        // M. West Substation Wing (Branches East from West Main at Z = -10.0 from X = -6.5 to X = -1.5, Length: 5.0m, Width: 2.8m)
        CreateHallwaySection(floorsGroup, wallsGroup, lightsGroup,
            center: new Vector3(-4.0f, 0.0f, -10.0f),
            size: new Vector3(5.0f, wallH, 2.8f),
            wallMat, floorMat, steelMat,
            closeEnd: true);

        // ---------------------------------------------------------
        // 2. CHECKPOINTS & SECURITY PROPS
        // ---------------------------------------------------------
        CreateCheckpointGate(doorsGroup, new Vector3(-8.0f, 0f, 10.0f), "КПП ЗАПАД", redMat, steelMat);
        CreateCheckpointGate(doorsGroup, new Vector3(1.20f, 0f, -1.65f), "ТЕХСЕКТОР 3", redMat, steelMat);
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
        var r1 = new PatrolRoute
        {
            routeName = "Маршрут 1: Периметр безопасности и сектор ЭВМ",
            routeColor = new Color(1.0f, 0.85f, 0.2f),
            isLoop = true,
            waitTimeAtWaypoint = 1.8f
        };
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP0_DoorLab", new Vector3(13.5f, 0.03f, -1.65f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP1_SectorB", new Vector3(13.5f, 0.03f, -7.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP2_MainSouth", new Vector3(13.5f, 0.03f, -14.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP3_GeneratorSouth", new Vector3(13.5f, 0.03f, -20.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP4_SouthCross", new Vector3(19.1f, 0.03f, -20.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP5_EastSouthJunction", new Vector3(24.5f, 0.03f, -20.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP6_EastMidSouth", new Vector3(24.5f, 0.03f, -6.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP7_EastCenter", new Vector3(24.5f, 0.03f, 3.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP8_EastMidNorth", new Vector3(24.5f, 0.03f, 13.5f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP9_EastNorthJunction", new Vector3(24.5f, 0.03f, 26.0f)));
        r1.waypoints.Add(CreateWaypoint(wpRoot, "R1_WP10_NorthCross", new Vector3(19.1f, 0.03f, 26.0f)));
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
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP4_SouthCrossArchive", new Vector3(19.1f, 0.03f, -6.5f)));
        r2.waypoints.Add(CreateWaypoint(wpRoot, "R2_WP5_CentralCrossArchive", new Vector3(13.5f, 0.03f, -6.5f)));
        routes.Add(r2);

        // ROUTE 3: West Complex Research & Substation Circuit (Cyan) (~130m Loop)
        // Guard 3 patrols the spacious West Wing and North Concourse far from the server wall!
        var r3 = new PatrolRoute
        {
            routeName = "Маршрут 3: Западный комплекс и энергоподстанция",
            routeColor = new Color(0.2f, 0.85f, 0.95f),
            isLoop = true,
            waitTimeAtWaypoint = 2.0f
        };
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP0_WestSouth", new Vector3(-8.0f, 0.03f, -20.0f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP1_SubstationJunction", new Vector3(-8.0f, 0.03f, -10.0f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP2_SubstationBay", new Vector3(-3.5f, 0.03f, -10.0f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP3_SubstationReturn", new Vector3(-8.0f, 0.03f, -10.0f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP4_WestApproachJunction", new Vector3(-8.0f, 0.03f, -1.65f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP5_WestMainMid", new Vector3(-8.0f, 0.03f, 6.0f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP6_LabBayJunction", new Vector3(-8.0f, 0.03f, 13.5f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP7_LabBayInside", new Vector3(-2.75f, 0.03f, 13.5f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP8_LabBayReturn", new Vector3(-8.0f, 0.03f, 13.5f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP9_WestNorthJunction", new Vector3(-8.0f, 0.03f, 26.0f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP10_NorthConcourseMid", new Vector3(2.75f, 0.03f, 26.0f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP11_NorthConcourseCentral", new Vector3(12.0f, 0.03f, 26.0f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP12_NorthConcourseReturn", new Vector3(2.75f, 0.03f, 26.0f)));
        r3.waypoints.Add(CreateWaypoint(wpRoot, "R3_WP13_WestNorthReturn", new Vector3(-8.0f, 0.03f, 26.0f)));
        routes.Add(r3);
    }

    private void CreateVestibuleSection(Transform floors, Transform walls, Transform lights,
        Material wallMat, Material floorMat, Material steelMat, float wallH)
    {
        // Spans X = 8.90f to 12.00f, centered at Z = -1.65f, size Z = 2.40f
        float xMin = 8.90f;
        float xMax = 12.00f;
        float xCenter = (xMin + xMax) * 0.5f; // 10.45f
        float xLen = xMax - xMin;             // 3.10f
        float zCenter = -1.65f;
        float zLen = 2.40f;
        float zSouth = zCenter - zLen * 0.5f; // -2.85f

        // 1. Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "VestibuleFloor";
        floor.transform.SetParent(floors, false);
        floor.transform.position = new Vector3(xCenter, -0.05f, zCenter);
        floor.transform.localScale = new Vector3(xLen, 0.10f, zLen);
        if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

        // 2. Ceiling
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = "VestibuleCeiling";
        ceiling.transform.SetParent(walls, false);
        ceiling.transform.position = new Vector3(xCenter, wallH + 0.05f, zCenter);
        ceiling.transform.localScale = new Vector3(xLen, 0.10f, zLen);
        if (wallMat != null) ceiling.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // 3. South Wall (solid along Z = -2.85f)
        var wallS = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallS.name = "VestibuleWall_South";
        wallS.transform.SetParent(walls, false);
        wallS.transform.position = new Vector3(xCenter, wallH * 0.5f, zSouth);
        wallS.transform.localScale = new Vector3(xLen, wallH, 0.20f);
        if (wallMat != null) wallS.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // 4. West Archway overhead (X = 8.90f) connecting West Approach Corridor
        var archW = GameObject.CreatePrimitive(PrimitiveType.Cube);
        archW.name = "VestibuleArch_West";
        archW.transform.SetParent(walls, false);
        archW.transform.position = new Vector3(xMin, wallH * 0.85f, zCenter);
        archW.transform.localScale = new Vector3(0.20f, wallH * 0.3f, zLen);
        if (wallMat != null) archW.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // 5. East Archway overhead (X = 12.00f) connecting Central Hallway
        var archE = GameObject.CreatePrimitive(PrimitiveType.Cube);
        archE.name = "VestibuleArch_East";
        archE.transform.SetParent(walls, false);
        archE.transform.position = new Vector3(xMax, wallH * 0.85f, zCenter);
        archE.transform.localScale = new Vector3(0.20f, wallH * 0.3f, zLen);
        if (wallMat != null) archE.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // 6. North Wall with Doorway for MetalDoor3 (doorway at X = 9.10 to 10.24)
        // Segment 1 (West of door): 8.90 to 9.10 (width 0.20)
        var wN1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wN1.name = "VestibuleWall_North_W";
        wN1.transform.SetParent(walls, false);
        wN1.transform.position = new Vector3(9.00f, wallH * 0.5f, -0.52f);
        wN1.transform.localScale = new Vector3(0.20f, wallH, 0.20f);
        if (wallMat != null) wN1.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // Segment 2 (East of door): 10.24 to 12.00 (width 1.76)
        var wN2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wN2.name = "VestibuleWall_North_E";
        wN2.transform.SetParent(walls, false);
        wN2.transform.position = new Vector3((10.24f + 12.00f) * 0.5f, wallH * 0.5f, -0.52f);
        wN2.transform.localScale = new Vector3(12.00f - 10.24f, wallH, 0.20f);
        if (wallMat != null) wN2.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // Lintel over MetalDoor3: 9.10 to 10.24
        var lintelDoor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lintelDoor.name = "VestibuleDoorLintel";
        lintelDoor.transform.SetParent(walls, false);
        lintelDoor.transform.position = new Vector3((9.10f + 10.24f) * 0.5f, wallH * 0.85f, -0.52f);
        lintelDoor.transform.localScale = new Vector3(1.14f, wallH * 0.3f, 0.20f);
        if (wallMat != null) lintelDoor.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // 7. Lamp
        var lampFixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lampFixture.name = "VestibuleLamp";
        lampFixture.transform.SetParent(lights, false);
        lampFixture.transform.position = new Vector3(xCenter, wallH - 0.15f, zCenter);
        lampFixture.transform.localScale = new Vector3(0.25f, 0.08f, 1.20f);
        if (steelMat != null) lampFixture.GetComponent<MeshRenderer>().sharedMaterial = steelMat;
    }

    private void CreateHallwaySection(Transform floors, Transform walls, Transform lights,
        Vector3 center, Vector3 size, Material wallMat, Material floorMat, Material steelMat,
        List<float> westOpeningsAtZ = null, List<float> eastOpeningsAtZ = null,
        List<float> northOpeningsAtX = null, List<float> southOpeningsAtX = null,
        bool closeStart = false, bool closeEnd = false,
        float defaultDoorOpeningW = 2.6f)
    {
        // 1. Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = $"Floor_{center.x:F1}_{center.z:F1}";
        floor.transform.SetParent(floors, false);
        floor.transform.position = new Vector3(center.x, -0.05f, center.z);
        floor.transform.localScale = new Vector3(size.x, 0.10f, size.z);
        if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

        // 2. Ceiling
        var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ceiling.name = $"Ceiling_{center.x:F1}_{center.z:F1}";
        ceiling.transform.SetParent(walls, false);
        ceiling.transform.position = new Vector3(center.x, size.y + 0.05f, center.z);
        ceiling.transform.localScale = new Vector3(size.x, 0.10f, size.z);
        if (wallMat != null) ceiling.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

        // 3. Side Walls with Openings
        float halfW = size.x * 0.5f;
        float halfL = size.z * 0.5f;

        if (size.z >= size.x) // Running along Z
        {
            float zMin = center.z - halfL;
            float zMax = center.z + halfL;

            // West Wall
            BuildWallSpanAlongZ(walls, wallMat, center.x - halfW, size.y, zMin, zMax, westOpeningsAtZ, 0.2f, defaultDoorOpeningW);

            // East Wall
            BuildWallSpanAlongZ(walls, wallMat, center.x + halfW, size.y, zMin, zMax, eastOpeningsAtZ, 0.2f, defaultDoorOpeningW);

            // Optional end caps
            if (closeStart)
            {
                BuildEndCapWall(walls, wallMat, new Vector3(center.x, size.y * 0.5f, zMin), new Vector3(size.x, size.y, 0.2f));
            }
            if (closeEnd)
            {
                BuildEndCapWall(walls, wallMat, new Vector3(center.x, size.y * 0.5f, zMax), new Vector3(size.x, size.y, 0.2f));
            }
        }
        else // Running along X
        {
            float xMin = center.x - halfW;
            float xMax = center.x + halfW;

            // North Wall
            BuildWallSpanAlongX(walls, wallMat, center.z + halfL, size.y, xMin, xMax, northOpeningsAtX, 0.2f, defaultDoorOpeningW);

            // South Wall
            BuildWallSpanAlongX(walls, wallMat, center.z - halfL, size.y, xMin, xMax, southOpeningsAtX, 0.2f, defaultDoorOpeningW);

            // Optional end caps
            if (closeStart)
            {
                BuildEndCapWall(walls, wallMat, new Vector3(xMin, size.y * 0.5f, center.z), new Vector3(0.2f, size.y, size.z));
            }
            if (closeEnd)
            {
                BuildEndCapWall(walls, wallMat, new Vector3(xMax, size.y * 0.5f, center.z), new Vector3(0.2f, size.y, size.z));
            }
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

    private void BuildWallSpanAlongZ(Transform walls, Material wallMat, float wallX, float wallY,
        float zMin, float zMax, List<float> openings, float wallThickness = 0.2f, float doorOpeningW = 2.6f)
    {
        if (openings == null || openings.Count == 0)
        {
            float len = zMax - zMin;
            if (len <= 0.1f) return;
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = $"Wall_Z_{wallX:F1}_{zMin:F1}_to_{zMax:F1}";
            w.transform.SetParent(walls, false);
            w.transform.position = new Vector3(wallX, wallY * 0.5f, (zMin + zMax) * 0.5f);
            w.transform.localScale = new Vector3(wallThickness, wallY, len);
            if (wallMat != null) w.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            return;
        }

        var sorted = new List<float>();
        foreach (var op in openings)
        {
            if (op >= zMin - 0.2f && op <= zMax + 0.2f) sorted.Add(op);
        }
        sorted.Sort();

        float curZ = zMin;
        for (int i = 0; i < sorted.Count; i++)
        {
            float opZ = sorted[i];
            float opStart = Mathf.Max(zMin, opZ - doorOpeningW * 0.5f);
            float opEnd = Mathf.Min(zMax, opZ + doorOpeningW * 0.5f);

            if (opStart > curZ + 0.15f)
            {
                float segLen = opStart - curZ;
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                w.name = $"Wall_Z_{wallX:F1}_{curZ:F1}_to_{opStart:F1}";
                w.transform.SetParent(walls, false);
                w.transform.position = new Vector3(wallX, wallY * 0.5f, (curZ + opStart) * 0.5f);
                w.transform.localScale = new Vector3(wallThickness, wallY, segLen);
                if (wallMat != null) w.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            }

            float lintelLen = opEnd - opStart;
            if (lintelLen > 0.2f)
            {
                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = $"Lintel_Z_{wallX:F1}_{opZ:F1}";
                lintel.transform.SetParent(walls, false);
                lintel.transform.position = new Vector3(wallX, wallY * 0.85f, (opStart + opEnd) * 0.5f);
                lintel.transform.localScale = new Vector3(wallThickness, wallY * 0.3f, lintelLen);
                if (wallMat != null) lintel.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            }

            curZ = opEnd;
        }

        if (curZ < zMax - 0.15f)
        {
            float segLen = zMax - curZ;
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = $"Wall_Z_{wallX:F1}_{curZ:F1}_to_{zMax:F1}";
            w.transform.SetParent(walls, false);
            w.transform.position = new Vector3(wallX, wallY * 0.5f, (curZ + zMax) * 0.5f);
            w.transform.localScale = new Vector3(wallThickness, wallY, segLen);
            if (wallMat != null) w.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        }
    }

    private void BuildWallSpanAlongX(Transform walls, Material wallMat, float wallZ, float wallY,
        float xMin, float xMax, List<float> openings, float wallThickness = 0.2f, float doorOpeningW = 2.6f)
    {
        if (openings == null || openings.Count == 0)
        {
            float len = xMax - xMin;
            if (len <= 0.1f) return;
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = $"Wall_X_{wallZ:F1}_{xMin:F1}_to_{xMax:F1}";
            w.transform.SetParent(walls, false);
            w.transform.position = new Vector3((xMin + xMax) * 0.5f, wallY * 0.5f, wallZ);
            w.transform.localScale = new Vector3(len, wallY, wallThickness);
            if (wallMat != null) w.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            return;
        }

        var sorted = new List<float>();
        foreach (var op in openings)
        {
            if (op >= xMin - 0.2f && op <= xMax + 0.2f) sorted.Add(op);
        }
        sorted.Sort();

        float curX = xMin;
        for (int i = 0; i < sorted.Count; i++)
        {
            float opX = sorted[i];
            float opStart = Mathf.Max(xMin, opX - doorOpeningW * 0.5f);
            float opEnd = Mathf.Min(xMax, opX + doorOpeningW * 0.5f);

            if (opStart > curX + 0.15f)
            {
                float segLen = opStart - curX;
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                w.name = $"Wall_X_{wallZ:F1}_{curX:F1}_to_{opStart:F1}";
                w.transform.SetParent(walls, false);
                w.transform.position = new Vector3((curX + opStart) * 0.5f, wallY * 0.5f, wallZ);
                w.transform.localScale = new Vector3(segLen, wallY, wallThickness);
                if (wallMat != null) w.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            }

            float lintelLen = opEnd - opStart;
            if (lintelLen > 0.2f)
            {
                var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lintel.name = $"Lintel_X_{wallZ:F1}_{opX:F1}";
                lintel.transform.SetParent(walls, false);
                lintel.transform.position = new Vector3((opStart + opEnd) * 0.5f, wallY * 0.85f, wallZ);
                lintel.transform.localScale = new Vector3(lintelLen, wallY * 0.3f, wallThickness);
                if (wallMat != null) lintel.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
            }

            curX = opEnd;
        }

        if (curX < xMax - 0.15f)
        {
            float segLen = xMax - curX;
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = $"Wall_X_{wallZ:F1}_{curX:F1}_to_{xMax:F1}";
            w.transform.SetParent(walls, false);
            w.transform.position = new Vector3((curX + xMax) * 0.5f, wallY * 0.5f, wallZ);
            w.transform.localScale = new Vector3(segLen, wallY, wallThickness);
            if (wallMat != null) w.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        }
    }

    private void BuildEndCapWall(Transform walls, Material wallMat, Vector3 pos, Vector3 size)
    {
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = $"EndCap_{pos.x:F1}_{pos.z:F1}";
        cap.transform.SetParent(walls, false);
        cap.transform.position = pos;
        cap.transform.localScale = size;
        if (wallMat != null) cap.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
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
        InspectingRoomDoorway,
        ClosingDoorReturning,
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
                g.isTerminalInspectionActive = false;
                if (IsCheatPeaceMode && (g.currentState == GuardState.BreachMovingToDoor || 
                                         g.currentState == GuardState.BreachOpeningDoor || 
                                         g.currentState == GuardState.BreachEnteringRoom || 
                                         g.currentState == GuardState.InspectingRoomDoorway ||
                                         g.currentState == GuardState.ClosingDoorReturning ||
                                         g.currentState == GuardState.ChasingPlayer || 
                                         g.currentState == GuardState.InvestigatingSuspicious))
                {
                    g.currentState = GuardState.Patrolling;
                    if (g.doorHingePivot != null) g.doorHingePivot.localRotation = Quaternion.identity;
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

    public static GuardPawn GetClosestGuardToDoor()
    {
        var guards = FindObjectsByType<GuardPawn>(FindObjectsInactive.Exclude);
        if (guards == null || guards.Length == 0) return null;
        GuardPawn best = null;
        float minD = float.MaxValue;
        Vector3 doorPos = new Vector3(9.67f, 0f, -0.52f);
        foreach (var g in guards)
        {
            if (g == null) continue;
            float d = Vector3.Distance(g.transform.position, doorPos);
            if (d < minD) { minD = d; best = g; }
        }
        return best;
    }

    public static void TriggerTimeExpiredAssault()
    {
        if (IsGameOver || GameVictorySystem.IsGameWon) return;
        IsCheatPeaceMode = false;

        if (PlayerController.Instance != null)
        {
            PlayerController.Instance.ExitMonitor();
            PlayerController.Instance.ApplyStun(6.0f);
        }

        var guard = GetClosestGuardToDoor();
        if (guard != null)
        {
            guard.StartCoroutine(guard.TimeExpiredBreachSequence());
        }
        else
        {
            TriggerGameOverStatic(
                "ВРЕМЯ ИСТЕКЛО! ОБЪЕКТ ЗАБЛОКИРОВАН",
                "СЕКРЕТНЫЙ ОБЪЕКТ «ВЕКТОР-74» — ШТУРМ КОМЕНДАТУРЫ",
                "Время на карманных часах подошло к концу (00:00).\n\n" +
                "Дежурный караул комендатуры выбил служебную дверь машинного зала и заблокировал объект.\n" +
                "Агент нейтрализован на рабочем месте, материалы шифрования изъяты.\n" +
                "Операция полностью провалена."
            );
        }
    }

    public IEnumerator TimeExpiredBreachSequence()
    {
        FindDoorHingePivot();
        transform.position = outsideDoorPos;
        transform.rotation = Quaternion.LookRotation(Vector3.forward);
        currentState = GuardState.BreachOpeningDoor;
        doorOpenProgress = 0f;

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetTrigger("OpenDoor");
        }

        if (sfxAudioSource != null && doorCreakClip != null)
        {
            sfxAudioSource.PlayOneShot(doorCreakClip, 1f);
        }
        MainframeBootQuest.Instance?.ShowNotification("<color=#FF2222>[ВРЕМЯ ИСТЕКЛО!]</color> Охранники выбивают дверь!");

        float elapsed = 0f;
        float duration = 0.22f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (doorHingePivot != null)
            {
                doorHingePivot.localRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, -95f, t), 0f);
            }
            yield return null;
        }
        if (doorHingePivot != null)
        {
            doorHingePivot.localRotation = Quaternion.Euler(0f, -95f, 0f);
        }

        if (sfxAudioSource != null && alertStingClip != null)
        {
            sfxAudioSource.PlayOneShot(alertStingClip, 1f);
        }

        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
            animator.SetTrigger("Alert");
        }

        Vector3 targetPos = doorwayThresholdPos;
        float stormElapsed = 0f;
        Vector3 startP = transform.position;
        while (stormElapsed < 0.6f)
        {
            stormElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(stormElapsed / 0.6f);
            transform.position = Vector3.Lerp(startP, targetPos, t);
            if (PlayerController.Instance != null)
            {
                Vector3 lookDir = PlayerController.Instance.transform.position - transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(lookDir);
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.6f);

        TriggerGameOverStatic(
            "ВРЕМЯ ИСТЕКЛО! ОБЪЕКТ ЗАБЛОКИРОВАН",
            "СЕКРЕТНЫЙ ОБЪЕКТ «ВЕКТОР-74» — ШТУРМ КОМЕНДАТУРЫ",
            "Время на карманных часах подошло к концу (00:00).\n\n" +
            "Дежурный караул комендатуры выбил служебную дверь машинного зала и заблокировал объект.\n" +
            "Агент нейтрализован на рабочем месте, материалы шифрования изъяты.\n" +
            "Операция полностью провалена."
        );
    }

    public static void TriggerCheatBreach()
    {
        var guard = GetClosestGuardToDoor();
        if (guard == null) return;
        guard.FindDoorHingePivot();
        guard.transform.position = guard.outsideDoorPos;
        guard.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        guard.currentState = GuardState.BreachOpeningDoor;
        guard.doorOpenProgress = 0f;
        if (guard.animator != null)
        {
            guard.animator.SetBool("IsMoving", false);
            guard.animator.SetTrigger("OpenDoor");
        }
        if (guard.sfxAudioSource != null && guard.doorCreakClip != null)
        {
            guard.sfxAudioSource.PlayOneShot(guard.doorCreakClip, 1f);
        }
        MainframeBootQuest.Instance?.ShowNotification("<color=#FFCC00>[ЧИТ-ТЕСТ]</color> Охранник подошел к двери и открывает её (анимация OpenDoor)!");
    }

    public static void TriggerCheatDoorwayInspect()
    {
        var guard = GetClosestGuardToDoor();
        if (guard == null) return;
        guard.FindDoorHingePivot();
        if (guard.doorHingePivot != null) guard.doorHingePivot.localRotation = Quaternion.Euler(0f, -90f, 0f);
        guard.transform.position = guard.doorwayThresholdPos;
        guard.transform.rotation = Quaternion.LookRotation(new Vector3(8.30f - guard.doorwayThresholdPos.x, 0f, 3.95f - guard.doorwayThresholdPos.z));
        guard.currentState = GuardState.InspectingRoomDoorway;
        guard.inspectionTimer = 6.0f;
        if (guard.animator != null)
        {
            guard.animator.SetBool("IsMoving", false);
            guard.animator.SetTrigger("Inspect");
        }
        MainframeBootQuest.Instance?.ShowNotification("<color=#FFCC00>[ЧИТ-ТЕСТ]</color> Охранник в дверях осматривает зал (анимация Hard Head Nod)!");
    }

    public static void TriggerCheatStealthSuccess()
    {
        var guard = GetClosestGuardToDoor();
        if (guard == null) return;
        guard.FindDoorHingePivot();
        if (guard.doorHingePivot != null) guard.doorHingePivot.localRotation = Quaternion.Euler(0f, -90f, 0f);
        guard.transform.position = guard.doorwayThresholdPos;
        guard.currentState = GuardState.ClosingDoorReturning;
        if (guard.animator != null)
        {
            guard.animator.SetBool("IsMoving", false);
            guard.animator.SetTrigger("StealthSuccess");
        }
        guard.returnPath.Clear();
        guard.returnPath.Add(guard.outsideDoorPos);
        guard.returnPath.Add(new Vector3(12.0f, 0.03f, -1.65f));
        guard.returnPath.Add(new Vector3(13.5f, 0.03f, -1.65f));
        guard.returnPathIndex = 0;
        MainframeBootQuest.Instance?.ShowNotification("<color=#00FFAA>[ЧИТ-ТЕСТ]</color> Охранник качает головой («никого нет») и закрывает дверь (Shaking Head No)!");
    }

    public static void TriggerCheatAlertChase()
    {
        var guard = GetClosestGuardToDoor();
        if (guard == null) return;
        guard.FindDoorHingePivot();
        if (guard.doorHingePivot != null) guard.doorHingePivot.localRotation = Quaternion.Euler(0f, -90f, 0f);
        guard.transform.position = guard.doorwayThresholdPos;
        guard.currentState = GuardState.ChasingPlayer;
        if (guard.animator != null)
        {
            guard.animator.SetBool("IsMoving", false);
            guard.animator.SetTrigger("Alert");
        }
        if (guard.sfxAudioSource != null && guard.alertStingClip != null)
        {
            guard.sfxAudioSource.PlayOneShot(guard.alertStingClip, 1f);
        }
        MainframeBootQuest.Instance?.ShowNotification("<color=#FF2222>[ЧИТ-ТЕСТ]</color> Тревога! Охранник вскидывает руки и начинает погоню (Reacting -> Chase)!");
    }

    public static void ResetAllToPatrol()
    {
        var guards = FindObjectsByType<GuardPawn>(FindObjectsInactive.Include);
        if (guards != null)
        {
            foreach (var g in guards)
            {
                if (g == null) continue;
                g.currentState = GuardState.Patrolling;
                g.suspicion = 0f;
                g.isTerminalInspectionActive = false;
                if (g.doorHingePivot != null) g.doorHingePivot.localRotation = Quaternion.identity;
                if (g.animator != null) g.animator.SetBool("IsMoving", true);
            }
        }
        MainframeBootQuest.Instance?.ShowNotification("<color=#00FFFF>[ЧИТ-ТЕСТ]</color> Все охранники возвращены на штатные маршруты патрулирования.");
    }

    private GuardPatrolSystem.PatrolRoute route;
    private int currentWaypointIndex = 0;
    private float walkSpeed = 1.8f;
    private float turnSpeed = 180f;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    // State machine
    private GuardState currentState = GuardState.Patrolling;
    private bool isTerminalInspectionActive = false;
    private float inspectionTimer = 0f;
    private float inspectionApproachTimer = 0f;
    private readonly List<Vector3> returnPath = new List<Vector3>();
    private int returnPathIndex = 0;

    // Door and room breach points
    private readonly Vector3 vestibuleEntrancePos = new Vector3(12.0f, 0.0f, -1.65f);
    private readonly Vector3 outsideDoorPos = new Vector3(9.67f, 0.0f, -1.40f);
    private readonly Vector3 doorwayThresholdPos = new Vector3(9.67f, 0.0f, 0.30f);
    private readonly Vector3 roomSearchPos = new Vector3(8.50f, 0.0f, 1.80f);
    private readonly Vector3 labDoorCoord = new Vector3(9.67f, 0.0f, -0.52f);

    // Deterministic corridor breach & investigation navigation (no wall clipping!)
    private readonly List<Vector3> breachPath = new List<Vector3>();
    private int breachPathIndex = 0;

    private Transform doorHingePivot;
    private float doorOpenProgress = 0f;
    private float suspicion = 0f;
    private float lastAlertNotifyTime = -10f;
    private float stepTimer = 0f;
    private bool isLeftStep = false;
    // Logging & Aggro state tracking
    private bool hasLoggedInitialDetect = false;
    private bool hasLoggedYellowAlert = false;
    private float lastAggroLogTime = -99f;
    private bool isAnyAlertSourceActive = false;

    // Character Model & Animations
    private Animator animator;
    private bool wasMoving = false;

    // Procedural Audio
    private AudioSource footstepAudioSource;
    private AudioSource sfxAudioSource;
    private AudioClip footstepLeftClip;
    private AudioClip footstepRightClip;
    private AudioClip doorCreakClip;
    private AudioClip alertStingClip;
    private bool isUsingLoopingSteps = false;

    public float Suspicion => suspicion;
    public bool IsInvestigating => suspicion > 0.35f;
    public bool IsHighAlert => suspicion >= 0.85f;
    public GuardState CurrentState => currentState;
    public bool IsTerminalInspectionActive => isTerminalInspectionActive;

    public static bool IsAnyGuardAtDoorOrInspecting()
    {
        var guards = FindObjectsByType<GuardPawn>(FindObjectsInactive.Exclude);
        if (guards == null) return false;
        foreach (var g in guards)
        {
            if (g == null) continue;
            if (g.currentState == GuardState.BreachOpeningDoor ||
                g.currentState == GuardState.BreachEnteringRoom ||
                g.currentState == GuardState.InspectingRoomDoorway ||
                g.currentState == GuardState.ClosingDoorReturning)
            {
                return true;
            }
        }
        return false;
    }

    public static void DispatchGuardForTerminalInspection(string reason)
    {
        var guards = FindObjectsByType<GuardPawn>(FindObjectsInactive.Exclude);
        if (guards == null || guards.Length == 0) return;

        GuardPawn bestGuard = null;
        float bestDist = float.MaxValue;
        Vector3 doorPos = new Vector3(9.67f, 0.0f, -0.52f);

        foreach (var g in guards)
        {
            if (g == null || IsGameOver || IsCheatPeaceMode || GameVictorySystem.IsGameWon) continue;
            float d = Vector3.Distance(g.transform.position, doorPos);
            if (d < bestDist)
            {
                bestDist = d;
                bestGuard = g;
            }
        }

        if (bestGuard != null)
        {
            bestGuard.StartTerminalInspection(reason);
        }

        // Raise corridor alertness on any other guards
        foreach (var g in guards)
        {
            if (g != null && g != bestGuard)
            {
                g.AddSuspicion(0.30f, "Тревога терминала в Секторе 7");
            }
        }
    }

    public void StartTerminalInspection(string reason)
    {
        if (IsCheatPeaceMode || IsGameOver || GameVictorySystem.IsGameWon) return;

        isTerminalInspectionActive = true;
        inspectionTimer = 4.2f;
        inspectionApproachTimer = 6.2f;
        suspicion = 1.0f;

        Debug.Log($"<color=#FF2222><b>[AGGRO: ВЫЗОВ ОХРАНЫ ПО ТРЕВОГЕ ТЕРМИНАЛА]</b></color> <b>{name}</b> направлен к машинному залу!\n" +
                  $"• Причина: <b>{reason}</b>\n" +
                  $"• Дистанция до двери: {Vector3.Distance(transform.position, labDoorCoord):F1}м");

        StartBreachSequence();
    }

    public void AddSuspicion(float amount, string reason)
    {
        float oldSusp = suspicion;
        suspicion = Mathf.Clamp01(suspicion + amount);
        float distToDoor = Vector3.Distance(transform.position, labDoorCoord);
        Debug.Log($"<color=#FF5500><b>[AGGRO: ВСПЛЕСК ТРЕВОГИ +{(amount * 100f):F0}%]</b></color> <b>{name}</b> среагировал на: <b>{reason}</b>\n" +
                  $"• Позиция охраны: {transform.position:F1} | Дистанция до двери: {distToDoor:F1}м\n" +
                  $"• Уровень тревоги: {(oldSusp * 100f):F0}% -> {(suspicion * 100f):F0}%");

        if (suspicion >= 0.85f && currentState < GuardState.BreachMovingToDoor)
        {
            StartBreachSequence();
        }
    }

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

        // 1. Footstep 3D AudioSource (audible up to 11.5m with balanced spatial rolloff curve)
        var footstepObj = new GameObject("GuardFootstepAudio");
        footstepObj.transform.SetParent(transform, false);
        footstepObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);

        footstepAudioSource = footstepObj.AddComponent<AudioSource>();
        footstepAudioSource.spatialBlend = 1.0f; // 100% true 3D spatial sound!
        footstepAudioSource.rolloffMode = AudioRolloffMode.Custom;
        footstepAudioSource.minDistance = 1.5f;
        footstepAudioSource.maxDistance = 11.5f;
        footstepAudioSource.spread = 15.0f; // sharp stereo directionality for pinpoint location
        footstepAudioSource.dopplerLevel = 0.10f;
        footstepAudioSource.volume = 1.0f;
        footstepAudioSource.playOnAwake = false;

        // Custom distance attenuation curve for maxDistance = 11.5m:
        // Ensures distant guards in other corridors (>11.5m) are completely silent,
        // and footsteps only become audible when approaching within 11.5m.
        var footstepCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f, 0f, -0.6f),     // 0m - 1.5m: 100% (right outside door)
            new Keyframe(0.17f, 1.00f, -0.6f, -1.2f),  // ~2m: 100%
            new Keyframe(0.35f, 0.65f, -1.2f, -1.0f),  // ~4m: 65% (in vestibule)
            new Keyframe(0.65f, 0.30f, -0.8f, -0.8f),  // ~7.5m: 30% (hallway approach)
            new Keyframe(0.87f, 0.10f, -0.5f, -0.5f),  // ~10m: 10% (faint distant footsteps)
            new Keyframe(1.00f, 0.00f, -0.3f, 0f)      // 11.5m: 0% (silence beyond)
        );
        footstepAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, footstepCurve);

        // 2. SFX AudioSource (creak, sting, breach)
        var sfxObj = new GameObject("GuardSFXAudio");
        sfxObj.transform.SetParent(transform, false);
        sfxObj.transform.localPosition = Vector3.up * 1.5f;

        sfxAudioSource = sfxObj.AddComponent<AudioSource>();
        sfxAudioSource.spatialBlend = 1.0f; // 100% 3D spatial
        sfxAudioSource.rolloffMode = AudioRolloffMode.Custom;
        sfxAudioSource.minDistance = 1.5f;
        sfxAudioSource.maxDistance = 14.0f;
        sfxAudioSource.spread = 25.0f;
        sfxAudioSource.volume = 1.0f;
        sfxAudioSource.playOnAwake = false;

        var sfxCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.25f, 0.80f),
            new Keyframe(0.55f, 0.45f),
            new Keyframe(0.80f, 0.20f),
            new Keyframe(1.00f, 0.00f)
        );
        sfxAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, sfxCurve);

        // Generate procedural audio clips
        CreateFootstepClips();
        CreateDoorCreakClip();
        CreateAlertStingClip();
    }

    private void CreateFootstepClips()
    {
        var loadedSteps = Resources.Load<AudioClip>("Sound/GuardFootsteps");
        if (loadedSteps != null)
        {
            isUsingLoopingSteps = true;
            footstepLeftClip = loadedSteps;
            footstepRightClip = loadedSteps;
            if (footstepAudioSource != null)
            {
                footstepAudioSource.clip = loadedSteps;
                footstepAudioSource.loop = true;
            }
            return;
        }

        int sampleRate = 44100;

        // Clip 1: Left boot heel strike & concrete tap (crisp heel transient + firm sole impact)
        int len1 = (int)(sampleRate * 0.24f);
        float[] d1 = new float[len1];
        System.Random r1 = new System.Random(1337);
        for (int i = 0; i < len1; i++)
        {
            float t = (float)i / sampleRate;
            // 1. Sharp iron heel rim strike on concrete (820Hz + 1450Hz sharp 25ms transient)
            float heelClick = (Mathf.Sin(2f * Mathf.PI * 820f * t) * 0.70f + 
                               Mathf.Sin(2f * Mathf.PI * 1450f * t) * 0.45f) * Mathf.Exp(-t * 90f);

            // 2. Resonant heavy boot sole thud (135Hz, 80ms decay)
            float soleThud = Mathf.Sin(2f * Mathf.PI * 135f * t) * Mathf.Exp(-t * 26f) * 0.65f;

            // 3. Concrete surface grit & leather scuff (filtered friction noise)
            float grit = ((float)r1.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 38f) * 0.22f;

            d1[i] = Mathf.Clamp(heelClick + soleThud + grit, -1f, 1f);
        }
        footstepLeftClip = AudioClip.Create("GuardBoot_L", len1, 1, sampleRate, false);
        footstepLeftClip.SetData(d1, 0);

        // Clip 2: Right boot sole impact & subtle equipment/buckle clink
        int len2 = (int)(sampleRate * 0.26f);
        float[] d2 = new float[len2];
        System.Random r2 = new System.Random(4242);
        for (int i = 0; i < len2; i++)
        {
            float t = (float)i / sampleRate;
            // 1. Firm heel strike (760Hz + 1280Hz transient)
            float heelClick = (Mathf.Sin(2f * Mathf.PI * 760f * t) * 0.65f + 
                               Mathf.Sin(2f * Mathf.PI * 1280f * t) * 0.40f) * Mathf.Exp(-t * 85f);

            // 2. Deep boot sole impact (115Hz, 75ms decay)
            float soleThud = Mathf.Sin(2f * Mathf.PI * 115f * t) * Mathf.Exp(-t * 22f) * 0.70f;

            // 3. Subtle equipment / belt buckle metallic clink (2400Hz + 3100Hz, very subtle characteristic patrol sound)
            float gearClink = (Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.12f + 
                               Mathf.Sin(2f * Mathf.PI * 3100f * t) * 0.08f) * Mathf.Exp(-t * 70f);

            // 4. Concrete contact friction
            float grit = ((float)r2.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 36f) * 0.20f;

            d2[i] = Mathf.Clamp(heelClick + soleThud + gearClink + grit, -1f, 1f);
        }
        footstepRightClip = AudioClip.Create("GuardBoot_R", len2, 1, sampleRate, false);
        footstepRightClip.SetData(d2, 0);
    }

    private void CreateDoorCreakClip()
    {
        var loadedDoor = Resources.Load<AudioClip>("Sound/DoorOpen");
        if (loadedDoor != null)
        {
            doorCreakClip = loadedDoor;
            return;
        }

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
            if (currentState >= GuardState.BreachMovingToDoor)
            {
                UpdateBreach();
                return;
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
        if (breachPath != null && breachPath.Count > 0)
        {
            breachPath.Clear();
            breachPathIndex = 0;
        }
        PatrolWaypoints();
    }

    private void UpdateStealthDetection()
    {
        float distToDoor = Vector3.Distance(transform.position, labDoorCoord);

        // 1. Acoustic detection (Mainframe 50Hz hum / cooling fans / disk alarms)
        bool isNoiseActive = MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsNoiseActive;
        float noiseRad = MainframeBootQuest.Instance != null ? MainframeBootQuest.Instance.NoiseRadius : 3.2f;

        // Acoustic occlusion & corridor path calculation:
        // Guards behind thick room walls cannot hear noise through 40cm solid concrete + server rack
        bool isBehindThickRoomWalls = (transform.position.x < 8.90f && transform.position.z > -0.60f);

        // In the West complex, noise travels strictly along the open corridor path:
        float effectiveAcousticDist;
        if (transform.position.x < 8.90f)
        {
            // Corridor path: from door (9.67, -1.65) westward along approach corridor, plus lateral distance along Z
            effectiveAcousticDist = (9.67f - transform.position.x) + Mathf.Abs(transform.position.z - (-1.65f));
        }
        else
        {
            effectiveAcousticDist = distToDoor;
        }

        bool hearsNoise = isNoiseActive && !isBehindThickRoomWalls && (effectiveAcousticDist <= noiseRad);

        // 2. Sprinting noise (Shift) - calibrated to 5.5m radius
        float sprintRad = 5.5f;
        bool playerRunning = PlayerController.Instance != null && PlayerController.Instance.IsRunning && !isBehindThickRoomWalls && (effectiveAcousticDist <= sprintRad);

        // 3. Optical detection (CRT screen green phosphor glow)
        bool isLightActive = MonitorTerminal.Instance != null && MonitorTerminal.Instance.IsLightEmitting;
        float lightDist = MonitorTerminal.Instance != null ? MonitorTerminal.Instance.LightEmissionDistance : 7.0f;
        bool inMainHallway = Mathf.Abs(transform.position.x - 13.5f) < 2.5f && transform.position.z >= -10f && transform.position.z <= 12f;
        bool seesLight = isLightActive && inMainHallway && distToDoor <= lightDist;

        // Compile active detection triggers for reasoning and console logs
        List<string> activeCauses = new List<string>();
        if (hearsNoise)
        {
            string nSrc = MainframeBootQuest.Instance != null ? MainframeBootQuest.Instance.CurrentNoiseSource : "Шум ЭВМ";
            activeCauses.Add($"{nSrc} (дист: {effectiveAcousticDist:F1}м <= {noiseRad:F1}м)");
        }
        if (playerRunning)
        {
            activeCauses.Add($"Бег игрока на Shift (дист: {effectiveAcousticDist:F1}м <= {sprintRad:F1}м)");
        }
        if (seesLight)
        {
            activeCauses.Add($"Зеленое свечение CRT монитора (дист: {distToDoor:F1}м <= {lightDist:F1}м)");
        }

        bool anyAlertSource = activeCauses.Count > 0;
        isAnyAlertSourceActive = anyAlertSource;

        // 4. Suspicion Accumulation Rate (balanced for fair 6-7s reaction window)
        if (hearsNoise && seesLight)
        {
            suspicion += 0.25f * Time.deltaTime;
        }
        else if (hearsNoise)
        {
            // If it's a huge crash (12m), 0.22/s; if alarm (5.8m), 0.13/s (~6.5s window); if base hum (3.2m), 0.10/s (~8.5s window)
            float baseRate = (noiseRad >= 10.0f) ? 0.22f : ((noiseRad > 5.0f) ? 0.13f : 0.10f);
            float prox = 1.0f - Mathf.Clamp01(effectiveAcousticDist / noiseRad);
            suspicion += (baseRate + prox * 0.04f) * Time.deltaTime;
        }
        else if (playerRunning)
        {
            suspicion += 0.16f * Time.deltaTime;
        }
        else if (seesLight)
        {
            float brightness = MonitorTerminal.Instance != null ? MonitorTerminal.Instance.BrightnessLevel : 1.0f;
            suspicion += (brightness > 0.6f ? 0.14f : 0.08f) * Time.deltaTime;
        }
        else
        {
            // Rapid decay when quiet (rewards flipping switch to Hot Standby), but NOT when terminal inspection was ordered!
            if (!isTerminalInspectionActive)
            {
                suspicion = Mathf.Max(0f, suspicion - 0.45f * Time.deltaTime);
            }
        }

        suspicion = Mathf.Clamp01(suspicion);

        // 5. Console Aggro Logging
        if (anyAlertSource)
        {
            if (!hasLoggedInitialDetect && suspicion >= 0.08f)
            {
                hasLoggedInitialDetect = true;
                Debug.Log($"<color=#FF9900><b>[AGGRO: ЗАСЕЧЕН ШУМ]</b></color> <b>{name}</b> обратил внимание на источник звука/света:\n" +
                          $"• Причина: {string.Join("; ", activeCauses)}\n" +
                          $"• Позиция охраны: {transform.position:F1} | До двери: {distToDoor:F1}м (акустич.: {effectiveAcousticDist:F1}м)\n" +
                          $"• Текущая тревога: {(suspicion * 100f):F0}%");
            }

            if (!hasLoggedYellowAlert && suspicion >= 0.40f)
            {
                hasLoggedYellowAlert = true;
                Debug.Log($"<color=#FFCC00><b>[AGGRO: ЖЕЛТАЯ ТРЕВОГА (40%)]</b></color> <b>{name}</b> остановил патруль и идёт проверять дверь!\n" +
                          $"• Причина: {string.Join("; ", activeCauses)}\n" +
                          $"• Позиция охраны: {transform.position:F1} | До двери: {distToDoor:F1}м (акустич.: {effectiveAcousticDist:F1}м)\n" +
                          $"• Тревога: {(suspicion * 100f):F0}% (при 85% начнётся штурм)");
            }

            // Periodic aggro log if suspicion remains high (every 3.0s)
            if (suspicion >= 0.40f && Time.time - lastAggroLogTime >= 3.0f)
            {
                lastAggroLogTime = Time.time;
                Debug.Log($"<color=#FFB300><b>[AGGRO: ОСМОТР ДВЕРИ]</b></color> <b>{name}</b> приближается к двери. Тревога: {(suspicion * 100f):F0}%\n" +
                          $"• Источники: {string.Join("; ", activeCauses)} | До двери: {distToDoor:F1}м");
            }
        }
        else
        {
            // Calming down
            if (suspicion <= 0.05f && (hasLoggedYellowAlert || hasLoggedInitialDetect))
            {
                hasLoggedYellowAlert = false;
                hasLoggedInitialDetect = false;
                Debug.Log($"<color=#00FF88><b>[AGGRO: УСПОКОЕНИЕ]</b></color> <b>{name}</b> потерял источник шума (тишина/Standby). Возврат на патруль.\n" +
                          $"• Позиция: {transform.position:F1} | До двери: {distToDoor:F1}м | Тревога снята.");
            }
        }

        // 6. High Alert Trigger: Breach door sequence
        if (suspicion >= 0.85f && currentState < GuardState.BreachMovingToDoor)
        {
            Debug.Log($"<color=#FF2222><b>[AGGRO: КРАСНАЯ ТРЕВОГА / ВЗЛОМ ДВЕРИ (85%+)]</b></color> <b>{name}</b> начинает выбивать дверь!\n" +
                      $"• Причина штурма: {(activeCauses.Count > 0 ? string.Join("; ", activeCauses) : "Критический уровень тревоги")}\n" +
                      $"• Позиция охраны: {transform.position:F1} | До двери: {distToDoor:F1}м | Тревога: {(suspicion * 100f):F0}%");
            StartBreachSequence();
        }
    }

    private void StartBreachSequence()
    {
        currentState = GuardState.BreachMovingToDoor;
        FindDoorHingePivot();

        // Build corridor breach path
        breachPath.Clear();
        breachPath.AddRange(BuildCorridorBreachPath(transform.position));
        breachPathIndex = 0;

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
                // If noise stopped completely and suspicion decayed below 0.35f before door opened:
                if (!isTerminalInspectionActive && !isAnyAlertSourceActive && suspicion <= 0.35f)
                {
                    currentState = GuardState.Patrolling;
                    breachPath.Clear();
                    breachPathIndex = 0;
                    Debug.Log($"<color=#00FF88><b>[AGGRO: ШТУРМ ОТМЕНЕН]</b></color> <b>{name}</b> на подходе к двери: звук прекратился (тишина/Standby). Возврат на патруль.");
                    MainframeBootQuest.Instance?.ShowNotification("<color=#00FF88>[ОТБОЙ ТРЕВОГИ]</color> Охрана услышала тишину и вернулась на маршрут.");
                    return;
                }

                if (breachPath == null || breachPath.Count == 0)
                {
                    breachPath.Clear();
                    breachPath.AddRange(BuildCorridorBreachPath(transform.position));
                    breachPathIndex = 0;
                }

                if (breachPathIndex < breachPath.Count)
                {
                    Vector3 targetWp = breachPath[breachPathIndex];
                    targetWp.y = transform.position.y;
                    Vector3 toWp = targetWp - transform.position;
                    toWp.y = 0f;

                    if (toWp.magnitude <= 0.45f)
                    {
                        breachPathIndex++;
                        if (breachPathIndex >= breachPath.Count)
                        {
                            // If sound stopped while guard was on his way, guard listens at door in silence!
                            if (!isTerminalInspectionActive && !isAnyAlertSourceActive && suspicion <= 0.55f)
                            {
                                currentState = GuardState.Patrolling;
                                breachPath.Clear();
                                breachPathIndex = 0;
                                suspicion = 0f;
                                hasLoggedYellowAlert = false;
                                hasLoggedInitialDetect = false;
                                Debug.Log($"<color=#00FF88><b>[AGGRO: ШТУРМ ОТМЕНЕН У ДВЕРИ]</b></color> <b>{name}</b> подошёл к двери машинного зала, но внутри полная тишина. Подозрение снято.");
                                MainframeBootQuest.Instance?.ShowNotification("<color=#00FF88>[ОТБОЙ ТРЕВОГИ]</color> Охранник подошёл к двери, прислушался к тишине: «Показалось...» и ушёл.");
                                return;
                            }

                            // If player still has approach prep time, guard is outside door listening/checking
                            if (isTerminalInspectionActive && inspectionApproachTimer > 0f)
                            {
                                inspectionApproachTimer -= Time.deltaTime;
                                return;
                            }

                            // Reached outside door! Begin opening!
                            currentState = GuardState.BreachOpeningDoor;
                            doorOpenProgress = 0f;
                            if (animator != null)
                            {
                                animator.SetBool("IsMoving", false);
                                animator.SetTrigger("OpenDoor");
                            }

                            if (sfxAudioSource != null && doorCreakClip != null)
                            {
                                sfxAudioSource.PlayOneShot(doorCreakClip, 1.0f);
                            }

                            SpyMiniGames.Instance?.OnGuardInspectionBreach();
                            MainframeBootQuest.Instance?.ShowNotification("<color=#FF2222>[⚠ ДВЕРЬ ВЗЛОМАНА!]</color> «Служба безопасности! Всем оставаться на местах!»");
                            return;
                        }
                    }

                    MoveTowards(breachPath[breachPathIndex], walkSpeed * 1.25f);
                }
                else
                {
                    if (!isTerminalInspectionActive && !isAnyAlertSourceActive && suspicion <= 0.55f)
                    {
                        currentState = GuardState.Patrolling;
                        breachPath.Clear();
                        breachPathIndex = 0;
                        suspicion = 0f;
                        hasLoggedYellowAlert = false;
                        hasLoggedInitialDetect = false;
                        Debug.Log($"<color=#00FF88><b>[AGGRO: ШТУРМ ОТМЕНЕН У ДВЕРИ]</b></color> <b>{name}</b> подошёл к двери, внутри тишина. Тревога снята.");
                        MainframeBootQuest.Instance?.ShowNotification("<color=#00FF88>[ОТБОЙ ТРЕВОГИ]</color> Охрана у двери прислушалась к тишине: «Всё чисто» и ушла.");
                        return;
                    }

                    if (isTerminalInspectionActive && inspectionApproachTimer > 0f)
                    {
                        inspectionApproachTimer -= Time.deltaTime;
                        return;
                    }

                    currentState = GuardState.BreachOpeningDoor;
                    doorOpenProgress = 0f;
                    if (animator != null)
                    {
                        animator.SetBool("IsMoving", false);
                        animator.SetTrigger("OpenDoor");
                    }
                    SpyMiniGames.Instance?.OnGuardInspectionBreach();
                }
                break;

            case GuardState.BreachOpeningDoor:
                // Face the door (pointing +Z into room)
                Quaternion faceDoor = Quaternion.LookRotation(Vector3.forward);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, faceDoor, turnSpeed * Time.deltaTime);

                // doorOpenProgress tracks elapsed animation time (total clip: 6.38s)
                doorOpenProgress += Time.deltaTime;

                float openAngle = 0f;
                // Timeline:
                // 0.0s - 2.8s: Guard approaches handle and turns knob (door stays closed, 0 deg)
                // 2.8s - 3.8s: Door swings inwards from 0 deg to -90 deg
                // 3.8s - 4.8s: Door held fully open at -90 deg
                // 4.8s - 6.2s: Guard lets go and door swings closed from -90 deg back to 0 deg
                // 6.2s+: Animation finishes -> transition to Inspecting / entering
                if (doorOpenProgress < 2.8f)
                {
                    openAngle = 0f;
                }
                else if (doorOpenProgress < 3.8f)
                {
                    // Swings open
                    float t = (doorOpenProgress - 2.8f) / 1.0f;
                    openAngle = Mathf.Lerp(0f, -90f, Mathf.SmoothStep(0f, 1f, t));
                }
                else if (doorOpenProgress < 4.8f)
                {
                    openAngle = -90f;
                }
                else if (doorOpenProgress < 6.2f)
                {
                    // Swings closed
                    float t = (doorOpenProgress - 4.8f) / 1.4f;
                    openAngle = Mathf.Lerp(-90f, 0f, Mathf.SmoothStep(0f, 1f, t));
                }
                else
                {
                    openAngle = 0f;
                }

                if (doorHingePivot != null)
                {
                    doorHingePivot.localRotation = Quaternion.Euler(0f, openAngle, 0f);
                }

                if (doorOpenProgress >= 6.35f)
                {
                    if (doorHingePivot != null) doorHingePivot.localRotation = Quaternion.identity;
                    currentState = GuardState.BreachEnteringRoom;
                }
                break;

            case GuardState.BreachEnteringRoom:
                // Step across threshold into the room
                Vector3 toThresh = doorwayThresholdPos - transform.position;
                toThresh.y = 0f;
                if (toThresh.magnitude > 0.35f)
                {
                    MoveTowards(doorwayThresholdPos, walkSpeed * 1.0f);
                }
                else
                {
                    currentState = GuardState.InspectingRoomDoorway;
                    inspectionTimer = 4.2f;
                    if (animator != null)
                    {
                        animator.SetBool("IsMoving", false);
                        animator.SetTrigger("Inspect");
                    }
                    MainframeBootQuest.Instance?.ShowNotification("<color=#FFCC00>[ПРОВЕРКА ОХРАНЫ]</color> Дверь открыта! Не шевелитесь в укрытии!");
                    Debug.Log($"<color=#FFCC00><b>[AGGRO: ОСМОТР КОМНАТЫ]</b></color> <b>{name}</b> стоит в дверях и осматривает машинный зал (4.2с)...");
                }
                break;

            case GuardState.InspectingRoomDoorway:
                // Sweep gaze across the room (towards desk & room center)
                Vector3 lookTarget = new Vector3(8.30f, transform.position.y, 3.95f);
                Vector3 dirToRoom = lookTarget - transform.position;
                dirToRoom.y = 0f;
                if (dirToRoom.sqrMagnitude > 0.01f)
                {
                    Quaternion rotRoom = Quaternion.LookRotation(dirToRoom.normalized);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, rotRoom, turnSpeed * Time.deltaTime);
                }

                // 1. Check if server power / breaker was cut (Light safe check)
                bool isBreakerCut = StealthIndicatorUI.IsLightSafe();
                if (!isBreakerCut)
                {
                    // Guard immediately sees the glowing green CRT screen and active server running!
                    string failMsg = "«Стоп! Почему аппаратура включена ночью?! Кто здесь?!»";
                    Debug.Log($"<color=#FF2222><b>[AGGRO: ПРОВАЛ МАСКИРОВКИ]</b></color> <b>{name}</b> заметил включенный терминал: рубильник не был выключен!");
                    MainframeBootQuest.Instance?.ShowNotification($"<color=#FF0000>[ПРОВАЛ МАСКИРОВКИ!]</color> {failMsg}");
                    if (animator != null) animator.SetTrigger("Alert");
                    isTerminalInspectionActive = false;
                    currentState = GuardState.ChasingPlayer;
                    return;
                }

                // 2. Check if disk crashed in fire/alarm
                bool isDiskCrash = DiskDriveBay.Instance != null && DiskDriveBay.Instance.State == DiskDriveBay.DriveBayState.OverheatedCrash;
                if (isDiskCrash)
                {
                    string failMsg = "«Сервер горит! Диверсия!»";
                    Debug.Log($"<color=#FF2222><b>[AGGRO: ОБНАРУЖЕН]</b></color> <b>{name}</b> увидел аварию оборудования!");
                    MainframeBootQuest.Instance?.ShowNotification($"<color=#FF0000>[ТРЕВОГА!]</color> {failMsg}");
                    if (animator != null) animator.SetTrigger("Alert");
                    isTerminalInspectionActive = false;
                    currentState = GuardState.ChasingPlayer;
                    return;
                }

                // 3. Line of sight and stealth cover check (Eye hidden check)
                if (!StealthIndicatorUI.IsPlayerHidden(out string failReason))
                {
                    string failMsg = failReason.Contains("кресле")
                        ? "«СТОЯТЬ! ДИВЕРСАНТ В МАШИННОМ ЗАЛЕ! РУКИ ЗА ГОЛОВУ!»"
                        : (failReason.Contains("Бег")
                            ? "«Куда бежишь?! Стоять на месте!»"
                            : "«СТОЯТЬ! КТО ЗДЕСЬ?! РУКИ ЗА ГОЛОВУ!»");

                    Debug.Log($"<color=#FF2222><b>[AGGRO: ОБНАРУЖЕН ({failReason})]</b></color> <b>{name}</b> заметил нарушителя в комнате!");
                    MainframeBootQuest.Instance?.ShowNotification($"<color=#FF0000>[ОБНАРУЖЕН: {failReason.ToUpper()}]</color> {failMsg}");
                    if (animator != null) animator.SetTrigger("Alert");
                    isTerminalInspectionActive = false;
                    currentState = GuardState.ChasingPlayer;
                    return;
                }

                // 6. Inspection timer: player is hidden behind server AND breaker is cut!
                inspectionTimer -= Time.deltaTime;
                if (inspectionTimer <= 0f)
                {
                    // Deceit & Stealth SUCCESS!
                    Debug.Log($"<color=#00FFAA><b>[AGGRO: СКРЫТНОСТЬ УСПЕШНА]</b></color> <b>{name}</b> осмотрел тёмный зал: никого нет, аппаратура обесточена. Ложная тревога.");
                    MainframeBootQuest.Instance?.ShowNotification("<color=#00FFAA>[ОХРАНА УШЛА]</color> «Хм... зал обесточен, никого. Видимо, скачок напряжения». Включите рубильник [E]!");

                    if (animator != null) animator.SetTrigger("StealthSuccess");

                    SpyMiniGames.Instance?.OnDeceitSuccess();

                    currentState = GuardState.ClosingDoorReturning;
                    returnPath.Clear();
                    returnPath.Add(outsideDoorPos); // Step back out of room

                    // Determine route corridor junction
                    if (transform.position.x < 0f || (route != null && route.routeName.Contains("Западный")))
                    {
                        returnPath.Add(new Vector3(8.90f, 0.03f, -1.65f));
                        returnPath.Add(new Vector3(-8.0f, 0.03f, -1.65f));
                    }
                    else
                    {
                        returnPath.Add(new Vector3(12.0f, 0.03f, -1.65f));
                        returnPath.Add(new Vector3(13.5f, 0.03f, -1.65f));
                    }
                    returnPathIndex = 0;
                }
                break;

            case GuardState.ClosingDoorReturning:
                // Step 0: Step back outside to outsideDoorPos and swing door shut
                if (returnPathIndex == 0)
                {
                    Vector3 toOutside = outsideDoorPos - transform.position;
                    toOutside.y = 0f;
                    if (toOutside.magnitude > 0.30f)
                    {
                        MoveTowards(outsideDoorPos, walkSpeed * 0.9f);
                    }
                    else
                    {
                        // Swing door shut
                        doorOpenProgress -= Time.deltaTime * 0.9f;
                        float closeAngle = Mathf.Lerp(0f, -90f, Mathf.Clamp01(doorOpenProgress));
                        if (doorHingePivot != null)
                        {
                            doorHingePivot.localRotation = Quaternion.Euler(0f, closeAngle, 0f);
                        }

                        if (doorOpenProgress <= 0.02f)
                        {
                            doorOpenProgress = 0f;
                            if (doorHingePivot != null) doorHingePivot.localRotation = Quaternion.identity;
                            returnPathIndex = 1;
                        }
                    }
                }
                else
                {
                    if (returnPathIndex < returnPath.Count)
                    {
                        Vector3 targetWp = returnPath[returnPathIndex];
                        targetWp.y = transform.position.y;
                        Vector3 toWp = targetWp - transform.position;
                        toWp.y = 0f;

                        if (toWp.magnitude <= 0.45f)
                        {
                            returnPathIndex++;
                        }
                        else
                        {
                            MoveTowards(targetWp, walkSpeed);
                        }
                    }
                    else
                    {
                        // Safely back in corridor highway!
                        suspicion = 0f;
                        isTerminalInspectionActive = false;
                        hasLoggedYellowAlert = false;
                        hasLoggedInitialDetect = false;
                        currentState = GuardState.Patrolling;
                        returnPath.Clear();
                        returnPathIndex = 0;
                        Debug.Log($"<color=#00FF88><b>[AGGRO: ВОЗВРАТ НА МАРШРУТ]</b></color> <b>{name}</b> вернулся в коридор и возобновил патруль.");
                    }
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
        // Don't walk through walls! Follow corridor breach path slowly
        if (breachPath == null || breachPath.Count == 0 || breachPathIndex >= breachPath.Count)
        {
            breachPath.Clear();
            breachPath.AddRange(BuildCorridorBreachPath(transform.position));
            breachPathIndex = 0;
        }

        if (breachPathIndex < breachPath.Count)
        {
            Vector3 targetWp = breachPath[breachPathIndex];
            targetWp.y = transform.position.y;
            Vector3 toWp = targetWp - transform.position;
            toWp.y = 0f;

            if (toWp.magnitude <= 0.45f)
            {
                breachPathIndex++;
            }
            else
            {
                MoveTowards(targetWp, walkSpeed * 0.45f);
            }
        }
    }

    /// <summary>
    /// Computes a deterministic, corridor-safe waypoint route from any position in the facility
    /// directly to the outside of the lab door, ensuring guards never walk through walls or room geometry.
    /// </summary>
    public static List<Vector3> BuildCorridorBreachPath(Vector3 startPos)
    {
        var path = new List<Vector3>();
        Vector3 outsideDoor = new Vector3(9.67f, 0.03f, -1.40f);

        // CASE 1: Inside Vestibule (X: 8.80 to 12.10, Z: -2.80 to -0.60)
        if (startPos.x >= 8.80f && startPos.x <= 12.10f && startPos.z >= -2.80f && startPos.z <= -0.60f)
        {
            path.Add(outsideDoor);
            return path;
        }

        // CASE 2: West Complex (X < 8.80f)
        if (startPos.x < 8.80f)
        {
            // Subcase 2A: Already in West Approach Corridor (Z around -1.65f)
            if (Mathf.Abs(startPos.z - (-1.65f)) <= 1.0f && startPos.x >= -6.5f)
            {
                path.Add(new Vector3(8.90f, 0.03f, -1.65f)); // Enter Vestibule
                path.Add(outsideDoor);
                return path;
            }

            // Subcase 2B: In a West branch wing (Lab Bay at Z=13.5 or Substation at Z=-10)
            if (startPos.x > -6.5f && startPos.z < 24.0f && startPos.z > -18.0f)
            {
                // Walk west back to West Main trunk
                path.Add(new Vector3(-8.0f, 0.03f, startPos.z));
            }

            // Subcase 2C: In North Concourse west of room (Z=26.0) or South Concourse (Z=-20.0)
            if (startPos.z >= 24.5f && startPos.x > -6.5f)
            {
                if (startPos.x <= 2.75f)
                {
                    path.Add(new Vector3(-8.0f, 0.03f, 26.0f));
                }
                else
                {
                    path.Add(new Vector3(13.5f, 0.03f, 26.0f));
                    path.Add(new Vector3(13.5f, 0.03f, -1.65f));
                    path.Add(new Vector3(12.0f, 0.03f, -1.65f));
                    path.Add(outsideDoor);
                    return path;
                }
            }
            else if (startPos.z <= -18.5f && startPos.x > -6.5f)
            {
                if (startPos.x <= 2.75f)
                {
                    path.Add(new Vector3(-8.0f, 0.03f, -20.0f));
                }
                else
                {
                    path.Add(new Vector3(13.5f, 0.03f, -20.0f));
                    path.Add(new Vector3(13.5f, 0.03f, -1.65f));
                    path.Add(new Vector3(12.0f, 0.03f, -1.65f));
                    path.Add(outsideDoor);
                    return path;
                }
            }

            // In West Main trunk: walk along X = -8.0 to West Approach junction at Z = -1.65
            path.Add(new Vector3(-8.0f, 0.03f, -1.65f));
            // Walk east along West Approach corridor to Vestibule
            path.Add(new Vector3(8.90f, 0.03f, -1.65f));
            // Step to outside door
            path.Add(outsideDoor);
            return path;
        }

        // CASE 3: Central Hallway (X: 12.00 to 15.50)
        if (startPos.x >= 12.00f && startPos.x <= 15.50f)
        {
            // Walk along Central Hallway to Vestibule junction at Z = -1.65
            path.Add(new Vector3(13.5f, 0.03f, -1.65f));
            // Turn west into Vestibule
            path.Add(new Vector3(12.0f, 0.03f, -1.65f));
            // Step to outside door
            path.Add(outsideDoor);
            return path;
        }

        // CASE 4: East Perimeter / East Cross Wings (X > 15.50f)
        float nearestCrossZ;
        if (startPos.z >= 19.5f) nearestCrossZ = 26.0f;
        else if (startPos.z >= 3.5f) nearestCrossZ = 13.5f;
        else if (startPos.z >= -13.0f) nearestCrossZ = -6.5f;
        else nearestCrossZ = -20.0f;

        // If in East Perimeter Hallway, walk along it to nearest cross junction
        if (startPos.x >= 23.0f)
        {
            if (Mathf.Abs(startPos.z - nearestCrossZ) > 0.8f)
            {
                path.Add(new Vector3(24.5f, 0.03f, nearestCrossZ));
            }
        }
        // Walk west through cross wing to Central Hallway
        path.Add(new Vector3(13.5f, 0.03f, nearestCrossZ));
        // Walk down Central Hallway to Vestibule junction
        path.Add(new Vector3(13.5f, 0.03f, -1.65f));
        // Turn west into Vestibule
        path.Add(new Vector3(12.0f, 0.03f, -1.65f));
        // Step to outside door
        path.Add(outsideDoor);
        return path;
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
            if (animator != null) animator.SetBool("IsMoving", false);
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

            if (animator != null) animator.SetBool("IsMoving", true);
            UpdateFootstepSounds(speed);
        }
        else
        {
            if (animator != null) animator.SetBool("IsMoving", false);
            StopFootstepSound();
        }
    }

    private void UpdateFootstepSounds(float currentSpeed)
    {
        if (isUsingLoopingSteps)
        {
            if (currentSpeed > 0.08f && !isWaiting && currentState != GuardState.GameOver && currentState != GuardState.InspectingRoomDoorway)
            {
                if (footstepAudioSource != null)
                {
                    if (!footstepAudioSource.isPlaying)
                    {
                        footstepAudioSource.Play();
                    }
                    footstepAudioSource.pitch = Mathf.Clamp(currentSpeed / walkSpeed, 0.85f, 1.25f);
                }
            }
            else
            {
                StopFootstepSound();
            }
            return;
        }

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

    private void StopFootstepSound()
    {
        if (footstepAudioSource != null && footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Pause();
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

    public static string GameOverTitle = "ЗАДЕРЖАН СЛУЖБОЙ БЕЗОПАСНОСТИ";
    public static string GameOverSub = "СЕКРЕТНЫЙ ОБЪЕКТ «ВЕКТОР-74» — НАРУШЕНИЕ РЕЖИМА";
    public static string GameOverMessage =
        "Дежурный патруль вошёл в вычислительный зал через служебную дверь и задержал вас на месте преступления.\n\n" +
        "Несанкционированный шум аппаратуры ЭВМ и взлом шифра были зафиксированы комендатурой.\n" +
        "Вся аппаратура опечатана. Смена сорвана.";

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

    public static void TriggerGameOverStatic(string title = null, string sub = null, string message = null)
    {
        if (IsGameOver || GameVictorySystem.IsGameWon) return;
        if (!string.IsNullOrEmpty(title)) GameOverTitle = title;
        if (!string.IsNullOrEmpty(sub)) GameOverSub = sub;
        if (!string.IsNullOrEmpty(message)) GameOverMessage = message;

        IsGameOver = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var guards = FindObjectsByType<GuardPawn>(FindObjectsInactive.Exclude);
        if (guards != null)
        {
            foreach (var g in guards)
            {
                if (g != null)
                {
                    g.currentState = GuardState.GameOver;
                    if (g.sfxAudioSource != null && g.alertStingClip != null)
                    {
                        g.sfxAudioSource.PlayOneShot(g.alertStingClip, 1.0f);
                    }
                }
            }
        }
    }

    public static void RestartGame()
    {
        IsGameOver = false;
        GameOverTitle = "ЗАДЕРЖАН СЛУЖБОЙ БЕЗОПАСНОСТИ";
        GameOverSub = "СЕКРЕТНЫЙ ОБЪЕКТ «ВЕКТОР-74» — НАРУШЕНИЕ РЕЖИМА";
        GameOverMessage =
            "Дежурный патруль вошёл в вычислительный зал через служебную дверь и задержал вас на месте преступления.\n\n" +
            "Несанкционированный шум аппаратуры ЭВМ и взлом шифра были зафиксированы комендатурой.\n" +
            "Вся аппаратура опечатана. Смена сорвана.";
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnGUI()
    {
        GUIFontHelper.Apply();
        // Active cheat mode indicator badge and test event panel in top right corner
        if (IsCheatPeaceMode && !IsGameOver)
        {
            float panelW = 260f;
            float panelX = Screen.width - panelW - 12f;
            float panelY = 12f;

            var badgeStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            badgeStyle.normal.textColor = new Color(0.2f, 1.0f, 0.85f);
            GUI.color = new Color(0.08f, 0.18f, 0.12f, 0.92f);
            GUI.Box(new Rect(panelX, panelY, panelW, 26f), "★ ЧИТ: GreeH [ТЕСТ ОХРАНЫ]", badgeStyle);
            GUI.color = Color.white;

            var testBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            float btnH = 26f;
            float btnSpacing = 3f;
            float currentY = panelY + 30f;

            // Button 1: Breach & Open Door
            if (GUI.Button(new Rect(panelX, currentY, panelW, btnH), "[1] Открытие двери (Штурм)", testBtnStyle))
            {
                TriggerCheatBreach();
            }
            currentY += btnH + btnSpacing;

            // Button 2: Inspect room doorway (nodding)
            if (GUI.Button(new Rect(panelX, currentY, panelW, btnH), "[2] Осмотр зала (В проеме)", testBtnStyle))
            {
                TriggerCheatDoorwayInspect();
            }
            currentY += btnH + btnSpacing;

            // Button 3: Stealth success (shake head & leave)
            if (GUI.Button(new Rect(panelX, currentY, panelW, btnH), "[3] Успех скрытности (Уход)", testBtnStyle))
            {
                TriggerCheatStealthSuccess();
            }
            currentY += btnH + btnSpacing;

            // Button 4: Alert reaction & chase
            if (GUI.Button(new Rect(panelX, currentY, panelW, btnH), "[4] Тревога / Поимка (Alert)", testBtnStyle))
            {
                TriggerCheatAlertChase();
            }
            currentY += btnH + btnSpacing;

            // Button 5: Time expired breach assault
            if (GUI.Button(new Rect(panelX, currentY, panelW, btnH), "[5] Время вышло (Штурм двери)", testBtnStyle))
            {
                TriggerTimeExpiredAssault();
            }
            currentY += btnH + btnSpacing;

            // Button 5: Reset back to corridor patrol
            if (GUI.Button(new Rect(panelX, currentY, panelW, btnH), "[0] Сброс на патруль", testBtnStyle))
            {
                ResetAllToPatrol();
            }
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

        GUILayout.Label(GameOverTitle, titleStyle);
        GUILayout.Space(6f);
        GUILayout.Label(GameOverSub, subStyle);
        GUILayout.Space(18f);

        GUILayout.Label(GameOverMessage, bodyStyle);

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
        var prefab = Resources.Load<GameObject>("Guard_Enemy");
        if (prefab != null)
        {
            var model = Instantiate(prefab, transform);
            model.name = "GuardModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.GetComponentInChildren<Animator>();

            // Flashlight on guard hand
            var flashLightObj = new GameObject("GuardFlashlight");
            flashLightObj.transform.SetParent(model.transform, false);
            flashLightObj.transform.localPosition = new Vector3(0.25f, 1.15f, 0.35f);
            flashLightObj.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

            var light = flashLightObj.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1.0f, 0.95f, 0.75f);
            light.range = 10.0f;
            light.spotAngle = 45f;
            light.intensity = 1.8f;
            return;
        }

        // Fallback procedural visuals if prefab not found
        var toonShader = Shader.Find("Custom/SovietToonLit");
        if (toonShader == null) toonShader = Shader.Find("Universal Render Pipeline/Lit");

        var coatMat = new Material(toonShader);
        coatMat.SetColor("_BaseColor", new Color(0.18f, 0.24f, 0.20f));

        var headMat = new Material(toonShader);
        headMat.SetColor("_BaseColor", new Color(0.85f, 0.72f, 0.60f));

        var capMat = new Material(toonShader);
        capMat.SetColor("_BaseColor", new Color(0.15f, 0.18f, 0.22f));

        var redMat = new Material(toonShader);
        redMat.SetColor("_BaseColor", new Color(0.82f, 0.12f, 0.12f));

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Coat";
        DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        body.transform.localScale = new Vector3(0.55f, 0.85f, 0.45f);
        body.GetComponent<MeshRenderer>().sharedMaterial = coatMat;

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        DestroyImmediate(head.GetComponent<Collider>());
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
        head.transform.localScale = new Vector3(0.32f, 0.35f, 0.32f);
        head.GetComponent<MeshRenderer>().sharedMaterial = headMat;
    }
}
