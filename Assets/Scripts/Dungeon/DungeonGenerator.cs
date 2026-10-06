using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RogueDungeon3D : MonoBehaviour
{
    [Header("Dungeon Dimensions")]
    [Tooltip("Total width of the dungeon grid in tiles.")]
    public int mapWidth = 40;
    [Tooltip("Total height (depth) of the dungeon grid in tiles.")]
    public int mapHeight = 40;
    [Tooltip("How many columns to split the world grid into.")]
    public int gridCols = 3;
    [Tooltip("How many rows to split the world grid into.")]
    public int gridRows = 3;

    [Header("3D Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject doorPrefab;

    [Header("Generation Settings")]
    [Tooltip("The physical size multiplier of your 3D assets (e.g., 2 means tiles are spaced 2 units apart).")]
    public float tileSize = 2.0f;

    // Add this to your top variables section if you want to tweak it in the Inspector
    [Header("Room Variability")]
    [Range(0.1f, 1.0f)]
    [Tooltip("Percentage chance (0.5 = 50%) that a grid cell will successfully sprout a room.")]
    public float roomSpawnChance = 0.75f;

    [Header("Corridor Density")]
    [Range(0.2f, 1.0f)]
    [Tooltip("Lower values create windier paths with more dead ends. Higher values create loops and networks.")]
    public float connectionDensity = 0.5f;

    // The 2D grid matrix: 0 = Wall, 1 = Floor, 2 = Door, 3 = Unchangeable Floor
    private int[,] mapData;

    // A master list holding structural data for all generated rooms
    private List<Room> rooms = new List<Room>();

    // A lightweight helper class to hold individual room dimensions and center points
    private class Room
    {
        public int x, y, w, h;
        public int cx, cy; // Center coordinates used to connect corridors

        public Room(int x, int y, int w, int h)
        {
            this.x = x; this.y = y; this.w = w; this.h = h;
            this.cx = x + w / 2; // Calculate the horizontal center
            this.cy = y + h / 2; // Calculate the vertical center
        }
    }

    // A lightweight helper class to track network connectivity and prevent isolated rooms
    private class UnionFind
    {
        private Dictionary<Room, Room> parent = new Dictionary<Room, Room>();

        public void Add(Room room) { parent[room] = room; }

        public Room Find(Room room)
        {
            if (parent[room] == room) return room;
            parent[room] = Find(parent[room]); // Path compression
            return parent[room];
        }

        public bool Union(Room r1, Room r2)
        {
            Room root1 = Find(r1);
            Room root2 = Find(r2);
            if (root1 != root2)
            {
                parent[root1] = root2;
                return true; // They were separate, now joined
            }
            return false; // They were already connected
        }
    }

    void Start()
    {
        // Kick off the generation process as soon as the scene plays
        GenerateDungeon();
    }

    public void GenerateDungeon()
    {
        // Initialize/reset the layout grid (default state is all 0s, which means solid walls)
        mapData = new int[mapWidth, mapHeight];
        rooms.Clear();

        // Calculate the maximum width and height allowed for each grid sector cell
        int cellW = mapWidth / gridCols;
        int cellH = mapHeight / gridRows;

        // A 2D array to track exactly which room belongs to which grid slot for wiring up corridors later
        Room[,] roomGrid = new Room[gridRows, gridCols];

        // ==========================================
        // STEP 1: CARVE ROOMS WITH SPAWN CHANCE
        // ==========================================
        for (int r = 0; r < gridRows; r++)
        {
            for (int c = 0; c < gridCols; c++)
            {
                // Roll to see if this room should exist.
                // If it fails the check, leave it as null (solid wall) and jump to the next cell.
                if (Random.value > roomSpawnChance)
                {
                    roomGrid[r, c] = null;
                    continue;
                }

                int minRoomWidth = 4;
                int maxRoomWidth = cellW - 2;
                int minRoomHeight = 4;
                int maxRoomHeight = cellH - 2;

                int w = Random.Range(minRoomWidth, maxRoomWidth + 1);
                int h = Random.Range(minRoomHeight, maxRoomHeight + 1);

                int maxRemainingX = cellW - w;
                int maxRemainingY = cellH - h;

                int offsetX = (maxRemainingX > 1) ? Random.Range(1, maxRemainingX) : 1;
                int offsetY = (maxRemainingY > 1) ? Random.Range(1, maxRemainingY) : 1;

                int x = (c * cellW) + offsetX;
                int y = (r * cellH) + offsetY;

                int endX = Mathf.Min(x + w, mapWidth - 1);
                int endY = Mathf.Min(y + h, mapHeight - 1);

                Room room = new Room(x, y, (endX - x), (endY - y));
                roomGrid[r, c] = room;
                rooms.Add(room);

                for (int i = x; i < endX; i++)
                {
                    for (int j = y; j < endY; j++)
                    {
                        mapData[i, j] = 3;
                    }
                }
            }
        }

        // ==========================================
        // STEP 2: SELECTIVELY CONNECT ROOMS
        // ==========================================
        ConnectAllRooms(roomGrid);

        // ==========================================
        // STEP 3: INSTANTIATE THE GAMEOBJECTS
        // ==========================================
        Build3DWorld();
    }

    private void ConnectAllRooms(Room[,] roomGrid)
    {
        UnionFind network = new UnionFind();
        List<System.Tuple<Room, Room>> possibleConnections = new List<System.Tuple<Room, Room>>();

        // 1. Initialize our network map with all rooms that actually exist
        foreach (Room r in rooms)
        {
            network.Add(r);
        }

        // 2. Scan the grid and collect every possible adjacent horizontal/vertical connection
        for (int r = 0; r < gridRows; r++)
        {
            for (int c = 0; c < gridCols; c++)
            {
                Room current = roomGrid[r, c];
                if (current == null) continue;

                // Look for next horizontal neighbor
                for (int nextC = c + 1; nextC < gridCols; nextC++)
                {
                    if (roomGrid[r, nextC] != null)
                    {
                        possibleConnections.Add(new System.Tuple<Room, Room>(current, roomGrid[r, nextC]));
                        break;
                    }
                }

                // Look for next vertical neighbor
                for (int nextR = r + 1; nextR < gridRows; nextR++)
                {
                    if (roomGrid[nextR, c] != null)
                    {
                        possibleConnections.Add(new System.Tuple<Room, Room>(current, roomGrid[nextR, c]));
                        break;
                    }
                }
            }
        }

        // 3. Shuffle the list of connections completely to ensure a random layout shape every time
        for (int i = 0; i < possibleConnections.Count; i++)
        {
            var temp = possibleConnections[i];
            int randomIndex = Random.Range(i, possibleConnections.Count);
            possibleConnections[i] = possibleConnections[randomIndex];
            possibleConnections[randomIndex] = temp;
        }

        // 4. Process the shuffled connections
        foreach (var edge in possibleConnections)
        {
            Room r1 = edge.Item1;
            Room r2 = edge.Item2;

            // If these two rooms belong to completely separate networks, we FORCE a tunnel.
            // This mathematically guarantees that no room can ever be isolated!
            if (network.Union(r1, r2))
            {
                CreateTunnel(r1.cx, r1.cy, r2.cx, r2.cy);
            }
            // If they are already connected through a roundabout path, check our connection density modifier.
            // This is where extra loops are allowed to form. If density is 0, no extra loops form.
            else if (Random.value < connectionDensity)
            {
                CreateTunnel(r1.cx, r1.cy, r2.cx, r2.cy);
            }
        }
    }

    // Creates an L-shaped path between two room centers
    private void CreateTunnel(int x1, int y1, int x2, int y2)
    {
        // Flip a coin to choose whether to dig horizontally first or vertically first
        if (Random.value < 0.5f)
        {
            CarveH(x1, x2, y1); // Horizontal first
            CarveV(y1, y2, x2); // Then Vertical
        }
        else
        {
            CarveV(y1, y2, x1); // Vertical first
            CarveH(x1, x2, y2); // Then Horizontal
        }
    }

    // Carves a horizontal pathway from x1 to x2 at a specific y coordinate
    private void CarveH(int x1, int x2, int y)
    {
        for (int x = Mathf.Min(x1, x2); x <= Mathf.Max(x1, x2); x++)
        {
            if (mapData[x, y] != 3)
            {
                mapData[x, y] = 1; // Mark as floor path
                CheckAndCarveDoor(x, y);
            }
        }
    }

    // Carves a vertical pathway from y1 to y2 at a specific x coordinate
    private void CarveV(int y1, int y2, int x)
    {
        for (int y = Mathf.Min(y1, y2); y <= Mathf.Max(y1, y2); y++)
        {
            if (mapData[x, y] != 3)
            {
                mapData[x, y] = 1; // Mark as floor path
                CheckAndCarveDoor(x, y);
            }
        }
    }

    // Marks a tile as a potential door if a tunnel cuts through an existing room wall boundary
    private void CheckAndCarveDoor(int x, int y)
    {
        // If a tunnel path intercepts an existing floor boundary tile, designate it as a door path (Value 2)
        if (mapData[x, y] == 1 && doorPrefab != null)
        {
            mapData[x, y] = 2;
        }
    }

    // Loops through the final mapData matrix and instantiates 3D GameObjects
    private void Build3DWorld()
    {
        // Creates a runtime container object to hold all the tiles and keep the hierarchy tidy
        GameObject dungeonContainer = new GameObject("GeneratedDungeon");

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                // Calculate the real global Vector3 coordinates in Unity space using the spacing multiplier
                Vector3 position = new Vector3(x * tileSize, 0, y * tileSize);
                GameObject spawnedTile = null;

                if (mapData[x, y] == 1) // Floor Tile
                {
                    if (floorPrefab) spawnedTile = Instantiate(floorPrefab, position, Quaternion.identity);
                }
                else if (mapData[x, y] == 2) // Door Tile
                {
                    if (doorPrefab) spawnedTile = Instantiate(doorPrefab, position, Quaternion.identity);
                }
                else if (mapData[x, y] == 3) // Forced Floor
                {
                    if (floorPrefab) spawnedTile = Instantiate(floorPrefab, position, Quaternion.identity);
                }
                else // Wall Tile (Value 0)
                {
                    // Raise structural walls up slightly on the Y-Axis so they sit flush over the floor plane
                    Vector3 wallPos = new Vector3(position.x, 0f, position.z);
                    if (wallPrefab) spawnedTile = Instantiate(wallPrefab, wallPos, Quaternion.identity);
                }

                // If an object was successfully spawned, nest it under our master container parent
                if (spawnedTile != null)
                {
                    spawnedTile.transform.SetParent(dungeonContainer.transform);
                }
            }
        }
    }
}
