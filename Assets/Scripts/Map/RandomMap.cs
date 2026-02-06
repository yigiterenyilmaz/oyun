using UnityEngine;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    [Header("Map Size")]
    public int width = 256;
    public int height = 256;

    [Header("Main Island")]
    [Range(0.25f, 0.45f)]
    public float islandSize = 0.35f;
    [Range(4, 10)]
    public int basePoints = 7;              // more points = more complex shape
    [Range(3, 7)]
    public int subdivisions = 5;            // more = more detailed coastline
    [Range(0.3f, 0.7f)]
    public float roughness = 0.5f;          // coastline jaggedness
    [Range(0f, 0.5f)]
    public float stretchVariation = 0.3f;   // how elongated/irregular

    [Header("Organic Details")]
    public bool addPeninsulas = true;
    [Range(0, 5)]
    public int peninsulaCount = 3;
    [Range(0.1f, 0.3f)]
    public float peninsulaSize = 0.15f;

    public bool addBays = true;
    [Range(0, 4)]
    public int bayCount = 2;
    [Range(0.08f, 0.2f)]
    public float baySize = 0.12f;

    [Header("Fog")]
    public bool useFog = true;
    [Range(0.5f, 0.95f)]
    public float fogStart = 0.8f;           // where fog begins (0 = center, 1 = edge)
    [Range(0f, 1f)]
    public float cornerRadius = 0.5f;       // 0 = rectangle, 1 = ellipse
    
    [Header("Colors")]
    public Color waterColor = new Color(0.1f, 0.3f, 0.8f);
    public Color fogColor = new Color(0.7f, 0.75f, 0.8f);

    [Header("Biomes")]
    public Color biome1Color = new Color(0.2f, 0.6f, 0.2f);   // Forest (green)
    public Color biome2Color = new Color(0.85f, 0.75f, 0.4f); // Desert (yellow)
    public Color biome3Color = new Color(0.4f, 0.4f, 0.45f);  // Mountains (gray)
    public Color biome4Color = new Color(0.3f, 0.7f, 0.5f);   // Plains (light green)

    [Header("Biome Ratios (will be normalized)")]
    [Range(0f, 1f)] public float biome1Ratio = 0.3f;  // Forest
    [Range(0f, 1f)] public float biome2Ratio = 0.2f;  // Desert
    [Range(0f, 1f)] public float biome3Ratio = 0.2f;  // Mountains
    [Range(0f, 1f)] public float biome4Ratio = 0.3f;  // Plains

    // Public getters for actual ratios (for skill system)
    public float ForestRatio { get; private set; }
    public float DesertRatio { get; private set; }
    public float MountainRatio { get; private set; }
    public float PlainsRatio { get; private set; }

    [Header("Cleanup")]
    public bool fillSmallLakes = true;
    public int minLakeSize = 80;

    [Header("References")]
    public SpriteRenderer mapRenderer;

    public System.Action OnMapGenerated;

    private bool[,] landMap;
    private int[,] biomeMap;  // 0 = water, 1-4 = biomes
    private float[,] fogMap;
    private Texture2D mapTexture;
    private int totalLandTiles;
    private int[] biomeTileCounts = new int[4];

    void Start()
    {
        if (mapRenderer != null)
            mapRenderer.enabled = false;

        GenerateMap();
    }

    public void GenerateMap()
    {
        landMap = new bool[width, height];
        biomeMap = new int[width, height];
        fogMap = new float[width, height];

        // Generate main island
        Vector2 center = new Vector2(width / 2f, height / 2f);
        float baseRadius = Mathf.Min(width, height) * islandSize;
        
        // Random stretch for organic shape
        float stretchX = 1f + Random.Range(-stretchVariation, stretchVariation);
        float stretchY = 1f + Random.Range(-stretchVariation, stretchVariation);
        float rotation = Random.Range(0f, Mathf.PI * 2f);

        List<Vector2> mainPoly = GenerateDetailedPolygon(center, baseRadius, stretchX, stretchY, rotation);
        FillPolygon(mainPoly);

        // Add peninsulas (land jutting out)
        if (addPeninsulas)
        {
            for (int i = 0; i < peninsulaCount; i++)
            {
                AddPeninsula(center, baseRadius);
            }
        }

        // Add bays (water cutting in)
        if (addBays)
        {
            for (int i = 0; i < bayCount; i++)
            {
                AddBay(center, baseRadius);
            }
        }

        // Cleanup
        if (fillSmallLakes)
            FillSmallLakes();

        // Generate biomes on land
        GenerateBiomes();

        // Generate fog
        if (useFog)
            GenerateFog();

        // Calculate actual ratios
        CalculateBiomeRatios();

        mapTexture = CreateTexture();
        ApplyTexture();
    }

    List<Vector2> GenerateDetailedPolygon(Vector2 center, float radius, float stretchX, float stretchY, float rotation)
    {
        List<Vector2> polygon = new List<Vector2>();

        // Create base polygon with varied radii
        float angleStep = (Mathf.PI * 2f) / basePoints;
        float startAngle = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < basePoints; i++)
        {
            float angle = startAngle + i * angleStep;
            
            // Vary radius significantly for each point
            float r = radius * Random.Range(0.7f, 1.3f);
            
            // Apply stretch
            float x = Mathf.Cos(angle) * r * stretchX;
            float y = Mathf.Sin(angle) * r * stretchY;
            
            // Apply rotation
            float rx = x * Mathf.Cos(rotation) - y * Mathf.Sin(rotation);
            float ry = x * Mathf.Sin(rotation) + y * Mathf.Cos(rotation);
            
            polygon.Add(new Vector2(center.x + rx, center.y + ry));
        }

        // Midpoint displacement for detailed coastline
        for (int s = 0; s < subdivisions; s++)
        {
            polygon = SubdividePolygon(polygon, roughness * Mathf.Pow(0.55f, s));
        }

        return polygon;
    }

    List<Vector2> SubdividePolygon(List<Vector2> polygon, float displacement)
    {
        List<Vector2> newPoly = new List<Vector2>();

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];

            newPoly.Add(current);

            // Midpoint with perpendicular displacement
            Vector2 mid = (current + next) / 2f;
            Vector2 dir = next - current;
            Vector2 perp = new Vector2(-dir.y, dir.x).normalized;

            // Randomize displacement with bias toward outward
            float disp = dir.magnitude * displacement * Random.Range(-0.8f, 1f);
            mid += perp * disp;

            newPoly.Add(mid);
        }

        return newPoly;
    }

    void AddPeninsula(Vector2 islandCenter, float islandRadius)
    {
        // Pick random angle from center
        float angle = Random.Range(0f, Mathf.PI * 2f);
        
        // Start from edge of island
        float distFromCenter = islandRadius * Random.Range(0.6f, 0.9f);
        Vector2 basePos = islandCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distFromCenter;
        
        // Peninsula extends outward
        float penRadius = islandRadius * peninsulaSize * Random.Range(0.7f, 1.3f);
        Vector2 penCenter = basePos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * penRadius * 0.5f;
        
        // Create elongated peninsula
        List<Vector2> penPoly = GenerateDetailedPolygon(penCenter, penRadius, 
            1f + Random.Range(0.3f, 0.8f), // stretch along length
            Random.Range(0.4f, 0.7f),       // narrow width
            angle);
        
        FillPolygon(penPoly);
    }

    void AddBay(Vector2 islandCenter, float islandRadius)
    {
        // Pick random angle
        float angle = Random.Range(0f, Mathf.PI * 2f);
        
        // Position bay at island edge
        float distFromCenter = islandRadius * Random.Range(0.5f, 0.8f);
        Vector2 bayCenter = islandCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distFromCenter;
        
        float bayRadius = islandRadius * baySize * Random.Range(0.8f, 1.2f);
        
        // Create bay shape and carve it out
        List<Vector2> bayPoly = GenerateDetailedPolygon(bayCenter, bayRadius, 
            Random.Range(0.6f, 1f),
            Random.Range(0.6f, 1f),
            Random.Range(0f, Mathf.PI * 2f));
        
        CarveBay(bayPoly);
    }

    void FillPolygon(List<Vector2> polygon)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var p in polygon)
        {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        int startX = Mathf.Max(0, Mathf.FloorToInt(minX));
        int endX = Mathf.Min(width - 1, Mathf.CeilToInt(maxX));
        int startY = Mathf.Max(0, Mathf.FloorToInt(minY));
        int endY = Mathf.Min(height - 1, Mathf.CeilToInt(maxY));

        for (int y = startY; y <= endY; y++)
        {
            List<float> intersections = new List<float>();

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 p1 = polygon[i];
                Vector2 p2 = polygon[(i + 1) % polygon.Count];

                if ((p1.y <= y && p2.y > y) || (p2.y <= y && p1.y > y))
                {
                    float x = p1.x + (y - p1.y) / (p2.y - p1.y) * (p2.x - p1.x);
                    intersections.Add(x);
                }
            }

            intersections.Sort();

            for (int i = 0; i < intersections.Count - 1; i += 2)
            {
                int x1 = Mathf.Max(0, Mathf.CeilToInt(intersections[i]));
                int x2 = Mathf.Min(width - 1, Mathf.FloorToInt(intersections[i + 1]));

                for (int x = x1; x <= x2; x++)
                {
                    landMap[x, y] = true;
                }
            }
        }
    }

    void CarveBay(List<Vector2> polygon)
    {
        // Same as FillPolygon but sets to false (water)
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var p in polygon)
        {
            minX = Mathf.Min(minX, p.x);
            maxX = Mathf.Max(maxX, p.x);
            minY = Mathf.Min(minY, p.y);
            maxY = Mathf.Max(maxY, p.y);
        }

        int startX = Mathf.Max(0, Mathf.FloorToInt(minX));
        int endX = Mathf.Min(width - 1, Mathf.CeilToInt(maxX));
        int startY = Mathf.Max(0, Mathf.FloorToInt(minY));
        int endY = Mathf.Min(height - 1, Mathf.CeilToInt(maxY));

        for (int y = startY; y <= endY; y++)
        {
            List<float> intersections = new List<float>();

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 p1 = polygon[i];
                Vector2 p2 = polygon[(i + 1) % polygon.Count];

                if ((p1.y <= y && p2.y > y) || (p2.y <= y && p1.y > y))
                {
                    float x = p1.x + (y - p1.y) / (p2.y - p1.y) * (p2.x - p1.x);
                    intersections.Add(x);
                }
            }

            intersections.Sort();

            for (int i = 0; i < intersections.Count - 1; i += 2)
            {
                int x1 = Mathf.Max(0, Mathf.CeilToInt(intersections[i]));
                int x2 = Mathf.Min(width - 1, Mathf.FloorToInt(intersections[i + 1]));

                for (int x = x1; x <= x2; x++)
                {
                    landMap[x, y] = false;
                }
            }
        }
    }

    void FillSmallLakes()
    {
        bool[,] visited = new bool[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!landMap[x, y] && !visited[x, y])
                {
                    List<Vector2Int> lake = new List<Vector2Int>();
                    bool touchesEdge = FloodFillWater(x, y, visited, lake);

                    if (!touchesEdge && lake.Count < minLakeSize)
                    {
                        foreach (var pos in lake)
                            landMap[pos.x, pos.y] = true;
                    }
                }
            }
        }
    }

    bool FloodFillWater(int startX, int startY, bool[,] visited, List<Vector2Int> region)
    {
        bool touchesEdge = false;
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(startX, startY));

        while (stack.Count > 0)
        {
            Vector2Int pos = stack.Pop();
            int x = pos.x, y = pos.y;

            if (x < 0 || x >= width || y < 0 || y >= height) continue;
            if (visited[x, y] || landMap[x, y]) continue;

            visited[x, y] = true;
            region.Add(pos);

            if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                touchesEdge = true;

            stack.Push(new Vector2Int(x + 1, y));
            stack.Push(new Vector2Int(x - 1, y));
            stack.Push(new Vector2Int(x, y + 1));
            stack.Push(new Vector2Int(x, y - 1));
        }

        return touchesEdge;
    }

    void GenerateFog()
    {
        float noiseOffset = Random.Range(0f, 1000f);
        
        float halfW = width / 2f;
        float halfH = height / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Normalize coordinates to -1 to 1 range
                float nx = (x - halfW) / halfW;
                float ny = (y - halfH) / halfH;
                
                // Rounded rectangle distance (Squircle-like)
                // cornerRadius controls how rounded (0 = rectangle, 1 = ellipse)
                float power = 2f + (1f - cornerRadius) * 6f;  // 2 = ellipse, 8 = nearly rectangle
                float dist = Mathf.Pow(Mathf.Abs(nx), power) + Mathf.Pow(Mathf.Abs(ny), power);
                dist = Mathf.Pow(dist, 1f / power);

                // Add noise to fog boundary
                float noise = Mathf.PerlinNoise((x + noiseOffset) / 50f, (y + noiseOffset) / 50f) * 0.06f;
                float fogThreshold = fogStart + noise;

                if (dist > fogThreshold)
                {
                    // Soft gradient using smoothstep
                    float t = (dist - fogThreshold) / (1f - fogThreshold);
                    t = Mathf.Clamp01(t);
                    float fogAmount = t * t * (3f - 2f * t);
                    fogMap[x, y] = Mathf.Clamp01(fogAmount);
                }
                else
                {
                    fogMap[x, y] = 0f;
                }
            }
        }
    }

 void GenerateBiomes()
{
    totalLandTiles = 0;
    List<Vector2Int> landTiles = new List<Vector2Int>();
    
    // 1. Map out all land
    for (int x = 0; x < width; x++) {
        for (int y = 0; y < height; y++) {
            if (landMap[x, y]) {
                landTiles.Add(new Vector2Int(x, y));
                totalLandTiles++;
            }
        }
    }

    if (totalLandTiles == 0) return;

    // 2. Calculate the "Power" of the secondary biomes
    float totalRatio = biome1Ratio + biome2Ratio + biome3Ratio + biome4Ratio;
    float b1Normalized = biome1Ratio / totalRatio;
    
    // This determines how far Biomes 2, 3, and 4 "spread" over Biome 1
    // If b1 is 0.8, spread is small. If b1 is 0.1, spread is huge.
    float secondarySpread = (1f - b1Normalized); 
    float influenceRange = (width * 0.6f) * secondarySpread; 

    // 3. Place Seeds for the secondary biomes
    List<Vector3> invasionSeeds = new List<Vector3>();
    int clustersPerBiome = 3; 

    for (int bType = 2; bType <= 4; bType++) {
        for (int i = 0; i < clustersPerBiome; i++) {
            Vector2Int pos = landTiles[Random.Range(0, landTiles.Count)];
            invasionSeeds.Add(new Vector3(pos.x, pos.y, bType));
        }
    }

    // 4. Domain Warping for Organic Borders
    float warpStrength = 25f;
    float warpScale = 0.05f;
    float noiseOffset = Random.Range(0f, 1000f);

    for (int x = 0; x < width; x++) {
        for (int y = 0; y < height; y++) { // The missing loop that caused your error
            if (!landMap[x, y]) {
                biomeMap[x, y] = 0;
                continue;
            }

            // Distort the position for wavy borders
            float nx = (Mathf.PerlinNoise(x * warpScale + noiseOffset, y * warpScale) - 0.5f) * warpStrength;
            float ny = (Mathf.PerlinNoise(y * warpScale, x * warpScale + noiseOffset) - 0.5f) * warpStrength;
            float warpedX = x + nx;
            float warpedY = y + ny;

            float minDist = float.MaxValue;
            int nearestInvasionBiome = 1;

            // Find closest secondary biome seed
            foreach (var seed in invasionSeeds) {
                float d = Vector2.Distance(new Vector2(warpedX, warpedY), new Vector2(seed.x, seed.y));
                if (d < minDist) {
                    minDist = d;
                    nearestInvasionBiome = (int)seed.z;
                }
            }

            // If we are close enough to a secondary seed, use it. 
            // Otherwise, stay as the background (Biome 1).
            if (minDist < influenceRange) {
                biomeMap[x, y] = nearestInvasionBiome;
            } else {
                biomeMap[x, y] = 1;
            }
        }
    }

    // 5. Update counts (Fixed loops)
    for (int i = 0; i < 4; i++) biomeTileCounts[i] = 0;
    for (int x = 0; x < width; x++) {
        for (int y = 0; y < height; y++) { // Fixed: added nested y loop
            int b = biomeMap[x, y];
            if (b >= 1 && b <= 4) {
                biomeTileCounts[b - 1]++;
            }
        }
    }
}
    void CalculateBiomeRatios()
    {
        if (totalLandTiles == 0)
        {
            ForestRatio = DesertRatio = MountainRatio = PlainsRatio = 0f;
            return;
        }

        ForestRatio = (float)biomeTileCounts[0] / totalLandTiles;
        DesertRatio = (float)biomeTileCounts[1] / totalLandTiles;
        MountainRatio = (float)biomeTileCounts[2] / totalLandTiles;
        PlainsRatio = (float)biomeTileCounts[3] / totalLandTiles;
    }

    // Get biome at tile (1-4, or 0 for water)
    public int GetBiome(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return 0;
        return biomeMap[x, y];
    }

    Texture2D CreateTexture()
    {
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;

        Color[] biomeColors = { biome1Color, biome2Color, biome3Color, biome4Color };

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Color baseColor;
                
                int biome = biomeMap[x, y];
                if (biome >= 1 && biome <= 4)
                    baseColor = biomeColors[biome - 1];
                else
                    baseColor = waterColor;
                
                // Blend with fog
                if (useFog && fogMap[x, y] > 0)
                {
                    baseColor = Color.Lerp(baseColor, fogColor, fogMap[x, y]);
                }

                texture.SetPixel(x, y, baseColor);
            }
        }

        texture.Apply();
        return texture;
    }

    void ApplyTexture()
    {
        if (mapRenderer == null)
        {
            Debug.LogWarning("MapGenerator: No SpriteRenderer assigned!");
            return;
        }

        if (mapRenderer.sprite != null)
            Destroy(mapRenderer.sprite);

        Sprite sprite = Sprite.Create(
            mapTexture,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        sprite.name = "GeneratedMap";
        mapRenderer.sprite = sprite;
        mapRenderer.enabled = true;

        OnMapGenerated?.Invoke();
    }

    public void SetTile(int x, int y, bool isLand, int biome = 1)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        landMap[x, y] = isLand;
        biomeMap[x, y] = isLand ? Mathf.Clamp(biome, 1, 4) : 0;
        
        Color[] biomeColors = { biome1Color, biome2Color, biome3Color, biome4Color };
        Color baseColor = isLand ? biomeColors[biomeMap[x, y] - 1] : waterColor;
        
        if (useFog && fogMap[x, y] > 0)
            baseColor = Color.Lerp(baseColor, fogColor, fogMap[x, y]);
            
        mapTexture.SetPixel(x, y, baseColor);
        mapTexture.Apply();
    }

    public bool IsLand(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return landMap[x, y];
    }

    public float GetFog(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return 1f;
        return fogMap[x, y];
    }

    public void Regenerate()
    {
        GenerateMap();
    }

    void OnValidate()
    {
        width = Mathf.Max(64, width);
        height = Mathf.Max(64, height);
    }
}